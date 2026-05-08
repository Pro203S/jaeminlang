using System.Text;

namespace jaeminlang
{
    public class JMLCommand
    {
        public Action action = new(() => { });
        public string rawCmd;
        public string cmdName;
        public string[] args;

        public JMLCommand(
            string raw,
            Action<int>? repeat,
            Action<string, Function>? registerFunction = null,
            Action<string>? invokeFunction = null)
        {
            rawCmd = raw;
            args = Utils.GetArguments(raw);
            cmdName = args[0];

            if (cmdName == "안산" /* 출력 */)
            {
                action = new Action(() =>
                {
                    string content = args[1] ?? throw new NullReferenceException("안산에 인수가 없잖아;;");
                    Stream stdout = Console.OpenStandardOutput();
                    string output = ResolveOutput(content);

                    stdout.Write(Encoding.UTF8.GetBytes(output));
                });
                return;
            }

            if (cmdName == "재민" /* 입력 */)
            {
                action = new Action(() =>
                {
                    string key = args[1] ?? throw new NullReferenceException("재민에 인수가 없잖아;;");
                    string? data = Console.ReadLine();

                    Variables.SetValue(key, data ?? "");
                });
                return;
            }

            if (cmdName == "그램" /* 변수 */)
            {
                action = new Action(() =>
                {
                    string key = args[1] ?? throw new NullReferenceException("재민에 인수가 없잖아;;");

                    if (key.StartsWith('[') && key.EndsWith(']'))
                    {
                        SetArrayValue(key[1..^1], args.Skip(2).ToArray());
                        return;
                    }

                    string data = args[2] ?? throw new NullReferenceException("변수에 값은 줘야지;;");

                    if (key.Contains('.'))
                    {
                        SetArrayItemValue(key, data);
                        return;
                    }

                    if (Utils.IsExpression(data))
                    {
                        string exp = data[..1];
                        int origin = Utils.GetIntValue(key);
                        int valueToCalc = ResolveIntValue(data[1..]);

                        switch (exp)
                        {
                            case "+":
                                Variables.SetValue(key, origin + valueToCalc);
                                break;
                            case "-":
                                Variables.SetValue(key, origin - valueToCalc);
                                break;
                            case "*":
                                Variables.SetValue(key, origin * valueToCalc);
                                break;
                            case "/":
                                Variables.SetValue(key, origin / valueToCalc);
                                break;
                            case "^":
                                Variables.SetValue(key, origin ^ valueToCalc);
                                break;
                            default:
                                throw new ArgumentException("이런 수식은 안산에도 없어;;");
                        }
                        return;
                    }

                    if (data == "여친")
                    {
                        if (!Variables.storage.ContainsKey(key))
                            return;

                        Variables.SetValue(key, null);
                        return;
                    }

                    Variables.SetValue(key, ResolveAssignableValue(data));
                });
                return;
            }

            if (cmdName == "러스트" /* 반복 */)
            {
                action = new Action(() =>
                {
                    if (repeat == null) return;

                    string rawVal1 = args[1] ?? throw new NullReferenceException("이거 안주는건 너무하지;;");
                    string rawVal2 = args[2] ?? throw new NullReferenceException("뭐랑 같아야하는진 알아야지;;");
                    string rawGoTo = args[3] ?? throw new NullReferenceException("어디로 가야하는진 알아야 할 거 아니야;;");

                    int val1 = ResolveIntValue(rawVal1);
                    int val2 = ResolveIntValue(rawVal2);

                    if (!int.TryParse(rawGoTo, out int goTo))
                        throw new ArgumentException(rawGoTo + "은(는) 숫자가 아니잖아;;");

                    if (val1 != val2)
                        repeat(goTo);
                });
                return;
            }

            if (cmdName == "엘릭서" /* 함수 */)
            {
                action = new(() =>
                {
                    string functionName = args[1] ?? throw new NullReferenceException("함수 이름은 있어야지;;");

                    if (args.Length == 2)
                    {
                        if (invokeFunction == null)
                            throw new InvalidOperationException("함수를 실행할 수가 없잖아;;");

                        invokeFunction(functionName);
                        return;
                    }

                    string rawStart = args[2] ?? throw new NullReferenceException("함수 시작 줄을 줘야지;;");
                    string rawEnd = args[3] ?? throw new NullReferenceException("함수 끝 줄을 줘야지;;");

                    if (!int.TryParse(rawStart, out int start) || !int.TryParse(rawEnd, out int end))
                        throw new ArgumentException("함수 줄 번호는 숫자여야지;;");

                    if (start <= 0 || end < start)
                        throw new ArgumentException("함수 범위가 이상하잖아;;");

                    if (registerFunction == null)
                        throw new InvalidOperationException("함수를 저장할 수가 없잖아;;");

                    registerFunction(functionName, new Function
                    {
                        start = start,
                        end = end
                    });
                });
                return;
            }

            if (string.IsNullOrEmpty(cmdName)) return;

            throw new ArgumentException("아니 " + cmdName + "은(는) 안산에도 없는 명령언데;;");
        }

        private static void SetArrayValue(string key, string[] rawValues)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("배열 이름이 비었잖아;;");

            int[] values = rawValues.Select(ResolveIntValue).ToArray();
            Variables.SetValue(key, values);
        }

        private static void SetArrayItemValue(string rawKey, string rawValue)
        {
            (string arrayName, int arrayIndex) = ParseArrayAccess(rawKey);
            int[] array = Utils.GetIntArray(arrayName);

            if (arrayIndex < 0 || arrayIndex >= array.Length)
                throw new IndexOutOfRangeException("배열 범위를 벗어났잖아;;");

            array[arrayIndex] = ResolveIntValue(rawValue);
            Variables.SetValue(arrayName, array);
        }

        private static object ResolveAssignableValue(string token)
        {
            if (IsStringLiteral(token))
                return token[1..^1];

            if (Utils.IsNumber(token))
                return int.Parse(token);

            if (token.Contains('.'))
                return ResolveArrayItemValue(token);

            if (!Variables.storage.ContainsKey(token))
                throw new ArgumentNullException(token + "이(가) 정의가 안됐잖아;;");

            return Variables.GetValue(token) ?? throw new NullReferenceException(token + " 변수에 값이 저장이 안되어있잖아;;");
        }

        private static string ResolveOutput(string token)
        {
            if (IsStringLiteral(token))
                return token[1..^1];

            if (Utils.IsNumber(token))
                return token;

            if (token.Contains('.'))
                return ResolveArrayItemValue(token).ToString() ?? "여친";

            if (!Variables.storage.ContainsKey(token))
                return "여친";

            object? value = Variables.GetValue(token);
            return value?.ToString() ?? "여친";
        }

        private static int ResolveIntValue(string token)
        {
            if (Utils.IsNumber(token))
                return int.Parse(token);

            if (token.Contains('.'))
                return ResolveArrayItemValue(token);

            return Utils.GetIntValue(token);
        }

        private static int ResolveArrayItemValue(string token)
        {
            (string arrayName, int arrayIndex) = ParseArrayAccess(token);
            int[] array = Utils.GetIntArray(arrayName);

            if (arrayIndex < 0 || arrayIndex >= array.Length)
                throw new IndexOutOfRangeException("배열 범위를 벗어났잖아;;");

            return array[arrayIndex];
        }

        private static (string arrayName, int arrayIndex) ParseArrayAccess(string token)
        {
            string[] parts = token.Split('.', 2);
            if (parts.Length != 2 || !int.TryParse(parts[1], out int arrayIndex))
                throw new InvalidCastException("어이쿠?? 넌 이게 딕셔너리냐?? 숫자를 적어야지;;");

            return (parts[0], arrayIndex);
        }

        private static bool IsStringLiteral(string token)
        {
            return token.StartsWith('\"') && token.EndsWith('\"');
        }
    }
}
