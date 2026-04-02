using System.Text;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Stream stderr = Console.OpenStandardError();
            stderr.Write(Encoding.UTF8.GetBytes("인수에 파일명을 줘야지;;"));

            Environment.Exit(1);
        }

        string JmlFilePath = args[0];
        if (!File.Exists(JmlFilePath))
        {
            Stream stderr = Console.OpenStandardError();
            stderr.Write(Encoding.UTF8.GetBytes("아니;; 파일을 왜 안주냐고;;"));

            Environment.Exit(1);
        }


    }
}