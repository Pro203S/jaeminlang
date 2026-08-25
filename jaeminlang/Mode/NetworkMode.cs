using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;

namespace jaeminlang.Mode
{
    internal sealed class NetworkMode
    {
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

        async public void ExecuteRequest()
        {
            try
            {
                string payload = args[1];
                string result = args[2];

                if (
                    string.IsNullOrEmpty(payload) ||
                    string.IsNullOrEmpty(result)
                ) throw new ArgumentException("인수 똑바로 안쓰지?");

                Dictionary<string, string> dictionary = (Dictionary<string, string>)(Utils.ResolveAssignableValue(payload) ?? throw new ArgumentNullException("아니 페이로드가 없잖아;;"));
                string url = dictionary["url"];
                if (string.IsNullOrWhiteSpace(url)) throw new ArgumentException("아니 이걸 왜 빼먹어??");
                string method = dictionary["method"] ?? "get";

                long now = DateTime.Now.Ticks;

                using HttpClient client = new();
                using HttpRequestMessage message = new(new HttpMethod(method), url);

                using HttpResponseMessage response = await client.SendAsync(message);

                string body = await response.Content.ReadAsStringAsync();

                Utils.SetDictionaryValue(result, [
                    $"time,{(double)(DateTime.Now.Ticks - now)}",
                    $"time,{(double)(DateTime.Now.Ticks - now)}"
                ]);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw new Exception("HTTP 요청중에 오류났다고? 저런...", ex);
            }
        }
    }
}
