namespace jaeminlang.Utils
{
    public static class Scope
    {
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
    }
}