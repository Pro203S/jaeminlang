using static jaeminlang.Utils;

namespace jaeminlang
{
    public class JMLParser
    {
        private static readonly HashSet<string> RegisteredFiles = new(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        private readonly string _filepath;
        private readonly string[][] _commands;
        private readonly Dictionary<int, int> _functionBlocks = [];

        public JMLParser(string filepath)
        {
            _filepath = Path.GetFullPath(filepath);
            _commands = File.ReadLines(_filepath).Select(line => ShouldSkipLine(line) ? [] : GetArguments(line)).ToArray();
        }

        public void Run()
        {
            Variables.Reset();
            Functions.Reset();
            RegisteredFiles.Clear();
            try
            {
                RegisterFunctions();
                RunRange(0, _commands.Length, false);
            }
            finally
            {
                Variables.Reset();
                Functions.Reset();
                RegisteredFiles.Clear();
                RegisteredFiles.TrimExcess();
            }
        }

        public void RegisterFunctions()
        {
            if (!RegisteredFiles.Add(_filepath))
                return;

            _functionBlocks.Clear();

            for (int i = 0; i < _commands.Length; i++)
            {
                string[] args = _commands[i];
                if (args.Length == 0)
                    continue;

                if (args[0] == "팝콘")
                {
                    ImportLibrary(args.Length > 1 ? args[1] : throw new NullReferenceException("파일 경로는 있어야지;;"));
                    continue;
                }

                if (args[0] != "엘릭서")
                    continue;

                if (args.Length < 2)
                    throw new NullReferenceException("함수 이름은 있어야지;;");

                bool alreadyDefined = Functions.Contains(args[1]);
                int returnLine = i + 1;
                while (returnLine < _commands.Length && (_commands[returnLine].Length == 0 || _commands[returnLine][0] != "음..."))
                    returnLine++;
                if (returnLine == _commands.Length)
                {
                    if (alreadyDefined)
                        continue;
                    throw new ArgumentException("함수 끝에 음... 이 없잖아;;");
                }
                if (!alreadyDefined)
                {
                    Functions.SetValue(args[1], new Function
                    {
                        owner = this,
                        bodyStart = i + 1,
                        returnLine = returnLine,
                        parameters = args[2..]
                    });
                }
                _functionBlocks[i] = returnLine;
                i = returnLine;
            }
        }

        private object?[] RunRange(int startIndex, int endIndex, bool allowReturn)
        {
            int i = startIndex;
            JMLExecutor executor = new([], goTo => { if (goTo < 1 || goTo > _commands.Length) throw new ArgumentException("러스트 줄 번호가 파일 범위를 벗어났잖아;;"); i = goTo - 2; }, InvokeFunction, Functions.Contains, ImportLibrary);
            for (; i < endIndex; i++)
            {
                try
                {
                    string[] args = _commands[i];
                    if (args.Length == 0)
                        continue;

                    if (_functionBlocks.TryGetValue(i, out int functionEnd))
                    {
                        i = functionEnd;
                        continue;
                    }

                    if (allowReturn && args[0] == "음...")
                        return ResolveReturnValues(args.AsSpan(1), Functions.Contains, InvokeFunction);

                    if (!executor.Execute(args))
                        throw new ArgumentException("아니 " + args[0] + "은(는) 안산에도 없는 명령언데;;");
                }
                catch (JMLReturnSignal) when (allowReturn)
                {
                    throw;
                }
                catch (JMLReturnSignal)
                {
                    throw new InvalidOperationException("여기서 음... 쓰면 어떡해;;");
                }
                catch (Exception e)
                {
                    if (allowReturn)
                        throw;

                    HandleError(e);
                    Environment.Exit(1);
                }
            }

            return [];
        }

        private object?[] InvokeFunction(string name, string[] rawArgs)
        {
            Function function = Functions.GetRequired(name);
            return function.owner.InvokeRegisteredFunction(name, function, rawArgs);
        }

        private object?[] InvokeRegisteredFunction(string name, Function function, string[] rawArgs)
        {
            if (function.parameters.Length != rawArgs.Length)
                throw new ArgumentException(name + " 함수 인수 개수가 안맞잖아;;");

            object?[] resolvedArgs = new object?[rawArgs.Length];
            for (int i = 0; i < rawArgs.Length; i++)
                resolvedArgs[i] = ResolveAssignableValue(rawArgs[i]);
            Variables.PushScope();
            try
            {
                for (int i = 0; i < function.parameters.Length; i++)
                {
                    Variables.SetLocalValue(function.parameters[i], resolvedArgs[i]);
                }

                return RunRange(function.bodyStart, function.returnLine + 1, true);
            }
            finally
            {
                Variables.PopScope();
            }
        }

        private void ImportLibrary(string rawPath)
        {
            string filePath = ResolveImportPath(rawPath);

            if (RegisteredFiles.Contains(filePath))
                return;

            if (!File.Exists(filePath))
                throw new NullReferenceException("아니 없는 파일이잖아;;");

            JMLParser parser = new(filePath);
            parser.RegisterFunctions();
        }

        private string ResolveImportPath(string rawPath)
        {
            string path = IsStringLiteral(rawPath)
                ? rawPath[1..^1]
                : rawPath;

            if (Path.IsPathRooted(path))
                return Path.GetFullPath(path);

            string baseDirectory = Path.GetDirectoryName(_filepath) ?? Directory.GetCurrentDirectory();
            return Path.GetFullPath(Path.Combine(baseDirectory, path));
        }
    }
}
