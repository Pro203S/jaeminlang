using System.Text;

namespace jaeminlang
{
    public class JMLParser
    {
        string _filepath;

        public JMLParser(string filepath)
        {
            _filepath = filepath;
        }

        public void Run()
        {
            string[] fileContent = File.ReadAllLines(_filepath);

            for (int i = 0; i < fileContent.Length; i++)
            {
                try
                {
                    string line = fileContent[i];
                    JMLCommand cmd = new(line, line.StartsWith("러스트") ? new Action<int>((goTo) =>
                    {
                        i = goTo - 1;
                    }) : null);
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
