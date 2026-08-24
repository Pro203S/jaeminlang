using static jaeminlang.Utils;

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
            Action<string>? importLibrary = null)
        {
            rawCmd = raw;
            args = GetArguments(raw);
            cmdName = args.Length == 0 ? "" : args[0];
            defaultMode = new DefaultMode(args, repeat, invokeFunction, functionExists, importLibrary);
            networkMode = new NetworkMode(args, repeat, invokeFunction, functionExists, importLibrary);
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
                case ExecutionMode.Default:
                    defaultMode.Execute(cmdName);
                    return;
                case ExecutionMode.Network:
                    networkMode.Execute(cmdName);
                    return;
            }
        }
    }
}
