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
                    buffer.Add(c);
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
    }
}
