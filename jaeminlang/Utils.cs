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
                    else if(c == 'r')
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

                if (c == '$')
                {
                    result.Add(new string([..buffer]));
                    buffer.Clear();
                    continue;
                }

                buffer.Add(c);
            }

            result.Add(new string([..buffer]));

            return [..result];
        }

        public static bool IsNumber(string n)
        {
            return int.TryParse(n, out _);
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

        public static int GetIntValue(string key)
        {
            object? raw = Variables.GetValue(key);

            if (raw == null)
                throw new ArgumentNullException(key + "이(가) 정의가 안됐잖아;;");

            return raw switch
            {
                int i => i,
                string s when int.TryParse(s, out var v) => v,
                _ => throw new InvalidCastException(key + "은(는) 숫자가 아니잖아;;")
            };
        }
    }
}
