namespace jaeminlang.Mode
{
    internal sealed class FileIOMode
    {
        private static readonly HttpClient Client = new();

        private readonly string[] args;
        private readonly Action<int>? repeat;
        private readonly Func<string, string[], object?[]>? invokeFunction;
        private readonly Func<string, bool>? functionExists;
        private readonly Action<string>? importLibrary;
        private readonly Func<string, object?[], object?[]>? invokeFunctionValues;

        public FileIOMode(
            string[] args,
            Action<int>? repeat,
            Func<string, string[], object?[]>? invokeFunction,
            Func<string, bool>? functionExists,
            Action<string>? importLibrary,
            Func<string, object?[], object?[]>? invokeFunctionValues)
        {
            this.args = args;
            this.repeat = repeat;
            this.invokeFunction = invokeFunction;
            this.functionExists = functionExists;
            this.importLibrary = importLibrary;
            this.invokeFunctionValues = invokeFunctionValues;
        }

        public bool Execute(string cmdName)
        {
            switch (cmdName)
            {
                
                default:
                    return false;
            }
        }

    }
}
