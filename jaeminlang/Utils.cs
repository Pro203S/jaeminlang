namespace jaeminlang
{
    public class Utils
    {
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

        public static bool IsNumber(object n)
        {
            string? str = Convert.ToString(n);
            if (str == null) return false;
            return double.TryParse(str, out _);
        }

        public static bool IsExpression(string n)
        {
            return n.StartsWith("+") ||
                n.StartsWith("-") ||
                n.StartsWith("/") ||
                n.StartsWith("*") ||
                n.StartsWith("^");
        }

        public static string GetStringValue(string key)
        {
            object? raw = Variables.GetValue(key);

            if (raw == null)
                throw new NullReferenceException(key + " 변수에 값이 저장이 안되어있잖아;;");

            return raw switch
            {
                string s => s,
                int i => i.ToString(),
                _ => raw.ToString() ?? ""
            };
        }

        public static double GetNumberValue(string key)
        {
            object? raw = Variables.GetValue(key);

            if (raw == null)
                throw new ArgumentNullException(key + "이(가) 정의가 안됐잖아;;");

            return raw switch
            {
                double i => i,
                string s when double.TryParse(s, out var v) => v,
                _ => throw new InvalidCastException(key + "은(는) 숫자가 아니잖아;;")
            };
        }

        public static double[] GetNumberArray(string key)
        {
            object? raw = Variables.GetValue(key) ?? throw new ArgumentNullException(key + "이(가) 정의가 안됐잖아;;");

            if (raw is double[] numbers)
                return numbers;

            if (raw is not object[] objects)
                throw new InvalidCastException(key + "은(는) 배열이 아니잖아;;");

            List<double> parsedNumbers = [];
            foreach (object obj in objects)
            {
                string? str = Convert.ToString(obj);
                if (str == null) continue;
                if (!IsNumber(str))
                {
                    continue;
                }

                parsedNumbers.Add(double.Parse(str));
            }

            return [.. parsedNumbers];
        }

        public static void SetArrayValue(string key, string[] rawValues)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("배열 이름이 비었잖아;;");

            double[] values = rawValues.Select(ResolveNumberValue).ToArray();
            Variables.SetValue(key, values);
        }

        public static void SetArrayItemValue(string rawKey, string rawValue)
        {
            (string arrayName, int arrayIndex) = ParseArrayAccess(rawKey);
            double[] array = GetNumberArray(arrayName);

            if (arrayIndex < 0 || arrayIndex >= array.Length)
                throw new IndexOutOfRangeException("배열 범위를 벗어났잖아;;");

            array[arrayIndex] = ResolveNumberValue(rawValue);
            Variables.SetValue(arrayName, array);
        }

        public static object ResolveAssignableValue(string token)
        {
            if (IsStringLiteral(token))
                return token[1..^1];

            if (IsNumber(token))
                return int.Parse(token);

            if (token.Contains('.'))
                return ResolveArrayItemValue(token);

            if (!Variables.storage.ContainsKey(token))
                throw new ArgumentNullException(token + "이(가) 정의가 안됐잖아;;");

            return Variables.GetValue(token) ?? throw new NullReferenceException(token + " 변수에 값이 저장이 안되어있잖아;;");
        }

        public static string ResolveOutput(string token)
        {
            if (IsStringLiteral(token))
                return token[1..^1];

            if (IsNumber(token))
                return token;

            if (token.Contains('.'))
                return ResolveArrayItemValue(token).ToString() ?? "여친";

            if (!Variables.storage.ContainsKey(token))
                return "여친";

            object? value = Variables.GetValue(token);
            return value?.ToString() ?? "여친";
        }

        public static double ResolveNumberValue(string token)
        {
            if (IsNumber(token))
                return double.Parse(token);

            if (token.Contains('.'))
                return ResolveArrayItemValue(token);

            return GetNumberValue(token);
        }

        public static double ResolveArrayItemValue(string token)
        {
            (string arrayName, int arrayIndex) = ParseArrayAccess(token);
            double[] array = GetNumberArray(arrayName);

            if (arrayIndex < 0 || arrayIndex >= array.Length)
                throw new IndexOutOfRangeException("배열 범위를 벗어났잖아;;");

            return array[arrayIndex];
        }

        public static (string arrayName, int arrayIndex) ParseArrayAccess(string token)
        {
            string[] parts = token.Split('.', 2);
            if (parts.Length != 2 || !int.TryParse(parts[1], out int arrayIndex))
                throw new InvalidCastException("어이쿠?? 넌 이게 딕셔너리냐?? 숫자를 적어야지;;");

            return (parts[0], arrayIndex);
        }

        public static bool IsStringLiteral(string token)
        {
            return token.StartsWith('\"') && token.EndsWith('\"');
        }
    }
}
