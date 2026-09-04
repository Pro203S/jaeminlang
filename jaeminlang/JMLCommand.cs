using jaeminlang.Utils;

namespace jaeminlang.Mode
{
    public class JMLCommand
    {
        private readonly DefaultMode defaultMode;
        private readonly NetworkMode networkMode;

        public string rawCmd;
        public string cmdName;
        public string[] args;

        public JMLCommand(
            string raw,
            Action<int>? repeat,
            Func<string, string[], object?[]>? invokeFunction = null,
            Func<string, bool>? functionExists = null,
            Action<string>? importLibrary = null,
            Func<string, object?[], object?[]>? invokeFunctionValues = null)
        {
            rawCmd = raw;
            args = Parse.GetArguments(raw);
            cmdName = args.Length == 0 ? "" : args[0];
            defaultMode = new DefaultMode(args, repeat, invokeFunction, functionExists, importLibrary);
            networkMode = new NetworkMode(
                args,
                repeat,
                invokeFunction,
                functionExists,
                importLibrary,
                invokeFunctionValues);
        }

        public void Execute()
        {
            if (string.IsNullOrWhiteSpace(cmdName))
                return;

            switch (cmdName)
            {
                case "메가커피":
                    ModeManager.ExecuteSetMode(args);
                    return;
            }

            switch (ModeManager.Current)
            {
                case ExecutionMode.Network:
                    if (networkMode.Execute(cmdName)) return;
                    break;
                case ExecutionMode.FileIO:
                    if (networkMode.Execute(cmdName)) return;
                    break;
            }

            if (defaultMode.Execute(cmdName)) return;

            throw new ArgumentException("아니 " + cmdName + "은(는) 안산에도 없는 명령언데;;");
        }
    }
}
