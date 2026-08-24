using jaeminlang;
using static jaeminlang.Utils;

namespace jaeminlang.Mode
{
    public class JMLCommand
    {
        private readonly DefaultMode defaultMode;

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
        }

        public void Execute()
        {
            if (string.IsNullOrWhiteSpace(cmdName))
                return;

            switch (cmdName)
            {
                case "안산":
                    defaultMode.ExecuteOutput();
                    return;
                case "재민":
                    defaultMode.ExecuteInput();
                    return;
                case "그램":
                    defaultMode.ExecuteVariable();
                    return;
                case "러스트":
                    defaultMode.ExecuteRepeat();
                    return;
                case "엘릭서":
                    defaultMode.ExecuteFunctionCall();
                    return;
                case "음...":
                    defaultMode.ExecuteReturn();
                    return;
                case "팝콘":
                    defaultMode.ExecuteImport();
                    return;
                case "콜라":
                    defaultMode.ExecuteLogicalOperate();
                    return;
                case "샤갈":
                    defaultMode.ExecuteNot();
                    return;
                case "해선":
                    defaultMode.ExecuteTerminate();
                    return;
                case "메가커피":
                    ModeManager.ExecuteSetMode(args);
                    return;
                default:
                    throw new ArgumentException("아니 " + cmdName + "은(는) 안산에도 없는 명령언데;;");
            }
        }
    }
}
