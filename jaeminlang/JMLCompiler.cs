using System.Diagnostics;
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
                throw new ArgumentException("출력 파일이 소스 파일과 같잖아;; 다른 경로를 줘.");

            if (options.EmitAssemblyOnly && options.EmitObjectOnly)
                throw new ArgumentException("-S랑 -c는 같이 쓸 수 없잖아;;");

            string[] sourceLines = File.ReadAllLines(sourcePath);
            INativeAssemblyGenerator generator = target.IsArm64
                ? new Arm64AssemblyGenerator(target, sourceLines)
                : new NativeAssemblyGenerator(target, sourceLines);
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
                throw new ArgumentException("출력 파일이 오브젝트 파일과 같잖아;; 다른 경로를 줘.");

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
                throw new ArgumentException($"지원하지 않는 RID야: {rawRid}\n지원 RID: {string.Join(", ", SupportedRidList)}");

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
                ?? throw new InvalidOperationException("clang/gcc/cc를 못 찾았잖아;; 오브젝트를 만들려면 네이티브 툴체인이 필요해.");

            string? objectDirectory = Path.GetDirectoryName(objectPath);
            if (!string.IsNullOrEmpty(objectDirectory))
                Directory.CreateDirectory(objectDirectory);

            ProcessStartInfo startInfo = CreateNativeToolStartInfo(assembler, target);
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add(assemblyPath);
            startInfo.ArgumentList.Add("-o");
            startInfo.ArgumentList.Add(objectPath);

            RunNativeTool(startInfo, "네이티브 어셈블이 실패했잖아;;");
        }

        private static void RunNativeLinker(NativeTarget target, IReadOnlyList<string> objectPaths, string outputPath)
        {
            string linker = FindExecutable(GetToolCandidates(target))
                ?? throw new InvalidOperationException("clang/gcc/cc를 못 찾았잖아;; 실행 파일까지 만들려면 네이티브 툴체인이 필요해. 어셈블리만 보려면 --emit-asm을 써.");

            string? outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            ProcessStartInfo startInfo = CreateNativeToolStartInfo(linker, target);
            foreach (string objectPath in objectPaths)
                startInfo.ArgumentList.Add(objectPath);

            startInfo.ArgumentList.Add("-o");
            startInfo.ArgumentList.Add(outputPath);

            RunNativeTool(startInfo, "네이티브 링크가 실패했잖아;;");
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
                ?? throw new InvalidOperationException("네이티브 툴 실행에 실패했잖아;;");

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
                _ => throw new ArgumentException("지원하지 않는 RID야: " + rid)
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
        private const int InputBufferSize = 4096;

        private readonly NativeTarget _target;
        private readonly string[] _lines;
        private readonly Dictionary<string, string> _variables = [];
        private readonly Dictionary<string, string> _inputBuffers = [];
        private readonly Dictionary<string, string> _stringLiterals = [];
        private readonly Dictionary<int, ArrayStorage> _arrayStorages = [];
        private readonly Dictionary<string, FunctionDefinition> _functions = [];

        public NativeAssemblyGenerator(NativeTarget target, string[] lines)
        {
            _target = target;
            _lines = lines;
            CollectFunctionDefinitions();
        }

        public string Generate()
        {
            StringBuilder code = new();
            EmitHeader(code);
            EmitMain(code);
            EmitFunctions(code);
            EmitRuntime(code);
            EmitData(code);
            return code.ToString();
        }

        private void CollectFunctionDefinitions()
        {
            int functionIndex = 0;

            for (int i = 0; i < _lines.Length; i++)
            {
                string[] args = Utils.GetArguments(_lines[i]);
                if (args.Length < 4 || args[0] != "엘릭서")
                    continue;

                string name = RequireArg(args, 1, i);
                int start = ParseLineNumber(RequireArg(args, 2, i), i);
                int end = ParseLineNumber(RequireArg(args, 3, i), i);

                if (start <= 0 || start > _lines.Length || end < start)
                    throw new ArgumentException($"{i + 1}번째 줄 함수 범위가 이상하잖아;;");

                string label = _functions.TryGetValue(name, out FunctionDefinition? existingFunction)
                    ? existingFunction.Label
                    : "jml_func_" + functionIndex++;

                _functions[name] = new FunctionDefinition(name, start, Math.Min(end, _lines.Length), label);
            }
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
                code.AppendLine($"    jmp {LineLabel(function.Label, function.Start)}");
                EmitProgramRange(code, 1, function.End, function.Label, function.End, function.End);
                code.AppendLine(ReturnLabel(function.Label) + ":");
                code.AppendLine("    xor eax, eax");
                EmitFunctionEpilogue(code);
                code.AppendLine();
            }
        }

        private void EmitProgramRange(StringBuilder code, int startLine, int endLine, string context, int jumpLimitLine, int? returnAfterLine = null)
        {
            for (int lineNumber = startLine; lineNumber <= endLine; lineNumber++)
            {
                code.AppendLine(LineLabel(context, lineNumber) + ":");
                EmitLine(code, lineNumber, context, jumpLimitLine);

                if (returnAfterLine == lineNumber)
                    code.AppendLine($"    jmp {ReturnLabel(context)}");
            }
        }

        private void EmitLine(StringBuilder code, int lineNumber, string context, int jumpLimitLine)
        {
            string raw = _lines[lineNumber - 1];
            if (raw.StartsWith("어이쿠"))
                return;

            string[] args = Utils.GetArguments(raw);
            if (args.Length == 0 || string.IsNullOrEmpty(args[0]))
                return;

            switch (args[0])
            {
                case "안산":
                    EmitPrint(code, RequireArg(args, 1, lineNumber - 1), lineNumber - 1);
                    break;
                case "재민":
                    EmitInput(code, RequireArg(args, 1, lineNumber - 1));
                    break;
                case "그램":
                    EmitAssignment(code, args, lineNumber - 1);
                    break;
                case "러스트":
                    EmitRepeat(code, args, lineNumber - 1, context, jumpLimitLine);
                    break;
                case "엘릭서":
                    EmitFunctionCommand(code, args, lineNumber - 1);
                    break;
                default:
                    throw new ArgumentException($"{lineNumber}번째 줄에 모르는 명령어가 있잖아;; {args[0]}");
            }
        }

        private void EmitPrint(StringBuilder code, string token, int lineIndex)
        {
            if (IsStringLiteral(token))
            {
                EmitLoadAddressToArg(code, 0, StringLabel(StringValue(token)));
                EmitCall(code, "jml_print_string");
                return;
            }

            if (TryParseNumber(token, out long number))
            {
                EmitMoveToArg(code, 0, number.ToString());
                EmitCall(code, "jml_print_int");
                return;
            }

            if (TryParseArrayAccess(token, out string arrayName, out int arrayIndex))
            {
                EmitLoadArrayItem(code, arrayName, arrayIndex, "r11");
                EmitMoveToArg(code, 0, "r11");
                EmitCall(code, "jml_print_int");
                return;
            }

            EmitLoadAddressToArg(code, 0, VariableLabel(token));
            EmitCall(code, "jml_print_value");
        }

        private void EmitInput(StringBuilder code, string key)
        {
            EmitLoadAddressToArg(code, 0, VariableLabel(key));
            EmitLoadAddressToArg(code, 1, InputBufferLabel(key));
            EmitCall(code, "jml_read_line");
        }

        private void EmitAssignment(StringBuilder code, string[] args, int lineIndex)
        {
            string key = RequireArg(args, 1, lineIndex);

            if (key.StartsWith('[') && key.EndsWith(']'))
            {
                EmitArrayAssignment(code, key[1..^1], args.Skip(2).ToArray(), lineIndex);
                return;
            }

            string data = RequireArg(args, 2, lineIndex);

            if (TryParseArrayAccess(key, out string arraySetName, out int arraySetIndex))
            {
                EmitLoadInt(code, data, "r11");
                EmitLoadAddressToArg(code, 0, VariableLabel(arraySetName));
                EmitMoveToArg(code, 1, arraySetIndex.ToString());
                EmitMoveToArg(code, 2, "r11");
                EmitCall(code, "jml_array_set");
                return;
            }

            if (Utils.IsExpression(data))
            {
                EmitExpressionAssignment(code, key, data);
                return;
            }

            if (data == "여친")
            {
                EmitLoadAddressToArg(code, 0, VariableLabel(key));
                EmitCall(code, "jml_set_null");
                return;
            }

            if (IsStringLiteral(data))
            {
                EmitLoadAddressToArg(code, 0, VariableLabel(key));
                EmitLoadAddressToArg(code, 1, StringLabel(StringValue(data)));
                EmitCall(code, "jml_set_string");
                return;
            }

            if (TryParseNumber(data, out long number))
            {
                EmitLoadAddressToArg(code, 0, VariableLabel(key));
                EmitMoveToArg(code, 1, number.ToString());
                EmitCall(code, "jml_set_int");
                return;
            }

            if (TryParseArrayAccess(data, out string arrayName, out int arrayIndex))
            {
                EmitLoadArrayItem(code, arrayName, arrayIndex, "r11");
                EmitLoadAddressToArg(code, 0, VariableLabel(key));
                EmitMoveToArg(code, 1, "r11");
                EmitCall(code, "jml_set_int");
                return;
            }

            EmitLoadAddressToArg(code, 0, VariableLabel(key));
            EmitLoadAddressToArg(code, 1, VariableLabel(data));
            EmitCall(code, "jml_copy_value");
        }

        private void EmitArrayAssignment(StringBuilder code, string key, string[] rawValues, int lineIndex)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException($"{lineIndex + 1}번째 줄 배열 이름이 비었잖아;;");

            ArrayStorage storage = ArrayStorageLabel(lineIndex, rawValues.Length);

            for (int i = 0; i < rawValues.Length; i++)
            {
                EmitLoadInt(code, rawValues[i], "r11");
                code.AppendLine($"    mov qword ptr [rip + {storage.Label} + {i * 8}], r11");
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
                    code.AppendLine("    imul r10, r11");
                    code.AppendLine("    mov r11, r10");
                    break;
                case "/":
                    code.AppendLine("    mov rax, r10");
                    code.AppendLine("    cqo");
                    code.AppendLine("    idiv r11");
                    code.AppendLine("    mov r11, rax");
                    break;
                case "^":
                    code.AppendLine("    xor r10, r11");
                    code.AppendLine("    mov r11, r10");
                    break;
                default:
                    throw new ArgumentException("이런 수식은 안산에도 없어;; " + op);
            }

            EmitLoadAddressToArg(code, 0, VariableLabel(key));
            EmitMoveToArg(code, 1, "r11");
            EmitCall(code, "jml_set_int");
        }

        private void EmitRepeat(StringBuilder code, string[] args, int lineIndex, string context, int jumpLimitLine)
        {
            string rawVal1 = RequireArg(args, 1, lineIndex);
            string rawVal2 = RequireArg(args, 2, lineIndex);
            int goTo = ParseLineNumber(RequireArg(args, 3, lineIndex), lineIndex);

            EmitLoadInt(code, rawVal1, "r10");
            code.AppendLine("    mov qword ptr [rip + jml_tmp_0], r10");
            EmitLoadInt(code, rawVal2, "r11");
            code.AppendLine("    mov r10, qword ptr [rip + jml_tmp_0]");
            code.AppendLine("    cmp r10, r11");

            if (goTo <= 0)
                throw new ArgumentException($"{lineIndex + 1}번째 줄 이동할 줄 번호가 이상하잖아;;");

            if (goTo > jumpLimitLine)
                code.AppendLine($"    jne {ReturnLabel(context)}");
            else
                code.AppendLine($"    jne {LineLabel(context, goTo)}");
        }

        private void EmitFunctionCommand(StringBuilder code, string[] args, int lineIndex)
        {
            string functionName = RequireArg(args, 1, lineIndex);

            if (args.Length == 2)
            {
                if (!_functions.TryGetValue(functionName, out FunctionDefinition? function))
                    throw new ArgumentException($"{lineIndex + 1}번째 줄 {functionName} 함수가 정의가 안됐잖아;;");

                EmitCall(code, function.Label);
            }
        }

        private void EmitLoadInt(StringBuilder code, string token, string targetRegister)
        {
            if (TryParseNumber(token, out long number))
            {
                code.AppendLine($"    mov {targetRegister}, {number}");
                return;
            }

            if (TryParseArrayAccess(token, out string arrayName, out int arrayIndex))
            {
                EmitLoadArrayItem(code, arrayName, arrayIndex, targetRegister);
                return;
            }

            EmitLoadAddressToArg(code, 0, VariableLabel(token));
            EmitCall(code, "jml_value_to_int");

            if (targetRegister != "rax")
                code.AppendLine($"    mov {targetRegister}, rax");
        }

        private void EmitLoadArrayItem(StringBuilder code, string arrayName, int arrayIndex, string targetRegister)
        {
            EmitLoadAddressToArg(code, 0, VariableLabel(arrayName));
            EmitMoveToArg(code, 1, arrayIndex.ToString());
            EmitCall(code, "jml_array_get");

            if (targetRegister != "rax")
                code.AppendLine($"    mov {targetRegister}, rax");
        }

        private void EmitRuntime(StringBuilder code)
        {
            EmitSetNull(code);
            EmitSetInt(code);
            EmitSetString(code);
            EmitCopyValue(code);
            EmitSetArray(code);
            EmitArrayGet(code);
            EmitArraySet(code);
            EmitValueToInt(code);
            EmitPrintString(code);
            EmitPrintInt(code);
            EmitPrintValue(code);
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
            code.AppendLine("    xor rax, rax");
            code.AppendLine($"    cmp qword ptr [{_target.Arg0} + {ValueTypeOffset}], 3");
            code.AppendLine("    jne jml_array_get_done");
            code.AppendLine($"    cmp {_target.Arg1}, 0");
            code.AppendLine("    jl jml_array_get_done");
            code.AppendLine($"    cmp {_target.Arg1}, qword ptr [{_target.Arg0} + {ValueIntOffset}]");
            code.AppendLine("    jge jml_array_get_done");
            code.AppendLine($"    mov r10, qword ptr [{_target.Arg0} + {ValuePointerOffset}]");
            code.AppendLine($"    mov rax, qword ptr [r10 + {_target.Arg1} * 8]");
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
            code.AppendLine($"    mov r11, {_target.Arg2}");
            code.AppendLine($"    mov qword ptr [r10 + {_target.Arg1} * 8], r11");
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
            code.AppendLine($"    mov rdx, qword ptr [{_target.Arg0} + {ValuePointerOffset}]");
            code.AppendLine("    xor rax, rax");
            code.AppendLine("    xor r8, r8");
            code.AppendLine("    movzx rcx, byte ptr [rdx]");
            code.AppendLine("    cmp rcx, 45");
            code.AppendLine("    jne jml_value_to_int_loop");
            code.AppendLine("    mov r8, 1");
            code.AppendLine("    inc rdx");
            code.AppendLine("jml_value_to_int_loop:");
            code.AppendLine("    movzx rcx, byte ptr [rdx]");
            code.AppendLine("    cmp rcx, 48");
            code.AppendLine("    jl jml_value_to_int_done");
            code.AppendLine("    cmp rcx, 57");
            code.AppendLine("    jg jml_value_to_int_done");
            code.AppendLine("    imul rax, rax, 10");
            code.AppendLine("    sub rcx, 48");
            code.AppendLine("    add rax, rcx");
            code.AppendLine("    inc rdx");
            code.AppendLine("    jmp jml_value_to_int_loop");
            code.AppendLine("jml_value_to_int_done:");
            code.AppendLine("    test r8, r8");
            code.AppendLine("    jz jml_value_to_int_ret");
            code.AppendLine("    neg rax");
            code.AppendLine("jml_value_to_int_ret:");
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
            EmitLoadAddressToArg(code, 0, "jml_fmt_int");
            EmitMoveToArg(code, 1, "r10");
            code.AppendLine("    xor eax, eax");
            code.AppendLine("    call " + _target.PrintfSymbol);
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
            EmitLoadAddressToArg(code, 0, "jml_fmt_str");
            EmitLoadAddressToArg(code, 1, "jml_null_str");
            code.AppendLine("    xor eax, eax");
            code.AppendLine("    call " + _target.PrintfSymbol);
            code.AppendLine("    jmp jml_print_value_done");
            code.AppendLine("jml_print_value_int:");
            EmitLoadAddressToArg(code, 0, "jml_fmt_int");
            code.AppendLine($"    mov {_target.Arg1}, qword ptr [r10 + {ValueIntOffset}]");
            code.AppendLine("    xor eax, eax");
            code.AppendLine("    call " + _target.PrintfSymbol);
            code.AppendLine("    jmp jml_print_value_done");
            code.AppendLine("jml_print_value_string:");
            EmitLoadAddressToArg(code, 0, "jml_fmt_str");
            code.AppendLine($"    mov {_target.Arg1}, qword ptr [r10 + {ValuePointerOffset}]");
            code.AppendLine("    xor eax, eax");
            code.AppendLine("    call " + _target.PrintfSymbol);
            code.AppendLine("jml_print_value_done:");
            EmitExternalCallEpilogue(code);
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

            foreach (KeyValuePair<string, string> item in _stringLiterals.OrderBy(item => item.Value))
                EmitByteString(code, item.Value, item.Key);

            code.AppendLine(".p2align 3");
            code.AppendLine("jml_tmp_0:");
            code.AppendLine("    .quad 0");

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
                code.AppendLine($"    .space {Math.Max(1, storage.Length) * 8}");
            }
        }

        private void EmitFunctionPrologue(StringBuilder code)
        {
            code.AppendLine("    push rbp");
            code.AppendLine("    mov rbp, rsp");

            if (_target.IsWindows)
                code.AppendLine("    sub rsp, 32");
        }

        private void EmitFunctionEpilogue(StringBuilder code)
        {
            if (_target.IsWindows)
                code.AppendLine("    add rsp, 32");

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

        private void EmitLoadAddressToArg(StringBuilder code, int argumentIndex, string label)
        {
            code.AppendLine($"    lea {Arg(argumentIndex)}, [rip + {label}]");
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
            if (_variables.TryGetValue(name, out string? label))
                return label;

            label = "jml_var_" + _variables.Count;
            _variables[name] = label;
            return label;
        }

        private string InputBufferLabel(string name)
        {
            if (_inputBuffers.TryGetValue(name, out string? label))
                return label;

            label = "jml_input_" + _inputBuffers.Count;
            _inputBuffers[name] = label;
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

        private static string LineLabel(string context, int lineNumber)
        {
            return $"L_{context}_line_{lineNumber}";
        }

        private static string ReturnLabel(string context)
        {
            return $"L_{context}_return";
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
            return long.TryParse(token, out value);
        }

        private static bool TryParseArrayAccess(string token, out string arrayName, out int arrayIndex)
        {
            arrayName = "";
            arrayIndex = 0;
            string[] parts = token.Split('.', 2);
            if (parts.Length != 2 || !int.TryParse(parts[1], out arrayIndex))
                return false;

            arrayName = parts[0];
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

        private sealed record FunctionDefinition(string Name, int Start, int End, string Label);
    }

    internal sealed class Arm64AssemblyGenerator : INativeAssemblyGenerator
    {
        private const int ValueTypeOffset = 0;
        private const int ValueIntOffset = 8;
        private const int ValuePointerOffset = 16;
        private const int InputBufferSize = 4096;

        private readonly NativeTarget _target;
        private readonly string[] _lines;
        private readonly Dictionary<string, string> _variables = [];
        private readonly Dictionary<string, string> _inputBuffers = [];
        private readonly Dictionary<string, string> _stringLiterals = [];
        private readonly Dictionary<int, ArrayStorage> _arrayStorages = [];
        private readonly Dictionary<string, FunctionDefinition> _functions = [];

        public Arm64AssemblyGenerator(NativeTarget target, string[] lines)
        {
            _target = target;
            _lines = lines;
            CollectFunctionDefinitions();
        }

        public string Generate()
        {
            StringBuilder code = new();
            EmitHeader(code);
            EmitMain(code);
            EmitFunctions(code);
            EmitRuntime(code);
            EmitData(code);
            return code.ToString();
        }

        private void CollectFunctionDefinitions()
        {
            int functionIndex = 0;

            for (int i = 0; i < _lines.Length; i++)
            {
                string[] args = Utils.GetArguments(_lines[i]);
                if (args.Length < 4 || args[0] != "엘릭서")
                    continue;

                string name = RequireArg(args, 1, i);
                int start = ParseLineNumber(RequireArg(args, 2, i), i);
                int end = ParseLineNumber(RequireArg(args, 3, i), i);

                if (start <= 0 || start > _lines.Length || end < start)
                    throw new ArgumentException($"{i + 1}번째 줄 함수 범위가 이상하잖아;;");

                string label = _functions.TryGetValue(name, out FunctionDefinition? existingFunction)
                    ? existingFunction.Label
                    : "jml_func_" + functionIndex++;

                _functions[name] = new FunctionDefinition(name, start, Math.Min(end, _lines.Length), label);
            }
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
                code.AppendLine($"    b {LineLabel(function.Label, function.Start)}");
                EmitProgramRange(code, 1, function.End, function.Label, function.End, function.End);
                code.AppendLine(ReturnLabel(function.Label) + ":");
                code.AppendLine("    mov w0, #0");
                EmitFunctionEpilogue(code);
                code.AppendLine();
            }
        }

        private void EmitProgramRange(StringBuilder code, int startLine, int endLine, string context, int jumpLimitLine, int? returnAfterLine = null)
        {
            for (int lineNumber = startLine; lineNumber <= endLine; lineNumber++)
            {
                code.AppendLine(LineLabel(context, lineNumber) + ":");
                EmitLine(code, lineNumber, context, jumpLimitLine);

                if (returnAfterLine == lineNumber)
                    code.AppendLine($"    b {ReturnLabel(context)}");
            }
        }

        private void EmitLine(StringBuilder code, int lineNumber, string context, int jumpLimitLine)
        {
            string raw = _lines[lineNumber - 1];
            if (raw.StartsWith("어이쿠"))
                return;

            string[] args = Utils.GetArguments(raw);
            if (args.Length == 0 || string.IsNullOrEmpty(args[0]))
                return;

            switch (args[0])
            {
                case "안산":
                    EmitPrint(code, RequireArg(args, 1, lineNumber - 1));
                    break;
                case "재민":
                    EmitInput(code, RequireArg(args, 1, lineNumber - 1));
                    break;
                case "그램":
                    EmitAssignment(code, args, lineNumber - 1);
                    break;
                case "러스트":
                    EmitRepeat(code, args, lineNumber - 1, context, jumpLimitLine);
                    break;
                case "엘릭서":
                    EmitFunctionCommand(code, args, lineNumber - 1);
                    break;
                default:
                    throw new ArgumentException($"{lineNumber}번째 줄에 모르는 명령어가 있잖아;; {args[0]}");
            }
        }

        private void EmitPrint(StringBuilder code, string token)
        {
            if (IsStringLiteral(token))
            {
                EmitLoadAddress(code, "x0", StringLabel(StringValue(token)));
                EmitCall(code, "jml_print_string");
                return;
            }

            if (TryParseNumber(token, out long number))
            {
                EmitLoadImmediate(code, "x0", number);
                EmitCall(code, "jml_print_int");
                return;
            }

            if (TryParseArrayAccess(token, out string arrayName, out int arrayIndex))
            {
                EmitLoadArrayItem(code, arrayName, arrayIndex, "x11");
                EmitMove(code, "x0", "x11");
                EmitCall(code, "jml_print_int");
                return;
            }

            EmitLoadAddress(code, "x0", VariableLabel(token));
            EmitCall(code, "jml_print_value");
        }

        private void EmitInput(StringBuilder code, string key)
        {
            EmitLoadAddress(code, "x0", VariableLabel(key));
            EmitLoadAddress(code, "x1", InputBufferLabel(key));
            EmitCall(code, "jml_read_line");
        }

        private void EmitAssignment(StringBuilder code, string[] args, int lineIndex)
        {
            string key = RequireArg(args, 1, lineIndex);

            if (key.StartsWith('[') && key.EndsWith(']'))
            {
                EmitArrayAssignment(code, key[1..^1], args.Skip(2).ToArray(), lineIndex);
                return;
            }

            string data = RequireArg(args, 2, lineIndex);

            if (TryParseArrayAccess(key, out string arraySetName, out int arraySetIndex))
            {
                EmitLoadInt(code, data, "x11");
                EmitLoadAddress(code, "x0", VariableLabel(arraySetName));
                EmitLoadImmediate(code, "x1", arraySetIndex);
                EmitMove(code, "x2", "x11");
                EmitCall(code, "jml_array_set");
                return;
            }

            if (Utils.IsExpression(data))
            {
                EmitExpressionAssignment(code, key, data);
                return;
            }

            if (data == "여친")
            {
                EmitLoadAddress(code, "x0", VariableLabel(key));
                EmitCall(code, "jml_set_null");
                return;
            }

            if (IsStringLiteral(data))
            {
                EmitLoadAddress(code, "x0", VariableLabel(key));
                EmitLoadAddress(code, "x1", StringLabel(StringValue(data)));
                EmitCall(code, "jml_set_string");
                return;
            }

            if (TryParseNumber(data, out long number))
            {
                EmitLoadAddress(code, "x0", VariableLabel(key));
                EmitLoadImmediate(code, "x1", number);
                EmitCall(code, "jml_set_int");
                return;
            }

            if (TryParseArrayAccess(data, out string arrayName, out int arrayIndex))
            {
                EmitLoadArrayItem(code, arrayName, arrayIndex, "x11");
                EmitLoadAddress(code, "x0", VariableLabel(key));
                EmitMove(code, "x1", "x11");
                EmitCall(code, "jml_set_int");
                return;
            }

            EmitLoadAddress(code, "x0", VariableLabel(key));
            EmitLoadAddress(code, "x1", VariableLabel(data));
            EmitCall(code, "jml_copy_value");
        }

        private void EmitArrayAssignment(StringBuilder code, string key, string[] rawValues, int lineIndex)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException($"{lineIndex + 1}번째 줄 배열 이름이 비었잖아;;");

            ArrayStorage storage = ArrayStorageLabel(lineIndex, rawValues.Length);

            for (int i = 0; i < rawValues.Length; i++)
            {
                EmitLoadInt(code, rawValues[i], "x11");
                EmitLoadAddress(code, "x12", storage.Label);
                EmitStoreWithOffset(code, "x11", "x12", i * 8);
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
                    break;
                case "/":
                    code.AppendLine("    sdiv x11, x10, x11");
                    break;
                case "^":
                    code.AppendLine("    eor x11, x10, x11");
                    break;
                default:
                    throw new ArgumentException("이런 수식은 안산에도 없어;; " + op);
            }

            EmitLoadAddress(code, "x0", VariableLabel(key));
            EmitMove(code, "x1", "x11");
            EmitCall(code, "jml_set_int");
        }

        private void EmitRepeat(StringBuilder code, string[] args, int lineIndex, string context, int jumpLimitLine)
        {
            string rawVal1 = RequireArg(args, 1, lineIndex);
            string rawVal2 = RequireArg(args, 2, lineIndex);
            int goTo = ParseLineNumber(RequireArg(args, 3, lineIndex), lineIndex);

            EmitLoadInt(code, rawVal1, "x10");
            EmitStoreGlobal(code, "x10", "jml_tmp_0");
            EmitLoadInt(code, rawVal2, "x11");
            EmitLoadGlobal(code, "x10", "jml_tmp_0");
            code.AppendLine("    cmp x10, x11");

            if (goTo <= 0)
                throw new ArgumentException($"{lineIndex + 1}번째 줄 이동할 줄 번호가 이상하잖아;;");

            if (goTo > jumpLimitLine)
                code.AppendLine($"    b.ne {ReturnLabel(context)}");
            else
                code.AppendLine($"    b.ne {LineLabel(context, goTo)}");
        }

        private void EmitFunctionCommand(StringBuilder code, string[] args, int lineIndex)
        {
            string functionName = RequireArg(args, 1, lineIndex);

            if (args.Length == 2)
            {
                if (!_functions.TryGetValue(functionName, out FunctionDefinition? function))
                    throw new ArgumentException($"{lineIndex + 1}번째 줄 {functionName} 함수가 정의가 안됐잖아;;");

                EmitCall(code, function.Label);
            }
        }

        private void EmitLoadInt(StringBuilder code, string token, string targetRegister)
        {
            if (TryParseNumber(token, out long number))
            {
                EmitLoadImmediate(code, targetRegister, number);
                return;
            }

            if (TryParseArrayAccess(token, out string arrayName, out int arrayIndex))
            {
                EmitLoadArrayItem(code, arrayName, arrayIndex, targetRegister);
                return;
            }

            EmitLoadAddress(code, "x0", VariableLabel(token));
            EmitCall(code, "jml_value_to_int");

            if (targetRegister != "x0")
                EmitMove(code, targetRegister, "x0");
        }

        private void EmitLoadArrayItem(StringBuilder code, string arrayName, int arrayIndex, string targetRegister)
        {
            EmitLoadAddress(code, "x0", VariableLabel(arrayName));
            EmitLoadImmediate(code, "x1", arrayIndex);
            EmitCall(code, "jml_array_get");

            if (targetRegister != "x0")
                EmitMove(code, targetRegister, "x0");
        }

        private void EmitRuntime(StringBuilder code)
        {
            EmitSetNull(code);
            EmitSetInt(code);
            EmitSetString(code);
            EmitCopyValue(code);
            EmitSetArray(code);
            EmitArrayGet(code);
            EmitArraySet(code);
            EmitValueToInt(code);
            EmitPrintString(code);
            EmitPrintInt(code);
            EmitPrintValue(code);
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
            code.AppendLine("    mov x12, #0");
            code.AppendLine($"    ldr x9, [x0, #{ValueTypeOffset}]");
            code.AppendLine("    cmp x9, #3");
            code.AppendLine("    b.ne jml_array_get_done");
            code.AppendLine("    cmp x1, #0");
            code.AppendLine("    b.lt jml_array_get_done");
            code.AppendLine($"    ldr x9, [x0, #{ValueIntOffset}]");
            code.AppendLine("    cmp x1, x9");
            code.AppendLine("    b.ge jml_array_get_done");
            code.AppendLine($"    ldr x9, [x0, #{ValuePointerOffset}]");
            code.AppendLine("    ldr x12, [x9, x1, lsl #3]");
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
            code.AppendLine("    str x2, [x9, x1, lsl #3]");
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
            code.AppendLine("    ldrb w2, [x1]");
            code.AppendLine("    cmp w2, #45");
            code.AppendLine("    b.ne jml_value_to_int_loop");
            code.AppendLine("    mov x8, #1");
            code.AppendLine("    add x1, x1, #1");
            code.AppendLine("jml_value_to_int_loop:");
            code.AppendLine("    ldrb w2, [x1]");
            code.AppendLine("    cmp w2, #48");
            code.AppendLine("    b.lt jml_value_to_int_done");
            code.AppendLine("    cmp w2, #57");
            code.AppendLine("    b.gt jml_value_to_int_done");
            code.AppendLine("    mov x3, #10");
            code.AppendLine("    mul x0, x0, x3");
            code.AppendLine("    sub w2, w2, #48");
            code.AppendLine("    add x0, x0, x2");
            code.AppendLine("    add x1, x1, #1");
            code.AppendLine("    b jml_value_to_int_loop");
            code.AppendLine("jml_value_to_int_done:");
            code.AppendLine("    cbz x8, jml_value_to_int_ret");
            code.AppendLine("    neg x0, x0");
            code.AppendLine("jml_value_to_int_ret:");
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
            EmitPrintfWithOneArg(code, "jml_fmt_int", "x9");
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
            EmitLoadAddress(code, "x9", "jml_null_str");
            EmitPrintfWithOneArg(code, "jml_fmt_str", "x9");
            code.AppendLine("    b jml_print_value_done");
            code.AppendLine("jml_print_value_int:");
            code.AppendLine($"    ldr x9, [x10, #{ValueIntOffset}]");
            EmitPrintfWithOneArg(code, "jml_fmt_int", "x9");
            code.AppendLine("    b jml_print_value_done");
            code.AppendLine("jml_print_value_string:");
            code.AppendLine($"    ldr x9, [x10, #{ValuePointerOffset}]");
            EmitPrintfWithOneArg(code, "jml_fmt_str", "x9");
            code.AppendLine("jml_print_value_done:");
            EmitFunctionEpilogue(code);
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

            foreach (KeyValuePair<string, string> item in _stringLiterals.OrderBy(item => item.Value))
                EmitByteString(code, item.Value, item.Key);

            code.AppendLine(".p2align 3");
            code.AppendLine("jml_tmp_0:");
            code.AppendLine("    .quad 0");

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
                code.AppendLine($"    .space {Math.Max(1, storage.Length) * 8}");
            }
        }

        private void EmitFunctionPrologue(StringBuilder code)
        {
            code.AppendLine("    stp x29, x30, [sp, #-16]!");
            code.AppendLine("    mov x29, sp");
        }

        private void EmitFunctionEpilogue(StringBuilder code)
        {
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
            if (_target.IsMacOS)
            {
                code.AppendLine($"    adrp {register}, {label}@PAGE");
                code.AppendLine($"    add {register}, {register}, {label}@PAGEOFF");
                return;
            }

            code.AppendLine($"    adrp {register}, {label}");
            code.AppendLine($"    add {register}, {register}, :lo12:{label}");
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
            EmitLoadAddress(code, "x12", label);
            code.AppendLine($"    str {sourceRegister}, [x12]");
        }

        private void EmitLoadGlobal(StringBuilder code, string destinationRegister, string label)
        {
            EmitLoadAddress(code, "x12", label);
            code.AppendLine($"    ldr {destinationRegister}, [x12]");
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
            if (_variables.TryGetValue(name, out string? label))
                return label;

            label = "jml_var_" + _variables.Count;
            _variables[name] = label;
            return label;
        }

        private string InputBufferLabel(string name)
        {
            if (_inputBuffers.TryGetValue(name, out string? label))
                return label;

            label = "jml_input_" + _inputBuffers.Count;
            _inputBuffers[name] = label;
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

        private static string LineLabel(string context, int lineNumber)
        {
            return $"L_{context}_line_{lineNumber}";
        }

        private static string ReturnLabel(string context)
        {
            return $"L_{context}_return";
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
            return long.TryParse(token, out value);
        }

        private static bool TryParseArrayAccess(string token, out string arrayName, out int arrayIndex)
        {
            arrayName = "";
            arrayIndex = 0;
            string[] parts = token.Split('.', 2);
            if (parts.Length != 2 || !int.TryParse(parts[1], out arrayIndex))
                return false;

            arrayName = parts[0];
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

        private sealed record FunctionDefinition(string Name, int Start, int End, string Label);
    }
}
