namespace jaeminlang.Utils
{
    public static class Parse
    {
        
        public static string[] GetArguments(string raw)
        {
            List<string> result = [];
            List<char> buffer = [];

            bool isEscaped = false;
            bool isInsideString = false;

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

                if (c == '"')
                {
                    isInsideString = !isInsideString;
                    buffer.Add(c);
                    continue;
                }

                if (c == '\\')
                {
                    isEscaped = true;
                    continue;
                }

                if (c == ',' && !isInsideString)
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

    }
}