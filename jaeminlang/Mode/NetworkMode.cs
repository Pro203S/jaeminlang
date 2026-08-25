using System.Diagnostics;
using System.Text;
using static jaeminlang.Utils;

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
                throw new ArgumentException("재민에 페이로드와 결과 변수명을 둘 다 줘야지;;");

            string payloadName = args[1];
            string resultName = args[2];
            object? rawPayload = Utils.ResolveAssignableValue(payloadName);

            if (rawPayload is not Dictionary<string, object?> payload)
                throw new InvalidCastException(payloadName + "은(는) 딕셔너리가 아니잖아;;");

            string url = GetRequiredString(payload, "url", "요청 보낼 URL은 있어야지;;");
            string method = GetOptionalString(payload, "method") ?? "GET";
            List<KeyValuePair<string, string>> headers = GetRequestHeaders(payload);
            string? contentType = headers
                .FirstOrDefault(header => header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                .Value;

            using HttpRequestMessage message = new(new HttpMethod(method), url);

            if (payload.TryGetValue("body", out object? requestBody) && requestBody != null)
            {
                string body = SerializeRequestBody(requestBody, GetMediaType(contentType));
                message.Content = new StringContent(body, Encoding.UTF8);
            }

            ApplyRequestHeaders(message, headers);

            Stopwatch stopwatch = Stopwatch.StartNew();
            using HttpResponseMessage response = await Client.SendAsync(message);
            string responseText = await response.Content.ReadAsStringAsync();
            stopwatch.Stop();

            Dictionary<string, object?> result = new()
            {
                ["status"] = (double)(int)response.StatusCode,
                ["time"] = stopwatch.Elapsed.TotalMilliseconds,
                ["headers"] = GetResponseHeaders(response),
                ["text"] = responseText
            };

            string? responseMediaType = response.Content.Headers.ContentType?.MediaType;
            if (IsJsonMediaType(responseMediaType) && TryParseJson(responseText, out object? json))
                result["json"] = json;

            if (IsFormMediaType(responseMediaType))
                result["form"] = ParseForm(responseText);

            Variables.SetValue(resultName, result);
        }

    }
}
