using System.Globalization;

namespace jaeminlang.Utils
{
    public static class FormatUtils
    {
        public static string FormatOutputValue(object? value)
        {
            return value switch
            {
                null => "여친",
                string s => s,
                double d => d.ToString("0.###############################", Global.Culture),
                float f => f.ToString("0.###############################", Global.Culture),
                object?[] array => "[" + string.Join(", ", array.Select(FormatOutputValue)) + "]",
                double[] numbers => "[" + string.Join(", ", numbers.Select(number => FormatOutputValue(number))) + "]",
                Dictionary<string, object?> dictionary => "{" + string.Join(", ", dictionary.Select(entry => $"{entry.Key}: {FormatOutputValue(entry.Value)}")) + "}",
                IFormattable formattable => formattable.ToString(null, Global.Culture),
                _ => value.ToString() ?? "여친"
            };
        }

        public static double ConvertToNumber(object? value, string message)
        {
            if (value == null)
                throw new InvalidCastException(message);

            if (value is string s && double.TryParse(s, NumberStyles.Float, Global.Culture, out double parsed))
                return parsed;

            if (value is IConvertible convertible)
            {
                try
                {
                    return Convert.ToDouble(convertible, Global.Culture);
                }
                catch
                {
                }
            }

            throw new InvalidCastException(message);
        }
    }
}