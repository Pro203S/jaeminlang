using System.Globalization;

namespace jaeminlang.Utils
{
    public static class ArrayUtils
    {
        public static object?[] GetArrayValue(string key)
        {
            object? raw = Variables.GetValue(key) ?? throw new ArgumentNullException(key + "이(가) 정의가 안됐잖아;;");
            return InternalUtils.GetArrayValue(raw, key + "은(는) 배열이 아니잖아;;");
        }

        public static void SetArrayValue(string key, string[] rawValues)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("배열 이름이 비었잖아;;");

            object?[] values = rawValues.Select(VarUtils.ResolveAssignableValue).ToArray();
            Variables.SetValue(key, values);
        }

        public static void SetArrayItemValue(string rawKey, string rawValue)
        {
            SetArrayItemValue(rawKey, VarUtils.ResolveAssignableValue(rawValue));
        }

        public static void SetArrayItemValue(string rawKey, object? rawValue)
        {
            (string arrayName, int[] indices) = ParseArrayAccess(rawKey);
            object?[] array = GetArrayValue(arrayName);

            for (int i = 0; i < indices.Length - 1; i++)
            {
                object? next = InternalUtils.GetArrayElement(array, indices[i]);
                array = InternalUtils.GetArrayValue(next, "배열 안에 배열이 있어야 하잖아;;");
            }

            int lastIndex = indices[^1];
            InternalUtils.EnsureArrayIndex(array, lastIndex);
            array[lastIndex] = rawValue;
        }

        public static object? ResolveArrayItemValue(string token)
        {
            (string arrayName, int[] indices) = ParseArrayAccess(token);
            object? current = Variables.GetValue(arrayName) ?? throw new ArgumentNullException(arrayName + "이(가) 정의가 안됐잖아;;");

            foreach (int index in indices)
            {
                object?[] array = InternalUtils.GetArrayValue(current, "배열 접근이 이상하잖아;;");
                current = InternalUtils.GetArrayElement(array, index);
            }

            return current;
        }

        public static (string arrayName, int[] indices) ParseArrayAccess(string token)
        {
            string[] parts = token.Split('.');
            if (parts.Length < 2)
                throw new InvalidCastException("어이쿠?? 넌 이게 딕셔너리냐?? 숫자를 적어야지;;");

            List<int> indices = [];
            foreach (string part in parts.Skip(1))
            {
                indices.Add(ResolveArrayIndex(part));
            }

            return (parts[0], [.. indices]);
        }

        public static int ResolveArrayIndex(string token)
        {
            if (int.TryParse(token, NumberStyles.Integer, Global.Culture, out int literalIndex))
                return literalIndex;

            if (!Variables.TryGetValue(token, out object? rawIndex))
                throw new ArgumentNullException(token + "이(가) 정의가 안됐잖아;;");

            double index = FormatUtils.ConvertToNumber(rawIndex, token + "은(는) 배열 인덱스로 쓸 수 있는 숫자가 아니잖아;;");
            if (!double.IsFinite(index) || index != Math.Truncate(index) || index < int.MinValue || index > int.MaxValue)
                throw new InvalidCastException(token + "은(는) 배열 인덱스로 쓸 수 있는 정수가 아니잖아;;");

            return (int)index;
        }
    }
}