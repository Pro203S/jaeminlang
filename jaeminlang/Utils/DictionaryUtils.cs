namespace jaeminlang.Utils
{
    public static class DictionaryUtils
    {
        public static Dictionary<string, object?> GetDictionaryValue(string key)
        {
            object? raw = Variables.GetValue(key) ?? throw new ArgumentNullException(key + "이(가) 정의가 안됐잖아;;");
            return InternalUtils.GetDictionaryValue(raw, key + "은(는) 딕셔너리가 아니잖아;;");
        }

        public static void SetDictionaryValue(string key, string[] rawEntries)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("딕셔너리 이름이 비었잖아;;");

            if (rawEntries.Length % 2 != 0)
                throw new ArgumentException("딕셔너리는 키와 값을 짝으로 줘야지;;");

            Dictionary<string, object?> dictionary = [];
            for (int i = 0; i < rawEntries.Length; i += 2)
            {
                string dictionaryKey = InternalUtils.ResolveDictionaryKey(rawEntries[i]);
                dictionary[dictionaryKey] = VarUtils.ResolveAssignableValue(rawEntries[i + 1]);
            }

            Variables.SetValue(key, dictionary);
        }
    }
}