namespace jaeminlang.Mode
{
    internal sealed class NetworkMode
    {
        private readonly string[] args;
        private readonly Action<int>? repeat;
        private readonly Func<string, string[], object?[]>? invokeFunction;
        private readonly Func<string, bool>? functionExists;
        private readonly Action<string>? importLibrary;

        public NetworkMode(
            string[] args,
            Action<int>? repeat,
            Func<string, string[], object?[]>? invokeFunction,
            Func<string, bool>? functionExists,
            Action<string>? importLibrary)
        {
            this.args = args;
            this.repeat = repeat;
            this.invokeFunction = invokeFunction;
            this.functionExists = functionExists;
            this.importLibrary = importLibrary;
        }

        public void Execute(string cmdName)
        {
            switch (cmdName)
            {
                
                default:
                    throw new ArgumentException("아니 " + cmdName + "은(는) 안산에도 없는 명령언데;;");
            }
        }

    }
}
