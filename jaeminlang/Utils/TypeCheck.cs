using System.Globalization;

namespace jaeminlang.Utils
{
    public static class TypeCheck
    {
        public static bool IsNumber(object n)
        {
            string? str = Convert.ToString(n, Global.Culture);
            if (str == null)
                return false;

            return double.TryParse(str, NumberStyles.Float, Global.Culture, out _);
        }

        public static bool IsExpression(string n)
        {
            return !string.IsNullOrEmpty(n) &&
                (n.StartsWith("+") ||
                n.StartsWith("-") ||
                n.StartsWith("/") ||
                n.StartsWith("*") ||
                n.StartsWith("^"));
        }

        public static bool IsStringLiteral(string token)
        {
            return token.StartsWith('\"') && token.EndsWith('\"');
        }
    }
}