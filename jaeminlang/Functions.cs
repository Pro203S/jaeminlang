namespace jaeminlang
{
    public struct Function
    {
        public int start;
        public int end;
    }
    public class Functions
    {
        public static Dictionary<string, Function> functions = [];

        public static void SetValue(string key, Function func)
        {
            functions[key] = func;
        }

        public static object? GetValue(string key)
        {
            try
            {
                return functions[key];
            }
            catch
            {
                return null;
            }
        }
    }
}
