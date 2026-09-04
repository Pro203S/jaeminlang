namespace jaeminlang.Utils
{
    public static class CollectionUtils
    {
        public static object? ResolveCollectionItemValue(string token)
        {
            (string collectionName, string[] members) = ParseCollectionAccess(token);
            if (!Variables.TryGetValue(collectionName, out object? current))
                throw new ArgumentNullException(collectionName + "이(가) 정의가 안됐잖아;;");

            foreach (string member in members)
            {
                current = InternalUtils.GetCollectionElement(current, member);
            }

            return current;
        }

        public static void SetCollectionItemValue(string token, object? value)
        {
            (string collectionName, string[] members) = ParseCollectionAccess(token);
            if (!Variables.TryGetValue(collectionName, out object? current))
                throw new ArgumentNullException(collectionName + "이(가) 정의가 안됐잖아;;");

            for (int i = 0; i < members.Length - 1; i++)
            {
                current = InternalUtils.GetCollectionElement(current, members[i]);
            }

            InternalUtils.SetCollectionElement(current, members[^1], value);
        }

        public static (string collectionName, string[] members) ParseCollectionAccess(string token)
        {
            string[] parts = token.Split('.');
            if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[0]))
                throw new InvalidCastException("컬렉션 접근이 이상하잖아;;");

            return (parts[0], parts[1..]);
        }
    }
}