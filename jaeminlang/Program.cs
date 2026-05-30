using System.Text;

namespace jaeminlang
{
    class Program
    {
        static int Main(string[] args)
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
                        cmd.Execute();
                        Console.WriteLine();
                    }
                    catch (Exception e)
                    {
                        stderr.Write(Encoding.UTF8.GetBytes(e.Message + "\r\n"));
                        stderr.Write(Encoding.UTF8.GetBytes(e.StackTrace + "\r\n"));
                    }
                }
            }

            if (args[0] == "-h" || args[0] == "--help")
            {
                PrintHelp();
                return 0;
            }

            if (args[0].StartsWith('-'))
            {
                Stream stderr = Console.OpenStandardError();
                stderr.Write(Encoding.UTF8.GetBytes("아니;; 모르는 옵션이잖아;;\r\n"));
                return 1;
            }

            string jmlFilePath = args[0];
            if (!File.Exists(jmlFilePath))
            {
                Stream stderr = Console.OpenStandardError();
                stderr.Write(Encoding.UTF8.GetBytes("아니;; 파일을 왜 안주냐고;;"));

                return 1;
            }

            JMLParser parse = new JMLParser(jmlFilePath);
            parse.Run();

            return 0;
        }

        private static void PrintHelp()
        {
            Console.WriteLine("""
                jaeminlang

                실행:
                  jaeminlang <source.jml>

                도움말:
                  jaeminlang --help
                """);
        }
    }
}
