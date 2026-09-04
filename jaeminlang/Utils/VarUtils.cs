namespace jaeminlang.Utils
{
    public static class VarUtils
    {
        public static string GetStringValue(string key)
        {
            object? raw = Variables.GetValue(key);

            if (raw == null)
                throw new NullReferenceException(key + " 변수에 값이 저장이 안되어있잖아;;");

            return raw switch
            {
                string s => s,
                _ => FormatUtils.FormatOutputValue(raw)
            };
        }

        public static double GetNumberValue(string key)
        {
            object? raw = Variables.GetValue(key);

            if (raw == null)
                throw new ArgumentNullException(key + "이(가) 정의가 안됐잖아;;");

            return FormatUtils.ConvertToNumber(raw, key + "은(는) 숫자가 아니잖아;;");
        }

        public static object? ResolveAssignableValue(string token)
        {
            if (token == "여친")
                return null;

            if (TypeCheck.IsStringLiteral(token))
                return token[1..^1];

            if (TypeCheck.IsNumber(token))
                return double.Parse(token, Global.Culture);

            if (token.Contains('.'))
                return CollectionUtils.ResolveCollectionItemValue(token);

            if (!Variables.ContainsKey(token))
                throw new ArgumentNullException(token + "이(가) 정의가 안됐잖아;;");

            return Variables.GetValue(token);
        }

        public static string ResolveOutput(string token)
        {
            return FormatUtils.FormatOutputValue(ResolveOptionalValue(token));
        }

        public static object? ResolveOptionalValue(string token)
        {
            if (token == "여친")
                return null;

            if (TypeCheck.IsStringLiteral(token))
                return token[1..^1];

            if (TypeCheck.IsNumber(token))
                return double.Parse(token, Global.Culture);

            if (token.Contains('.'))
                return CollectionUtils.ResolveCollectionItemValue(token);

            return Variables.GetValue(token);
        }

        public static double ResolveNumberValue(string token)
        {
            if (TypeCheck.IsNumber(token))
                return double.Parse(token, Global.Culture);

            if (token.Contains('.'))
                return FormatUtils.ConvertToNumber(CollectionUtils.ResolveCollectionItemValue(token), token + "은(는) 숫자가 아니잖아;;");

            return GetNumberValue(token);
        }

        public static object? ResolveSingleValue(
            string[] tokens,
            Func<string, bool>? functionExists,
            Func<string, string[], object?[]>? invokeFunction)
        {
            if (LooksLikeFunctionCall(tokens, functionExists))
            {
                object?[] returned = InvokeFunctionTokens(tokens, invokeFunction);
                if (returned.Length != 1)
                    throw new ArgumentException("값은 하나여야지;;");

                return returned[0];
            }

            if (tokens.Length != 1)
                throw new ArgumentException("값은 하나여야지;;");

            return ResolveAssignableValue(tokens[0]);
        }

        public static object?[] ResolveOutputValues(
            string[] tokens,
            Func<string, bool>? functionExists,
            Func<string, string[], object?[]>? invokeFunction)
        {
            if (LooksLikeFunctionCall(tokens, functionExists))
                return InvokeFunctionTokens(tokens, invokeFunction);

            return tokens.Select(ResolveOptionalValue).ToArray();
        }

        public static object?[] ResolveReturnValues(
            string[] tokens,
            Func<string, bool>? functionExists,
            Func<string, string[], object?[]>? invokeFunction)
        {
            if (tokens.Length == 0)
                return [];

            if (LooksLikeFunctionCall(tokens, functionExists))
                return InvokeFunctionTokens(tokens, invokeFunction);

            return tokens.Select(ResolveAssignableValue).ToArray();
        }

        public static bool LooksLikeFunctionCall(string[] tokens, Func<string, bool>? functionExists)
        {
            if (tokens.Length == 0 || functionExists == null)
                return false;

            if (!functionExists(tokens[0]))
                return false;

            return tokens.Length > 1 || !Variables.ContainsKey(tokens[0]);
        }

        public static object?[] InvokeFunctionTokens(
            string[] tokens,
            Func<string, string[], object?[]>? invokeFunction)
        {
            if (invokeFunction == null)
                throw new InvalidOperationException("함수를 실행할 수가 없잖아;;");

            return invokeFunction(tokens[0], tokens.Skip(1).ToArray());
        }
    }
}