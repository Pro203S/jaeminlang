using System.Text;

namespace jaeminlang
{
    public class JMLCommand
    {
        public Action action = new Action(() => { });
        public string rawCmd;
        public string cmdName;
        public string[] args;

        public JMLCommand(string raw)
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
                    
                    if (content.StartsWith("\"") /* string 형식 */) {
                        if (!content.EndsWith("\""))
                            throw new ArgumentException("string이면 \"로 끝나야지;;");
                        stdout.Write(Encoding.UTF8.GetBytes(content[1..].Substring(0, content.Length - 2)));
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

                    string value = Variables.GetValue(content) as string ?? throw new NullReferenceException(content + " 변수에 값이 저장이 안되어있잖아;;");
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

                    Variables.SetValue(key, data);
                });
                return;
            }

            throw new ArgumentException("아니 " + cmdName + "은(는) 안산에도 없는 명령언데;;");
        }
    }
}
