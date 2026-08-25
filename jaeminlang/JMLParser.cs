using jaeminlang.Mode;
using static jaeminlang.Utils;

namespace jaeminlang
{
    public class JMLParser
    {
        private static readonly HashSet<string> RegisteredFiles = new(
            OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        private readonly string _filepath;
        private string[] _fileContent = [];
        private readonly Dictionary<int, int> _functionBlocks = [];

        public JMLParser(string filepath)
        {
            _filepath = Path.GetFullPath(filepath);
            _fileContent = File.ReadAllLines(_filepath);
        }

        public void Run()
        {
            ModeManager.Reset();
            Variables.Reset();
            Functions.Reset();
            RegisteredFiles.Clear();
            RegisterFunctions();
            RunRange(0, _fileContent.Length, false);
        }

        public void RegisterFunctions()
        {
            if (!RegisteredFiles.Add(_filepath))
                return;

            _functionBlocks.Clear();
            ExecutionMode? declaredMode = ExecutionMode.Default;

            for (int i = 0; i < _fileContent.Length; i++)
            {
                string line = _fileContent[i];
                if (ShouldSkipLine(line))
                    continue;

                string[] args = GetArguments(line);
                if (args.Length == 0)
                    continue;

                if (args[0] == "메가커피")
                {
                    declaredMode = ModeManager.GetDeclaredMode(args);
                    continue;
                }

                if (args[0] == "팝콘")
                {
                    ImportLibrary(args.Length > 1 ? args[1] : throw new NullReferenceException("파일 경로는 있어야지;;"));
                    continue;
                }

                if (args[0] != "엘릭서" || declaredMode == ExecutionMode.Network)
                    continue;

                if (args.Length < 2)
                    throw new NullReferenceException("함수 이름은 있어야지;;");

                if (Functions.Contains(args[1]))
                    continue;

                int returnLine = FindFunctionReturnLine(_fileContent, i + 1);
                Functions.SetValue(args[1], new Function
                {
                    owner = this,
                    bodyStart = i + 1,
                    returnLine = returnLine,
                    parameters = args.Skip(2).ToArray(),
                    executionMode = declaredMode
                });
                _functionBlocks[i] = returnLine;
                i = returnLine;
            }
        }

        private void RunRange(int startIndex, int endIndex, bool allowReturn)
        {
            for (int i = startIndex; i < endIndex; i++)
            {
                try
                {
                    string line = _fileContent[i];
                    if (ShouldSkipLine(line))
                        continue;

                    if (_functionBlocks.TryGetValue(i, out int functionEnd))
                    {
                        i = functionEnd;
                        continue;
                    }

                    JMLCommand cmd = new(
                        line,
                        new Action<int>((goTo) =>
                        {
                            i = goTo - 2;
                        }),
                        InvokeFunction,
                        Functions.Contains,
                        ImportLibrary);
                    cmd.Execute();
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
                    HandleError(e, $"[{_filepath}] {i + 1}번째 줄");
                    Environment.Exit(1);
                }
            }
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

            object?[] resolvedArgs = rawArgs.Select(Utils.ResolveAssignableValue).ToArray();
            ExecutionMode functionMode = function.executionMode ?? ModeManager.Current;

            using (ModeManager.EnterMode(functionMode))
            {
                Variables.PushScope();
                try
                {
                    for (int i = 0; i < function.parameters.Length; i++)
                    {
                        Variables.SetLocalValue(function.parameters[i], resolvedArgs[i]);
                    }

                    RunRange(function.bodyStart, function.returnLine + 1, true);
                    return [];
                }
                catch (JMLReturnSignal signal)
                {
                    return signal.Values;
                }
                finally
                {
                    Variables.PopScope();
                }
            }
        }

        private void ImportLibrary(string rawPath)
        {
            string filePath = ResolveImportPath(rawPath);

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
