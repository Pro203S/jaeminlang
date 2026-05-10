namespace jaeminlang
{
    public static class Variables
    {
        private static readonly List<Dictionary<string, object?>> scopes =
        [
            []
        ];

        public static IReadOnlyDictionary<string, object?> storage => scopes[0];

        public static void Reset()
        {
            scopes.Clear();
            scopes.Add([]);
        }

        public static void PushScope()
        {
            scopes.Add([]);
        }

        public static void PopScope()
        {
            if (scopes.Count == 1)
                throw new InvalidOperationException("전역 스코프는 지우면 안되지;;");

            scopes.RemoveAt(scopes.Count - 1);
        }

        public static bool ContainsKey(string key)
        {
            return Utils.FindScope(scopes, key) != null;
        }

        public static void SetValue(string key, object? value)
        {
            Dictionary<string, object?> targetScope = Utils.FindScope(scopes, key) ?? scopes[^1];
            targetScope[key] = value;
        }

        public static void SetLocalValue(string key, object? value)
        {
            scopes[^1][key] = value;
        }

        public static object? GetValue(string key)
        {
            return TryGetValue(key, out object? value) ? value : null;
        }

        public static bool TryGetValue(string key, out object? value)
        {
            for (int i = scopes.Count - 1; i >= 0; i--)
            {
                if (scopes[i].TryGetValue(key, out value))
                    return true;
            }

            value = null;
            return false;
        }
    }
}
