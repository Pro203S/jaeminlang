using System.Globalization;
using System.Text;

namespace jaeminlang
{
    public static class Utils
    {
        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

        public static void HandleError(string message)
        {
            Stream stderr = Console.OpenStandardError();
            stderr.Write(Encoding.UTF8.GetBytes(message + Environment.NewLine));
        }

        public static void HandleError(Exception exception, string? context = null)
        {
            string message = string.IsNullOrWhiteSpace(context)
                ? exception.Message
                : $"{context}: {exception.Message}";

            HandleError(message);
        }

        #region parse

        public static string[] GetArguments(string raw)
        {
            List<string> result = [];
            List<char> buffer = [];

            bool isEscaped = false;

            foreach (char c in raw)
            {
                if (isEscaped)
                {
                    isEscaped = false;
                    if (c == 'n')
                    {
                        buffer.Add('\n');
                    }
                    else if (c == 'r')
                    {
                        buffer.Add('\r');
                    }
                    else
                    {
                        buffer.Add(c);
                    }
                    continue;
                }

                if (c == '\\')
                {
                    isEscaped = true;
                    continue;
                }

                if (c == ',')
                {
                    result.Add(new string([.. buffer]));
                    buffer.Clear();
                    continue;
                }

                buffer.Add(c);
            }

            result.Add(new string([.. buffer]));

            return [.. result];
        }

        public static bool ShouldSkipLine(string line)
        {
            return string.IsNullOrWhiteSpace(line) || line.StartsWith("어이쿠");
        }

        public static int FindFunctionReturnLine(string[] fileContent, int startIndex)
        {
            for (int i = startIndex; i < fileContent.Length; i++)
            {
                if (ShouldSkipLine(fileContent[i]))
                    continue;

                string[] args = GetArguments(fileContent[i]);
                if (args.Length > 0 && args[0] == "음...")
                    return i;
            }

            throw new ArgumentException("함수 끝에 음... 이 없잖아;;");
        }

        #endregion

        #region 타입 체크

        public static bool IsNumber(object n)
        {
            string? str = Convert.ToString(n, Culture);
            if (str == null)
                return false;

            return double.TryParse(str, NumberStyles.Float, Culture, out _);
        }

        public static bool IsExpression(string n)
        {
            return !string.IsNullOrEmpty(n) &&
                (n.StartsWith("+") ||
                n.StartsWith("-") ||
                n.StartsWith("/") ||
                n.StartsWith("*") ||
                n.StartsWith("^"));
        }

        public static bool IsStringLiteral(string token)
        {
            return token.StartsWith('\"') && token.EndsWith('\"');
        }

        #endregion

        #region 변수

        public static string GetStringValue(string key)
        {
            object? raw = Variables.GetValue(key);

            if (raw == null)
                throw new NullReferenceException(key + " 변수에 값이 저장이 안되어있잖아;;");

            return raw switch
            {
                string s => s,
                _ => FormatOutputValue(raw)
            };
        }

        public static double GetNumberValue(string key)
        {
            object? raw = Variables.GetValue(key);

            if (raw == null)
                throw new ArgumentNullException(key + "이(가) 정의가 안됐잖아;;");

            return ConvertToNumber(raw, key + "은(는) 숫자가 아니잖아;;");
        }

        public static object? ResolveAssignableValue(string token)
        {
            if (token == "여친")
                return null;

            if (IsStringLiteral(token))
                return token[1..^1];

            if (IsNumber(token))
                return double.Parse(token, Culture);

            if (token.Contains('.'))
                return ResolveCollectionItemValue(token);

            if (!Variables.ContainsKey(token))
                throw new ArgumentNullException(token + "이(가) 정의가 안됐잖아;;");

            return Variables.GetValue(token);
        }

        public static string ResolveOutput(string token)
        {
            return FormatOutputValue(ResolveOptionalValue(token));
        }

        public static object? ResolveOptionalValue(string token)
        {
            if (token == "여친")
                return null;

            if (IsStringLiteral(token))
                return token[1..^1];

            if (IsNumber(token))
                return double.Parse(token, Culture);

            if (token.Contains('.'))
                return ResolveCollectionItemValue(token);

            return Variables.GetValue(token);
        }

        public static double ResolveNumberValue(string token)
        {
            if (IsNumber(token))
                return double.Parse(token, Culture);

            if (token.Contains('.'))
                return ConvertToNumber(ResolveCollectionItemValue(token), token + "은(는) 숫자가 아니잖아;;");

            return GetNumberValue(token);
        }

        public static object? ResolveSingleValue(
            string[] tokens,
            Func<string, bool>? functionExists,
            Func<string, string[], object?[]>? invokeFunction)
        {
            if (LooksLikeFunctionCall(tokens, functionExists))
            {
                object?[] returned = InvokeFunctionTokens(tokens, invokeFunction);
                if (returned.Length != 1)
                    throw new ArgumentException("값은 하나여야지;;");

                return returned[0];
            }

            if (tokens.Length != 1)
                throw new ArgumentException("값은 하나여야지;;");

            return ResolveAssignableValue(tokens[0]);
        }

        public static object?[] ResolveOutputValues(
            string[] tokens,
            Func<string, bool>? functionExists,
            Func<string, string[], object?[]>? invokeFunction)
        {
            if (LooksLikeFunctionCall(tokens, functionExists))
                return InvokeFunctionTokens(tokens, invokeFunction);

            return tokens.Select(ResolveOptionalValue).ToArray();
        }

        public static object?[] ResolveReturnValues(
            string[] tokens,
            Func<string, bool>? functionExists,
            Func<string, string[], object?[]>? invokeFunction)
        {
            if (tokens.Length == 0)
                return [];

            if (LooksLikeFunctionCall(tokens, functionExists))
                return InvokeFunctionTokens(tokens, invokeFunction);

            return tokens.Select(ResolveAssignableValue).ToArray();
        }

        public static bool LooksLikeFunctionCall(string[] tokens, Func<string, bool>? functionExists)
        {
            if (tokens.Length == 0 || functionExists == null)
                return false;

            if (!functionExists(tokens[0]))
                return false;

            return tokens.Length > 1 || !Variables.ContainsKey(tokens[0]);
        }

        public static object?[] InvokeFunctionTokens(
            string[] tokens,
            Func<string, string[], object?[]>? invokeFunction)
        {
            if (invokeFunction == null)
                throw new InvalidOperationException("함수를 실행할 수가 없잖아;;");

            return invokeFunction(tokens[0], tokens.Skip(1).ToArray());
        }

        #endregion

        #region 배열

        public static object?[] GetArrayValue(string key)
        {
            object? raw = Variables.GetValue(key) ?? throw new ArgumentNullException(key + "이(가) 정의가 안됐잖아;;");
            return GetArrayValue(raw, key + "은(는) 배열이 아니잖아;;");
        }

        public static void SetArrayValue(string key, string[] rawValues)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("배열 이름이 비었잖아;;");

            object?[] values = rawValues.Select(ResolveAssignableValue).ToArray();
            Variables.SetValue(key, values);
        }

        public static void SetArrayItemValue(string rawKey, string rawValue)
        {
            SetArrayItemValue(rawKey, ResolveAssignableValue(rawValue));
        }

        public static void SetArrayItemValue(string rawKey, object? rawValue)
        {
            (string arrayName, int[] indices) = ParseArrayAccess(rawKey);
            object?[] array = GetArrayValue(arrayName);

            for (int i = 0; i < indices.Length - 1; i++)
            {
                object? next = GetArrayElement(array, indices[i]);
                array = GetArrayValue(next, "배열 안에 배열이 있어야 하잖아;;");
            }

            int lastIndex = indices[^1];
            EnsureArrayIndex(array, lastIndex);
            array[lastIndex] = rawValue;
        }

        public static object? ResolveArrayItemValue(string token)
        {
            (string arrayName, int[] indices) = ParseArrayAccess(token);
            object? current = Variables.GetValue(arrayName) ?? throw new ArgumentNullException(arrayName + "이(가) 정의가 안됐잖아;;");

            foreach (int index in indices)
            {
                object?[] array = GetArrayValue(current, "배열 접근이 이상하잖아;;");
                current = GetArrayElement(array, index);
            }

            return current;
        }

        public static (string arrayName, int[] indices) ParseArrayAccess(string token)
        {
            string[] parts = token.Split('.');
            if (parts.Length < 2)
                throw new InvalidCastException("어이쿠?? 넌 이게 딕셔너리냐?? 숫자를 적어야지;;");

            List<int> indices = [];
            foreach (string part in parts.Skip(1))
            {
                indices.Add(ResolveArrayIndex(part));
            }

            return (parts[0], [.. indices]);
        }

        private static int ResolveArrayIndex(string token)
        {
            if (int.TryParse(token, NumberStyles.Integer, Culture, out int literalIndex))
                return literalIndex;

            if (!Variables.TryGetValue(token, out object? rawIndex))
                throw new ArgumentNullException(token + "이(가) 정의가 안됐잖아;;");

            double index = ConvertToNumber(rawIndex, token + "은(는) 배열 인덱스로 쓸 수 있는 숫자가 아니잖아;;");
            if (!double.IsFinite(index) || index != Math.Truncate(index) || index < int.MinValue || index > int.MaxValue)
                throw new InvalidCastException(token + "은(는) 배열 인덱스로 쓸 수 있는 정수가 아니잖아;;");

            return (int)index;
        }

        #endregion

        #region 딕셔너리

        public static Dictionary<string, object?> GetDictionaryValue(string key)
        {
            object? raw = Variables.GetValue(key) ?? throw new ArgumentNullException(key + "이(가) 정의가 안됐잖아;;");
            return GetDictionaryValue(raw, key + "은(는) 딕셔너리가 아니잖아;;");
        }

        public static void SetDictionaryValue(string key, string[] rawEntries)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("딕셔너리 이름이 비었잖아;;");

            if (rawEntries.Length % 2 != 0)
                throw new ArgumentException("딕셔너리는 키와 값을 짝으로 줘야지;;");

            Dictionary<string, object?> dictionary = [];
            for (int i = 0; i < rawEntries.Length; i += 2)
            {
                string dictionaryKey = ResolveDictionaryKey(rawEntries[i]);
                dictionary[dictionaryKey] = ResolveAssignableValue(rawEntries[i + 1]);
            }

            Variables.SetValue(key, dictionary);
        }

        #endregion

        #region 컬렉션

        public static object? ResolveCollectionItemValue(string token)
        {
            (string collectionName, string[] members) = ParseCollectionAccess(token);
            if (!Variables.TryGetValue(collectionName, out object? current))
                throw new ArgumentNullException(collectionName + "이(가) 정의가 안됐잖아;;");

            foreach (string member in members)
            {
                current = GetCollectionElement(current, member);
            }

            return current;
        }

        public static void SetCollectionItemValue(string token, object? value)
        {
            (string collectionName, string[] members) = ParseCollectionAccess(token);
            if (!Variables.TryGetValue(collectionName, out object? current))
                throw new ArgumentNullException(collectionName + "이(가) 정의가 안됐잖아;;");

            for (int i = 0; i < members.Length - 1; i++)
            {
                current = GetCollectionElement(current, members[i]);
            }

            SetCollectionElement(current, members[^1], value);
        }

        public static (string collectionName, string[] members) ParseCollectionAccess(string token)
        {
            string[] parts = token.Split('.');
            if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[0]))
                throw new InvalidCastException("컬렉션 접근이 이상하잖아;;");

            return (parts[0], parts[1..]);
        }

        #endregion

        #region 포맷

        public static string FormatOutputValue(object? value)
        {
            return value switch
            {
                null => "여친",
                string s => s,
                double d => d.ToString("0.###############################", Culture),
                float f => f.ToString("0.###############################", Culture),
                object?[] array => "[" + string.Join(", ", array.Select(FormatOutputValue)) + "]",
                double[] numbers => "[" + string.Join(", ", numbers.Select(number => FormatOutputValue(number))) + "]",
                Dictionary<string, object?> dictionary => "{" + string.Join(", ", dictionary.Select(entry => $"{entry.Key}: {FormatOutputValue(entry.Value)}")) + "}",
                IFormattable formattable => formattable.ToString(null, Culture),
                _ => value.ToString() ?? "여친"
            };
        }

        public static double ConvertToNumber(object? value, string message)
        {
            if (value == null)
                throw new InvalidCastException(message);

            if (value is string s && double.TryParse(s, NumberStyles.Float, Culture, out double parsed))
                return parsed;

            if (value is IConvertible convertible)
            {
                try
                {
                    return Convert.ToDouble(convertible, Culture);
                }
                catch
                {
                }
            }

            throw new InvalidCastException(message);
        }

        #endregion

        #region 스코프

        public static Dictionary<string, object?>? FindScope(
            IReadOnlyList<Dictionary<string, object?>> scopes,
            string key)
        {
            for (int i = scopes.Count - 1; i >= 0; i--)
            {
                if (scopes[i].ContainsKey(key))
                    return scopes[i];
            }

            return null;
        }

        #endregion

        #region 내부 모듈

        private static object?[] GetArrayValue(object? raw, string message)
        {
            return raw switch
            {
                object?[] array => array,
                double[] numbers => [.. numbers.Cast<object?>()],
                _ => throw new InvalidCastException(message)
            };
        }

        private static Dictionary<string, object?> GetDictionaryValue(object? raw, string message)
        {
            return raw as Dictionary<string, object?> ?? throw new InvalidCastException(message);
        }

        private static object? GetCollectionElement(object? collection, string member)
        {
            if (collection is Dictionary<string, object?> dictionary)
            {
                string key = ResolveDictionaryKey(member);
                if (!dictionary.TryGetValue(key, out object? value))
                    throw new KeyNotFoundException(key + " 키가 딕셔너리에 없잖아;;");

                return value;
            }

            object?[] array = GetArrayValue(collection, "배열이나 딕셔너리가 아니잖아;;");
            return GetArrayElement(array, ResolveArrayIndex(member));
        }

        private static void SetCollectionElement(object? collection, string member, object? value)
        {
            if (collection is Dictionary<string, object?> dictionary)
            {
                dictionary[ResolveDictionaryKey(member)] = value;
                return;
            }

            object?[] array = GetArrayValue(collection, "배열이나 딕셔너리가 아니잖아;;");
            int index = ResolveArrayIndex(member);
            EnsureArrayIndex(array, index);
            array[index] = value;
        }

        private static string ResolveDictionaryKey(string token)
        {
            string key = IsStringLiteral(token) ? token[1..^1] : token;
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("딕셔너리 키가 비었잖아;;");

            return key;
        }

        private static object? GetArrayElement(object?[] array, int index)
        {
            EnsureArrayIndex(array, index);
            return array[index];
        }

        private static void EnsureArrayIndex(object?[] array, int index)
        {
            if (index < 0 || index >= array.Length)
                throw new IndexOutOfRangeException("배열 범위를 벗어났잖아;;");
        }

        #endregion
    }
}
