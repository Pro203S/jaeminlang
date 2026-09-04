using System.Diagnostics;
using System.Text;
using jaeminlang.Utils;

namespace jaeminlang.Mode
{
    internal sealed class NetworkMode
    {
        private static readonly HttpClient Client = new();

        private readonly string[] args;
        private readonly Action<int>? repeat;
        private readonly Func<string, string[], object?[]>? invokeFunction;
        private readonly Func<string, bool>? functionExists;
        private readonly Action<string>? importLibrary;
        private readonly Func<string, object?[], object?[]>? invokeFunctionValues;

        public NetworkMode(
            string[] args,
            Action<int>? repeat,
            Func<string, string[], object?[]>? invokeFunction,
            Func<string, bool>? functionExists,
            Action<string>? importLibrary,
            Func<string, object?[], object?[]>? invokeFunctionValues)
        {
            this.args = args;
            this.repeat = repeat;
            this.invokeFunction = invokeFunction;
            this.functionExists = functionExists;
            this.importLibrary = importLibrary;
            this.invokeFunctionValues = invokeFunctionValues;
        }

        public bool Execute(string cmdName)
        {
            switch (cmdName)
            {
                case "재민":
                    ExecuteRequest();
                    return true;
                case "엘릭서":
                    ExecuteDownload();
                    return true;
                case "안산":
                    ExecuteServer();
                    return true;
                case "팝콘":
                    ExecuteEndpoint();
                    return true;
                case "콜라":
                    ExecuteStaticFile();
                    return true;
                default:
                    return false;
            }
        }

        public void ExecuteRequest()
        {
            ExecuteRequestAsync().GetAwaiter().GetResult();
        }

        private async Task ExecuteRequestAsync()
        {
            if (args.Length < 3 || string.IsNullOrWhiteSpace(args[1]) || string.IsNullOrWhiteSpace(args[2]))
                throw new ArgumentException("아니 인수 똑바로 안써??");

            string payloadName = args[1];
            string resultName = args[2];
            object? rawPayload = VarUtils.ResolveAssignableValue(payloadName);

            if (rawPayload is not Dictionary<string, object?> payload)
                throw new InvalidCastException("니 눈엔 이게 딕셔너리냐?;;");

            string url = NetworkUtils.GetRequiredString(payload, "url", "아니 요청 보낼 URL은 있어야지;;");
            string method = NetworkUtils.GetOptionalString(payload, "method") ?? "GET";
            List<KeyValuePair<string, string>> headers = NetworkUtils.GetRequestHeaders(payload);
            string? contentType = headers
                .FirstOrDefault(header => header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                .Value;

            using HttpRequestMessage message = new(new HttpMethod(method), url);

            if (payload.TryGetValue("body", out object? requestBody) && requestBody != null)
            {
                string body = NetworkUtils.SerializeRequestBody(requestBody, NetworkUtils.GetMediaType(contentType));
                message.Content = new StringContent(body, Encoding.UTF8);
            }

            NetworkUtils.ApplyRequestHeaders(message, headers);

            Stopwatch stopwatch = Stopwatch.StartNew();
            using HttpResponseMessage response = await Client.SendAsync(message);
            string responseText = await response.Content.ReadAsStringAsync();
            stopwatch.Stop();

            Dictionary<string, object?> result = new()
            {
                ["status"] = (double)(int)response.StatusCode,
                ["time"] = stopwatch.Elapsed.TotalMilliseconds,
                ["headers"] = NetworkUtils.GetResponseHeaders(response),
                ["text"] = responseText
            };

            string? responseMediaType = response.Content.Headers.ContentType?.MediaType;
            if (NetworkUtils.IsJsonMediaType(responseMediaType) && NetworkUtils.TryParseJson(responseText, out object? json))
                result["json"] = json;

            if (NetworkUtils.IsFormMediaType(responseMediaType))
                result["form"] = NetworkUtils.ParseForm(responseText);

            Variables.SetValue(resultName, result);
        }

        public void ExecuteDownload()
        {
            ExecuteDownloadAsync().GetAwaiter().GetResult();
        }

        private async Task ExecuteDownloadAsync()
        {
            if (args.Length < 3 || string.IsNullOrWhiteSpace(args[1]) || string.IsNullOrWhiteSpace(args[2]))
                throw new ArgumentException("아니 인수 똑바로 안써??");

            string payloadName = args[1];
            string filePath = TypeCheck.IsStringLiteral(args[2]) ? args[2][1..^1] : args[2];
            object? rawPayload = VarUtils.ResolveAssignableValue(payloadName);

            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("아니 저장할 파일 이름은 있어야지;;");

            if (rawPayload is not Dictionary<string, object?> payload)
                throw new InvalidCastException("니 눈엔 이게 딕셔너리냐?;;");

            string url = NetworkUtils.GetRequiredString(payload, "url", "아니 요청 보낼 URL은 있어야지;;");
            string method = NetworkUtils.GetOptionalString(payload, "method") ?? "GET";
            List<KeyValuePair<string, string>> headers = NetworkUtils.GetRequestHeaders(payload);
            string? contentType = headers
                .FirstOrDefault(header => header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                .Value;

            using HttpRequestMessage message = new(new HttpMethod(method), url);

            if (payload.TryGetValue("body", out object? requestBody) && requestBody != null)
            {
                string body = NetworkUtils.SerializeRequestBody(requestBody, NetworkUtils.GetMediaType(contentType));
                message.Content = new StringContent(body, Encoding.UTF8);
            }

            NetworkUtils.ApplyRequestHeaders(message, headers);

            using HttpResponseMessage response = await Client.SendAsync(
                message,
                HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            await using Stream source = await response.Content.ReadAsStreamAsync();
            await using FileStream destination = new(
                filePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true);
            await source.CopyToAsync(destination);
        }

        public void ExecuteServer()
        {
            if (args.Length < 2 || args.Length > 3 || string.IsNullOrWhiteSpace(args[1]))
                throw new ArgumentException("안산에는 웹서버 이름과 포트만 줘야지;;");

            string serverName = NetworkUtils.ResolveTextToken(args[1]);
            if (string.IsNullOrWhiteSpace(serverName))
                throw new ArgumentException("웹서버 이름이 비었잖아;;");

            int port = 8080;

            if (args.Length == 3)
            {
                double rawPort = VarUtils.ResolveNumberValue(args[2]);
                if (!double.IsFinite(rawPort) || rawPort != Math.Truncate(rawPort) || rawPort < 1 || rawPort > 65535)
                    throw new ArgumentException("웹서버 포트가 이상하잖아;;");

                port = (int)rawPort;
            }

            WebServerManager.Create(serverName, port);
        }

        public void ExecuteEndpoint()
        {
            if (args.Length != 5)
                throw new ArgumentException("팝콘에는 웹서버, 메서드, 경로, 함수를 줘야지;;");

            string serverName = NetworkUtils.ResolveTextToken(args[1]);
            string method = NetworkUtils.ResolveTextToken(args[2]);
            string path = NetworkUtils.ResolveTextToken(args[3]);
            string handlerName = args[4];

            if (string.IsNullOrWhiteSpace(serverName))
                throw new ArgumentException("웹서버 이름이 비었잖아;;");

            if (functionExists == null || !functionExists(handlerName))
                throw new ArgumentNullException(handlerName + " 함수가 정의가 안됐잖아;;");

            if (invokeFunctionValues == null)
                throw new InvalidOperationException("엔드포인트 함수를 실행할 수가 없잖아;;");

            WebServerManager.RegisterEndpoint(serverName, method, path, request =>
            {
                object?[] returned = invokeFunctionValues(handlerName, [request]);
                if (returned.Length != 1 || returned[0] is not Dictionary<string, object?> response)
                    throw new InvalidOperationException(handlerName + " 함수는 응답 딕셔너리 하나를 반환해야지;;");

                return response;
            });
        }

        public void ExecuteStaticFile()
        {
            if (args.Length != 4)
                throw new ArgumentException("콜라에는 웹서버, 경로, 파일을 줘야지;;");

            string serverName = NetworkUtils.ResolveTextToken(args[1]);
            string route = NetworkUtils.ResolveTextToken(args[2]);
            string filePath = NetworkUtils.ResolveTextToken(args[3]);

            if (string.IsNullOrWhiteSpace(serverName))
                throw new ArgumentException("웹서버 이름이 비었잖아;;");

            WebServerManager.RegisterStaticFile(serverName, route, filePath);
        }

    }
}
