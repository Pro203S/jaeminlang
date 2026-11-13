namespace jaeminlang
{
    internal sealed class JMLExecutor
    {
        private string[] args;
        private readonly Action<int>? repeat;
        private readonly Func<string, string[], object?[]>? invokeFunction;
        private readonly Func<string, bool>? functionExists;
        private readonly Action<string>? importLibrary;

        public JMLExecutor(string[] args, Action<int>? repeat, Func<string, string[], object?[]>? invokeFunction, Func<string, bool>? functionExists, Action<string>? importLibrary)
        {
            this.args = args;
            this.repeat = repeat;
            this.invokeFunction = invokeFunction;
            this.functionExists = functionExists;
            this.importLibrary = importLibrary;
        }

        public bool Execute(string cmdName)
        {
            switch (cmdName)
            {
                case "안산":
                    ExecuteOutput();
                    return true;
                case "재민":
                    ExecuteInput();
                    return true;
                case "그램":
                    ExecuteVariable();
                    return true;
                case "러스트":
                    ExecuteRepeat();
                    return true;
                case "엘릭서":
                    ExecuteFunctionCall();
                    return true;
                case "음...":
                    ExecuteReturn();
                    return true;
                case "팝콘":
                    ExecuteImport();
                    return true;
                case "콜라":
                    ExecuteLogicalOperate();
                    return true;
                case "샤갈":
                    ExecuteNot();
                    return true;
                case "해선":
                    ExecuteTerminate();
                    return true;
                default:
                    return false;
            }
        }

        internal bool Execute(string[] commandArgs)
        {
            args = commandArgs;
            return string.IsNullOrWhiteSpace(args[0]) || Execute(args[0]);
        }

        public void ExecuteOutput()
        {
            if (args.Length < 2)
                throw new NullReferenceException("안산에 인수가 없잖아;;");

            object?[] values = Utils.ResolveOutputValues(args.AsSpan(1), functionExists, invokeFunction);
            string output = string.Concat(values.Select(Utils.FormatOutputValue));
            Utils.WriteOutput(output);
        }

        public void ExecuteInput()
        {
            if (args.Length < 2)
                throw new NullReferenceException("재민에 인수가 없잖아;;");

            string key = args[1];
            string? data = Console.ReadLine();

            Variables.SetValue(key, data ?? "");
        }

        public void ExecuteVariable()
        {
            if (args.Length < 2)
                throw new NullReferenceException("변수에 값은 줘야지;;");

            string key = args[1];
            ReadOnlySpan<string> valueTokens = args.AsSpan(2);

            if (key.StartsWith('{') && key.EndsWith('}'))
            {
                Utils.SetDictionaryValue(key[1..^1], valueTokens);
                return;
            }

            if (valueTokens.Length == 0)
                throw new NullReferenceException("변수에 값은 줘야지;;");

            if (key.StartsWith('[') && key.EndsWith(']'))
            {
                Utils.SetArrayValue(key[1..^1], valueTokens);
                return;
            }

            if (Utils.LooksLikeFunctionCall(valueTokens, functionExists))
            {
                object?[] returned = Utils.InvokeFunctionTokens(valueTokens, invokeFunction);
                if (returned.Length != 1)
                    throw new ArgumentException("변수에는 값 하나만 넣어야지;;");

                SetTargetValue(key, returned[0]);
                return;
            }

            if (valueTokens.Length != 1)
                throw new ArgumentException("변수에 값은 하나만 줘야지;;");

            string data = valueTokens[0];
            if (Utils.IsExpression(data))
            {
                char exp = data[0];
                string operand = data[1..];

                bool collectionTarget = key.Contains('.');
                if (!collectionTarget && !Variables.ContainsKey(key))
                {
                    if (exp is not '+' and not '-')
                        throw new ArgumentException("새 변수는 +나 -로만 초기화할 수 있잖아;;");

                    double initialValue = Utils.ResolveNumberValue(operand);
                    Variables.SetValue(key, exp == '-' ? -initialValue : initialValue);
                    return;
                }

                object? current = collectionTarget ? Utils.ResolveCollectionItemValue(key) : Variables.GetValue(key);
                if (exp == '+' && current is string originString)
                {
                    object? valueToAppend = Utils.ResolveAssignableValue(operand);
                    SetTargetValue(key, originString + Utils.FormatOutputValue(valueToAppend));
                    return;
                }

                double origin = collectionTarget ? Utils.ConvertToNumber(current, key + "은(는) 숫자가 아니잖아;;") : Utils.GetNumberValue(key);
                double valueToCalc = Utils.ResolveNumberValue(operand);

                double result = exp switch
                {
                    '+' => origin + valueToCalc,
                    '-' => origin - valueToCalc,
                    '*' => origin * valueToCalc,
                    '/' => origin / valueToCalc,
                    '^' => Math.Pow(origin, valueToCalc),
                    _ => throw new ArgumentException("이런 수식은 안산에도 없어;;")
                };
                SetTargetValue(key, result);
                return;
            }

            if (key.Contains('.'))
            {
                object? value = Utils.ResolveSingleValue(valueTokens, functionExists, invokeFunction);
                Utils.SetCollectionItemValue(key, value);
                return;
            }

            if (data == "여친")
            {
                if (!Variables.ContainsKey(key))
                    return;

                Variables.SetValue(key, null);
                return;
            }

            Variables.SetValue(key, Utils.ResolveAssignableValue(data));
        }

        private static void SetTargetValue(string key, object? value)
        {
            if (key.Contains('.'))
                Utils.SetCollectionItemValue(key, value);
            else
                Variables.SetValue(key, value);
        }

        public void ExecuteRepeat()
        {
            if (repeat == null)
                return;

            if (args.Length < 4)
                throw new NullReferenceException("러스트 인수가 부족하잖아;;");

            string rawVal1 = args[1];
            string rawVal2 = args[2];
            string rawGoTo = args[3];

            double val1 = Utils.ResolveNumberValue(rawVal1);
            double val2 = Utils.ResolveNumberValue(rawVal2);

            if (!int.TryParse(rawGoTo, out int goTo))
                throw new ArgumentException(rawGoTo + "은(는) 숫자가 아니잖아;;");

            if (Math.Abs(val1 - val2) > Utils.NumericEpsilon)
                repeat(goTo);
        }

        public void ExecuteFunctionCall()
        {
            if (args.Length < 2)
                throw new NullReferenceException("함수 이름은 있어야지;;");

            Utils.InvokeFunctionTokens(args.AsSpan(1), invokeFunction);
        }

        public void ExecuteImport()
        {
            if (args.Length < 2)
                throw new NullReferenceException("파일 경로는 있어야지;;");

            if (importLibrary == null)
                throw new InvalidOperationException("여기서는 팝콘을 불러올 수가 없잖아;;");

            importLibrary(args[1]);
        }

        public void ExecuteReturn()
        {
            object?[] values = Utils.ResolveReturnValues(args.AsSpan(1), functionExists, invokeFunction);
            throw new JMLReturnSignal(values);
        }

        public void ExecuteLogicalOperate()
        {
            if (args.Length < 5)
                throw new NullReferenceException("콜라 인수가 부족하잖아;;");

            double val1 = Utils.ResolveNumberValue(args[1]);
            string op = args[2];
            double val2 = Utils.ResolveNumberValue(args[3]);
            string saveTo = args[4];
            double value;

            switch (op)
            {
                case "&":
                    value = (val1 == 1 && val2 == 1) ? 1d : 0d;
                    break;
                case "|":
                    value = (val1 == 1 || val2 == 1) ? 1d : 0d;
                    break;
                case "=":
                    value = Utils.NumbersEqual(val1, val2) ? 1d : 0d;
                    break;
                case "<":
                    value = (val1 < val2) ? 1d : 0d;
                    break;
                case ">":
                    value = (val1 > val2) ? 1d : 0d;
                    break;
                default:
                    throw new ArgumentException($"어이쿠 {op} 연산자가 뭐야");
            }

            Variables.SetValue(saveTo, value);
        }

        public void ExecuteNot()
        {
            if (args.Length < 3)
                throw new NullReferenceException("샤갈 인수가 부족하잖아;;");

            double val = Utils.ResolveNumberValue(args[1]);
            string toSave = args[2];

            Variables.SetValue(toSave, val == 1 ? 0d : 1d);
        }

        public void ExecuteTerminate()
        {
            Environment.Exit(0);
        }
    }
}
