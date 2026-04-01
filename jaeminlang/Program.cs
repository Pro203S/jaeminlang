using System.Text;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Stream stderr = Console.OpenStandardError();
            stderr.Write(Encoding.UTF8.GetBytes("파일명을 제공해주세요!"));

            Environment.Exit(1);
        }

        string JmlFilePath = args[0];
        if (!File.Exists(JmlFilePath))
        {
            Stream stderr = Console.OpenStandardError();
            stderr.Write(Encoding.UTF8.GetBytes("파일이 존재하지 않습니다!"));

            Environment.Exit(1);
        }
    }
}