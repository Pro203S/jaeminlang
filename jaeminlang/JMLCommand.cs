using static jaeminlang.Utils;

namespace jaeminlang
{
    public class JMLCommand
    {
        private readonly JMLExecutor executor;

        public string rawCmd;
        public string cmdName;
        public string[] args;

        public JMLCommand(string raw, Action<int>? repeat, Func<string, string[], object?[]>? invokeFunction = null, Func<string, bool>? functionExists = null, Action<string>? importLibrary = null)
        {
            rawCmd = raw;
            args = GetArguments(raw);
            cmdName = args.Length == 0 ? "" : args[0];
            executor = new JMLExecutor(args, repeat, invokeFunction, functionExists, importLibrary);
        }

        public void Execute()
        {
            if (string.IsNullOrWhiteSpace(cmdName))
                return;

            if (executor.Execute(cmdName)) return;

            throw new ArgumentException("아니 " + cmdName + "은(는) 안산에도 없는 명령언데;;");
        }
    }
}
