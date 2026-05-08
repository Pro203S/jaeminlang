using System.Text;

namespace jaeminlang
{
    public class JMLParser
    {
        string _filepath;
        string[] _fileContent = [];

        public JMLParser(string filepath)
        {
            _filepath = filepath;
        }

        public void Run()
        {
            _fileContent = File.ReadAllLines(_filepath);
            RunRange(0, _fileContent.Length);
        }

        private void RunRange(int startIndex, int endIndex)
        {
            for (int i = startIndex; i < endIndex; i++)
            {
                try
                {
                    string line = _fileContent[i];
                    if (line.StartsWith("어이쿠")) continue;

                    JMLCommand cmd = new(
                        line,
                        new Action<int>((goTo) =>
                        {
                            i = goTo - 1;
                        }),
                        new Action<string, Function>((name, func) =>
                        {
                            Functions.SetValue(name, func);
                        }),
                        new Action<string>((name) =>
                        {
                            Function func = (Function?)Functions.GetValue(name)
                                ?? throw new ArgumentNullException(name + " 함수가 정의가 안됐잖아;;");

                            RunRange(func.start - 1, func.end);
                        }));
                    cmd.action();
                }
                catch (Exception e)
                {
                    Stream stderr = Console.OpenStandardError();
                    stderr.Write(Encoding.UTF8.GetBytes("아니;; 재민랭 똑바로 못써??\r\n"));
                    stderr.Write(Encoding.UTF8.GetBytes($"{i + 1}번째 줄에 오류났잖아;;\r\n"));
                    stderr.Write(Encoding.UTF8.GetBytes($"\r\n"));
                    stderr.Write(Encoding.UTF8.GetBytes(e.Message + "\r\n"));
                    stderr.Write(Encoding.UTF8.GetBytes(e.StackTrace + "\r\n"));

                    Environment.Exit(1);
                }
            }
        }
    }
}
