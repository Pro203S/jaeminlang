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
            return storage[key];
        }

        public static void RemoveValue(string key)
        {
            storage.Remove(key);
        }
    }
}
