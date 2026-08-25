using System.Net;
using System.Text;
using System.Text.Json;

namespace jaeminlang.Mode
{
    internal static class WebServerManager
    {
        private static readonly Dictionary<string, JmlWebServer> Servers = [];
        private static readonly object Sync = new();

        public static void Create(string name, int port)
        {
            lock (Sync)
            {
                if (Servers.ContainsKey(name))
                    throw new ArgumentException(name + " 웹서버는 이미 있잖아;;");

                JmlWebServer server = new(name, port);
                server.Start();
                Servers[name] = server;
            }
        }

        public static void RegisterEndpoint(
            string serverName,
            string method,
            string path,
            Func<Dictionary<string, object?>, Dictionary<string, object?>> handler)
        {
            GetRequired(serverName).RegisterEndpoint(method, path, handler);
        }

        public static void RegisterStaticFile(string serverName, string route, string filePath)
        {
            GetRequired(serverName).RegisterStaticFile(route, filePath);
        }

        public static void Reset()
        {
            JmlWebServer[] servers;
            lock (Sync)
            {
                servers = [.. Servers.Values];
                Servers.Clear();
            }

            foreach (JmlWebServer server in servers)
                server.Dispose();
        }

        private static JmlWebServer GetRequired(string name)
        {
            lock (Sync)
            {
                return Servers.TryGetValue(name, out JmlWebServer? server)
                    ? server
                    : throw new ArgumentNullException(name + " 웹서버가 없잖아;;");
            }
        }
    }

    internal sealed class JmlWebServer : IDisposable
    {
        private static readonly object FunctionExecutionLock = new();

        private readonly string name;
        private readonly int port;
        private readonly HttpListener listener = new();
        private readonly List<EndpointRoute> endpoints = [];
        private readonly List<StaticRoute> staticRoutes = [];
        private readonly object routeSync = new();

        private Thread? listenerThread;
        private bool disposed;
        private int nextEndpointOrder;

        public JmlWebServer(string name, int port)
        {
            this.name = name;
            this.port = port;
            listener.Prefixes.Add($"http://127.0.0.1:{port}/");
            listener.IgnoreWriteExceptions = true;
        }

        public void Start()
        {
            listener.Start();
            listenerThread = new Thread(Listen)
            {
                IsBackground = false,
                Name = $"jaeminlang-web-{name}-{port}"
            };
            listenerThread.Start();
        }

        public void RegisterEndpoint(
            string method,
            string path,
            Func<Dictionary<string, object?>, Dictionary<string, object?>> handler)
        {
            string normalizedMethod = method.Trim().ToUpperInvariant();
            string normalizedPath = NormalizeRoute(path);

            if (normalizedMethod.Length == 0)
                throw new ArgumentException("HTTP 메서드가 비었잖아;;");

            if (normalizedMethod != "*")
                _ = new HttpMethod(normalizedMethod);

            lock (routeSync)
            {
                EndpointRoute endpoint = CreateEndpointRoute(
                    normalizedMethod,
                    normalizedPath,
                    nextEndpointOrder,
                    handler);

                if (endpoints.Any(item =>
                    item.Method.Equals(normalizedMethod, StringComparison.Ordinal) &&
                    item.Signature.Equals(endpoint.Signature, StringComparison.Ordinal)))
                {
                    throw new ArgumentException($"{normalizedMethod} {normalizedPath} 엔드포인트는 이미 있잖아;;");
                }

                endpoints.Add(endpoint);
                nextEndpointOrder++;
            }
        }

        public void RegisterStaticFile(string route, string filePath)
        {
            string normalizedRoute = NormalizeRoute(route);
            string fullPath = Path.GetFullPath(filePath);
            bool isDirectory = Directory.Exists(fullPath);

            if (!isDirectory && !File.Exists(fullPath))
                throw new FileNotFoundException("정적 파일이 없잖아;;", fullPath);

            lock (routeSync)
            {
                if (staticRoutes.Any(item => item.Route.Equals(normalizedRoute, StringComparison.Ordinal)))
                    throw new ArgumentException(normalizedRoute + " 정적 경로는 이미 있잖아;;");

                staticRoutes.Add(new StaticRoute(normalizedRoute, fullPath, isDirectory));
                staticRoutes.Sort((left, right) => right.Route.Length.CompareTo(left.Route.Length));
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            listener.Stop();
            listener.Close();

            if (listenerThread != null &&
                listenerThread != Thread.CurrentThread &&
                listenerThread.IsAlive)
            {
                listenerThread.Join(TimeSpan.FromSeconds(1));
            }
        }

        private void Listen()
        {
            while (!disposed && listener.IsListening)
            {
                try
                {
                    HttpListenerContext context = listener.GetContext();
                    _ = Task.Run(() => HandleContextAsync(context));
                }
                catch (HttpListenerException) when (disposed || !listener.IsListening)
                {
                    return;
                }
                catch (ObjectDisposedException) when (disposed)
                {
                    return;
                }
                catch (Exception exception)
                {
                    Utils.HandleError(exception, name + " 웹서버");
                }
            }
        }

        private async Task HandleContextAsync(HttpListenerContext context)
        {
            try
            {
                string method = context.Request.HttpMethod.ToUpperInvariant();
                string path = NormalizeRoute(Uri.UnescapeDataString(context.Request.Url?.AbsolutePath ?? "/"));
                EndpointMatch? endpoint = FindEndpoint(method, path);

                if (endpoint != null)
                {
                    Dictionary<string, object?> request = await CreateRequestAsync(
                        context.Request,
                        endpoint.Parameters);
                    Dictionary<string, object?> response;
                    lock (FunctionExecutionLock)
                    {
                        response = endpoint.Route.Handler(request);
                    }

                    await WriteResponseAsync(context.Response, response, method == "HEAD");
                    return;
                }

                StaticRoute? staticRoute = FindStaticRoute(path);
                if (staticRoute != null && (method == "GET" || method == "HEAD"))
                {
                    await WriteStaticFileAsync(context.Response, staticRoute, path, method == "HEAD");
                    return;
                }

                await WriteTextAsync(context.Response, 404, "text/plain; charset=utf-8", "Not Found", method == "HEAD");
            }
            catch (UnauthorizedAccessException)
            {
                await TryWriteErrorAsync(context.Response, 403, "Forbidden");
            }
            catch (FileNotFoundException)
            {
                await TryWriteErrorAsync(context.Response, 404, "Not Found");
            }
            catch (Exception exception)
            {
                Utils.HandleError(exception, name + " 웹서버 요청 처리");
                await TryWriteErrorAsync(context.Response, 500, "Internal Server Error");
            }
            finally
            {
                context.Response.Close();
            }
        }

        private EndpointMatch? FindEndpoint(string method, string path)
        {
            lock (routeSync)
            {
                string[] requestSegments = SplitRouteSegments(path);
                EndpointMatch? bestMatch = null;

                foreach (EndpointRoute endpoint in endpoints)
                {
                    int methodRank = GetMethodRank(endpoint.Method, method);
                    if (methodRank == 0 || !TryMatchEndpoint(endpoint, requestSegments, out Dictionary<string, object?> parameters))
                        continue;

                    EndpointMatch candidate = new(endpoint, parameters, methodRank);
                    if (bestMatch == null || IsBetterMatch(candidate, bestMatch))
                        bestMatch = candidate;
                }

                return bestMatch;
            }
        }

        private static EndpointRoute CreateEndpointRoute(
            string method,
            string path,
            int registrationOrder,
            Func<Dictionary<string, object?>, Dictionary<string, object?>> handler)
        {
            string[] rawSegments = SplitRouteSegments(path);
            List<RouteSegment> segments = [];
            HashSet<string> parameterNames = [];

            for (int i = 0; i < rawSegments.Length; i++)
            {
                string segment = rawSegments[i];
                if (segment.StartsWith('{') && segment.EndsWith('}'))
                {
                    string parameterName = segment[1..^1];
                    bool isCatchAll = parameterName.StartsWith('*');
                    if (isCatchAll)
                        parameterName = parameterName[1..];

                    if (string.IsNullOrWhiteSpace(parameterName) ||
                        parameterName.Any(char.IsWhiteSpace) ||
                        parameterName.Contains('{') ||
                        parameterName.Contains('}') ||
                        parameterName.Contains('*') ||
                        parameterName.Contains('.') ||
                        parameterName.Contains(','))
                    {
                        throw new FormatException(segment + " 경로 파라미터 이름이 이상하잖아;;");
                    }

                    if (!parameterNames.Add(parameterName))
                        throw new FormatException(parameterName + " 경로 파라미터를 두 번 썼잖아;;");

                    if (isCatchAll && i != rawSegments.Length - 1)
                        throw new FormatException("catch-all 경로 파라미터는 마지막에 있어야지;;");

                    segments.Add(new RouteSegment(
                        isCatchAll ? RouteSegmentKind.CatchAll : RouteSegmentKind.Parameter,
                        parameterName));
                    continue;
                }

                if (segment.Contains('{') || segment.Contains('}'))
                    throw new FormatException(segment + " 동적 경로 형식이 이상하잖아;;");

                segments.Add(new RouteSegment(RouteSegmentKind.Literal, segment));
            }

            string signature = string.Join("/", segments.Select(segment => segment.Kind switch
            {
                RouteSegmentKind.Literal => $"L{segment.Value.Length}:{segment.Value}",
                RouteSegmentKind.Parameter => "P",
                RouteSegmentKind.CatchAll => "C",
                _ => throw new InvalidOperationException()
            }));

            return new EndpointRoute(
                method,
                path,
                signature,
                [.. segments],
                segments.Count(segment => segment.Kind == RouteSegmentKind.Literal),
                segments.Any(segment => segment.Kind == RouteSegmentKind.CatchAll),
                registrationOrder,
                handler);
        }

        private static bool TryMatchEndpoint(
            EndpointRoute endpoint,
            string[] requestSegments,
            out Dictionary<string, object?> parameters)
        {
            parameters = [];
            int requestIndex = 0;

            foreach (RouteSegment segment in endpoint.Segments)
            {
                if (segment.Kind == RouteSegmentKind.CatchAll)
                {
                    parameters[segment.Value] = string.Join('/', requestSegments[requestIndex..]);
                    requestIndex = requestSegments.Length;
                    break;
                }

                if (requestIndex >= requestSegments.Length)
                    return false;

                string requestSegment = requestSegments[requestIndex];
                if (segment.Kind == RouteSegmentKind.Literal)
                {
                    if (!segment.Value.Equals(requestSegment, StringComparison.Ordinal))
                        return false;
                }
                else
                {
                    parameters[segment.Value] = requestSegment;
                }

                requestIndex++;
            }

            return requestIndex == requestSegments.Length;
        }

        private static bool IsBetterMatch(EndpointMatch candidate, EndpointMatch current)
        {
            if (candidate.Route.LiteralCount != current.Route.LiteralCount)
                return candidate.Route.LiteralCount > current.Route.LiteralCount;

            if (candidate.Route.HasCatchAll != current.Route.HasCatchAll)
                return !candidate.Route.HasCatchAll;

            if (candidate.Route.Segments.Length != current.Route.Segments.Length)
                return candidate.Route.Segments.Length > current.Route.Segments.Length;

            if (candidate.MethodRank != current.MethodRank)
                return candidate.MethodRank > current.MethodRank;

            return candidate.Route.RegistrationOrder < current.Route.RegistrationOrder;
        }

        private static int GetMethodRank(string routeMethod, string requestMethod)
        {
            if (routeMethod.Equals(requestMethod, StringComparison.Ordinal))
                return 3;

            if (requestMethod == "HEAD" && routeMethod == "GET")
                return 2;

            return routeMethod == "*" ? 1 : 0;
        }

        private static string[] SplitRouteSegments(string route)
        {
            return route == "/"
                ? []
                : route[1..].Split('/', StringSplitOptions.RemoveEmptyEntries);
        }

        private StaticRoute? FindStaticRoute(string requestPath)
        {
            lock (routeSync)
            {
                return staticRoutes.FirstOrDefault(route =>
                    route.IsDirectory
                        ? route.Route == "/" ||
                          requestPath.Equals(route.Route, StringComparison.Ordinal) ||
                          requestPath.StartsWith(route.Route + "/", StringComparison.Ordinal)
                        : requestPath.Equals(route.Route, StringComparison.Ordinal));
            }
        }

        private static async Task<Dictionary<string, object?>> CreateRequestAsync(
            HttpListenerRequest request,
            Dictionary<string, object?> parameters)
        {
            Dictionary<string, object?> headers = new(StringComparer.OrdinalIgnoreCase);
            foreach (string? headerName in request.Headers.AllKeys)
            {
                if (headerName != null)
                    headers[headerName] = request.Headers[headerName] ?? "";
            }

            Dictionary<string, object?> query = [];
            foreach (string? queryName in request.QueryString.AllKeys)
            {
                if (queryName != null)
                    query[queryName] = request.QueryString[queryName] ?? "";
            }

            string text = "";
            if (request.HasEntityBody)
            {
                using StreamReader reader = new(
                    request.InputStream,
                    request.ContentEncoding,
                    detectEncodingFromByteOrderMarks: true,
                    leaveOpen: true);
                text = await reader.ReadToEndAsync();
            }

            Dictionary<string, object?> result = new()
            {
                ["method"] = request.HttpMethod,
                ["path"] = Uri.UnescapeDataString(request.Url?.AbsolutePath ?? "/"),
                ["params"] = parameters,
                ["query"] = query,
                ["headers"] = headers,
                ["text"] = text,
                ["remoteAddress"] = request.RemoteEndPoint?.Address.ToString() ?? ""
            };

            string? mediaType = request.ContentType == null ? null : Utils.GetMediaType(request.ContentType);
            if (Utils.IsJsonMediaType(mediaType) && Utils.TryParseJson(text, out object? json))
                result["json"] = json;

            if (Utils.IsFormMediaType(mediaType))
                result["form"] = Utils.ParseForm(text);

            return result;
        }

        private static async Task WriteResponseAsync(
            HttpListenerResponse response,
            Dictionary<string, object?> responseData,
            bool isHead)
        {
            int status = 200;
            if (responseData.TryGetValue("status", out object? rawStatus) && rawStatus != null)
            {
                double statusNumber = Utils.ConvertToNumber(rawStatus, "응답 status는 숫자여야 하잖아;;");
                if (!double.IsFinite(statusNumber) || statusNumber != Math.Truncate(statusNumber) || statusNumber < 100 || statusNumber > 999)
                    throw new ArgumentException("응답 status가 이상하잖아;;");

                status = (int)statusNumber;
            }

            string contentType = "text/plain; charset=utf-8";
            byte[] body = [];

            if (responseData.TryGetValue("json", out object? json))
            {
                body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(json));
                contentType = "application/json; charset=utf-8";
            }
            else if (responseData.TryGetValue("text", out object? text))
            {
                body = Encoding.UTF8.GetBytes(Utils.FormatOutputValue(text));
            }
            else if (responseData.TryGetValue("body", out object? rawBody))
            {
                if (rawBody is string bodyText)
                {
                    body = Encoding.UTF8.GetBytes(bodyText);
                }
                else
                {
                    body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(rawBody));
                    contentType = "application/json; charset=utf-8";
                }
            }

            if (responseData.TryGetValue("contentType", out object? rawContentType) && rawContentType is string customContentType)
                contentType = customContentType;

            response.StatusCode = status;
            response.ContentType = contentType;
            ApplyResponseHeaders(response, responseData);
            response.ContentLength64 = body.LongLength;

            if (!isHead && body.Length > 0)
                await response.OutputStream.WriteAsync(body);
        }

        private static void ApplyResponseHeaders(
            HttpListenerResponse response,
            IReadOnlyDictionary<string, object?> responseData)
        {
            if (!responseData.TryGetValue("headers", out object? rawHeaders) || rawHeaders == null)
                return;

            if (rawHeaders is not Dictionary<string, object?> headers)
                throw new InvalidCastException("응답 headers는 딕셔너리여야 하잖아;;");

            foreach ((string name, object? value) in headers)
            {
                string headerValue = Utils.FormatOutputValue(value);
                if (name.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                {
                    response.ContentType = headerValue;
                    continue;
                }

                if (name.Equals("Content-Length", StringComparison.OrdinalIgnoreCase))
                    continue;

                response.Headers[name] = headerValue;
            }
        }

        private static async Task WriteStaticFileAsync(
            HttpListenerResponse response,
            StaticRoute route,
            string requestPath,
            bool isHead)
        {
            string filePath = route.FilePath;
            if (route.IsDirectory)
            {
                string relativePath = route.Route == "/"
                    ? requestPath.TrimStart('/')
                    : requestPath.Length == route.Route.Length
                        ? ""
                        : requestPath[(route.Route.Length + 1)..];
                relativePath = Uri.UnescapeDataString(relativePath)
                    .Replace('/', Path.DirectorySeparatorChar);

                string root = Path.GetFullPath(route.FilePath);
                filePath = Path.GetFullPath(Path.Combine(root, relativePath));
                string rootPrefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
                StringComparison comparison = OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal;

                if (!filePath.Equals(root, comparison) && !filePath.StartsWith(rootPrefix, comparison))
                    throw new UnauthorizedAccessException();

                if (Directory.Exists(filePath))
                    filePath = Path.Combine(filePath, "index.html");
            }

            FileInfo file = new(filePath);
            if (!file.Exists)
                throw new FileNotFoundException();

            response.StatusCode = 200;
            response.ContentType = GetContentType(file.Extension);
            response.ContentLength64 = file.Length;

            if (isHead)
                return;

            await using FileStream stream = new(
                file.FullName,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                useAsync: true);
            await stream.CopyToAsync(response.OutputStream);
        }

        private static async Task TryWriteErrorAsync(HttpListenerResponse response, int status, string text)
        {
            try
            {
                if (response.OutputStream.CanWrite)
                    await WriteTextAsync(response, status, "text/plain; charset=utf-8", text, false);
            }
            catch
            {
            }
        }

        private static async Task WriteTextAsync(
            HttpListenerResponse response,
            int status,
            string contentType,
            string text,
            bool isHead)
        {
            byte[] body = Encoding.UTF8.GetBytes(text);
            response.StatusCode = status;
            response.ContentType = contentType;
            response.ContentLength64 = body.LongLength;

            if (!isHead)
                await response.OutputStream.WriteAsync(body);
        }

        private static string NormalizeRoute(string route)
        {
            string normalized = string.IsNullOrWhiteSpace(route) ? "/" : route.Trim();
            if (!normalized.StartsWith('/'))
                normalized = "/" + normalized;

            return normalized.Length > 1 ? normalized.TrimEnd('/') : normalized;
        }

        private static string GetContentType(string extension)
        {
            return extension.ToLowerInvariant() switch
            {
                ".html" or ".htm" => "text/html; charset=utf-8",
                ".css" => "text/css; charset=utf-8",
                ".js" or ".mjs" => "text/javascript; charset=utf-8",
                ".json" => "application/json; charset=utf-8",
                ".txt" or ".md" => "text/plain; charset=utf-8",
                ".svg" => "image/svg+xml",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".ico" => "image/x-icon",
                ".pdf" => "application/pdf",
                ".xml" => "application/xml",
                ".wasm" => "application/wasm",
                _ => "application/octet-stream"
            };
        }

        private sealed record EndpointRoute(
            string Method,
            string Template,
            string Signature,
            RouteSegment[] Segments,
            int LiteralCount,
            bool HasCatchAll,
            int RegistrationOrder,
            Func<Dictionary<string, object?>, Dictionary<string, object?>> Handler);

        private sealed record EndpointMatch(
            EndpointRoute Route,
            Dictionary<string, object?> Parameters,
            int MethodRank);

        private sealed record RouteSegment(RouteSegmentKind Kind, string Value);

        private sealed record StaticRoute(string Route, string FilePath, bool IsDirectory);

        private enum RouteSegmentKind
        {
            Literal,
            Parameter,
            CatchAll
        }
    }
}
