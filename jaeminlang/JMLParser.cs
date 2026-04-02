using System.Text;

namespace jaeminlang
{
    public class JMLParser
    {
        string _filepath;
        public bool isReady = false;
        public List<JMLCommand> commands = [];

        public JMLParser(string filepath)
        {
            _filepath = filepath;
        }

        public void Ready()
        {
            string[] fileContent = File.ReadAllLines(_filepath);

            for (int i = 0; i < fileContent.Length; i++)
            {
                try
                {
                    string line = fileContent[i];
                    JMLCommand cmd = new(line);
                    commands.Add(cmd);
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

            isReady = true;
        }

        public void Run()
        {
            if (!isReady) Environment.Exit(1);

            for (int i = 0; i < commands.Count; i++)
            {
                try
                {
                    JMLCommand cmd = commands[i];
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
