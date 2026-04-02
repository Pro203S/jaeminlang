namespace jaeminlang
{
    public class Variables
    {
        public static Dictionary<string, object?> storage = [];

        public static void SetValue(string key, object? value)
        {
            storage[key] = value;
        }

        public static object? GetValue(string key)
        {
            try
            {
                return storage[key];
            }
            catch
            {
                return null;
            }
        }
    }
}
