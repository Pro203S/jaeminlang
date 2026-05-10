namespace jaeminlang
{
    public struct Function
    {
        public int bodyStart;
        public int returnLine;
        public string[] parameters;
    }

    public static class Functions
    {
        private static readonly Dictionary<string, Function> functions = [];

        public static void Reset()
        {
            functions.Clear();
        }

        public static bool Contains(string key)
        {
            return functions.ContainsKey(key);
        }

        public static void SetValue(string key, Function func)
        {
            functions[key] = func;
        }

        public static Function? GetValue(string key)
        {
            return functions.TryGetValue(key, out Function function) ? function : null;
        }

        public static Function GetRequired(string key)
        {
            return GetValue(key) ?? throw new ArgumentNullException(key + " 함수가 정의가 안됐잖아;;");
        }
    }
}
