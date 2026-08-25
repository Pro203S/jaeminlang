using System.Diagnostics;
using System.Text;

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

        public NetworkMode(
            string[] args,
            Action<int>? repeat,
            Func<string, string[], object?[]>? invokeFunction,
            Func<string, bool>? functionExists,
            Action<string>? importLibrary)
        {
            this.args = args;
            this.repeat = repeat;
            this.invokeFunction = invokeFunction;
            this.functionExists = functionExists;
            this.importLibrary = importLibrary;
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
            object? rawPayload = Utils.ResolveAssignableValue(payloadName);

            if (rawPayload is not Dictionary<string, object?> payload)
                throw new InvalidCastException("니 눈엔 이게 딕셔너리냐?;;");

            string url = Utils.GetRequiredString(payload, "url", "아니 요청 보낼 URL은 있어야지;;");
            string method = Utils.GetOptionalString(payload, "method") ?? "GET";
            List<KeyValuePair<string, string>> headers = Utils.GetRequestHeaders(payload);
            string? contentType = headers
                .FirstOrDefault(header => header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                .Value;

            using HttpRequestMessage message = new(new HttpMethod(method), url);

            if (payload.TryGetValue("body", out object? requestBody) && requestBody != null)
            {
                string body = Utils.SerializeRequestBody(requestBody, Utils.GetMediaType(contentType));
                message.Content = new StringContent(body, Encoding.UTF8);
            }

            Utils.ApplyRequestHeaders(message, headers);

            Stopwatch stopwatch = Stopwatch.StartNew();
            using HttpResponseMessage response = await Client.SendAsync(message);
            string responseText = await response.Content.ReadAsStringAsync();
            stopwatch.Stop();

            Dictionary<string, object?> result = new()
            {
                ["status"] = (double)(int)response.StatusCode,
                ["time"] = stopwatch.Elapsed.TotalMilliseconds,
                ["headers"] = Utils.GetResponseHeaders(response),
                ["text"] = responseText
            };

            string? responseMediaType = response.Content.Headers.ContentType?.MediaType;
            if (Utils.IsJsonMediaType(responseMediaType) && Utils.TryParseJson(responseText, out object? json))
                result["json"] = json;

            if (Utils.IsFormMediaType(responseMediaType))
                result["form"] = Utils.ParseForm(responseText);

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
            string filePath = Utils.IsStringLiteral(args[2]) ? args[2][1..^1] : args[2];
            object? rawPayload = Utils.ResolveAssignableValue(payloadName);

            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("아니 저장할 파일 이름은 있어야지;;");

            if (rawPayload is not Dictionary<string, object?> payload)
                throw new InvalidCastException("니 눈엔 이게 딕셔너리냐?;;");

            string url = Utils.GetRequiredString(payload, "url", "아니 요청 보낼 URL은 있어야지;;");
            string method = Utils.GetOptionalString(payload, "method") ?? "GET";
            List<KeyValuePair<string, string>> headers = Utils.GetRequestHeaders(payload);
            string? contentType = headers
                .FirstOrDefault(header => header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                .Value;

            using HttpRequestMessage message = new(new HttpMethod(method), url);

            if (payload.TryGetValue("body", out object? requestBody) && requestBody != null)
            {
                string body = Utils.SerializeRequestBody(requestBody, Utils.GetMediaType(contentType));
                message.Content = new StringContent(body, Encoding.UTF8);
            }

            Utils.ApplyRequestHeaders(message, headers);

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
    
        
    }
}
