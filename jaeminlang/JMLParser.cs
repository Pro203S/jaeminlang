using System.Text;
using static jaeminlang.Utils;

namespace jaeminlang
{
    public class JMLParser
    {   
        private readonly string _filepath;
        private string[] _fileContent = [];
        private readonly Dictionary<int, int> _functionBlocks = [];

        public JMLParser(string filepath)
        {
            _filepath = filepath;
        }

        public void Run()
        {
            Variables.Reset();
            Functions.Reset();
            _fileContent = File.ReadAllLines(_filepath);
            RegisterFunctions();
            RunRange(0, _fileContent.Length, false);
        }

        private void RegisterFunctions()
        {
            _functionBlocks.Clear();

            for (int i = 0; i < _fileContent.Length; i++)
            {
                string line = _fileContent[i];
                if (ShouldSkipLine(line))
                    continue;

                string[] args = Utils.GetArguments(line);
                if (args.Length == 0 || args[0] != "엘릭서")
                    continue;

                if (args.Length < 2)
                    throw new NullReferenceException("함수 이름은 있어야지;;");

                if (Functions.Contains(args[1]))
                    continue;

                int returnLine = FindFunctionReturnLine(_fileContent, i + 1);
                Functions.SetValue(args[1], new Function
                {
                    bodyStart = i + 1,
                    returnLine = returnLine,
                    parameters = args.Skip(2).ToArray()
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
                        Functions.Contains);
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
                    Stream stderr = Console.OpenStandardError();
                    stderr.Write(Encoding.UTF8.GetBytes($"{i + 1}번째 줄: {e.Message}\r\n"));
                    stderr.Write(Encoding.UTF8.GetBytes(e.StackTrace + "\r\n"));

                    Environment.Exit(1);
                }
            }
        }

        private object?[] InvokeFunction(string name, string[] rawArgs)
        {
            Function function = Functions.GetRequired(name);
            if (function.parameters.Length != rawArgs.Length)
                throw new ArgumentException(name + " 함수 인수 개수가 안맞잖아;;");

            object?[] resolvedArgs = rawArgs.Select(Utils.ResolveAssignableValue).ToArray();

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
}
