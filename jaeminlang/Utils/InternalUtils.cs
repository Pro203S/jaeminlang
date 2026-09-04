using System.Text.Json;

namespace jaeminlang.Utils
{
    internal static class InternalUtils
    {
        public static object? ConvertJsonValue(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Object => element.EnumerateObject().ToDictionary(
                    property => property.Name,
                    property => ConvertJsonValue(property.Value)),
                JsonValueKind.Array => element.EnumerateArray().Select(ConvertJsonValue).ToArray(),
                JsonValueKind.Number => element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Null or JsonValueKind.Undefined => null,
                _ => element.GetRawText()
            };
        }

        public static object?[] GetArrayValue(object? raw, string message)
        {
            return raw switch
            {
                object?[] array => array,
                double[] numbers => [.. numbers.Cast<object?>()],
                _ => throw new InvalidCastException(message)
            };
        }

        public static Dictionary<string, object?> GetDictionaryValue(object? raw, string message)
        {
            return raw as Dictionary<string, object?> ?? throw new InvalidCastException(message);
        }

        public static object? GetCollectionElement(object? collection, string member)
        {
            if (collection is Dictionary<string, object?> dictionary)
            {
                string key = ResolveDictionaryKey(member);
                if (!dictionary.TryGetValue(key, out object? value))
                    throw new KeyNotFoundException(key + " 키가 딕셔너리에 없잖아;;");

                return value;
            }

            object?[] array = GetArrayValue(collection, "배열이나 딕셔너리가 아니잖아;;");
            return GetArrayElement(array, ArrayUtils.ResolveArrayIndex(member));
        }

        public static void SetCollectionElement(object? collection, string member, object? value)
        {
            if (collection is Dictionary<string, object?> dictionary)
            {
                dictionary[ResolveDictionaryKey(member)] = value;
                return;
            }

            object?[] array = GetArrayValue(collection, "배열이나 딕셔너리가 아니잖아;;");
            int index = ArrayUtils.ResolveArrayIndex(member);
            EnsureArrayIndex(array, index);
            array[index] = value;
        }

        public static string ResolveDictionaryKey(string token)
        {
            string key = TypeCheck.IsStringLiteral(token) ? token[1..^1] : token;
            if (string.IsNullOrEmpty(key))
                throw new ArgumentException("딕셔너리 키가 비었잖아;;");

            return key;
        }

        public static object? GetArrayElement(object?[] array, int index)
        {
            EnsureArrayIndex(array, index);
            return array[index];
        }

        public static void EnsureArrayIndex(object?[] array, int index)
        {
            if (index < 0 || index >= array.Length)
                throw new IndexOutOfRangeException("배열 범위를 벗어났잖아;;");
        }
    }
}