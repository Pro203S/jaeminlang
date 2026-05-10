using System.Text;
using static jaeminlang.Utils;

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
            args = GetArguments(raw);
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

                    if (IsExpression(data))
                    {
                        string exp = data[..1];
                        double origin = GetNumberValue(key);
                        double valueToCalc = ResolveNumberValue(data[1..]);

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
                                Variables.SetValue(key, Math.Pow(origin, valueToCalc));
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

                    int val1 = (int)ResolveNumberValue(rawVal1);
                    int val2 = (int)ResolveNumberValue(rawVal2);

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
    }
}
