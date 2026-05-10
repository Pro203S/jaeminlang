using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace jaeminlang
{
    public class JMLCompiler
    {
        private static readonly string[] SupportedRidList =
        [
            "linux-x64",
            "linux-arm64",
            "osx-x64",
            "osx-arm64",
            "win-x64",
            "win-arm64"
        ];

        private static readonly HashSet<string> SupportedRids = new(SupportedRidList, StringComparer.OrdinalIgnoreCase);

        public static int Compile(JMLCompileOptions options)
        {
            if (!File.Exists(options.SourcePath))
                throw new FileNotFoundException("컴파일할 재민랭 파일이 없잖아;;", options.SourcePath);

            NativeTarget target = ResolveTarget(options.Rid);
            string sourcePath = Path.GetFullPath(options.SourcePath);
            string outputPath = ResolveOutputPath(options, sourcePath, target);
            string assemblyPath = ResolveAssemblyPath(options, sourcePath, outputPath);
            string objectPath = ResolveObjectPath(options, sourcePath, outputPath);

            if (!options.EmitAssemblyOnly && PathEquals(sourcePath, outputPath))
                throw new ArgumentException("출력 파일이 소스 파일과 같잖아;;");

            if (options.EmitAssemblyOnly && options.EmitObjectOnly)
                throw new ArgumentException("-S랑 -c는 같이 쓸 수 없잖아;;");

            string[] sourceLines = File.ReadAllLines(sourcePath);
            JmlNativeProgram program = JMLNativeParser.Parse(sourceLines);
            INativeAssemblyGenerator generator = target.IsArm64
                ? new Arm64AssemblyGenerator(target, program)
                : new NativeAssemblyGenerator(target, program);
            string assembly = generator.Generate();

            string? assemblyDirectory = Path.GetDirectoryName(assemblyPath);
            if (!string.IsNullOrEmpty(assemblyDirectory))
                Directory.CreateDirectory(assemblyDirectory);

            File.WriteAllText(assemblyPath, assembly, new UTF8Encoding(false));

            if (options.EmitAssemblyOnly)
            {
                Console.WriteLine($"어셈블리 생성 완료: {assemblyPath}");
                return 0;
            }

            RunNativeAssembler(target, assemblyPath, objectPath);

            if (!options.KeepAssembly && File.Exists(assemblyPath))
                File.Delete(assemblyPath);

            if (options.EmitObjectOnly)
            {
                Console.WriteLine($"오브젝트 생성 완료: {objectPath}");
                return 0;
            }

            RunNativeLinker(target, [objectPath], outputPath);

            if (!target.IsWindows)
                MakeExecutable(outputPath);

            Console.WriteLine($"오브젝트 생성 완료: {objectPath}");
            Console.WriteLine($"컴파일 완료: {outputPath}");
            return 0;
        }

        public static int Link(JMLLinkOptions options)
        {
            if (options.ObjectPaths.Count == 0)
                throw new ArgumentException("링크할 오브젝트 파일을 줘야지;;");

            NativeTarget target = ResolveTarget(options.Rid);
            string[] objectPaths = options.ObjectPaths.Select(Path.GetFullPath).ToArray();

            foreach (string objectPath in objectPaths)
            {
                if (!File.Exists(objectPath))
                    throw new FileNotFoundException("링크할 오브젝트 파일이 없잖아;;", objectPath);
            }

            string outputPath = ResolveLinkOutputPath(options, objectPaths[0], target);

            if (objectPaths.Any(objectPath => PathEquals(objectPath, outputPath)))
                throw new ArgumentException("출력 파일이 오브젝트 파일과 같잖아;;");

            RunNativeLinker(target, objectPaths, outputPath);

            if (!target.IsWindows)
                MakeExecutable(outputPath);

            Console.WriteLine($"링크 완료: {outputPath}");
            return 0;
        }

        public static string GetDefaultRid()
        {
            if (OperatingSystem.IsWindows())
                return "win-" + GetDefaultArchitecture();

            if (OperatingSystem.IsMacOS())
                return "osx-" + GetDefaultArchitecture();

            return "linux-" + GetDefaultArchitecture();
        }

        public static string NormalizeRid(string rid)
        {
            string normalized = rid.Trim().ToLowerInvariant();
            string arch = GetDefaultArchitecture();

            return normalized switch
            {
                "linux" => "linux-" + arch,
                "mac" => "osx-" + arch,
                "macos" => "osx-" + arch,
                "darwin" => "osx-" + arch,
                "osx" => "osx-" + arch,
                "windows" => "win-" + arch,
                "win" => "win-" + arch,
                _ => normalized
            };
        }

        public static string GetSupportedRidList()
        {
            return string.Join(Environment.NewLine, SupportedRidList.Select(rid => "  " + rid));
        }

        private static NativeTarget ResolveTarget(string rawRid)
        {
            string rid = NormalizeRid(rawRid);
            if (!SupportedRids.Contains(rid))
                throw new ArgumentException($"어이쿠?? 넌 이게 지원 하는걸로 보이냐?");

            return NativeTarget.FromRid(rid);
        }

        private static string ResolveOutputPath(JMLCompileOptions options, string sourcePath, NativeTarget target)
        {
            string sourceDirectory = Path.GetDirectoryName(sourcePath) ?? Directory.GetCurrentDirectory();
            string fileName = Path.GetFileNameWithoutExtension(sourcePath) + (target.IsWindows ? ".exe" : "");

            if (string.IsNullOrWhiteSpace(options.OutputPath))
            {
                if (options.EmitAssemblyOnly)
                    return Path.Combine(sourceDirectory, Path.GetFileNameWithoutExtension(sourcePath) + ".s");

                if (options.EmitObjectOnly)
                    return Path.Combine(sourceDirectory, Path.GetFileNameWithoutExtension(sourcePath) + ".o");

                string defaultOutputPath = Path.Combine(sourceDirectory, fileName);
                return PathEquals(sourcePath, defaultOutputPath)
                    ? defaultOutputPath + ".out"
                    : defaultOutputPath;
            }

            string outputPath = options.OutputPath;
            bool outputLooksLikeDirectory =
                outputPath.EndsWith(Path.DirectorySeparatorChar) ||
                outputPath.EndsWith(Path.AltDirectorySeparatorChar) ||
                Directory.Exists(outputPath);

            if (outputLooksLikeDirectory)
            {
                string emittedFileName =
                    options.EmitAssemblyOnly ? Path.GetFileNameWithoutExtension(sourcePath) + ".s" :
                    options.EmitObjectOnly ? Path.GetFileNameWithoutExtension(sourcePath) + ".o" :
                    fileName;
                outputPath = Path.Combine(outputPath, emittedFileName);
            }

            return Path.GetFullPath(outputPath);
        }

        private static string ResolveAssemblyPath(JMLCompileOptions options, string sourcePath, string outputPath)
        {
            if (options.EmitAssemblyOnly)
                return outputPath;

            if (options.KeepAssembly)
                return outputPath + ".s";

            return Path.Combine(Path.GetTempPath(), "jaeminlang-" + Path.GetFileNameWithoutExtension(sourcePath) + "-" + Guid.NewGuid().ToString("N") + ".s");
        }

        private static string ResolveObjectPath(JMLCompileOptions options, string sourcePath, string outputPath)
        {
            if (options.EmitAssemblyOnly)
                return "";

            if (options.EmitObjectOnly)
                return outputPath;

            string? outputDirectory = Path.GetDirectoryName(outputPath);
            string outputFileName = Path.GetFileNameWithoutExtension(outputPath);

            if (string.IsNullOrEmpty(outputFileName))
                outputFileName = Path.GetFileNameWithoutExtension(sourcePath);

            return Path.Combine(
                string.IsNullOrEmpty(outputDirectory) ? Directory.GetCurrentDirectory() : outputDirectory,
                outputFileName + ".o");
        }

        private static string ResolveLinkOutputPath(JMLLinkOptions options, string firstObjectPath, NativeTarget target)
        {
            string objectDirectory = Path.GetDirectoryName(firstObjectPath) ?? Directory.GetCurrentDirectory();
            string fileName = Path.GetFileNameWithoutExtension(firstObjectPath) + (target.IsWindows ? ".exe" : "");

            if (string.IsNullOrWhiteSpace(options.OutputPath))
                return Path.Combine(objectDirectory, fileName);

            string outputPath = options.OutputPath;
            bool outputLooksLikeDirectory =
                outputPath.EndsWith(Path.DirectorySeparatorChar) ||
                outputPath.EndsWith(Path.AltDirectorySeparatorChar) ||
                Directory.Exists(outputPath);

            if (outputLooksLikeDirectory)
                outputPath = Path.Combine(outputPath, fileName);

            return Path.GetFullPath(outputPath);
        }

        private static void RunNativeAssembler(NativeTarget target, string assemblyPath, string objectPath)
        {
            string assembler = FindExecutable(GetToolCandidates(target))
                ?? throw new InvalidOperationException("아니;;; 오브젝트를 만들려면 네이티브 툴체인이 필요한데 clang/gcc/cc도 안줘?");

            string? objectDirectory = Path.GetDirectoryName(objectPath);
            if (!string.IsNullOrEmpty(objectDirectory))
                Directory.CreateDirectory(objectDirectory);

            ProcessStartInfo startInfo = CreateNativeToolStartInfo(assembler, target);
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add(assemblyPath);
            startInfo.ArgumentList.Add("-o");
            startInfo.ArgumentList.Add(objectPath);

            RunNativeTool(startInfo, "어이쿠? 네이티브 어셈블이 실패했네?");
        }

        private static void RunNativeLinker(NativeTarget target, IReadOnlyList<string> objectPaths, string outputPath)
        {
            string linker = FindExecutable(GetToolCandidates(target))
                ?? throw new InvalidOperationException("아니;;; 오브젝트를 만들려면 네이티브 툴체인이 필요한데 clang/gcc/cc도 안줘? 어셈블리만 볼거면 --emit-asm을 쓰던가 해야지;;");

            string? outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            ProcessStartInfo startInfo = CreateNativeToolStartInfo(linker, target);
            foreach (string objectPath in objectPaths)
                startInfo.ArgumentList.Add(objectPath);

            startInfo.ArgumentList.Add("-o");
            startInfo.ArgumentList.Add(outputPath);

            RunNativeTool(startInfo, "어이쿠? 네이티브 링크가 실패했네?");
        }

        private static string[] GetToolCandidates(NativeTarget target)
        {
            return target.IsWindows && !target.IsArm64
                ? ["gcc", "cc", "clang"]
                : ["clang", "gcc", "cc"];
        }

        private static ProcessStartInfo CreateNativeToolStartInfo(string toolPath, NativeTarget target)
        {
            ProcessStartInfo startInfo = new()
            {
                FileName = toolPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            if (Path.GetFileNameWithoutExtension(toolPath).Contains("clang", StringComparison.OrdinalIgnoreCase) &&
                !CurrentMachineMatchesTarget(target))
            {
                startInfo.ArgumentList.Add("--target=" + target.ClangTriple);
            }

            return startInfo;
        }

        private static void RunNativeTool(ProcessStartInfo startInfo, string errorMessage)
        {
            using Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("어이쿠? 네이티브 툴 실행이 안됐네?");

            Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
            Task<string> stderrTask = process.StandardError.ReadToEndAsync();

            process.WaitForExit();
            string stdout = stdoutTask.GetAwaiter().GetResult();
            string stderr = stderrTask.GetAwaiter().GetResult();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    errorMessage +
                    Environment.NewLine +
                    stdout +
                    Environment.NewLine +
                    stderr);
            }
        }

        private static string? FindExecutable(string[] names)
        {
            string? path = Environment.GetEnvironmentVariable("PATH");
            if (path == null)
                return null;

            string[] extensions = OperatingSystem.IsWindows()
                ? [".exe", ".cmd", ".bat", ""]
                : [""];

            foreach (string directory in path.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(directory))
                    continue;

                foreach (string name in names)
                {
                    foreach (string extension in extensions)
                    {
                        string candidate = Path.Combine(directory, name + extension);
                        if (File.Exists(candidate))
                            return candidate;
                    }
                }
            }

            return null;
        }

        private static bool CurrentMachineMatchesTarget(NativeTarget target)
        {
            bool osMatches =
                target.IsWindows ? OperatingSystem.IsWindows() :
                target.IsMacOS ? OperatingSystem.IsMacOS() :
                OperatingSystem.IsLinux();

            bool archMatches = target.IsArm64
                ? System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.Arm64
                : System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.X64;

            return osMatches && archMatches;
        }

        private static string GetDefaultArchitecture()
        {
            return System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture switch
            {
                System.Runtime.InteropServices.Architecture.Arm64 => "arm64",
                _ => "x64"
            };
        }

        private static void MakeExecutable(string outputPath)
        {
            try
            {
                ProcessStartInfo chmodStartInfo = new()
                {
                    FileName = "chmod",
                    UseShellExecute = false
                };
                chmodStartInfo.ArgumentList.Add("+x");
                chmodStartInfo.ArgumentList.Add(outputPath);
                Process.Start(chmodStartInfo)?.WaitForExit();
            }
            catch
            {
                // chmod가 없는 환경에서는 링커가 만든 기본 권한을 그대로 둔다.
            }
        }

        private static bool PathEquals(string left, string right)
        {
            StringComparison comparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

            return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), comparison);
        }
    }

    public class JMLCompileOptions
    {
        public required string SourcePath { get; init; }
        public string? OutputPath { get; init; }
        public required string Rid { get; init; }
        public bool EmitAssemblyOnly { get; init; }
        public bool EmitObjectOnly { get; init; }
        public bool KeepAssembly { get; init; }
    }

    public class JMLLinkOptions
    {
        public required IReadOnlyList<string> ObjectPaths { get; init; }
        public string? OutputPath { get; init; }
        public required string Rid { get; init; }
    }

    internal sealed class NativeTarget
    {
        public required string Rid { get; init; }
        public required string ClangTriple { get; init; }
        public bool IsMacOS { get; init; }
        public bool IsWindows { get; init; }
        public bool IsArm64 { get; init; }

        public string MainSymbol => External("main");
        public string PrintfSymbol => External("printf");
        public string ScanfSymbol => External("scanf");
        public string GetcharSymbol => External("getchar");
        public string PutcharSymbol => External("putchar");

        public string Arg0 => IsWindows ? "rcx" : "rdi";
        public string Arg1 => IsWindows ? "rdx" : "rsi";
        public string Arg2 => IsWindows ? "r8" : "rdx";

        public string External(string name)
        {
            return IsMacOS ? "_" + name : name;
        }

        public static NativeTarget FromRid(string rid)
        {
            return rid switch
            {
                "linux-x64" => new NativeTarget
                {
                    Rid = rid,
                    ClangTriple = "x86_64-linux-gnu"
                },
                "linux-arm64" => new NativeTarget
                {
                    Rid = rid,
                    ClangTriple = "aarch64-linux-gnu",
                    IsArm64 = true
                },
                "osx-x64" => new NativeTarget
                {
                    Rid = rid,
                    ClangTriple = "x86_64-apple-macosx10.13",
                    IsMacOS = true
                },
                "osx-arm64" => new NativeTarget
                {
                    Rid = rid,
                    ClangTriple = "arm64-apple-macosx11.0",
                    IsMacOS = true,
                    IsArm64 = true
                },
                "win-x64" => new NativeTarget
                {
                    Rid = rid,
                    ClangTriple = "x86_64-w64-windows-gnu",
                    IsWindows = true
                },
                "win-arm64" => new NativeTarget
                {
                    Rid = rid,
                    ClangTriple = "aarch64-w64-windows-gnu",
                    IsWindows = true,
                    IsArm64 = true
                },
                _ => throw new ArgumentException("니 눈엔 " + rid + "가 지원하냐?")
            };
        }
    }

    internal interface INativeAssemblyGenerator
    {
        string Generate();
    }

    internal sealed class NativeAssemblyGenerator : INativeAssemblyGenerator
    {
        private const int ValueTypeOffset = 0;
        private const int ValueIntOffset = 8;
        private const int ValuePointerOffset = 16;
        private const int ValueSize = 32;
        private const int InputBufferSize = 4096;
        private const long NumberScale = 1_000_000;
        private readonly NativeTarget _target;
        private readonly JmlNativeProgram _program;
        private readonly string[] _lines;
        private readonly Dictionary<string, string> _variables = [];
        private readonly Dictionary<string, string> _inputBuffers = [];
        private readonly Dictionary<string, string> _stringLiterals = [];
        private readonly Dictionary<int, ArrayStorage> _arrayStorages = [];
        private readonly Dictionary<string, ArrayStorage> _returnBuffers = [];
        private readonly Dictionary<string, FunctionDefinition> _functions = [];
        private readonly Dictionary<int, int> _functionBlocks = [];
        private string _currentContext = JMLNativeParser.MainContext;

        public NativeAssemblyGenerator(NativeTarget target, JmlNativeProgram program)
        {
            _target = target;
            _program = program;
            _lines = program.Lines;

            foreach (NativeFunctionDefinition function in program.Functions.Values)
            {
                _functions[function.Name] = new FunctionDefinition(
                    function.Name,
                    function.DeclarationLine,
                    function.Start,
                    function.End,
                    function.Label,
                    function.Parameters,
                    function.ReturnCount);
            }

            foreach (KeyValuePair<int, int> block in program.FunctionBlocks)
                _functionBlocks[block.Key] = block.Value;
        }

        public string Generate()
        {
            StringBuilder code = new();
            EmitHeader(code);
            EmitMain(code);
            EmitFunctions(code);
            EmitRuntime(code);
            EmitData(code);
            EmitFooter(code);
            return code.ToString();
        }

        private void EmitHeader(StringBuilder code)
        {
            code.AppendLine(".intel_syntax noprefix");
            code.AppendLine(".extern " + _target.PrintfSymbol);
            code.AppendLine(".extern " + _target.ScanfSymbol);
            code.AppendLine(".extern " + _target.GetcharSymbol);

            if (_target.IsMacOS)
                code.AppendLine(".section __TEXT,__text,regular,pure_instructions");
            else
                code.AppendLine(".text");

            code.AppendLine();
        }

        private void EmitFooter(StringBuilder code)
        {
            if (!_target.IsMacOS && !_target.IsWindows)
            {
                code.AppendLine();
                code.AppendLine(".section .note.GNU-stack,\"\",@progbits");
            }
        }

        private void EmitMain(StringBuilder code)
        {
            code.AppendLine(".globl " + _target.MainSymbol);
            code.AppendLine(_target.MainSymbol + ":");
            EmitFunctionPrologue(code);
            EmitProgramRange(code, 1, _lines.Length, "main", _lines.Length);
            code.AppendLine(ReturnLabel("main") + ":");
            code.AppendLine("    xor eax, eax");
            EmitFunctionEpilogue(code);
            code.AppendLine();
        }

        private void EmitFunctions(StringBuilder code)
        {
            foreach (FunctionDefinition function in _functions.Values)
            {
                code.AppendLine(function.Label + ":");
                EmitFunctionPrologue(code);
                EmitStoreFunctionReturnBufferPointer(code, function.Label);
                EmitProgramRange(code, function.Start, function.End, function.Label, function.End);
                code.AppendLine(ReturnLabel(function.Label) + ":");
                code.AppendLine("    xor eax, eax");
                EmitFunctionEpilogue(code);
                code.AppendLine();
            }
        }

        private void EmitProgramRange(StringBuilder code, int startLine, int endLine, string context, int jumpLimitLine, int? returnAfterLine = null)
        {
            string previousContext = _currentContext;
            _currentContext = context;

            try
            {
                for (int lineNumber = startLine; lineNumber <= endLine; lineNumber++)
                {
                    if (ShouldEmitLineLabel(context, lineNumber))
                        code.AppendLine(LineLabel(context, lineNumber) + ":");

                    if (IsDeadLine(context, lineNumber))
                        continue;

                    if (context == JMLNativeParser.MainContext && _functionBlocks.TryGetValue(lineNumber, out int functionEnd))
                    {
                        lineNumber = functionEnd;
                        continue;
                    }

                    EmitLine(code, lineNumber, context, jumpLimitLine);

                    if (returnAfterLine == lineNumber)
                        code.AppendLine($"    jmp {ReturnLabel(context)}");
                }
            }
            finally
            {
                _currentContext = previousContext;
            }
        }

        private void EmitLine(StringBuilder code, int lineNumber, string context, int jumpLimitLine)
        {
            if (!_program.Statements.TryGetValue(lineNumber, out JmlStmt? statement))
                return;

            switch (statement)
            {
                case PrintStmt print:
                    EmitPrint(code, print, lineNumber - 1);
                    break;
                case InputStmt input:
                    EmitInput(code, input.Name);
                    break;
                case AssignStmt assign:
                    EmitAssignment(code, assign, lineNumber - 1);
                    break;
                case BranchNotEqualStmt branch:
                    EmitRepeat(code, branch.Args, lineNumber - 1, context, jumpLimitLine);
                    break;
                case FunctionStmt function:
                    EmitFunctionCommand(code, function, lineNumber, lineNumber - 1);
                    break;
                case ReturnStmt ret:
                    EmitReturn(code, ret, context, lineNumber - 1);
                    break;
                default:
                    throw new ArgumentException($"{lineNumber}번째 줄에 모르는 명령어가 있잖아;;");
            }
        }

        private void EmitPrint(StringBuilder code, PrintStmt statement, int lineIndex)
        {
            if (statement.Values.Count == 0)
                throw new ArgumentException($"{lineIndex + 1}번째 줄 안산에 인수가 없잖아;;");

            if (statement.Values.Count == 1 && statement.Values[0] is CallExpr call)
            {
                FunctionDefinition function = GetFunction(call.Name, lineIndex);
                string returnBuffer = ReturnBufferLabel(lineIndex, Math.Max(1, function.ReturnCount));
                EmitFunctionCall(code, call.Name, call.Args, lineIndex, returnBuffer);
                for (int i = 0; i < function.ReturnCount; i++)
                    EmitPrintValueAddress(code, returnBuffer, i * ValueSize);
                return;
            }

            foreach (Expr expr in statement.Values)
                EmitPrintExpr(code, expr);
        }

        private void EmitPrintExpr(StringBuilder code, Expr expr)
        {
            switch (expr)
            {
                case StringExpr text:
                    EmitLoadAddressToArg(code, 0, StringLabel(text.Value));
                    EmitCall(code, "jml_print_string");
                    return;
                case NumberExpr number:
                    EmitMoveToArg(code, 0, number.ScaledValue.ToString());
                    EmitCall(code, "jml_print_int");
                    return;
                case NullExpr:
                    EmitPrintValueAddress(code, "jml_null_value");
                    return;
                case ArrayAccessExpr array:
                    EmitLoadArrayElementAddress(code, array.Name, array.Indices.ToArray(), "r11");
                    EmitMoveToArg(code, 0, "r11");
                    EmitCall(code, "jml_print_value");
                    return;
                case VarExpr variable:
                    EmitPrintValueAddress(code, VariableLabel(variable.Name));
                    return;
                case BinaryExpr binary:
                    EmitNumericExpressionToLabel(code, binary, "jml_tmp_value");
                    EmitPrintValueAddress(code, "jml_tmp_value");
                    return;
                default:
                    throw new ArgumentException("출력할 수 없는 표현식이잖아;;");
            }
        }

        private void EmitPrintValueAddress(StringBuilder code, string label, int offset = 0)
        {
            EmitLoadAddressToArg(code, 0, label, offset);
            EmitCall(code, "jml_print_value");
        }

        private void EmitInput(StringBuilder code, string key)
        {
            EmitLoadAddressToArg(code, 0, VariableLabel(key));
            EmitLoadAddressToArg(code, 1, InputBufferLabel(key));
            EmitCall(code, "jml_read_line");
        }

        private void EmitAssignment(StringBuilder code, AssignStmt statement, int lineIndex)
        {
            string[] args = statement.Args;
            string key = RequireArg(args, 1, lineIndex);
            string[] valueTokens = args.Skip(2).ToArray();

            if (key.StartsWith('[') && key.EndsWith(']'))
            {
                EmitArrayAssignment(code, key[1..^1], valueTokens, lineIndex);
                return;
            }

            if (TryParseArrayAccess(key, out string arraySetName, out int[] arraySetIndices))
            {
                if (valueTokens.Length != 1)
                    throw new ArgumentException($"{lineIndex + 1}번째 줄 배열 값은 하나여야지;;");

                if (statement.Values.Count == 1 && statement.Values[0] is BinaryExpr arrayValueExpr)
                {
                    EmitAssignExprToLabel(code, arrayValueExpr, "jml_tmp_value");
                    EmitAssignLabelToArrayElement(code, arraySetName, arraySetIndices, "jml_tmp_value");
                }
                else
                {
                    EmitAssignTokenToArrayElement(code, arraySetName, arraySetIndices, valueTokens[0]);
                }
                return;
            }

            if (LooksLikeFunctionCall(valueTokens))
            {
                CallExpr call = (CallExpr)statement.Values[0];
                FunctionDefinition function = GetFunction(call.Name, lineIndex);
                if (function.ReturnCount != 1)
                    throw new ArgumentException("변수에는 값 하나만 넣어야지;;");

                EmitFunctionCall(code, call.Name, call.Args, lineIndex, VariableLabel(key));
                return;
            }

            string data = RequireArg(args, 2, lineIndex);
            if (valueTokens.Length != 1)
                throw new ArgumentException("변수에 값은 하나만 줘야지;;");

            if (Utils.IsExpression(data))
            {
                EmitExpressionAssignment(code, key, data);
                return;
            }

            if (statement.Values.Count == 1 && statement.Values[0] is BinaryExpr binary)
            {
                EmitNumericExpressionToLabel(code, binary, VariableLabel(key));
                return;
            }

            EmitAssignTokenToLabel(code, data, VariableLabel(key));
        }

        private void EmitArrayAssignment(StringBuilder code, string key, string[] rawValues, int lineIndex)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException($"{lineIndex + 1}번째 줄 배열 이름이 비었잖아;;");

            ArrayStorage storage = ArrayStorageLabel(lineIndex, rawValues.Length);

            for (int i = 0; i < rawValues.Length; i++)
            {
                EmitAssignTokenToLabel(code, rawValues[i], storage.Label, i * ValueSize);
            }

            EmitLoadAddressToArg(code, 0, VariableLabel(key));
            EmitLoadAddressToArg(code, 1, storage.Label);
            EmitMoveToArg(code, 2, rawValues.Length.ToString());
            EmitCall(code, "jml_set_array");
        }

        private void EmitExpressionAssignment(StringBuilder code, string key, string expression)
        {
            string op = expression[..1];
            string operand = expression[1..];

            if (TryParseNumber(operand, out long constantOperand))
            {
                if ((op is "+" or "-" && constantOperand == 0) ||
                    (op is "*" or "/" or "^" && constantOperand == NumberScale))
                {
                    return;
                }

                if (op == "*" && constantOperand == 0)
                {
                    EmitAssignTokenToLabel(code, "0", VariableLabel(key));
                    return;
                }

                if (op == "^" && constantOperand == 0)
                {
                    EmitAssignTokenToLabel(code, "1", VariableLabel(key));
                    return;
                }
            }

            EmitLoadInt(code, key, "r10");
            code.AppendLine("    mov qword ptr [rip + jml_tmp_0], r10");
            EmitLoadInt(code, operand, "r11");
            code.AppendLine("    mov r10, qword ptr [rip + jml_tmp_0]");

            switch (op)
            {
                case "+":
                    code.AppendLine("    add r10, r11");
                    code.AppendLine("    mov r11, r10");
                    break;
                case "-":
                    code.AppendLine("    sub r10, r11");
                    code.AppendLine("    mov r11, r10");
                    break;
                case "*":
                    code.AppendLine("    mov rax, r10");
                    code.AppendLine("    imul r11");
                    code.AppendLine($"    mov r11, {NumberScale}");
                    code.AppendLine("    idiv r11");
                    code.AppendLine("    mov r11, rax");
                    break;
                case "/":
                    code.AppendLine("    mov rax, r10");
                    code.AppendLine($"    imul rax, rax, {NumberScale}");
                    code.AppendLine("    cqo");
                    code.AppendLine("    idiv r11");
                    code.AppendLine("    mov r11, rax");
                    break;
                case "^":
                    EmitMoveToArg(code, 0, "r10");
                    EmitMoveToArg(code, 1, "r11");
                    EmitCall(code, "jml_number_pow");
                    code.AppendLine("    mov r11, rax");
                    break;
                default:
                    throw new ArgumentException("이런 수식은 안산에도 없어;; " + op);
            }

            EmitLoadAddressToArg(code, 0, VariableLabel(key));
            EmitMoveToArg(code, 1, "r11");
            EmitCall(code, "jml_set_int");
        }

        private void EmitNumericExpressionToLabel(StringBuilder code, Expr expr, string destinationLabel)
        {
            ExpressionPlan plan = ExpressionPlanner.Build(expr, ["r12", "r13", "r14", "r15", "rbx"]);
            foreach (ExprIrInstr instruction in plan.Instructions)
            {
                switch (instruction)
                {
                    case LoadExprIrInstr load:
                        EmitLoadExprToRegister(code, load.Source, plan.Registers[load.Dest]);
                        break;
                    case BinaryExprIrInstr binary:
                        EmitBinaryExprInstruction(code, binary, plan.Registers);
                        break;
                }
            }

            EmitLoadAddressToArg(code, 0, destinationLabel);
            EmitMoveToArg(code, 1, plan.Registers[plan.Result]);
            EmitCall(code, "jml_set_int");
        }

        private void EmitLoadExprToRegister(StringBuilder code, Expr expr, string register)
        {
            switch (expr)
            {
                case NumberExpr number:
                    code.AppendLine($"    mov {register}, {number.ScaledValue}");
                    return;
                case VarExpr variable:
                    EmitLoadInt(code, variable.Name, register);
                    return;
                case ArrayAccessExpr array:
                    EmitLoadInt(code, ArrayAccessToken(array), register);
                    return;
                default:
                    throw new ArgumentException("숫자 표현식에 쓸 수 없는 값이잖아;;");
            }
        }

        private void EmitBinaryExprInstruction(StringBuilder code, BinaryExprIrInstr instruction, IReadOnlyDictionary<VReg, string> registers)
        {
            string destination = registers[instruction.Dest];
            string left = registers[instruction.Left];
            string right = registers[instruction.Right];

            if (destination != left)
                code.AppendLine($"    mov {destination}, {left}");

            switch (instruction.Op)
            {
                case "+":
                    code.AppendLine($"    add {destination}, {right}");
                    return;
                case "-":
                    code.AppendLine($"    sub {destination}, {right}");
                    return;
                case "*":
                    code.AppendLine($"    mov rax, {destination}");
                    code.AppendLine($"    imul {right}");
                    code.AppendLine($"    mov r9, {NumberScale}");
                    code.AppendLine("    idiv r9");
                    code.AppendLine($"    mov {destination}, rax");
                    return;
                case "/":
                    code.AppendLine($"    mov rax, {destination}");
                    code.AppendLine($"    imul rax, rax, {NumberScale}");
                    code.AppendLine("    cqo");
                    code.AppendLine($"    idiv {right}");
                    code.AppendLine($"    mov {destination}, rax");
                    return;
                case "^":
                    PreserveExpressionRegistersForCall(code, registers.Values.Distinct(), () =>
                    {
                        EmitMoveToArg(code, 0, left);
                        EmitMoveToArg(code, 1, right);
                        EmitCall(code, "jml_number_pow");
                        code.AppendLine("    mov qword ptr [rip + jml_tmp_0], rax");
                    });
                    code.AppendLine($"    mov {destination}, qword ptr [rip + jml_tmp_0]");
                    return;
                default:
                    throw new ArgumentException("이런 수식은 안산에도 없어;; " + instruction.Op);
            }
        }

        private void PreserveExpressionRegistersForCall(StringBuilder code, IEnumerable<string> registers, Action emitCall)
        {
            string[] savedRegisters = registers.Order().ToArray();
            foreach (string register in savedRegisters)
                code.AppendLine($"    push {register}");

            emitCall();

            foreach (string register in savedRegisters.Reverse())
                code.AppendLine($"    pop {register}");
        }

        private static string ArrayAccessToken(ArrayAccessExpr array)
        {
            return array.Name + "." + string.Join(".", array.Indices);
        }

        private void EmitRepeat(StringBuilder code, string[] args, int lineIndex, string context, int jumpLimitLine)
        {
            string rawVal1 = RequireArg(args, 1, lineIndex);
            string rawVal2 = RequireArg(args, 2, lineIndex);
            int goTo = ParseLineNumber(RequireArg(args, 3, lineIndex), lineIndex);

            if (goTo <= 0 || goTo > jumpLimitLine)
                throw new ArgumentException($"{lineIndex + 1}번째 줄 이동할 줄 번호가 이상하잖아;;");

            if (TryParseNumber(rawVal1, out long constantLeft) && TryParseNumber(rawVal2, out long constantRight))
            {
                if (constantLeft != constantRight)
                    code.AppendLine($"    jmp {LineLabel(context, goTo)}");

                return;
            }

            EmitLoadInt(code, rawVal1, "r10");
            code.AppendLine("    mov qword ptr [rip + jml_tmp_0], r10");
            EmitLoadInt(code, rawVal2, "r11");
            code.AppendLine("    mov r10, qword ptr [rip + jml_tmp_0]");
            code.AppendLine("    cmp r10, r11");
            code.AppendLine($"    jne {LineLabel(context, goTo)}");
        }

        private void EmitFunctionCommand(StringBuilder code, FunctionStmt statement, int lineNumber, int lineIndex)
        {
            string functionName = statement.Name;
            if (_functions.TryGetValue(functionName, out FunctionDefinition? definition) &&
                definition.DeclarationLine == lineNumber)
            {
                return;
            }

            FunctionDefinition function = GetFunction(functionName, lineIndex);
            string returnBuffer = ReturnBufferLabel(lineIndex, Math.Max(1, function.ReturnCount));
            EmitFunctionCall(code, functionName, statement.Arguments, lineIndex, returnBuffer);
        }

        private void EmitReturn(StringBuilder code, ReturnStmt statement, string context, int lineIndex)
        {
            if (context == "main")
                throw new InvalidOperationException("여기서 음... 쓰면 어떡해;;");

            string[] tokens = statement.Args.Skip(1).ToArray();

            if (LooksLikeFunctionCall(tokens))
            {
                CallExpr call = (CallExpr)statement.Values[0];
                EmitFunctionCallToCurrentReturnBuffer(code, call.Name, call.Args, lineIndex, context);
            }
            else
            {
                for (int i = 0; i < statement.Values.Count; i++)
                {
                    EmitAssignExprToLabel(code, statement.Values[i], "jml_tmp_value");
                    EmitCopyValueToCurrentReturnBuffer(code, "jml_tmp_value", i, context);
                }
            }

            code.AppendLine($"    jmp {ReturnLabel(context)}");
        }

        private FunctionDefinition EmitFunctionCall(StringBuilder code, string functionName, IReadOnlyList<Expr> args, int lineIndex, string returnBufferLabel)
        {
            FunctionDefinition function = PrepareFunctionCallArguments(code, functionName, args, lineIndex);
            EmitLoadAddressToArg(code, 0, returnBufferLabel);
            EmitCall(code, function.Label);
            return function;
        }

        private FunctionDefinition EmitFunctionCallToCurrentReturnBuffer(StringBuilder code, string functionName, IReadOnlyList<Expr> args, int lineIndex, string context)
        {
            FunctionDefinition function = PrepareFunctionCallArguments(code, functionName, args, lineIndex);
            code.AppendLine($"    mov {_target.Arg0}, qword ptr [rip + {FunctionReturnBufferPointerLabel(context)}]");
            EmitCall(code, function.Label);
            return function;
        }

        private FunctionDefinition PrepareFunctionCallArguments(StringBuilder code, string functionName, IReadOnlyList<Expr> args, int lineIndex)
        {
            FunctionDefinition function = GetFunction(functionName, lineIndex);

            if (function.Parameters.Length != args.Count)
                throw new ArgumentException(functionName + " 함수 인수 개수가 안맞잖아;;");

            for (int i = 0; i < args.Count; i++)
                EmitAssignExprToLabel(code, args[i], VariableLabel(function.Label, function.Parameters[i], forceLocal: true));

            return function;
        }

        private FunctionDefinition GetFunction(string functionName, int lineIndex)
        {
            if (!_functions.TryGetValue(functionName, out FunctionDefinition? function))
                throw new ArgumentException($"{lineIndex + 1}번째 줄 {functionName} 함수가 정의가 안됐잖아;;");

            return function;
        }

        private void EmitLoadInt(StringBuilder code, string token, string targetRegister)
        {
            if (TryParseNumber(token, out long number))
            {
                code.AppendLine($"    mov {targetRegister}, {number}");
                return;
            }

            if (TryParseArrayAccess(token, out string arrayName, out int[] arrayIndices))
            {
                EmitLoadArrayElementAddress(code, arrayName, arrayIndices, "r11");
                EmitMoveToArg(code, 0, "r11");
                EmitCall(code, "jml_value_to_int");

                if (targetRegister != "rax")
                    code.AppendLine($"    mov {targetRegister}, rax");
                return;
            }

            EmitLoadAddressToArg(code, 0, VariableLabel(token));
            EmitCall(code, "jml_value_to_int");

            if (targetRegister != "rax")
                code.AppendLine($"    mov {targetRegister}, rax");
        }

        private void EmitLoadArrayElementAddress(StringBuilder code, string arrayName, int[] arrayIndices, string targetRegister)
        {
            EmitLoadAddressToArg(code, 0, VariableLabel(arrayName));
            foreach (int arrayIndex in arrayIndices)
            {
                EmitMoveToArg(code, 1, arrayIndex.ToString());
                EmitCall(code, "jml_array_get");
                EmitMoveToArg(code, 0, "rax");
            }

            if (targetRegister != "rax")
                code.AppendLine($"    mov {targetRegister}, rax");
        }

        private void EmitAssignTokenToArrayElement(StringBuilder code, string arrayName, int[] arrayIndices, string token)
        {
            if (arrayIndices.Length == 0)
                throw new ArgumentException("배열 접근이 이상하잖아;;");

            EmitAssignTokenToLabel(code, token, "jml_tmp_value");
            EmitAssignLabelToArrayElement(code, arrayName, arrayIndices, "jml_tmp_value");
        }

        private void EmitAssignLabelToArrayElement(StringBuilder code, string arrayName, int[] arrayIndices, string sourceLabel)
        {
            EmitLoadAddressToArg(code, 0, VariableLabel(arrayName));
            for (int i = 0; i < arrayIndices.Length - 1; i++)
            {
                EmitMoveToArg(code, 1, arrayIndices[i].ToString());
                EmitCall(code, "jml_array_get");
                EmitMoveToArg(code, 0, "rax");
            }

            EmitMoveToArg(code, 1, arrayIndices[^1].ToString());
            EmitLoadAddressToArg(code, 2, sourceLabel);
            EmitCall(code, "jml_array_set");
        }

        private void EmitAssignExprToLabel(StringBuilder code, Expr expr, string destinationLabel)
        {
            switch (expr)
            {
                case NullExpr:
                    EmitLoadAddressToArg(code, 0, destinationLabel);
                    EmitCall(code, "jml_set_null");
                    return;
                case StringExpr text:
                    EmitLoadAddressToArg(code, 0, destinationLabel);
                    EmitLoadAddressToArg(code, 1, StringLabel(text.Value));
                    EmitCall(code, "jml_set_string");
                    return;
                case NumberExpr number:
                    EmitLoadAddressToArg(code, 0, destinationLabel);
                    EmitMoveToArg(code, 1, number.ScaledValue.ToString());
                    EmitCall(code, "jml_set_int");
                    return;
                case VarExpr variable:
                    EmitCopyValue(code, destinationLabel, VariableLabel(variable.Name));
                    return;
                case ArrayAccessExpr array:
                    EmitLoadArrayElementAddress(code, array.Name, array.Indices.ToArray(), "r11");
                    EmitLoadAddressToArg(code, 0, destinationLabel);
                    EmitMoveToArg(code, 1, "r11");
                    EmitCall(code, "jml_copy_value");
                    return;
                case BinaryExpr binary:
                    EmitNumericExpressionToLabel(code, binary, destinationLabel);
                    return;
                default:
                    throw new ArgumentException("대입할 수 없는 표현식이잖아;;");
            }
        }

        private void EmitAssignTokenToLabel(StringBuilder code, string token, string destinationLabel, int destinationOffset = 0)
        {
            EmitLoadAddressToRegister(code, "r10", destinationLabel, destinationOffset);
            code.AppendLine("    mov qword ptr [rip + jml_addr_tmp], r10");

            if (token == "여친")
            {
                code.AppendLine("    mov " + _target.Arg0 + ", qword ptr [rip + jml_addr_tmp]");
                EmitCall(code, "jml_set_null");
                return;
            }

            if (IsStringLiteral(token))
            {
                code.AppendLine("    mov " + _target.Arg0 + ", qword ptr [rip + jml_addr_tmp]");
                EmitLoadAddressToArg(code, 1, StringLabel(StringValue(token)));
                EmitCall(code, "jml_set_string");
                return;
            }

            if (TryParseNumber(token, out long number))
            {
                code.AppendLine("    mov " + _target.Arg0 + ", qword ptr [rip + jml_addr_tmp]");
                EmitMoveToArg(code, 1, number.ToString());
                EmitCall(code, "jml_set_int");
                return;
            }

            if (TryParseArrayAccess(token, out string arrayName, out int[] arrayIndices))
            {
                EmitLoadArrayElementAddress(code, arrayName, arrayIndices, "r11");
                code.AppendLine("    mov " + _target.Arg0 + ", qword ptr [rip + jml_addr_tmp]");
                EmitMoveToArg(code, 1, "r11");
                EmitCall(code, "jml_copy_value");
                return;
            }

            code.AppendLine("    mov " + _target.Arg0 + ", qword ptr [rip + jml_addr_tmp]");
            EmitLoadAddressToArg(code, 1, VariableLabel(token));
            EmitCall(code, "jml_copy_value");
        }

        private void EmitCopyValue(StringBuilder code, string destinationLabel, string sourceLabel)
        {
            EmitLoadAddressToArg(code, 0, destinationLabel);
            EmitLoadAddressToArg(code, 1, sourceLabel);
            EmitCall(code, "jml_copy_value");
        }

        private void EmitStoreFunctionReturnBufferPointer(StringBuilder code, string context)
        {
            code.AppendLine($"    mov qword ptr [rip + {FunctionReturnBufferPointerLabel(context)}], {_target.Arg0}");
        }

        private void EmitCopyValueToCurrentReturnBuffer(StringBuilder code, string sourceLabel, int returnIndex, string context)
        {
            code.AppendLine($"    mov r10, qword ptr [rip + {FunctionReturnBufferPointerLabel(context)}]");
            if (returnIndex > 0)
                code.AppendLine($"    add r10, {returnIndex * ValueSize}");
            code.AppendLine("    mov qword ptr [rip + jml_addr_tmp], r10");
            code.AppendLine($"    mov {_target.Arg0}, qword ptr [rip + jml_addr_tmp]");
            EmitLoadAddressToArg(code, 1, sourceLabel);
            EmitCall(code, "jml_copy_value");
        }

        private void EmitRuntime(StringBuilder code)
        {
            EmitSetNull(code);
            EmitSetInt(code);
            EmitSetString(code);
            EmitCopyValue(code);
            EmitValueToInt(code);

            if (_program.RuntimeFeatures.HasFlag(NativeRuntimeFeatures.Array))
            {
                EmitSetArray(code);
                EmitArrayGet(code);
                EmitArraySet(code);
            }

            if (_program.RuntimeFeatures.HasFlag(NativeRuntimeFeatures.Pow))
                EmitNumberPow(code);

            if (_program.RuntimeFeatures.HasFlag(NativeRuntimeFeatures.Print))
            {
                EmitPrintString(code);
                EmitPrintInt(code);
                EmitPrintValue(code);
                EmitPrintArray(code);
            }

            if (_program.RuntimeFeatures.HasFlag(NativeRuntimeFeatures.Input))
                EmitReadLine(code);
        }

        private void EmitSetNull(StringBuilder code)
        {
            code.AppendLine("jml_set_null:");
            code.AppendLine($"    mov qword ptr [{_target.Arg0} + {ValueTypeOffset}], 0");
            code.AppendLine($"    mov qword ptr [{_target.Arg0} + {ValueIntOffset}], 0");
            code.AppendLine($"    mov qword ptr [{_target.Arg0} + {ValuePointerOffset}], 0");
            code.AppendLine("    ret");
            code.AppendLine();
        }

        private void EmitSetInt(StringBuilder code)
        {
            code.AppendLine("jml_set_int:");
            code.AppendLine($"    mov qword ptr [{_target.Arg0} + {ValueTypeOffset}], 1");
            code.AppendLine($"    mov qword ptr [{_target.Arg0} + {ValueIntOffset}], {_target.Arg1}");
            code.AppendLine($"    mov qword ptr [{_target.Arg0} + {ValuePointerOffset}], 0");
            code.AppendLine("    ret");
            code.AppendLine();
        }

        private void EmitSetString(StringBuilder code)
        {
            code.AppendLine("jml_set_string:");
            code.AppendLine($"    mov qword ptr [{_target.Arg0} + {ValueTypeOffset}], 2");
            code.AppendLine($"    mov qword ptr [{_target.Arg0} + {ValueIntOffset}], 0");
            code.AppendLine($"    mov qword ptr [{_target.Arg0} + {ValuePointerOffset}], {_target.Arg1}");
            code.AppendLine("    ret");
            code.AppendLine();
        }

        private void EmitCopyValue(StringBuilder code)
        {
            code.AppendLine("jml_copy_value:");
            code.AppendLine($"    mov r10, qword ptr [{_target.Arg1} + 0]");
            code.AppendLine($"    mov qword ptr [{_target.Arg0} + 0], r10");
            code.AppendLine($"    mov r10, qword ptr [{_target.Arg1} + 8]");
            code.AppendLine($"    mov qword ptr [{_target.Arg0} + 8], r10");
            code.AppendLine($"    mov r10, qword ptr [{_target.Arg1} + 16]");
            code.AppendLine($"    mov qword ptr [{_target.Arg0} + 16], r10");
            code.AppendLine($"    mov r10, qword ptr [{_target.Arg1} + 24]");
            code.AppendLine($"    mov qword ptr [{_target.Arg0} + 24], r10");
            code.AppendLine("    ret");
            code.AppendLine();
        }

        private void EmitSetArray(StringBuilder code)
        {
            code.AppendLine("jml_set_array:");
            code.AppendLine($"    mov qword ptr [{_target.Arg0} + {ValueTypeOffset}], 3");
            code.AppendLine($"    mov qword ptr [{_target.Arg0} + {ValueIntOffset}], {_target.Arg2}");
            code.AppendLine($"    mov qword ptr [{_target.Arg0} + {ValuePointerOffset}], {_target.Arg1}");
            code.AppendLine("    ret");
            code.AppendLine();
        }

        private void EmitArrayGet(StringBuilder code)
        {
            code.AppendLine("jml_array_get:");
            EmitLoadAddressToRegister(code, "rax", "jml_null_value");
            code.AppendLine($"    cmp qword ptr [{_target.Arg0} + {ValueTypeOffset}], 3");
            code.AppendLine("    jne jml_array_get_done");
            code.AppendLine($"    cmp {_target.Arg1}, 0");
            code.AppendLine("    jl jml_array_get_done");
            code.AppendLine($"    cmp {_target.Arg1}, qword ptr [{_target.Arg0} + {ValueIntOffset}]");
            code.AppendLine("    jge jml_array_get_done");
            code.AppendLine($"    mov r10, qword ptr [{_target.Arg0} + {ValuePointerOffset}]");
            code.AppendLine($"    mov r11, {_target.Arg1}");
            code.AppendLine("    shl r11, 5");
            code.AppendLine("    lea rax, [r10 + r11]");
            code.AppendLine("jml_array_get_done:");
            code.AppendLine("    ret");
            code.AppendLine();
        }

        private void EmitArraySet(StringBuilder code)
        {
            code.AppendLine("jml_array_set:");
            code.AppendLine($"    cmp qword ptr [{_target.Arg0} + {ValueTypeOffset}], 3");
            code.AppendLine("    jne jml_array_set_done");
            code.AppendLine($"    cmp {_target.Arg1}, 0");
            code.AppendLine("    jl jml_array_set_done");
            code.AppendLine($"    cmp {_target.Arg1}, qword ptr [{_target.Arg0} + {ValueIntOffset}]");
            code.AppendLine("    jge jml_array_set_done");
            code.AppendLine($"    mov r10, qword ptr [{_target.Arg0} + {ValuePointerOffset}]");
            code.AppendLine($"    mov r11, {_target.Arg1}");
            code.AppendLine("    shl r11, 5");
            code.AppendLine("    add r10, r11");
            code.AppendLine($"    mov r11, {_target.Arg2}");
            code.AppendLine("    mov rax, qword ptr [r11 + 0]");
            code.AppendLine("    mov qword ptr [r10 + 0], rax");
            code.AppendLine("    mov rax, qword ptr [r11 + 8]");
            code.AppendLine("    mov qword ptr [r10 + 8], rax");
            code.AppendLine("    mov rax, qword ptr [r11 + 16]");
            code.AppendLine("    mov qword ptr [r10 + 16], rax");
            code.AppendLine("    mov rax, qword ptr [r11 + 24]");
            code.AppendLine("    mov qword ptr [r10 + 24], rax");
            code.AppendLine("jml_array_set_done:");
            code.AppendLine("    ret");
            code.AppendLine();
        }

        private void EmitValueToInt(StringBuilder code)
        {
            code.AppendLine("jml_value_to_int:");
            code.AppendLine($"    cmp qword ptr [{_target.Arg0} + {ValueTypeOffset}], 1");
            code.AppendLine("    je jml_value_to_int_number");
            code.AppendLine($"    cmp qword ptr [{_target.Arg0} + {ValueTypeOffset}], 2");
            code.AppendLine("    je jml_value_to_int_string");
            code.AppendLine("    xor rax, rax");
            code.AppendLine("    ret");
            code.AppendLine("jml_value_to_int_number:");
            code.AppendLine($"    mov rax, qword ptr [{_target.Arg0} + {ValueIntOffset}]");
            code.AppendLine("    ret");
            code.AppendLine("jml_value_to_int_string:");
            code.AppendLine($"    mov rsi, qword ptr [{_target.Arg0} + {ValuePointerOffset}]");
            code.AppendLine("    xor rax, rax");
            code.AppendLine("    xor r8, r8");
            code.AppendLine($"    mov r9, {NumberScale}");
            code.AppendLine("    movzx rcx, byte ptr [rsi]");
            code.AppendLine("    cmp rcx, 45");
            code.AppendLine("    jne jml_value_to_int_loop");
            code.AppendLine("    mov r8, 1");
            code.AppendLine("    inc rsi");
            code.AppendLine("jml_value_to_int_loop:");
            code.AppendLine("    movzx rcx, byte ptr [rsi]");
            code.AppendLine("    cmp rcx, 46");
            code.AppendLine("    je jml_value_to_int_fraction_start");
            code.AppendLine("    cmp rcx, 48");
            code.AppendLine("    jl jml_value_to_int_done");
            code.AppendLine("    cmp rcx, 57");
            code.AppendLine("    jg jml_value_to_int_done");
            code.AppendLine("    imul rax, rax, 10");
            code.AppendLine("    sub rcx, 48");
            code.AppendLine($"    imul rcx, rcx, {NumberScale}");
            code.AppendLine("    add rax, rcx");
            code.AppendLine("    inc rsi");
            code.AppendLine("    jmp jml_value_to_int_loop");
            code.AppendLine("jml_value_to_int_fraction_start:");
            code.AppendLine("    inc rsi");
            code.AppendLine($"    mov r9, {NumberScale / 10}");
            code.AppendLine("jml_value_to_int_fraction:");
            code.AppendLine("    test r9, r9");
            code.AppendLine("    jz jml_value_to_int_done");
            code.AppendLine("    movzx rcx, byte ptr [rsi]");
            code.AppendLine("    cmp rcx, 48");
            code.AppendLine("    jl jml_value_to_int_done");
            code.AppendLine("    cmp rcx, 57");
            code.AppendLine("    jg jml_value_to_int_done");
            code.AppendLine("    sub rcx, 48");
            code.AppendLine("    imul rcx, r9");
            code.AppendLine("    add rax, rcx");
            code.AppendLine("    mov rcx, 10");
            code.AppendLine("    xchg rax, r9");
            code.AppendLine("    xor rdx, rdx");
            code.AppendLine("    div rcx");
            code.AppendLine("    mov r9, rax");
            code.AppendLine("    xchg rax, r9");
            code.AppendLine("    inc rsi");
            code.AppendLine("    jmp jml_value_to_int_fraction");
            code.AppendLine("jml_value_to_int_done:");
            code.AppendLine("    test r8, r8");
            code.AppendLine("    jz jml_value_to_int_ret");
            code.AppendLine("    neg rax");
            code.AppendLine("jml_value_to_int_ret:");
            code.AppendLine("    ret");
            code.AppendLine();
        }

        private void EmitNumberPow(StringBuilder code)
        {
            code.AppendLine("jml_number_pow:");
            code.AppendLine($"    mov r10, {_target.Arg0}");
            code.AppendLine($"    mov r11, {_target.Arg1}");
            code.AppendLine("    xor r8, r8");
            code.AppendLine("    cmp r11, 0");
            code.AppendLine("    jge jml_number_pow_exp_positive");
            code.AppendLine("    neg r11");
            code.AppendLine("    mov r8, 1");
            code.AppendLine("jml_number_pow_exp_positive:");
            code.AppendLine("    mov rax, r11");
            code.AppendLine("    cqo");
            code.AppendLine($"    mov r9, {NumberScale}");
            code.AppendLine("    idiv r9");
            code.AppendLine("    mov rcx, rax");
            code.AppendLine($"    mov rax, {NumberScale}");
            code.AppendLine("jml_number_pow_loop:");
            code.AppendLine("    test rcx, rcx");
            code.AppendLine("    jz jml_number_pow_done");
            code.AppendLine("    test rcx, 1");
            code.AppendLine("    jz jml_number_pow_square");
            code.AppendLine("    imul r10");
            code.AppendLine($"    mov r9, {NumberScale}");
            code.AppendLine("    idiv r9");
            code.AppendLine("jml_number_pow_square:");
            code.AppendLine("    mov r9, rax");
            code.AppendLine("    mov rax, r10");
            code.AppendLine("    imul r10");
            code.AppendLine($"    mov r12, {NumberScale}");
            code.AppendLine("    idiv r12");
            code.AppendLine("    mov r10, rax");
            code.AppendLine("    mov rax, r9");
            code.AppendLine("    sar rcx, 1");
            code.AppendLine("    jmp jml_number_pow_loop");
            code.AppendLine("jml_number_pow_done:");
            code.AppendLine("    test r8, r8");
            code.AppendLine("    jz jml_number_pow_ret");
            code.AppendLine("    mov r10, rax");
            code.AppendLine($"    mov rax, {NumberScale}");
            code.AppendLine($"    imul rax, rax, {NumberScale}");
            code.AppendLine("    cqo");
            code.AppendLine("    idiv r10");
            code.AppendLine("jml_number_pow_ret:");
            code.AppendLine("    ret");
            code.AppendLine();
        }

        private void EmitPrintString(StringBuilder code)
        {
            code.AppendLine("jml_print_string:");
            EmitExternalCallPrologue(code);
            code.AppendLine($"    mov r10, {_target.Arg0}");
            EmitLoadAddressToArg(code, 0, "jml_fmt_str");
            EmitMoveToArg(code, 1, "r10");
            code.AppendLine("    xor eax, eax");
            code.AppendLine("    call " + _target.PrintfSymbol);
            EmitExternalCallEpilogue(code);
            code.AppendLine();
        }

        private void EmitPrintInt(StringBuilder code)
        {
            code.AppendLine("jml_print_int:");
            EmitExternalCallPrologue(code);
            code.AppendLine($"    mov r10, {_target.Arg0}");
            code.AppendLine("    cmp r10, 0");
            code.AppendLine("    jge jml_print_int_positive");
            code.AppendLine("    mov qword ptr [rip + jml_tmp_1], r10");
            EmitMoveToArg(code, 0, "45");
            code.AppendLine("    call " + _target.PutcharSymbol);
            code.AppendLine("    mov r10, qword ptr [rip + jml_tmp_1]");
            code.AppendLine("    neg r10");
            code.AppendLine("jml_print_int_positive:");
            code.AppendLine("    mov rax, r10");
            code.AppendLine("    cqo");
            code.AppendLine($"    mov r11, {NumberScale}");
            code.AppendLine("    idiv r11");
            code.AppendLine("    mov qword ptr [rip + jml_tmp_1], rdx");
            EmitLoadAddressToArg(code, 0, "jml_fmt_int");
            EmitMoveToArg(code, 1, "rax");
            code.AppendLine("    xor eax, eax");
            code.AppendLine("    call " + _target.PrintfSymbol);
            code.AppendLine("    mov r10, qword ptr [rip + jml_tmp_1]");
            code.AppendLine("    test r10, r10");
            code.AppendLine("    jz jml_print_int_done");
            code.AppendLine("    mov qword ptr [rip + jml_tmp_1], r10");
            EmitMoveToArg(code, 0, "46");
            code.AppendLine("    call " + _target.PutcharSymbol);
            code.AppendLine("    mov r10, qword ptr [rip + jml_tmp_1]");
            code.AppendLine($"    mov r11, {NumberScale / 10}");
            code.AppendLine("jml_print_int_trim:");
            code.AppendLine("    mov rax, r10");
            code.AppendLine("    xor rdx, rdx");
            code.AppendLine("    mov rcx, 10");
            code.AppendLine("    div rcx");
            code.AppendLine("    test rdx, rdx");
            code.AppendLine("    jne jml_print_int_digits");
            code.AppendLine("    mov r10, rax");
            code.AppendLine("    mov rax, r11");
            code.AppendLine("    xor rdx, rdx");
            code.AppendLine("    div rcx");
            code.AppendLine("    mov r11, rax");
            code.AppendLine("    jmp jml_print_int_trim");
            code.AppendLine("jml_print_int_digits:");
            code.AppendLine("    test r11, r11");
            code.AppendLine("    jz jml_print_int_done");
            code.AppendLine("    mov rax, r10");
            code.AppendLine("    xor rdx, rdx");
            code.AppendLine("    div r11");
            code.AppendLine("    mov qword ptr [rip + jml_tmp_1], rdx");
            code.AppendLine("    mov qword ptr [rip + jml_tmp_0], r11");
            code.AppendLine("    add rax, 48");
            EmitMoveToArg(code, 0, "rax");
            code.AppendLine("    call " + _target.PutcharSymbol);
            code.AppendLine("    mov r10, qword ptr [rip + jml_tmp_1]");
            code.AppendLine("    mov r11, qword ptr [rip + jml_tmp_0]");
            code.AppendLine("    mov rax, r11");
            code.AppendLine("    xor rdx, rdx");
            code.AppendLine("    mov rcx, 10");
            code.AppendLine("    div rcx");
            code.AppendLine("    mov r11, rax");
            code.AppendLine("    jmp jml_print_int_digits");
            code.AppendLine("jml_print_int_done:");
            EmitExternalCallEpilogue(code);
            code.AppendLine();
        }

        private void EmitPrintValue(StringBuilder code)
        {
            code.AppendLine("jml_print_value:");
            EmitExternalCallPrologue(code);
            code.AppendLine($"    mov r10, {_target.Arg0}");
            code.AppendLine($"    cmp qword ptr [r10 + {ValueTypeOffset}], 1");
            code.AppendLine("    je jml_print_value_int");
            code.AppendLine($"    cmp qword ptr [r10 + {ValueTypeOffset}], 2");
            code.AppendLine("    je jml_print_value_string");
            code.AppendLine($"    cmp qword ptr [r10 + {ValueTypeOffset}], 3");
            code.AppendLine("    je jml_print_value_array");
            EmitLoadAddressToArg(code, 0, "jml_fmt_str");
            EmitLoadAddressToArg(code, 1, "jml_null_str");
            code.AppendLine("    xor eax, eax");
            code.AppendLine("    call " + _target.PrintfSymbol);
            code.AppendLine("    jmp jml_print_value_done");
            code.AppendLine("jml_print_value_int:");
            code.AppendLine($"    mov {_target.Arg0}, qword ptr [r10 + {ValueIntOffset}]");
            EmitCall(code, "jml_print_int");
            code.AppendLine("    jmp jml_print_value_done");
            code.AppendLine("jml_print_value_string:");
            EmitLoadAddressToArg(code, 0, "jml_fmt_str");
            code.AppendLine($"    mov {_target.Arg1}, qword ptr [r10 + {ValuePointerOffset}]");
            code.AppendLine("    xor eax, eax");
            code.AppendLine("    call " + _target.PrintfSymbol);
            code.AppendLine("    jmp jml_print_value_done");
            code.AppendLine("jml_print_value_array:");
            EmitMoveToArg(code, 0, "r10");
            EmitCall(code, "jml_print_array");
            code.AppendLine("jml_print_value_done:");
            EmitExternalCallEpilogue(code);
            code.AppendLine();
        }

        private void EmitPrintArray(StringBuilder code)
        {
            code.AppendLine("jml_print_array:");
            code.AppendLine("    push rbp");
            code.AppendLine("    mov rbp, rsp");
            code.AppendLine("    push rbx");
            code.AppendLine("    push r12");
            code.AppendLine("    push r13");
            code.AppendLine("    push r14");
            if (_target.IsWindows)
                code.AppendLine("    sub rsp, 32");
            code.AppendLine($"    mov rbx, {_target.Arg0}");
            EmitLoadAddressToArg(code, 0, "jml_lbracket_str");
            EmitCall(code, "jml_print_string");
            code.AppendLine($"    mov r12, qword ptr [rbx + {ValueIntOffset}]");
            code.AppendLine($"    mov r13, qword ptr [rbx + {ValuePointerOffset}]");
            code.AppendLine("    xor r14, r14");
            code.AppendLine("jml_print_array_loop:");
            code.AppendLine("    cmp r14, r12");
            code.AppendLine("    jge jml_print_array_done");
            code.AppendLine("    test r14, r14");
            code.AppendLine("    jz jml_print_array_item");
            EmitLoadAddressToArg(code, 0, "jml_comma_space_str");
            EmitCall(code, "jml_print_string");
            code.AppendLine("jml_print_array_item:");
            code.AppendLine("    mov r10, r14");
            code.AppendLine("    shl r10, 5");
            code.AppendLine("    lea " + _target.Arg0 + ", [r13 + r10]");
            EmitCall(code, "jml_print_value");
            code.AppendLine("    inc r14");
            code.AppendLine("    jmp jml_print_array_loop");
            code.AppendLine("jml_print_array_done:");
            EmitLoadAddressToArg(code, 0, "jml_rbracket_str");
            EmitCall(code, "jml_print_string");
            if (_target.IsWindows)
                code.AppendLine("    add rsp, 32");
            code.AppendLine("    pop r14");
            code.AppendLine("    pop r13");
            code.AppendLine("    pop r12");
            code.AppendLine("    pop rbx");
            code.AppendLine("    pop rbp");
            code.AppendLine("    ret");
            code.AppendLine();
        }

        private void EmitReadLine(StringBuilder code)
        {
            code.AppendLine("jml_read_line:");
            EmitReadLinePrologue(code);
            code.AppendLine($"    mov rbx, {_target.Arg0}");
            code.AppendLine($"    mov r12, {_target.Arg1}");
            code.AppendLine("    mov byte ptr [r12], 0");
            EmitLoadAddressToArg(code, 0, "jml_fmt_input");
            EmitMoveToArg(code, 1, "r12");
            code.AppendLine("    xor eax, eax");
            code.AppendLine("    call " + _target.ScanfSymbol);
            code.AppendLine("    cmp eax, 1");
            code.AppendLine("    je jml_read_line_after_scan");
            code.AppendLine("    mov byte ptr [r12], 0");
            code.AppendLine("jml_read_line_after_scan:");
            code.AppendLine("    call " + _target.GetcharSymbol);
            code.AppendLine($"    mov qword ptr [rbx + {ValueTypeOffset}], 2");
            code.AppendLine($"    mov qword ptr [rbx + {ValueIntOffset}], 0");
            code.AppendLine($"    mov qword ptr [rbx + {ValuePointerOffset}], r12");
            EmitReadLineEpilogue(code);
            code.AppendLine();
        }

        private void EmitData(StringBuilder code)
        {
            if (_target.IsMacOS)
                code.AppendLine(".section __DATA,__data");
            else
                code.AppendLine(".data");

            code.AppendLine(".p2align 3");
            EmitByteString(code, "jml_fmt_str", "%s");
            EmitByteString(code, "jml_fmt_int", "%lld");
            EmitByteString(code, "jml_fmt_input", "%4095[^\n]");
            EmitByteString(code, "jml_null_str", "여친");
            EmitByteString(code, "jml_lbracket_str", "[");
            EmitByteString(code, "jml_rbracket_str", "]");
            EmitByteString(code, "jml_comma_space_str", ", ");

            foreach (KeyValuePair<string, string> item in _stringLiterals.OrderBy(item => item.Value))
                EmitByteString(code, item.Value, item.Key);

            code.AppendLine(".p2align 3");
            code.AppendLine("jml_tmp_0:");
            code.AppendLine("    .quad 0");
            code.AppendLine("jml_tmp_1:");
            code.AppendLine("    .quad 0");
            code.AppendLine("jml_addr_tmp:");
            code.AppendLine("    .quad 0");
            code.AppendLine("jml_null_value:");
            code.AppendLine("    .quad 0, 0, 0, 0");
            code.AppendLine("jml_tmp_value:");
            code.AppendLine("    .quad 0, 0, 0, 0");

            foreach (FunctionDefinition function in _functions.Values.OrderBy(function => function.Label))
            {
                code.AppendLine(FunctionReturnBufferPointerLabel(function.Label) + ":");
                code.AppendLine("    .quad 0");
            }

            foreach (ArrayStorage storage in _returnBuffers.Values.OrderBy(storage => storage.Label))
            {
                code.AppendLine(storage.Label + ":");
                code.AppendLine($"    .space {Math.Max(1, storage.Length) * ValueSize}");
            }

            foreach (string label in _variables.Values.Order())
            {
                code.AppendLine(label + ":");
                code.AppendLine("    .quad 0, 0, 0, 0");
            }

            foreach (string label in _inputBuffers.Values.Order())
            {
                code.AppendLine(label + ":");
                code.AppendLine($"    .space {InputBufferSize}");
            }

            foreach (ArrayStorage storage in _arrayStorages.Values.OrderBy(storage => storage.Label))
            {
                code.AppendLine(storage.Label + ":");
                code.AppendLine($"    .space {Math.Max(1, storage.Length) * ValueSize}");
            }
        }

        private void EmitFunctionPrologue(StringBuilder code)
        {
            code.AppendLine("    push rbp");
            code.AppendLine("    mov rbp, rsp");
            code.AppendLine("    push rbx");
            code.AppendLine("    push r12");
            code.AppendLine("    push r13");
            code.AppendLine("    push r14");
            code.AppendLine("    push r15");
            code.AppendLine("    push rsi");

            if (_target.IsWindows)
                code.AppendLine("    sub rsp, 32");
        }

        private void EmitFunctionEpilogue(StringBuilder code)
        {
            if (_target.IsWindows)
                code.AppendLine("    add rsp, 32");

            code.AppendLine("    pop rsi");
            code.AppendLine("    pop r15");
            code.AppendLine("    pop r14");
            code.AppendLine("    pop r13");
            code.AppendLine("    pop r12");
            code.AppendLine("    pop rbx");
            code.AppendLine("    pop rbp");
            code.AppendLine("    ret");
        }

        private void EmitExternalCallPrologue(StringBuilder code)
        {
            code.AppendLine("    push rbp");
            code.AppendLine("    mov rbp, rsp");

            if (_target.IsWindows)
                code.AppendLine("    sub rsp, 32");
        }

        private void EmitExternalCallEpilogue(StringBuilder code)
        {
            if (_target.IsWindows)
                code.AppendLine("    add rsp, 32");

            code.AppendLine("    pop rbp");
            code.AppendLine("    ret");
        }

        private void EmitReadLinePrologue(StringBuilder code)
        {
            code.AppendLine("    push rbp");
            code.AppendLine("    mov rbp, rsp");
            code.AppendLine("    push rbx");
            code.AppendLine("    push r12");

            if (_target.IsWindows)
                code.AppendLine("    sub rsp, 32");
        }

        private void EmitReadLineEpilogue(StringBuilder code)
        {
            if (_target.IsWindows)
                code.AppendLine("    add rsp, 32");

            code.AppendLine("    pop r12");
            code.AppendLine("    pop rbx");
            code.AppendLine("    pop rbp");
            code.AppendLine("    ret");
        }

        private void EmitLoadAddressToArg(StringBuilder code, int argumentIndex, string label, int offset = 0)
        {
            string suffix = offset == 0 ? "" : " + " + offset;
            code.AppendLine($"    lea {Arg(argumentIndex)}, [rip + {label}{suffix}]");
        }

        private void EmitLoadAddressToRegister(StringBuilder code, string register, string label, int offset = 0)
        {
            string suffix = offset == 0 ? "" : " + " + offset;
            code.AppendLine($"    lea {register}, [rip + {label}{suffix}]");
        }

        private void EmitMoveToArg(StringBuilder code, int argumentIndex, string value)
        {
            string arg = Arg(argumentIndex);
            if (arg != value)
                code.AppendLine($"    mov {arg}, {value}");
        }

        private void EmitCall(StringBuilder code, string label)
        {
            code.AppendLine("    call " + label);
        }

        private string Arg(int index)
        {
            return index switch
            {
                0 => _target.Arg0,
                1 => _target.Arg1,
                2 => _target.Arg2,
                _ => throw new ArgumentOutOfRangeException(nameof(index))
            };
        }

        private string VariableLabel(string name)
        {
            return VariableLabel(_currentContext, name);
        }

        private string VariableLabel(string context, string name)
        {
            return VariableLabel(context, name, forceLocal: false);
        }

        private string VariableLabel(string context, string name, bool forceLocal)
        {
            string key = context + ":" + name;
            if (_variables.TryGetValue(key, out string? label))
                return label;

            string globalKey = JMLNativeParser.MainContext + ":" + name;
            if (!forceLocal && context != JMLNativeParser.MainContext && _variables.TryGetValue(globalKey, out label))
                return label;

            label = "jml_var_" + _variables.Count;
            _variables[key] = label;
            return label;
        }

        private string InputBufferLabel(string name)
        {
            string key = _currentContext + ":" + name;
            if (_inputBuffers.TryGetValue(key, out string? label))
                return label;

            label = "jml_input_" + _inputBuffers.Count;
            _inputBuffers[key] = label;
            return label;
        }

        private string StringLabel(string value)
        {
            if (_stringLiterals.TryGetValue(value, out string? label))
                return label;

            label = "jml_str_" + _stringLiterals.Count;
            _stringLiterals[value] = label;
            return label;
        }

        private ArrayStorage ArrayStorageLabel(int lineIndex, int length)
        {
            if (_arrayStorages.TryGetValue(lineIndex, out ArrayStorage? storage))
                return storage;

            storage = new ArrayStorage("jml_arr_" + _arrayStorages.Count, length);
            _arrayStorages[lineIndex] = storage;
            return storage;
        }

        private string ReturnBufferLabel(int lineIndex, int valueCount)
        {
            string key = _currentContext + ":" + lineIndex;
            if (_returnBuffers.TryGetValue(key, out ArrayStorage? storage))
                return storage.Label;

            storage = new ArrayStorage("jml_call_ret_" + _returnBuffers.Count, Math.Max(1, valueCount));
            _returnBuffers[key] = storage;
            return storage.Label;
        }

        private static string FunctionReturnBufferPointerLabel(string context)
        {
            return context + "_return_buffer";
        }

        private static string LineLabel(string context, int lineNumber)
        {
            return $"L_{context}_line_{lineNumber}";
        }

        private bool ShouldEmitLineLabel(string context, int lineNumber)
        {
            return _program.LabelLines.TryGetValue(context, out HashSet<int>? lines) && lines.Contains(lineNumber);
        }

        private bool IsDeadLine(string context, int lineNumber)
        {
            return _program.DeadLines.TryGetValue(context, out HashSet<int>? lines) && lines.Contains(lineNumber);
        }

        private static string ReturnLabel(string context)
        {
            return $"L_{context}_return";
        }

        private bool LooksLikeFunctionCall(string[] tokens)
        {
            if (tokens.Length == 0)
                return false;

            return _functions.ContainsKey(tokens[0]);
        }

        private static string RequireArg(string[] args, int index, int lineIndex)
        {
            if (index >= args.Length || string.IsNullOrEmpty(args[index]))
                throw new ArgumentException($"{lineIndex + 1}번째 줄에 인수가 없잖아;;");

            return args[index];
        }

        private static int ParseLineNumber(string raw, int lineIndex)
        {
            if (!int.TryParse(raw, out int lineNumber))
                throw new ArgumentException($"{lineIndex + 1}번째 줄 번호는 숫자여야지;;");

            return lineNumber;
        }

        private static bool IsStringLiteral(string token)
        {
            return token.StartsWith('\"') && token.EndsWith('\"');
        }

        private static string StringValue(string token)
        {
            return token[1..^1];
        }

        private static bool TryParseNumber(string token, out long value)
        {
            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
            {
                value = checked((long)Math.Round(parsed * NumberScale));
                return true;
            }

            value = 0;
            return false;
        }

        private static bool TryParseArrayAccess(string token, out string arrayName, out int[] arrayIndices)
        {
            arrayName = "";
            arrayIndices = [];
            string[] parts = token.Split('.');
            if (parts.Length < 2)
                return false;

            List<int> indices = [];
            foreach (string part in parts.Skip(1))
            {
                if (!int.TryParse(part, out int arrayIndex))
                    return false;

                indices.Add(arrayIndex);
            }

            arrayName = parts[0];
            arrayIndices = [.. indices];
            return true;
        }

        private static void EmitByteString(StringBuilder code, string label, string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            code.AppendLine(label + ":");

            if (bytes.Length == 0)
            {
                code.AppendLine("    .byte 0");
                return;
            }

            for (int i = 0; i < bytes.Length; i += 16)
            {
                string chunk = string.Join(", ", bytes.Skip(i).Take(16));
                code.AppendLine("    .byte " + chunk);
            }

            code.AppendLine("    .byte 0");
        }

        private sealed record ArrayStorage(string Label, int Length);

        private sealed record FunctionDefinition(
            string Name,
            int DeclarationLine,
            int Start,
            int End,
            string Label,
            string[] Parameters,
            int ReturnCount);
    }

    internal sealed class Arm64AssemblyGenerator : INativeAssemblyGenerator
    {
        private const int ValueTypeOffset = 0;
        private const int ValueIntOffset = 8;
        private const int ValuePointerOffset = 16;
        private const int ValueSize = 32;
        private const int InputBufferSize = 4096;
        private const long NumberScale = 1_000_000;
        private readonly NativeTarget _target;
        private readonly JmlNativeProgram _program;
        private readonly string[] _lines;
        private readonly Dictionary<string, string> _variables = [];
        private readonly Dictionary<string, string> _inputBuffers = [];
        private readonly Dictionary<string, string> _stringLiterals = [];
        private readonly Dictionary<int, ArrayStorage> _arrayStorages = [];
        private readonly Dictionary<string, ArrayStorage> _returnBuffers = [];
        private readonly Dictionary<string, FunctionDefinition> _functions = [];
        private readonly Dictionary<int, int> _functionBlocks = [];
        private string _currentContext = JMLNativeParser.MainContext;

        public Arm64AssemblyGenerator(NativeTarget target, JmlNativeProgram program)
        {
            _target = target;
            _program = program;
            _lines = program.Lines;

            foreach (NativeFunctionDefinition function in program.Functions.Values)
            {
                _functions[function.Name] = new FunctionDefinition(
                    function.Name,
                    function.DeclarationLine,
                    function.Start,
                    function.End,
                    function.Label,
                    function.Parameters,
                    function.ReturnCount);
            }

            foreach (KeyValuePair<int, int> block in program.FunctionBlocks)
                _functionBlocks[block.Key] = block.Value;
        }

        public string Generate()
        {
            StringBuilder code = new();
            EmitHeader(code);
            EmitMain(code);
            EmitFunctions(code);
            EmitRuntime(code);
            EmitData(code);
            EmitFooter(code);
            return code.ToString();
        }

        private void EmitHeader(StringBuilder code)
        {
            code.AppendLine(".extern " + _target.PrintfSymbol);
            code.AppendLine(".extern " + _target.GetcharSymbol);

            if (_target.IsMacOS)
                code.AppendLine(".section __TEXT,__text,regular,pure_instructions");
            else
                code.AppendLine(".text");

            code.AppendLine();
        }

        private void EmitFooter(StringBuilder code)
        {
            if (!_target.IsMacOS && !_target.IsWindows)
            {
                code.AppendLine();
                code.AppendLine(".section .note.GNU-stack,\"\",@progbits");
            }
        }

        private void EmitMain(StringBuilder code)
        {
            code.AppendLine(".globl " + _target.MainSymbol);
            code.AppendLine(_target.MainSymbol + ":");
            EmitFunctionPrologue(code);
            EmitProgramRange(code, 1, _lines.Length, "main", _lines.Length);
            code.AppendLine(ReturnLabel("main") + ":");
            code.AppendLine("    mov w0, #0");
            EmitFunctionEpilogue(code);
            code.AppendLine();
        }

        private void EmitFunctions(StringBuilder code)
        {
            foreach (FunctionDefinition function in _functions.Values)
            {
                code.AppendLine(function.Label + ":");
                EmitFunctionPrologue(code);
                EmitStoreFunctionReturnBufferPointer(code, function.Label);
                EmitProgramRange(code, function.Start, function.End, function.Label, function.End);
                code.AppendLine(ReturnLabel(function.Label) + ":");
                code.AppendLine("    mov w0, #0");
                EmitFunctionEpilogue(code);
                code.AppendLine();
            }
        }

        private void EmitProgramRange(StringBuilder code, int startLine, int endLine, string context, int jumpLimitLine, int? returnAfterLine = null)
        {
            string previousContext = _currentContext;
            _currentContext = context;

            try
            {
                for (int lineNumber = startLine; lineNumber <= endLine; lineNumber++)
                {
                    if (ShouldEmitLineLabel(context, lineNumber))
                        code.AppendLine(LineLabel(context, lineNumber) + ":");

                    if (IsDeadLine(context, lineNumber))
                        continue;

                    if (context == JMLNativeParser.MainContext && _functionBlocks.TryGetValue(lineNumber, out int functionEnd))
                    {
                        lineNumber = functionEnd;
                        continue;
                    }

                    EmitLine(code, lineNumber, context, jumpLimitLine);

                    if (returnAfterLine == lineNumber)
                        code.AppendLine($"    b {ReturnLabel(context)}");
                }
            }
            finally
            {
                _currentContext = previousContext;
            }
        }

        private void EmitLine(StringBuilder code, int lineNumber, string context, int jumpLimitLine)
        {
            if (!_program.Statements.TryGetValue(lineNumber, out JmlStmt? statement))
                return;

            switch (statement)
            {
                case PrintStmt print:
                    EmitPrint(code, print, lineNumber - 1);
                    break;
                case InputStmt input:
                    EmitInput(code, input.Name);
                    break;
                case AssignStmt assign:
                    EmitAssignment(code, assign, lineNumber - 1);
                    break;
                case BranchNotEqualStmt branch:
                    EmitRepeat(code, branch.Args, lineNumber - 1, context, jumpLimitLine);
                    break;
                case FunctionStmt function:
                    EmitFunctionCommand(code, function, lineNumber, lineNumber - 1);
                    break;
                case ReturnStmt ret:
                    EmitReturn(code, ret, context, lineNumber - 1);
                    break;
                default:
                    throw new ArgumentException($"{lineNumber}번째 줄에 모르는 명령어가 있잖아;;");
            }
        }

        private void EmitPrint(StringBuilder code, PrintStmt statement, int lineIndex)
        {
            if (statement.Values.Count == 0)
                throw new ArgumentException($"{lineIndex + 1}번째 줄 안산에 인수가 없잖아;;");

            if (statement.Values.Count == 1 && statement.Values[0] is CallExpr call)
            {
                FunctionDefinition function = GetFunction(call.Name, lineIndex);
                string returnBuffer = ReturnBufferLabel(lineIndex, Math.Max(1, function.ReturnCount));
                EmitFunctionCall(code, call.Name, call.Args, lineIndex, returnBuffer);
                for (int i = 0; i < function.ReturnCount; i++)
                    EmitPrintValueAddress(code, returnBuffer, i * ValueSize);
                return;
            }

            foreach (Expr expr in statement.Values)
                EmitPrintExpr(code, expr);
        }

        private void EmitPrintExpr(StringBuilder code, Expr expr)
        {
            switch (expr)
            {
                case StringExpr text:
                    EmitLoadAddress(code, "x0", StringLabel(text.Value));
                    EmitCall(code, "jml_print_string");
                    return;
                case NumberExpr number:
                    EmitLoadImmediate(code, "x0", number.ScaledValue);
                    EmitCall(code, "jml_print_int");
                    return;
                case NullExpr:
                    EmitPrintValueAddress(code, "jml_null_value");
                    return;
                case ArrayAccessExpr array:
                    EmitLoadArrayElementAddress(code, array.Name, array.Indices.ToArray(), "x11");
                    EmitMove(code, "x0", "x11");
                    EmitCall(code, "jml_print_value");
                    return;
                case VarExpr variable:
                    EmitPrintValueAddress(code, VariableLabel(variable.Name));
                    return;
                case BinaryExpr binary:
                    EmitNumericExpressionToLabel(code, binary, "jml_tmp_value");
                    EmitPrintValueAddress(code, "jml_tmp_value");
                    return;
                default:
                    throw new ArgumentException("출력할 수 없는 표현식이잖아;;");
            }
        }

        private void EmitPrintValueAddress(StringBuilder code, string label, int offset = 0)
        {
            EmitLoadAddress(code, "x0", label, offset);
            EmitCall(code, "jml_print_value");
        }

        private void EmitInput(StringBuilder code, string key)
        {
            EmitLoadAddress(code, "x0", VariableLabel(key));
            EmitLoadAddress(code, "x1", InputBufferLabel(key));
            EmitCall(code, "jml_read_line");
        }

        private void EmitAssignment(StringBuilder code, AssignStmt statement, int lineIndex)
        {
            string[] args = statement.Args;
            string key = RequireArg(args, 1, lineIndex);
            string[] valueTokens = args.Skip(2).ToArray();

            if (key.StartsWith('[') && key.EndsWith(']'))
            {
                EmitArrayAssignment(code, key[1..^1], valueTokens, lineIndex);
                return;
            }

            if (TryParseArrayAccess(key, out string arraySetName, out int[] arraySetIndices))
            {
                if (valueTokens.Length != 1)
                    throw new ArgumentException($"{lineIndex + 1}번째 줄 배열 값은 하나여야지;;");

                if (statement.Values.Count == 1 && statement.Values[0] is BinaryExpr arrayValueExpr)
                {
                    EmitAssignExprToLabel(code, arrayValueExpr, "jml_tmp_value");
                    EmitAssignLabelToArrayElement(code, arraySetName, arraySetIndices, "jml_tmp_value");
                }
                else
                {
                    EmitAssignTokenToArrayElement(code, arraySetName, arraySetIndices, valueTokens[0]);
                }
                return;
            }

            if (LooksLikeFunctionCall(valueTokens))
            {
                CallExpr call = (CallExpr)statement.Values[0];
                FunctionDefinition function = GetFunction(call.Name, lineIndex);
                if (function.ReturnCount != 1)
                    throw new ArgumentException("변수에는 값 하나만 넣어야지;;");

                EmitFunctionCall(code, call.Name, call.Args, lineIndex, VariableLabel(key));
                return;
            }

            string data = RequireArg(args, 2, lineIndex);
            if (valueTokens.Length != 1)
                throw new ArgumentException("변수에 값은 하나만 줘야지;;");

            if (Utils.IsExpression(data))
            {
                EmitExpressionAssignment(code, key, data);
                return;
            }

            if (statement.Values.Count == 1 && statement.Values[0] is BinaryExpr binary)
            {
                EmitNumericExpressionToLabel(code, binary, VariableLabel(key));
                return;
            }

            EmitAssignTokenToLabel(code, data, VariableLabel(key));
        }

        private void EmitArrayAssignment(StringBuilder code, string key, string[] rawValues, int lineIndex)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException($"{lineIndex + 1}번째 줄 배열 이름이 비었잖아;;");

            ArrayStorage storage = ArrayStorageLabel(lineIndex, rawValues.Length);

            for (int i = 0; i < rawValues.Length; i++)
            {
                EmitAssignTokenToLabel(code, rawValues[i], storage.Label, i * ValueSize);
            }

            EmitLoadAddress(code, "x0", VariableLabel(key));
            EmitLoadAddress(code, "x1", storage.Label);
            EmitLoadImmediate(code, "x2", rawValues.Length);
            EmitCall(code, "jml_set_array");
        }

        private void EmitExpressionAssignment(StringBuilder code, string key, string expression)
        {
            string op = expression[..1];
            string operand = expression[1..];

            if (TryParseNumber(operand, out long constantOperand))
            {
                if ((op is "+" or "-" && constantOperand == 0) ||
                    (op is "*" or "/" or "^" && constantOperand == NumberScale))
                {
                    return;
                }

                if (op == "*" && constantOperand == 0)
                {
                    EmitAssignTokenToLabel(code, "0", VariableLabel(key));
                    return;
                }

                if (op == "^" && constantOperand == 0)
                {
                    EmitAssignTokenToLabel(code, "1", VariableLabel(key));
                    return;
                }
            }

            EmitLoadInt(code, key, "x10");
            EmitStoreGlobal(code, "x10", "jml_tmp_0");
            EmitLoadInt(code, operand, "x11");
            EmitLoadGlobal(code, "x10", "jml_tmp_0");

            switch (op)
            {
                case "+":
                    code.AppendLine("    add x11, x10, x11");
                    break;
                case "-":
                    code.AppendLine("    sub x11, x10, x11");
                    break;
                case "*":
                    code.AppendLine("    mul x11, x10, x11");
                    EmitLoadImmediate(code, "x12", NumberScale);
                    code.AppendLine("    sdiv x11, x11, x12");
                    break;
                case "/":
                    EmitLoadImmediate(code, "x12", NumberScale);
                    code.AppendLine("    mul x10, x10, x12");
                    code.AppendLine("    sdiv x11, x10, x11");
                    break;
                case "^":
                    EmitMove(code, "x0", "x10");
                    EmitMove(code, "x1", "x11");
                    EmitCall(code, "jml_number_pow");
                    EmitMove(code, "x11", "x0");
                    break;
                default:
                    throw new ArgumentException("이런 수식은 안산에도 없어;; " + op);
            }

            EmitLoadAddress(code, "x0", VariableLabel(key));
            EmitMove(code, "x1", "x11");
            EmitCall(code, "jml_set_int");
        }

        private void EmitNumericExpressionToLabel(StringBuilder code, Expr expr, string destinationLabel)
        {
            ExpressionPlan plan = ExpressionPlanner.Build(expr, ["x19", "x20", "x21", "x22", "x23", "x24", "x25"]);
            foreach (ExprIrInstr instruction in plan.Instructions)
            {
                switch (instruction)
                {
                    case LoadExprIrInstr load:
                        EmitLoadExprToRegister(code, load.Source, plan.Registers[load.Dest]);
                        break;
                    case BinaryExprIrInstr binary:
                        EmitBinaryExprInstruction(code, binary, plan.Registers);
                        break;
                }
            }

            EmitLoadAddress(code, "x0", destinationLabel);
            EmitMove(code, "x1", plan.Registers[plan.Result]);
            EmitCall(code, "jml_set_int");
        }

        private void EmitLoadExprToRegister(StringBuilder code, Expr expr, string register)
        {
            switch (expr)
            {
                case NumberExpr number:
                    EmitLoadImmediate(code, register, number.ScaledValue);
                    return;
                case VarExpr variable:
                    EmitLoadInt(code, variable.Name, register);
                    return;
                case ArrayAccessExpr array:
                    EmitLoadInt(code, ArrayAccessToken(array), register);
                    return;
                default:
                    throw new ArgumentException("숫자 표현식에 쓸 수 없는 값이잖아;;");
            }
        }

        private void EmitBinaryExprInstruction(StringBuilder code, BinaryExprIrInstr instruction, IReadOnlyDictionary<VReg, string> registers)
        {
            string destination = registers[instruction.Dest];
            string left = registers[instruction.Left];
            string right = registers[instruction.Right];

            if (destination != left)
                EmitMove(code, destination, left);

            switch (instruction.Op)
            {
                case "+":
                    code.AppendLine($"    add {destination}, {destination}, {right}");
                    return;
                case "-":
                    code.AppendLine($"    sub {destination}, {destination}, {right}");
                    return;
                case "*":
                    code.AppendLine($"    mul {destination}, {destination}, {right}");
                    EmitLoadImmediate(code, "x16", NumberScale);
                    code.AppendLine($"    sdiv {destination}, {destination}, x16");
                    return;
                case "/":
                    EmitLoadImmediate(code, "x16", NumberScale);
                    code.AppendLine($"    mul {destination}, {destination}, x16");
                    code.AppendLine($"    sdiv {destination}, {destination}, {right}");
                    return;
                case "^":
                    PreserveExpressionRegistersForCall(code, registers.Values.Distinct(), () =>
                    {
                        EmitMove(code, "x0", left);
                        EmitMove(code, "x1", right);
                        EmitCall(code, "jml_number_pow");
                        EmitStoreGlobal(code, "x0", "jml_tmp_0");
                    });
                    EmitLoadGlobal(code, destination, "jml_tmp_0");
                    return;
                default:
                    throw new ArgumentException("이런 수식은 안산에도 없어;; " + instruction.Op);
            }
        }

        private void PreserveExpressionRegistersForCall(StringBuilder code, IEnumerable<string> registers, Action emitCall)
        {
            string[] savedRegisters = registers.Order().ToArray();
            foreach (string register in savedRegisters)
                code.AppendLine($"    str {register}, [sp, #-16]!");

            emitCall();

            foreach (string register in savedRegisters.Reverse())
                code.AppendLine($"    ldr {register}, [sp], #16");
        }

        private static string ArrayAccessToken(ArrayAccessExpr array)
        {
            return array.Name + "." + string.Join(".", array.Indices);
        }

        private void EmitRepeat(StringBuilder code, string[] args, int lineIndex, string context, int jumpLimitLine)
        {
            string rawVal1 = RequireArg(args, 1, lineIndex);
            string rawVal2 = RequireArg(args, 2, lineIndex);
            int goTo = ParseLineNumber(RequireArg(args, 3, lineIndex), lineIndex);

            if (goTo <= 0 || goTo > jumpLimitLine)
                throw new ArgumentException($"{lineIndex + 1}번째 줄 이동할 줄 번호가 이상하잖아;;");

            if (TryParseNumber(rawVal1, out long constantLeft) && TryParseNumber(rawVal2, out long constantRight))
            {
                if (constantLeft != constantRight)
                    code.AppendLine($"    b {LineLabel(context, goTo)}");

                return;
            }

            EmitLoadInt(code, rawVal1, "x10");
            EmitStoreGlobal(code, "x10", "jml_tmp_0");
            EmitLoadInt(code, rawVal2, "x11");
            EmitLoadGlobal(code, "x10", "jml_tmp_0");
            code.AppendLine("    cmp x10, x11");
            code.AppendLine($"    b.ne {LineLabel(context, goTo)}");
        }

        private void EmitFunctionCommand(StringBuilder code, FunctionStmt statement, int lineNumber, int lineIndex)
        {
            string functionName = statement.Name;
            if (_functions.TryGetValue(functionName, out FunctionDefinition? definition) &&
                definition.DeclarationLine == lineNumber)
            {
                return;
            }

            FunctionDefinition function = GetFunction(functionName, lineIndex);
            string returnBuffer = ReturnBufferLabel(lineIndex, Math.Max(1, function.ReturnCount));
            EmitFunctionCall(code, functionName, statement.Arguments, lineIndex, returnBuffer);
        }

        private void EmitReturn(StringBuilder code, ReturnStmt statement, string context, int lineIndex)
        {
            if (context == "main")
                throw new InvalidOperationException("여기서 음... 쓰면 어떡해;;");

            string[] tokens = statement.Args.Skip(1).ToArray();

            if (LooksLikeFunctionCall(tokens))
            {
                CallExpr call = (CallExpr)statement.Values[0];
                EmitFunctionCallToCurrentReturnBuffer(code, call.Name, call.Args, lineIndex, context);
            }
            else
            {
                for (int i = 0; i < statement.Values.Count; i++)
                {
                    EmitAssignExprToLabel(code, statement.Values[i], "jml_tmp_value");
                    EmitCopyValueToCurrentReturnBuffer(code, "jml_tmp_value", i, context);
                }
            }

            code.AppendLine($"    b {ReturnLabel(context)}");
        }

        private FunctionDefinition EmitFunctionCall(StringBuilder code, string functionName, IReadOnlyList<Expr> args, int lineIndex, string returnBufferLabel)
        {
            FunctionDefinition function = PrepareFunctionCallArguments(code, functionName, args, lineIndex);
            EmitLoadAddress(code, "x0", returnBufferLabel);
            EmitCall(code, function.Label);
            return function;
        }

        private FunctionDefinition EmitFunctionCallToCurrentReturnBuffer(StringBuilder code, string functionName, IReadOnlyList<Expr> args, int lineIndex, string context)
        {
            FunctionDefinition function = PrepareFunctionCallArguments(code, functionName, args, lineIndex);
            EmitLoadGlobal(code, "x0", FunctionReturnBufferPointerLabel(context));
            EmitCall(code, function.Label);
            return function;
        }

        private FunctionDefinition PrepareFunctionCallArguments(StringBuilder code, string functionName, IReadOnlyList<Expr> args, int lineIndex)
        {
            FunctionDefinition function = GetFunction(functionName, lineIndex);

            if (function.Parameters.Length != args.Count)
                throw new ArgumentException(functionName + " 함수 인수 개수가 안맞잖아;;");

            for (int i = 0; i < args.Count; i++)
                EmitAssignExprToLabel(code, args[i], VariableLabel(function.Label, function.Parameters[i], forceLocal: true));

            return function;
        }

        private FunctionDefinition GetFunction(string functionName, int lineIndex)
        {
            if (!_functions.TryGetValue(functionName, out FunctionDefinition? function))
                throw new ArgumentException($"{lineIndex + 1}번째 줄 {functionName} 함수가 정의가 안됐잖아;;");

            return function;
        }

        private void EmitLoadInt(StringBuilder code, string token, string targetRegister)
        {
            if (TryParseNumber(token, out long number))
            {
                EmitLoadImmediate(code, targetRegister, number);
                return;
            }

            if (TryParseArrayAccess(token, out string arrayName, out int[] arrayIndices))
            {
                EmitLoadArrayElementAddress(code, arrayName, arrayIndices, "x11");
                EmitMove(code, "x0", "x11");
                EmitCall(code, "jml_value_to_int");

                if (targetRegister != "x0")
                    EmitMove(code, targetRegister, "x0");
                return;
            }

            EmitLoadAddress(code, "x0", VariableLabel(token));
            EmitCall(code, "jml_value_to_int");

            if (targetRegister != "x0")
                EmitMove(code, targetRegister, "x0");
        }

        private void EmitLoadArrayElementAddress(StringBuilder code, string arrayName, int[] arrayIndices, string targetRegister)
        {
            EmitLoadAddress(code, "x0", VariableLabel(arrayName));
            foreach (int arrayIndex in arrayIndices)
            {
                EmitLoadImmediate(code, "x1", arrayIndex);
                EmitCall(code, "jml_array_get");
            }

            if (targetRegister != "x0")
                EmitMove(code, targetRegister, "x0");
        }

        private void EmitAssignTokenToArrayElement(StringBuilder code, string arrayName, int[] arrayIndices, string token)
        {
            if (arrayIndices.Length == 0)
                throw new ArgumentException("배열 접근이 이상하잖아;;");

            EmitAssignTokenToLabel(code, token, "jml_tmp_value");
            EmitAssignLabelToArrayElement(code, arrayName, arrayIndices, "jml_tmp_value");
        }

        private void EmitAssignLabelToArrayElement(StringBuilder code, string arrayName, int[] arrayIndices, string sourceLabel)
        {
            EmitLoadAddress(code, "x0", VariableLabel(arrayName));
            for (int i = 0; i < arrayIndices.Length - 1; i++)
            {
                EmitLoadImmediate(code, "x1", arrayIndices[i]);
                EmitCall(code, "jml_array_get");
            }

            EmitLoadImmediate(code, "x1", arrayIndices[^1]);
            EmitLoadAddress(code, "x2", sourceLabel);
            EmitCall(code, "jml_array_set");
        }

        private void EmitAssignExprToLabel(StringBuilder code, Expr expr, string destinationLabel)
        {
            switch (expr)
            {
                case NullExpr:
                    EmitLoadAddress(code, "x0", destinationLabel);
                    EmitCall(code, "jml_set_null");
                    return;
                case StringExpr text:
                    EmitLoadAddress(code, "x0", destinationLabel);
                    EmitLoadAddress(code, "x1", StringLabel(text.Value));
                    EmitCall(code, "jml_set_string");
                    return;
                case NumberExpr number:
                    EmitLoadAddress(code, "x0", destinationLabel);
                    EmitLoadImmediate(code, "x1", number.ScaledValue);
                    EmitCall(code, "jml_set_int");
                    return;
                case VarExpr variable:
                    EmitCopyValue(code, destinationLabel, VariableLabel(variable.Name));
                    return;
                case ArrayAccessExpr array:
                    EmitLoadArrayElementAddress(code, array.Name, array.Indices.ToArray(), "x11");
                    EmitLoadAddress(code, "x0", destinationLabel);
                    EmitMove(code, "x1", "x11");
                    EmitCall(code, "jml_copy_value");
                    return;
                case BinaryExpr binary:
                    EmitNumericExpressionToLabel(code, binary, destinationLabel);
                    return;
                default:
                    throw new ArgumentException("대입할 수 없는 표현식이잖아;;");
            }
        }

        private void EmitAssignTokenToLabel(StringBuilder code, string token, string destinationLabel, int destinationOffset = 0)
        {
            EmitLoadAddress(code, "x10", destinationLabel, destinationOffset);
            EmitStoreGlobal(code, "x10", "jml_addr_tmp");

            if (token == "여친")
            {
                EmitLoadGlobal(code, "x0", "jml_addr_tmp");
                EmitCall(code, "jml_set_null");
                return;
            }

            if (IsStringLiteral(token))
            {
                EmitLoadGlobal(code, "x0", "jml_addr_tmp");
                EmitLoadAddress(code, "x1", StringLabel(StringValue(token)));
                EmitCall(code, "jml_set_string");
                return;
            }

            if (TryParseNumber(token, out long number))
            {
                EmitLoadGlobal(code, "x0", "jml_addr_tmp");
                EmitLoadImmediate(code, "x1", number);
                EmitCall(code, "jml_set_int");
                return;
            }

            if (TryParseArrayAccess(token, out string arrayName, out int[] arrayIndices))
            {
                EmitLoadArrayElementAddress(code, arrayName, arrayIndices, "x11");
                EmitLoadGlobal(code, "x0", "jml_addr_tmp");
                EmitMove(code, "x1", "x11");
                EmitCall(code, "jml_copy_value");
                return;
            }

            EmitLoadGlobal(code, "x0", "jml_addr_tmp");
            EmitLoadAddress(code, "x1", VariableLabel(token));
            EmitCall(code, "jml_copy_value");
        }

        private void EmitCopyValue(StringBuilder code, string destinationLabel, string sourceLabel)
        {
            EmitLoadAddress(code, "x0", destinationLabel);
            EmitLoadAddress(code, "x1", sourceLabel);
            EmitCall(code, "jml_copy_value");
        }

        private void EmitStoreFunctionReturnBufferPointer(StringBuilder code, string context)
        {
            EmitStoreGlobal(code, "x0", FunctionReturnBufferPointerLabel(context));
        }

        private void EmitCopyValueToCurrentReturnBuffer(StringBuilder code, string sourceLabel, int returnIndex, string context)
        {
            EmitLoadGlobal(code, "x10", FunctionReturnBufferPointerLabel(context));
            if (returnIndex > 0)
            {
                EmitLoadImmediate(code, "x11", returnIndex * ValueSize);
                code.AppendLine("    add x10, x10, x11");
            }

            EmitMove(code, "x0", "x10");
            EmitLoadAddress(code, "x1", sourceLabel);
            EmitCall(code, "jml_copy_value");
        }

        private void EmitRuntime(StringBuilder code)
        {
            EmitSetNull(code);
            EmitSetInt(code);
            EmitSetString(code);
            EmitCopyValue(code);
            EmitValueToInt(code);

            if (_program.RuntimeFeatures.HasFlag(NativeRuntimeFeatures.Array))
            {
                EmitSetArray(code);
                EmitArrayGet(code);
                EmitArraySet(code);
            }

            if (_program.RuntimeFeatures.HasFlag(NativeRuntimeFeatures.Pow))
                EmitNumberPow(code);

            if (_program.RuntimeFeatures.HasFlag(NativeRuntimeFeatures.Print))
            {
                EmitPrintString(code);
                EmitPrintInt(code);
                EmitPrintValue(code);
                EmitPrintArray(code);
            }

            if (_program.RuntimeFeatures.HasFlag(NativeRuntimeFeatures.Input))
                EmitReadLine(code);
        }

        private void EmitSetNull(StringBuilder code)
        {
            code.AppendLine("jml_set_null:");
            code.AppendLine($"    str xzr, [x0, #{ValueTypeOffset}]");
            code.AppendLine($"    str xzr, [x0, #{ValueIntOffset}]");
            code.AppendLine($"    str xzr, [x0, #{ValuePointerOffset}]");
            code.AppendLine("    ret");
            code.AppendLine();
        }

        private void EmitSetInt(StringBuilder code)
        {
            code.AppendLine("jml_set_int:");
            code.AppendLine("    mov x9, #1");
            code.AppendLine($"    str x9, [x0, #{ValueTypeOffset}]");
            code.AppendLine($"    str x1, [x0, #{ValueIntOffset}]");
            code.AppendLine($"    str xzr, [x0, #{ValuePointerOffset}]");
            code.AppendLine("    ret");
            code.AppendLine();
        }

        private void EmitSetString(StringBuilder code)
        {
            code.AppendLine("jml_set_string:");
            code.AppendLine("    mov x9, #2");
            code.AppendLine($"    str x9, [x0, #{ValueTypeOffset}]");
            code.AppendLine($"    str xzr, [x0, #{ValueIntOffset}]");
            code.AppendLine($"    str x1, [x0, #{ValuePointerOffset}]");
            code.AppendLine("    ret");
            code.AppendLine();
        }

        private void EmitCopyValue(StringBuilder code)
        {
            code.AppendLine("jml_copy_value:");
            code.AppendLine("    ldp x9, x10, [x1, #0]");
            code.AppendLine("    stp x9, x10, [x0, #0]");
            code.AppendLine("    ldp x9, x10, [x1, #16]");
            code.AppendLine("    stp x9, x10, [x0, #16]");
            code.AppendLine("    ret");
            code.AppendLine();
        }

        private void EmitSetArray(StringBuilder code)
        {
            code.AppendLine("jml_set_array:");
            code.AppendLine("    mov x9, #3");
            code.AppendLine($"    str x9, [x0, #{ValueTypeOffset}]");
            code.AppendLine($"    str x2, [x0, #{ValueIntOffset}]");
            code.AppendLine($"    str x1, [x0, #{ValuePointerOffset}]");
            code.AppendLine("    ret");
            code.AppendLine();
        }

        private void EmitArrayGet(StringBuilder code)
        {
            code.AppendLine("jml_array_get:");
            EmitLoadAddress(code, "x12", "jml_null_value");
            code.AppendLine($"    ldr x9, [x0, #{ValueTypeOffset}]");
            code.AppendLine("    cmp x9, #3");
            code.AppendLine("    b.ne jml_array_get_done");
            code.AppendLine("    cmp x1, #0");
            code.AppendLine("    b.lt jml_array_get_done");
            code.AppendLine($"    ldr x9, [x0, #{ValueIntOffset}]");
            code.AppendLine("    cmp x1, x9");
            code.AppendLine("    b.ge jml_array_get_done");
            code.AppendLine($"    ldr x9, [x0, #{ValuePointerOffset}]");
            code.AppendLine("    lsl x10, x1, #5");
            code.AppendLine("    add x12, x9, x10");
            code.AppendLine("jml_array_get_done:");
            code.AppendLine("    mov x0, x12");
            code.AppendLine("    ret");
            code.AppendLine();
        }

        private void EmitArraySet(StringBuilder code)
        {
            code.AppendLine("jml_array_set:");
            code.AppendLine($"    ldr x9, [x0, #{ValueTypeOffset}]");
            code.AppendLine("    cmp x9, #3");
            code.AppendLine("    b.ne jml_array_set_done");
            code.AppendLine("    cmp x1, #0");
            code.AppendLine("    b.lt jml_array_set_done");
            code.AppendLine($"    ldr x9, [x0, #{ValueIntOffset}]");
            code.AppendLine("    cmp x1, x9");
            code.AppendLine("    b.ge jml_array_set_done");
            code.AppendLine($"    ldr x9, [x0, #{ValuePointerOffset}]");
            code.AppendLine("    lsl x10, x1, #5");
            code.AppendLine("    add x9, x9, x10");
            code.AppendLine("    ldp x10, x11, [x2, #0]");
            code.AppendLine("    stp x10, x11, [x9, #0]");
            code.AppendLine("    ldp x10, x11, [x2, #16]");
            code.AppendLine("    stp x10, x11, [x9, #16]");
            code.AppendLine("jml_array_set_done:");
            code.AppendLine("    ret");
            code.AppendLine();
        }

        private void EmitValueToInt(StringBuilder code)
        {
            code.AppendLine("jml_value_to_int:");
            code.AppendLine($"    ldr x9, [x0, #{ValueTypeOffset}]");
            code.AppendLine("    cmp x9, #1");
            code.AppendLine("    b.eq jml_value_to_int_number");
            code.AppendLine("    cmp x9, #2");
            code.AppendLine("    b.eq jml_value_to_int_string");
            code.AppendLine("    mov x0, #0");
            code.AppendLine("    ret");
            code.AppendLine("jml_value_to_int_number:");
            code.AppendLine($"    ldr x0, [x0, #{ValueIntOffset}]");
            code.AppendLine("    ret");
            code.AppendLine("jml_value_to_int_string:");
            code.AppendLine($"    ldr x1, [x0, #{ValuePointerOffset}]");
            code.AppendLine("    mov x0, #0");
            code.AppendLine("    mov x8, #0");
            EmitLoadImmediate(code, "x12", NumberScale);
            code.AppendLine("    ldrb w2, [x1]");
            code.AppendLine("    cmp w2, #45");
            code.AppendLine("    b.ne jml_value_to_int_loop");
            code.AppendLine("    mov x8, #1");
            code.AppendLine("    add x1, x1, #1");
            code.AppendLine("jml_value_to_int_loop:");
            code.AppendLine("    ldrb w2, [x1]");
            code.AppendLine("    cmp w2, #46");
            code.AppendLine("    b.eq jml_value_to_int_fraction_start");
            code.AppendLine("    cmp w2, #48");
            code.AppendLine("    b.lt jml_value_to_int_done");
            code.AppendLine("    cmp w2, #57");
            code.AppendLine("    b.gt jml_value_to_int_done");
            code.AppendLine("    mov x3, #10");
            code.AppendLine("    mul x0, x0, x3");
            code.AppendLine("    sub w2, w2, #48");
            code.AppendLine("    mul x2, x2, x12");
            code.AppendLine("    add x0, x0, x2");
            code.AppendLine("    add x1, x1, #1");
            code.AppendLine("    b jml_value_to_int_loop");
            code.AppendLine("jml_value_to_int_fraction_start:");
            code.AppendLine("    add x1, x1, #1");
            EmitLoadImmediate(code, "x12", NumberScale / 10);
            code.AppendLine("jml_value_to_int_fraction:");
            code.AppendLine("    cbz x12, jml_value_to_int_done");
            code.AppendLine("    ldrb w2, [x1]");
            code.AppendLine("    cmp w2, #48");
            code.AppendLine("    b.lt jml_value_to_int_done");
            code.AppendLine("    cmp w2, #57");
            code.AppendLine("    b.gt jml_value_to_int_done");
            code.AppendLine("    sub w2, w2, #48");
            code.AppendLine("    mul x2, x2, x12");
            code.AppendLine("    add x0, x0, x2");
            code.AppendLine("    mov x3, #10");
            code.AppendLine("    sdiv x12, x12, x3");
            code.AppendLine("    add x1, x1, #1");
            code.AppendLine("    b jml_value_to_int_fraction");
            code.AppendLine("jml_value_to_int_done:");
            code.AppendLine("    cbz x8, jml_value_to_int_ret");
            code.AppendLine("    neg x0, x0");
            code.AppendLine("jml_value_to_int_ret:");
            code.AppendLine("    ret");
            code.AppendLine();
        }

        private void EmitNumberPow(StringBuilder code)
        {
            code.AppendLine("jml_number_pow:");
            code.AppendLine("    mov x10, x0");
            code.AppendLine("    mov x11, x1");
            code.AppendLine("    mov x8, #0");
            code.AppendLine("    cmp x11, #0");
            code.AppendLine("    b.ge jml_number_pow_exp_positive");
            code.AppendLine("    neg x11, x11");
            code.AppendLine("    mov x8, #1");
            code.AppendLine("jml_number_pow_exp_positive:");
            EmitLoadImmediate(code, "x12", NumberScale);
            code.AppendLine("    sdiv x11, x11, x12");
            EmitLoadImmediate(code, "x0", NumberScale);
            code.AppendLine("jml_number_pow_loop:");
            code.AppendLine("    cbz x11, jml_number_pow_done");
            code.AppendLine("    tbz x11, #0, jml_number_pow_square");
            code.AppendLine("    mul x0, x0, x10");
            EmitLoadImmediate(code, "x12", NumberScale);
            code.AppendLine("    sdiv x0, x0, x12");
            code.AppendLine("jml_number_pow_square:");
            code.AppendLine("    mul x10, x10, x10");
            EmitLoadImmediate(code, "x12", NumberScale);
            code.AppendLine("    sdiv x10, x10, x12");
            code.AppendLine("    lsr x11, x11, #1");
            code.AppendLine("    b jml_number_pow_loop");
            code.AppendLine("jml_number_pow_done:");
            code.AppendLine("    cbz x8, jml_number_pow_ret");
            EmitLoadImmediate(code, "x12", NumberScale);
            code.AppendLine("    mul x12, x12, x12");
            code.AppendLine("    sdiv x0, x12, x0");
            code.AppendLine("jml_number_pow_ret:");
            code.AppendLine("    ret");
            code.AppendLine();
        }

        private void EmitPrintString(StringBuilder code)
        {
            code.AppendLine("jml_print_string:");
            EmitFunctionPrologue(code);
            code.AppendLine("    mov x9, x0");
            EmitPrintfWithOneArg(code, "jml_fmt_str", "x9");
            EmitFunctionEpilogue(code);
            code.AppendLine();
        }

        private void EmitPrintInt(StringBuilder code)
        {
            code.AppendLine("jml_print_int:");
            EmitFunctionPrologue(code);
            code.AppendLine("    mov x9, x0");
            code.AppendLine("    cmp x9, #0");
            code.AppendLine("    b.ge jml_print_int_positive");
            EmitStoreGlobal(code, "x9", "jml_tmp_1");
            code.AppendLine("    mov w0, #45");
            code.AppendLine("    bl " + _target.PutcharSymbol);
            EmitLoadGlobal(code, "x9", "jml_tmp_1");
            code.AppendLine("    neg x9, x9");
            code.AppendLine("jml_print_int_positive:");
            EmitLoadImmediate(code, "x10", NumberScale);
            code.AppendLine("    sdiv x11, x9, x10");
            code.AppendLine("    msub x12, x11, x10, x9");
            EmitStoreGlobal(code, "x12", "jml_tmp_1");
            EmitPrintfWithOneArg(code, "jml_fmt_int", "x11");
            EmitLoadGlobal(code, "x10", "jml_tmp_1");
            code.AppendLine("    cbz x10, jml_print_int_done");
            EmitStoreGlobal(code, "x10", "jml_tmp_1");
            code.AppendLine("    mov w0, #46");
            code.AppendLine("    bl " + _target.PutcharSymbol);
            EmitLoadGlobal(code, "x10", "jml_tmp_1");
            EmitLoadImmediate(code, "x11", NumberScale / 10);
            code.AppendLine("jml_print_int_trim:");
            code.AppendLine("    mov x12, #10");
            code.AppendLine("    sdiv x13, x10, x12");
            code.AppendLine("    msub x14, x13, x12, x10");
            code.AppendLine("    cbnz x14, jml_print_int_digits");
            code.AppendLine("    mov x10, x13");
            code.AppendLine("    sdiv x11, x11, x12");
            code.AppendLine("    b jml_print_int_trim");
            code.AppendLine("jml_print_int_digits:");
            code.AppendLine("    cbz x11, jml_print_int_done");
            code.AppendLine("    sdiv x12, x10, x11");
            code.AppendLine("    msub x10, x12, x11, x10");
            code.AppendLine("    add x12, x12, #48");
            EmitStoreGlobal(code, "x10", "jml_tmp_1");
            EmitStoreGlobal(code, "x11", "jml_tmp_0");
            code.AppendLine("    mov w0, w12");
            code.AppendLine("    bl " + _target.PutcharSymbol);
            EmitLoadGlobal(code, "x10", "jml_tmp_1");
            EmitLoadGlobal(code, "x11", "jml_tmp_0");
            code.AppendLine("    mov x12, #10");
            code.AppendLine("    sdiv x11, x11, x12");
            code.AppendLine("    b jml_print_int_digits");
            code.AppendLine("jml_print_int_done:");
            EmitFunctionEpilogue(code);
            code.AppendLine();
        }

        private void EmitPrintValue(StringBuilder code)
        {
            code.AppendLine("jml_print_value:");
            EmitFunctionPrologue(code);
            code.AppendLine("    mov x10, x0");
            code.AppendLine($"    ldr x9, [x10, #{ValueTypeOffset}]");
            code.AppendLine("    cmp x9, #1");
            code.AppendLine("    b.eq jml_print_value_int");
            code.AppendLine("    cmp x9, #2");
            code.AppendLine("    b.eq jml_print_value_string");
            code.AppendLine("    cmp x9, #3");
            code.AppendLine("    b.eq jml_print_value_array");
            EmitLoadAddress(code, "x9", "jml_null_str");
            EmitPrintfWithOneArg(code, "jml_fmt_str", "x9");
            code.AppendLine("    b jml_print_value_done");
            code.AppendLine("jml_print_value_int:");
            code.AppendLine($"    ldr x9, [x10, #{ValueIntOffset}]");
            EmitMove(code, "x0", "x9");
            EmitCall(code, "jml_print_int");
            code.AppendLine("    b jml_print_value_done");
            code.AppendLine("jml_print_value_string:");
            code.AppendLine($"    ldr x9, [x10, #{ValuePointerOffset}]");
            EmitPrintfWithOneArg(code, "jml_fmt_str", "x9");
            code.AppendLine("    b jml_print_value_done");
            code.AppendLine("jml_print_value_array:");
            EmitMove(code, "x0", "x10");
            EmitCall(code, "jml_print_array");
            code.AppendLine("jml_print_value_done:");
            EmitFunctionEpilogue(code);
            code.AppendLine();
        }

        private void EmitPrintArray(StringBuilder code)
        {
            code.AppendLine("jml_print_array:");
            code.AppendLine("    stp x29, x30, [sp, #-16]!");
            code.AppendLine("    mov x29, sp");
            code.AppendLine("    stp x19, x20, [sp, #-16]!");
            code.AppendLine("    stp x21, x22, [sp, #-16]!");
            code.AppendLine("    mov x19, x0");
            EmitLoadAddress(code, "x0", "jml_lbracket_str");
            EmitCall(code, "jml_print_string");
            code.AppendLine($"    ldr x20, [x19, #{ValueIntOffset}]");
            code.AppendLine($"    ldr x21, [x19, #{ValuePointerOffset}]");
            code.AppendLine("    mov x22, #0");
            code.AppendLine("jml_print_array_loop:");
            code.AppendLine("    cmp x22, x20");
            code.AppendLine("    b.ge jml_print_array_done");
            code.AppendLine("    cbz x22, jml_print_array_item");
            EmitLoadAddress(code, "x0", "jml_comma_space_str");
            EmitCall(code, "jml_print_string");
            code.AppendLine("jml_print_array_item:");
            code.AppendLine("    lsl x9, x22, #5");
            code.AppendLine("    add x0, x21, x9");
            EmitCall(code, "jml_print_value");
            code.AppendLine("    add x22, x22, #1");
            code.AppendLine("    b jml_print_array_loop");
            code.AppendLine("jml_print_array_done:");
            EmitLoadAddress(code, "x0", "jml_rbracket_str");
            EmitCall(code, "jml_print_string");
            code.AppendLine("    ldp x21, x22, [sp], #16");
            code.AppendLine("    ldp x19, x20, [sp], #16");
            code.AppendLine("    ldp x29, x30, [sp], #16");
            code.AppendLine("    ret");
            code.AppendLine();
        }

        private void EmitReadLine(StringBuilder code)
        {
            code.AppendLine("jml_read_line:");
            EmitReadLinePrologue(code);
            code.AppendLine("    mov x19, x0");
            code.AppendLine("    mov x20, x1");
            code.AppendLine("    mov x21, #0");
            code.AppendLine("jml_read_line_loop:");
            code.AppendLine("    bl " + _target.GetcharSymbol);
            code.AppendLine("    cmn w0, #1");
            code.AppendLine("    b.eq jml_read_line_done");
            code.AppendLine("    cmp w0, #10");
            code.AppendLine("    b.eq jml_read_line_done");
            code.AppendLine("    cmp w0, #13");
            code.AppendLine("    b.eq jml_read_line_done");
            code.AppendLine("    cmp x21, #4095");
            code.AppendLine("    b.ge jml_read_line_loop");
            code.AppendLine("    strb w0, [x20, x21]");
            code.AppendLine("    add x21, x21, #1");
            code.AppendLine("    b jml_read_line_loop");
            code.AppendLine("jml_read_line_done:");
            code.AppendLine("    strb wzr, [x20, x21]");
            code.AppendLine("    mov x9, #2");
            code.AppendLine($"    str x9, [x19, #{ValueTypeOffset}]");
            code.AppendLine($"    str xzr, [x19, #{ValueIntOffset}]");
            code.AppendLine($"    str x20, [x19, #{ValuePointerOffset}]");
            EmitReadLineEpilogue(code);
            code.AppendLine();
        }

        private void EmitData(StringBuilder code)
        {
            if (_target.IsMacOS)
                code.AppendLine(".section __DATA,__data");
            else
                code.AppendLine(".data");

            code.AppendLine(".p2align 3");
            EmitByteString(code, "jml_fmt_str", "%s");
            EmitByteString(code, "jml_fmt_int", "%lld");
            EmitByteString(code, "jml_null_str", "여친");
            EmitByteString(code, "jml_lbracket_str", "[");
            EmitByteString(code, "jml_rbracket_str", "]");
            EmitByteString(code, "jml_comma_space_str", ", ");

            foreach (KeyValuePair<string, string> item in _stringLiterals.OrderBy(item => item.Value))
                EmitByteString(code, item.Value, item.Key);

            code.AppendLine(".p2align 3");
            code.AppendLine("jml_tmp_0:");
            code.AppendLine("    .quad 0");
            code.AppendLine("jml_tmp_1:");
            code.AppendLine("    .quad 0");
            code.AppendLine("jml_addr_tmp:");
            code.AppendLine("    .quad 0");
            code.AppendLine("jml_null_value:");
            code.AppendLine("    .quad 0, 0, 0, 0");
            code.AppendLine("jml_tmp_value:");
            code.AppendLine("    .quad 0, 0, 0, 0");

            foreach (FunctionDefinition function in _functions.Values.OrderBy(function => function.Label))
            {
                code.AppendLine(FunctionReturnBufferPointerLabel(function.Label) + ":");
                code.AppendLine("    .quad 0");
            }

            foreach (ArrayStorage storage in _returnBuffers.Values.OrderBy(storage => storage.Label))
            {
                code.AppendLine(storage.Label + ":");
                code.AppendLine($"    .space {Math.Max(1, storage.Length) * ValueSize}");
            }

            foreach (string label in _variables.Values.Order())
            {
                code.AppendLine(label + ":");
                code.AppendLine("    .quad 0, 0, 0, 0");
            }

            foreach (string label in _inputBuffers.Values.Order())
            {
                code.AppendLine(label + ":");
                code.AppendLine($"    .space {InputBufferSize}");
            }

            foreach (ArrayStorage storage in _arrayStorages.Values.OrderBy(storage => storage.Label))
            {
                code.AppendLine(storage.Label + ":");
                code.AppendLine($"    .space {Math.Max(1, storage.Length) * ValueSize}");
            }
        }

        private void EmitFunctionPrologue(StringBuilder code)
        {
            code.AppendLine("    stp x29, x30, [sp, #-16]!");
            code.AppendLine("    mov x29, sp");
            code.AppendLine("    stp x19, x20, [sp, #-16]!");
            code.AppendLine("    stp x21, x22, [sp, #-16]!");
            code.AppendLine("    stp x23, x24, [sp, #-16]!");
            code.AppendLine("    stp x25, x26, [sp, #-16]!");
        }

        private void EmitFunctionEpilogue(StringBuilder code)
        {
            code.AppendLine("    ldp x25, x26, [sp], #16");
            code.AppendLine("    ldp x23, x24, [sp], #16");
            code.AppendLine("    ldp x21, x22, [sp], #16");
            code.AppendLine("    ldp x19, x20, [sp], #16");
            code.AppendLine("    ldp x29, x30, [sp], #16");
            code.AppendLine("    ret");
        }

        private void EmitReadLinePrologue(StringBuilder code)
        {
            code.AppendLine("    stp x29, x30, [sp, #-48]!");
            code.AppendLine("    mov x29, sp");
            code.AppendLine("    stp x19, x20, [sp, #16]");
            code.AppendLine("    str x21, [sp, #32]");
        }

        private void EmitReadLineEpilogue(StringBuilder code)
        {
            code.AppendLine("    ldr x21, [sp, #32]");
            code.AppendLine("    ldp x19, x20, [sp, #16]");
            code.AppendLine("    ldp x29, x30, [sp], #48");
            code.AppendLine("    ret");
        }

        private void EmitPrintfWithOneArg(StringBuilder code, string formatLabel, string argRegister)
        {
            if (_target.IsMacOS)
            {
                code.AppendLine("    sub sp, sp, #16");
                code.AppendLine($"    str {argRegister}, [sp]");
                EmitLoadAddress(code, "x0", formatLabel);
                code.AppendLine("    bl " + _target.PrintfSymbol);
                code.AppendLine("    add sp, sp, #16");
                return;
            }

            EmitLoadAddress(code, "x0", formatLabel);
            EmitMove(code, "x1", argRegister);
            code.AppendLine("    bl " + _target.PrintfSymbol);
        }

        private void EmitLoadAddress(StringBuilder code, string register, string label)
        {
            EmitLoadAddress(code, register, label, 0);
        }

        private void EmitLoadAddress(StringBuilder code, string register, string label, int offset)
        {
            if (_target.IsMacOS)
            {
                code.AppendLine($"    adrp {register}, {label}@PAGE");
                code.AppendLine($"    add {register}, {register}, {label}@PAGEOFF");
                if (offset != 0)
                    code.AppendLine($"    add {register}, {register}, #{offset}");
                return;
            }

            code.AppendLine($"    adrp {register}, {label}");
            code.AppendLine($"    add {register}, {register}, :lo12:{label}");
            if (offset != 0)
                code.AppendLine($"    add {register}, {register}, #{offset}");
        }

        private void EmitLoadImmediate(StringBuilder code, string register, long value)
        {
            if (value == 0)
            {
                code.AppendLine($"    mov {register}, xzr");
                return;
            }

            code.AppendLine($"    ldr {register}, ={value}");
        }

        private void EmitMove(StringBuilder code, string destination, string source)
        {
            if (destination == source)
                return;

            code.AppendLine($"    mov {destination}, {source}");
        }

        private void EmitCall(StringBuilder code, string label)
        {
            code.AppendLine("    bl " + label);
        }

        private void EmitStoreGlobal(StringBuilder code, string sourceRegister, string label)
        {
            EmitLoadAddress(code, "x16", label);
            code.AppendLine($"    str {sourceRegister}, [x16]");
        }

        private void EmitLoadGlobal(StringBuilder code, string destinationRegister, string label)
        {
            EmitLoadAddress(code, "x16", label);
            code.AppendLine($"    ldr {destinationRegister}, [x16]");
        }

        private void EmitStoreWithOffset(StringBuilder code, string sourceRegister, string baseRegister, int offset)
        {
            if (offset <= 32760)
            {
                code.AppendLine($"    str {sourceRegister}, [{baseRegister}, #{offset}]");
                return;
            }

            EmitLoadImmediate(code, "x13", offset);
            code.AppendLine($"    add x13, {baseRegister}, x13");
            code.AppendLine($"    str {sourceRegister}, [x13]");
        }

        private string VariableLabel(string name)
        {
            return VariableLabel(_currentContext, name);
        }

        private string VariableLabel(string context, string name)
        {
            return VariableLabel(context, name, forceLocal: false);
        }

        private string VariableLabel(string context, string name, bool forceLocal)
        {
            string key = context + ":" + name;
            if (_variables.TryGetValue(key, out string? label))
                return label;

            string globalKey = JMLNativeParser.MainContext + ":" + name;
            if (!forceLocal && context != JMLNativeParser.MainContext && _variables.TryGetValue(globalKey, out label))
                return label;

            label = "jml_var_" + _variables.Count;
            _variables[key] = label;
            return label;
        }

        private string InputBufferLabel(string name)
        {
            string key = _currentContext + ":" + name;
            if (_inputBuffers.TryGetValue(key, out string? label))
                return label;

            label = "jml_input_" + _inputBuffers.Count;
            _inputBuffers[key] = label;
            return label;
        }

        private string StringLabel(string value)
        {
            if (_stringLiterals.TryGetValue(value, out string? label))
                return label;

            label = "jml_str_" + _stringLiterals.Count;
            _stringLiterals[value] = label;
            return label;
        }

        private ArrayStorage ArrayStorageLabel(int lineIndex, int length)
        {
            if (_arrayStorages.TryGetValue(lineIndex, out ArrayStorage? storage))
                return storage;

            storage = new ArrayStorage("jml_arr_" + _arrayStorages.Count, length);
            _arrayStorages[lineIndex] = storage;
            return storage;
        }

        private string ReturnBufferLabel(int lineIndex, int valueCount)
        {
            string key = _currentContext + ":" + lineIndex;
            if (_returnBuffers.TryGetValue(key, out ArrayStorage? storage))
                return storage.Label;

            storage = new ArrayStorage("jml_call_ret_" + _returnBuffers.Count, Math.Max(1, valueCount));
            _returnBuffers[key] = storage;
            return storage.Label;
        }

        private static string FunctionReturnBufferPointerLabel(string context)
        {
            return context + "_return_buffer";
        }

        private static string LineLabel(string context, int lineNumber)
        {
            return $"L_{context}_line_{lineNumber}";
        }

        private bool ShouldEmitLineLabel(string context, int lineNumber)
        {
            return _program.LabelLines.TryGetValue(context, out HashSet<int>? lines) && lines.Contains(lineNumber);
        }

        private bool IsDeadLine(string context, int lineNumber)
        {
            return _program.DeadLines.TryGetValue(context, out HashSet<int>? lines) && lines.Contains(lineNumber);
        }

        private static string ReturnLabel(string context)
        {
            return $"L_{context}_return";
        }

        private bool LooksLikeFunctionCall(string[] tokens)
        {
            if (tokens.Length == 0)
                return false;

            return _functions.ContainsKey(tokens[0]);
        }

        private static string RequireArg(string[] args, int index, int lineIndex)
        {
            if (index >= args.Length || string.IsNullOrEmpty(args[index]))
                throw new ArgumentException($"{lineIndex + 1}번째 줄에 인수가 없잖아;;");

            return args[index];
        }

        private static int ParseLineNumber(string raw, int lineIndex)
        {
            if (!int.TryParse(raw, out int lineNumber))
                throw new ArgumentException($"{lineIndex + 1}번째 줄 번호는 숫자여야지;;");

            return lineNumber;
        }

        private static bool IsStringLiteral(string token)
        {
            return token.StartsWith('\"') && token.EndsWith('\"');
        }

        private static string StringValue(string token)
        {
            return token[1..^1];
        }

        private static bool TryParseNumber(string token, out long value)
        {
            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
            {
                value = checked((long)Math.Round(parsed * NumberScale));
                return true;
            }

            value = 0;
            return false;
        }

        private static bool TryParseArrayAccess(string token, out string arrayName, out int[] arrayIndices)
        {
            arrayName = "";
            arrayIndices = [];
            string[] parts = token.Split('.');
            if (parts.Length < 2)
                return false;

            List<int> indices = [];
            foreach (string part in parts.Skip(1))
            {
                if (!int.TryParse(part, out int arrayIndex))
                    return false;

                indices.Add(arrayIndex);
            }

            arrayName = parts[0];
            arrayIndices = [.. indices];
            return true;
        }

        private static void EmitByteString(StringBuilder code, string label, string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            code.AppendLine(label + ":");

            if (bytes.Length == 0)
            {
                code.AppendLine("    .byte 0");
                return;
            }

            for (int i = 0; i < bytes.Length; i += 16)
            {
                string chunk = string.Join(", ", bytes.Skip(i).Take(16));
                code.AppendLine("    .byte " + chunk);
            }

            code.AppendLine("    .byte 0");
        }

        private sealed record ArrayStorage(string Label, int Length);

        private sealed record FunctionDefinition(
            string Name,
            int DeclarationLine,
            int Start,
            int End,
            string Label,
            string[] Parameters,
            int ReturnCount);
    }
}
