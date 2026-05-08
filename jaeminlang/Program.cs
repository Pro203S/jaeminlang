using System.Text;

namespace jaeminlang
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("jaeminlang by Pro203S (https://github.com/Pro203S/jaeminlang)\n아래에 재민랭을 입력하세요.");
                for (; ; )
                {
                    Stream stderr = Console.OpenStandardError();
                    try
                    {
                        string? line = Console.ReadLine();
                        if (line == null || string.IsNullOrEmpty(line))
                        {
                            Console.WriteLine();
                            continue;
                        }

                        if (line.StartsWith("러스트"))
                        {
                            Console.WriteLine("여기서 러스트는 못쓰긴해");
                            continue;
                        }

                        JMLCommand cmd = new(line, null);

                        cmd.action();
                        Console.WriteLine();
                    }
                    catch (Exception e)
                    {
                        stderr.Write(Encoding.UTF8.GetBytes("아니;; 재민랭 똑바로 못써??\r\n"));
                        stderr.Write(Encoding.UTF8.GetBytes($"\r\n"));
                        stderr.Write(Encoding.UTF8.GetBytes(e.Message + "\r\n"));
                        stderr.Write(Encoding.UTF8.GetBytes(e.StackTrace + "\r\n"));
                    }
                }
            }

            string JmlFilePath = args[0];
            if (!File.Exists(JmlFilePath))
            {
                Stream stderr = Console.OpenStandardError();
                stderr.Write(Encoding.UTF8.GetBytes("아니;; 파일을 왜 안주냐고;;"));

                Environment.Exit(1);
            }

            JMLParser parse = new JMLParser(JmlFilePath);
            parse.Run();

            Environment.Exit(0);
        }
    }
}