using System.Text;
using static jaeminlang.Utils;

namespace jaeminlang
{
    public class JMLCommand
    {
        private readonly Action<int>? repeat;
        private readonly Func<string, string[], object?[]>? invokeFunction;
        private readonly Func<string, bool>? functionExists;
        private readonly Action<string>? importLibrary;

        public string rawCmd;
        public string cmdName;
        public string[] args;

        public JMLCommand(
            string raw,
            Action<int>? repeat,
            Func<string, string[], object?[]>? invokeFunction = null,
            Func<string, bool>? functionExists = null,
            Action<string>? importLibrary = null)
        {
            rawCmd = raw;
            args = GetArguments(raw);
            cmdName = args.Length == 0 ? "" : args[0];
            this.repeat = repeat;
            this.invokeFunction = invokeFunction;
            this.functionExists = functionExists;
            this.importLibrary = importLibrary;
        }

        public void Execute()
        {
            if (string.IsNullOrWhiteSpace(cmdName))
                return;

            switch (cmdName)
            {
                case "안산":
                    ExecuteOutput();
                    return;
                case "재민":
                    ExecuteInput();
                    return;
                case "그램":
                    ExecuteVariable();
                    return;
                case "러스트":
                    ExecuteRepeat();
                    return;
                case "엘릭서":
                    ExecuteFunctionCall();
                    return;
                case "음...":
                    ExecuteReturn();
                    return;
                case "팝콘":
                    ExecuteImport();
                    return;
                case "콜라":
                    ExecuteLogicalOperate();
                    return;
                case "샤갈":
                    ExecuteNot();
                    return;
                case "해선":
                    ExecuteTerminate();
                    return;
                default:
                    throw new ArgumentException("아니 " + cmdName + "은(는) 안산에도 없는 명령언데;;");
            }
        }

        private void ExecuteOutput()
        {
            if (args.Length < 2)
                throw new NullReferenceException("안산에 인수가 없잖아;;");

            object?[] values = ResolveOutputValues(args[1..], functionExists, invokeFunction);
            string output = string.Concat(values.Select(FormatOutputValue));

            Stream stdout = Console.OpenStandardOutput();
            stdout.Write(Encoding.UTF8.GetBytes(output));
        }

        private void ExecuteInput()
        {
            if (args.Length < 2)
                throw new NullReferenceException("재민에 인수가 없잖아;;");

            string key = args[1];
            string? data = Console.ReadLine();

            Variables.SetValue(key, data ?? "");
        }

        private void ExecuteVariable()
        {
            if (args.Length < 3)
                throw new NullReferenceException("변수에 값은 줘야지;;");

            string key = args[1];
            string[] valueTokens = args[2..];

            if (key.StartsWith('[') && key.EndsWith(']'))
            {
                SetArrayValue(key[1..^1], valueTokens);
                return;
            }

            if (key.Contains('.'))
            {
                object? value = ResolveSingleValue(valueTokens, functionExists, invokeFunction);
                SetArrayItemValue(key, value);
                return;
            }

            if (LooksLikeFunctionCall(valueTokens, functionExists))
            {
                object?[] returned = InvokeFunctionTokens(valueTokens, invokeFunction);
                if (returned.Length != 1)
                    throw new ArgumentException("변수에는 값 하나만 넣어야지;;");

                Variables.SetValue(key, returned[0]);
                return;
            }

            if (valueTokens.Length != 1)
                throw new ArgumentException("변수에 값은 하나만 줘야지;;");

            string data = valueTokens[0];
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
                if (!Variables.ContainsKey(key))
                    return;

                Variables.SetValue(key, null);
                return;
            }

            Variables.SetValue(key, ResolveAssignableValue(data));
        }

        private void ExecuteRepeat()
        {
            if (repeat == null)
                return;

            if (args.Length < 4)
                throw new NullReferenceException("러스트 인수가 부족하잖아;;");

            string rawVal1 = args[1];
            string rawVal2 = args[2];
            string rawGoTo = args[3];

            double val1 = ResolveNumberValue(rawVal1);
            double val2 = ResolveNumberValue(rawVal2);

            if (!int.TryParse(rawGoTo, out int goTo))
                throw new ArgumentException(rawGoTo + "은(는) 숫자가 아니잖아;;");

            if (val1 != val2)
                repeat(goTo);
        }

        private void ExecuteFunctionCall()
        {
            if (args.Length < 2)
                throw new NullReferenceException("함수 이름은 있어야지;;");

            InvokeFunctionTokens(args[1..], invokeFunction);
        }

        private void ExecuteImport()
        {
            if (args.Length < 2)
                throw new NullReferenceException("파일 경로는 있어야지;;");

            if (importLibrary == null)
                throw new InvalidOperationException("여기서는 팝콘을 불러올 수가 없잖아;;");

            importLibrary(args[1]);
        }

        private void ExecuteReturn()
        {
            object?[] values = ResolveReturnValues(args.Length > 1 ? args[1..] : [], functionExists, invokeFunction);
            throw new JMLReturnSignal(values);
        }

        private void ExecuteLogicalOperate()
        {
            if (args.Length < 5)
                throw new NullReferenceException("콜라 인수가 부족하잖아;;");

            double val1 = ResolveNumberValue(args[1]);
            string op = args[2];
            double val2 = ResolveNumberValue(args[3]);
            string saveTo = args[4];
            string value;

            // `&`, `|`, `=`, `<`, `>`

            switch (op)
            {
                case "&":
                    value = (val1 == 1 && val2 == 1) ? "1" : "0";
                    break;
                case "|":
                    value = (val1 == 1 || val2 == 1) ? "1" : "0";
                    break;
                case "=":
                    value = val1 == val2 ? "1" : "0";
                    break;
                case "<":
                    value = (val1 < val2) ? "1" : "0";
                    break;
                case ">":
                    value = (val1 > val2) ? "1" : "0";
                    break;
                default:
                    throw new ArgumentException($"어이쿠 {op} 연산자가 뭐야");
            }

            Variables.SetValue(saveTo, value);
        }

        private void ExecuteNot()
        {
            if (args.Length < 3)
                throw new NullReferenceException("샤갈 인수가 부족하잖아;;");
            
            double val = ResolveNumberValue(args[1]);
            string toSave = args[2];

            Variables.SetValue(toSave, val == 1 ? 0 : 1);
        }

        private void ExecuteTerminate()
        {
            Environment.Exit(0);
        }

        private void ExecuteSetMode()
        {
            
        }
    }
}
