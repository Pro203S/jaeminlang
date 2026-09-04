using System.Net;
using System.Text.Json;

namespace jaeminlang.Utils
{
    public static class NetworkUtils
    {
        public static string GetRequiredString(
            IReadOnlyDictionary<string, object?> dictionary,
            string key,
            string errorMessage)
        {
            string? value = GetOptionalString(dictionary, key);
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException(errorMessage);

            return value;
        }

        public static string? GetOptionalString(
            IReadOnlyDictionary<string, object?> dictionary,
            string key)
        {
            if (!dictionary.TryGetValue(key, out object? value) || value == null)
                return null;

            return value as string
                ?? throw new InvalidCastException(key + " 값은 문자열이어야 하잖아;;");
        }

        public static string ResolveTextToken(string token)
        {
            if (TypeCheck.IsStringLiteral(token))
                return token[1..^1];

            if (!Variables.TryGetValue(token, out object? value))
                return token;

            return value as string
                ?? throw new InvalidCastException(token + " 값은 문자열이어야 하잖아;;");
        }

        public static List<KeyValuePair<string, string>> GetRequestHeaders(
            IReadOnlyDictionary<string, object?> payload)
        {
            if (!payload.TryGetValue("headers", out object? rawHeaders) || rawHeaders == null)
                return [];

            if (rawHeaders is Dictionary<string, object?> headerDictionary)
            {
                return headerDictionary.Select(header => new KeyValuePair<string, string>(
                    header.Key,
                    FormatUtils.FormatOutputValue(header.Value))).ToList();
            }

            if (rawHeaders is not string headerText)
                throw new InvalidCastException("headers 값은 문자열이나 딕셔너리여야 하잖아;;");

            List<KeyValuePair<string, string>> headers = [];
            foreach (string line in headerText.Replace("\r\n", "\n").Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                int separator = line.IndexOf(':');
                if (separator <= 0)
                    throw new FormatException(line + " 헤더 형식이 이상하잖아;;");

                string name = line[..separator].Trim();
                string value = line[(separator + 1)..].Trim();
                if (name.Length == 0)
                    throw new FormatException("헤더 이름이 비었잖아;;");

                headers.Add(new KeyValuePair<string, string>(name, value));
            }

            return headers;
        }

        public static void ApplyRequestHeaders(
            HttpRequestMessage message,
            IEnumerable<KeyValuePair<string, string>> headers)
        {
            foreach ((string name, string value) in headers)
            {
                if (message.Headers.TryAddWithoutValidation(name, value))
                    continue;

                message.Content ??= new ByteArrayContent([]);
                message.Content.Headers.Remove(name);
                if (!message.Content.Headers.TryAddWithoutValidation(name, value))
                    throw new FormatException(name + " 헤더를 요청에 넣을 수가 없잖아;;");
            }
        }

        public static string SerializeRequestBody(object body, string? mediaType)
        {
            if (body is string text)
                return text;

            if (IsFormMediaType(mediaType) && body is Dictionary<string, object?> form)
            {
                return string.Join("&", form.Select(field =>
                    $"{WebUtility.UrlEncode(field.Key)}={WebUtility.UrlEncode(FormatUtils.FormatOutputValue(field.Value))}"));
            }

            return JsonSerializer.Serialize(body);
        }

        public static Dictionary<string, object?> GetResponseHeaders(HttpResponseMessage response)
        {
            Dictionary<string, object?> headers = new(StringComparer.OrdinalIgnoreCase);

            foreach ((string name, IEnumerable<string> values) in response.Headers)
                headers[name] = string.Join(", ", values);

            foreach ((string name, IEnumerable<string> values) in response.Content.Headers)
                headers[name] = string.Join(", ", values);

            return headers;
        }

        public static bool TryParseJson(string text, out object? value)
        {
            try
            {
                using JsonDocument document = JsonDocument.Parse(text);
                value = InternalUtils.ConvertJsonValue(document.RootElement);
                return true;
            }
            catch (JsonException)
            {
                value = null;
                return false;
            }
        }

        public static Dictionary<string, object?> ParseForm(string text)
        {
            Dictionary<string, object?> form = [];
            if (string.IsNullOrEmpty(text))
                return form;

            foreach (string field in text.Split('&'))
            {
                int separator = field.IndexOf('=');
                string name = separator < 0 ? field : field[..separator];
                string value = separator < 0 ? "" : field[(separator + 1)..];
                form[WebUtility.UrlDecode(name)] = WebUtility.UrlDecode(value);
            }

            return form;
        }

        public static string? GetMediaType(string? contentType)
        {
            if (string.IsNullOrWhiteSpace(contentType))
                return null;

            return contentType.Split(';', 2)[0].Trim();
        }

        public static bool IsJsonMediaType(string? mediaType)
        {
            return mediaType != null &&
                (mediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase) ||
                 mediaType.Equals("text/json", StringComparison.OrdinalIgnoreCase) ||
                 mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsFormMediaType(string? mediaType)
        {
            return mediaType != null &&
                mediaType.Equals("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase);
        }
    }
}