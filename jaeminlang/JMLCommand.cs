using System.Text;

namespace jaeminlang
{
    public class JMLCommand
    {
        public Action action = new Action(() => { });
        public string rawCmd;
        public string cmdName;
        public string[] args;

        public JMLCommand(string raw, Action<int>? repeat)
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

                    if (content.StartsWith("\""))
                    {
                        if (!content.EndsWith("\""))
                            throw new ArgumentException("string이면 \"로 끝나야지;;");

                        string str = content.Substring(1, content.Length - 2);
                        stdout.Write(Encoding.UTF8.GetBytes(str));
                        return;
                    }

                    if (Utils.IsNumber(content))
                    {
                        stdout.Write(Encoding.UTF8.GetBytes(content));
                        return;
                    }

                    if (content == "여친")
                    {
                        stdout.Write(Encoding.UTF8.GetBytes("여친"));
                        return;
                    }

                    string value = Utils.GetStringValue(content);
                    stdout.Write(Encoding.UTF8.GetBytes(value));
                    stdout.Close();
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
                    string data = args[2] ?? throw new NullReferenceException("변수에 값은 줘야지;;");

                    // +a 꼴
                    if (Utils.IsExpression(data))
                    {
                        string exp = data.Substring(0, 1);
                        string contentKey = data.Substring(1);
                        object? raw = Variables.GetValue(contentKey) ?? contentKey;
                        int? valueToCalc = null;

                        if (Utils.IsNumber(contentKey))
                        {
                            valueToCalc = int.Parse(contentKey);
                        }
                        else
                        {
                            valueToCalc = raw switch
                            {
                                int i => i,
                                string s when int.TryParse(s, out var v) => v,
                                _ => throw new InvalidCastException("아니 숫자가 아니잖아;;")
                            };
                        }

                        int origin = Utils.GetIntValue(key);
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
                            default: throw new ArgumentException("이런 수식은 안산에도 없어;;");
                        }
                        return;
                    }

                    // 그냥 숫자
                    if (Utils.IsNumber(data))
                    {
                        Variables.SetValue(key, int.Parse(data));
                        return;
                    }

                    if (data.StartsWith('\"') && data.EndsWith('\"'))
                    {
                        string str = data.Substring(1, data.Length - 2);
                        Variables.SetValue(key, str);
                        return;
                    }

                    Variables.SetValue(key, data);
                });
                return;
            }

            if (cmdName == "러스트" /* 반복 */)
            {
                action = new Action(() =>
                {
                    string rawVal1 = args[1] ?? throw new NullReferenceException("러스트에 인수가 없잖아;;");
                    string rawVal2 = args[2] ?? throw new NullReferenceException("러스트에 인수가 없잖아;;");
                    string rawGoTo = args[3] ?? throw new NullReferenceException("러스트에 인수가 없잖아;;");

                    int val1 = Utils.IsNumber(rawVal1) ? int.Parse(rawVal1) : Utils.GetIntValue(rawVal1);
                    int val2 = Utils.IsNumber(rawVal2) ? int.Parse(rawVal2) : Utils.GetIntValue(rawVal2);

                    if (!int.TryParse(rawGoTo, out int goTo))
                        throw new ArgumentException(rawGoTo + "은(는) 숫자가 아니잖아;;");

                    if (repeat == null)
                        throw new Exception("이건 내실수인데");

                    repeat(goTo);
                });
                return;
            }

            if (string.IsNullOrEmpty(cmdName)) return;

            throw new ArgumentException("아니 " + cmdName + "은(는) 안산에도 없는 명령언데;;");
        }
    }
}