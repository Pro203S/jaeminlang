using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;

namespace jaeminlang.Utils
{
    public static class Global
    {
        public static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

        public static void HandleError(string message)
        {
            Stream stderr = Console.OpenStandardError();
            stderr.Write(Encoding.UTF8.GetBytes(message + Environment.NewLine));
        }

        public static void HandleError(Exception exception, string? context = null)
        {
            string message = string.IsNullOrWhiteSpace(context)
                ? exception.Message
                : $"{context}: {exception.Message}";

            HandleError(message);

            #if DEBUG
            Console.WriteLine(exception.StackTrace);
            #endif
        }
    }
}
