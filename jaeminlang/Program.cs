using System.Text;

namespace jaeminlang
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("jaeminlang by Pro203S (https://github.com/Pro203S/jaeminlang)\n아래에 재민랭을 입력하세요.");
                for (; ; )
                {
                    Stream stderr = Console.OpenStandardError();
                    try
                    {
                        string? line = Console.ReadLine();
                        if (line == null || string.IsNullOrEmpty(line))
                        {
                            Console.WriteLine();
                            continue;
                        }

                        if (line.StartsWith("러스트"))
                        {
                            Console.WriteLine("여기서 러스트는 못쓰긴해");
                            continue;
                        }

                        JMLCommand cmd = new(line, null);
                        cmd.Execute();
                        Console.WriteLine();
                    }
                    catch (Exception e)
                    {
                        stderr.Write(Encoding.UTF8.GetBytes(e.Message + "\r\n"));
                        stderr.Write(Encoding.UTF8.GetBytes(e.StackTrace + "\r\n"));
                    }
                }
            }

            if (IsCompileCommand(args[0]))
            {
                try
                {
                    JMLCompileOptions options = ParseCompileOptions(args[1..]);
                    return JMLCompiler.Compile(options);
                }
                catch (Exception e)
                {
                    Stream stderr = Console.OpenStandardError();
                    stderr.Write(Encoding.UTF8.GetBytes("아니;; 컴파일을 못했잖아;;\r\n"));
                    stderr.Write(Encoding.UTF8.GetBytes(e.Message + "\r\n"));
                    return 1;
                }
            }

            if (IsLinkCommand(args[0]))
            {
                try
                {
                    JMLLinkOptions options = ParseLinkOptions(args[1..]);
                    return JMLCompiler.Link(options);
                }
                catch (Exception e)
                {
                    Stream stderr = Console.OpenStandardError();
                    stderr.Write(Encoding.UTF8.GetBytes("아니;; 링크를 못했잖아;;\r\n"));
                    stderr.Write(Encoding.UTF8.GetBytes(e.Message + "\r\n"));
                    return 1;
                }
            }

            if (args[0] == "-h" || args[0] == "--help")
            {
                PrintHelp();
                return 0;
            }

            string JmlFilePath = args[0];
            if (!File.Exists(JmlFilePath))
            {
                Stream stderr = Console.OpenStandardError();
                stderr.Write(Encoding.UTF8.GetBytes("아니;; 파일을 왜 안주냐고;;"));

                return 1;
            }

            JMLParser parse = new JMLParser(JmlFilePath);
            parse.Run();

            return 0;
        }

        private static bool IsCompileCommand(string command)
        {
            return command == "compile" ||
                command == "build" ||
                command == "컴파일" ||
                command == "빌드";
        }

        private static bool IsLinkCommand(string command)
        {
            return command == "link" ||
                command == "링크";
        }

        private static JMLCompileOptions ParseCompileOptions(string[] args)
        {
            if (args.Length == 0 || args[0] == "-h" || args[0] == "--help")
            {
                PrintCompileHelp();
                Environment.Exit(args.Length == 0 ? 1 : 0);
            }

            if (args[0] == "--list-rids")
            {
                Console.WriteLine(JMLCompiler.GetSupportedRidList());
                Environment.Exit(0);
            }

            string? sourcePath = null;
            string? outputPath = null;
            string rid = JMLCompiler.GetDefaultRid();
            bool emitAssemblyOnly = false;
            bool emitObjectOnly = false;
            bool keepAssembly = false;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                switch (arg)
                {
                    case "-o":
                    case "--output":
                        outputPath = RequireOptionValue(args, ref i, arg);
                        break;
                    case "-r":
                    case "--rid":
                        rid = JMLCompiler.NormalizeRid(RequireOptionValue(args, ref i, arg));
                        break;
                    case "-S":
                    case "--emit-asm":
                        emitAssemblyOnly = true;
                        break;
                    case "-c":
                    case "--emit-obj":
                    case "--compile-only":
                        emitObjectOnly = true;
                        break;
                    case "--keep-asm":
                    case "--keep-temp":
                        keepAssembly = true;
                        break;
                    case "--list-rids":
                        Console.WriteLine(JMLCompiler.GetSupportedRidList());
                        Environment.Exit(0);
                        break;
                    default:
                        if (arg.StartsWith('-'))
                            throw new ArgumentException("모르는 옵션이잖아;; " + arg);

                        if (sourcePath != null)
                            throw new ArgumentException("소스 파일은 하나만 줘야지;;");

                        sourcePath = arg;
                        break;
                }
            }

            if (sourcePath == null)
                throw new ArgumentException("컴파일할 재민랭 파일을 줘야지;;");

            return new JMLCompileOptions
            {
                SourcePath = sourcePath,
                OutputPath = outputPath,
                Rid = rid,
                EmitAssemblyOnly = emitAssemblyOnly,
                EmitObjectOnly = emitObjectOnly,
                KeepAssembly = keepAssembly
            };
        }

        private static JMLLinkOptions ParseLinkOptions(string[] args)
        {
            if (args.Length == 0 || args[0] == "-h" || args[0] == "--help")
            {
                PrintLinkHelp();
                Environment.Exit(args.Length == 0 ? 1 : 0);
            }

            if (args[0] == "--list-rids")
            {
                Console.WriteLine(JMLCompiler.GetSupportedRidList());
                Environment.Exit(0);
            }

            List<string> objectPaths = [];
            string? outputPath = null;
            string rid = JMLCompiler.GetDefaultRid();
            bool readInputsOnly = false;

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];

                if (readInputsOnly)
                {
                    objectPaths.Add(arg);
                    continue;
                }

                switch (arg)
                {
                    case "-o":
                    case "--output":
                        outputPath = RequireOptionValue(args, ref i, arg);
                        break;
                    case "-r":
                    case "--rid":
                        rid = JMLCompiler.NormalizeRid(RequireOptionValue(args, ref i, arg));
                        break;
                    case "--list-rids":
                        Console.WriteLine(JMLCompiler.GetSupportedRidList());
                        Environment.Exit(0);
                        break;
                    case "--":
                        readInputsOnly = true;
                        break;
                    default:
                        if (arg.StartsWith('-'))
                            throw new ArgumentException("모르는 옵션이잖아;; " + arg);

                        objectPaths.Add(arg);
                        break;
                }
            }

            if (objectPaths.Count == 0)
                throw new ArgumentException("링크할 오브젝트 파일을 줘야지;;");

            return new JMLLinkOptions
            {
                ObjectPaths = objectPaths,
                OutputPath = outputPath,
                Rid = rid
            };
        }

        private static string RequireOptionValue(string[] args, ref int index, string optionName)
        {
            if (index + 1 >= args.Length)
                throw new ArgumentException(optionName + " 옵션 값이 없잖아;;");

            index++;
            return args[index];
        }

        private static void PrintHelp()
        {
            Console.WriteLine("""
                jaeminlang

                실행:
                  jaeminlang <source.jml>
                  jaeminlang compile <source.jml> [-o output] [--rid RID]
                  jaeminlang link <object.o...> [-o output] [--rid RID]

                도움말:
                  jaeminlang compile --help
                  jaeminlang link --help
                """);
        }

        private static void PrintCompileHelp()
        {
            Console.WriteLine($$"""
                사용법:
                  jaeminlang compile <source.jml> [-o output] [--rid RID]

                옵션:
                  -o, --output <path>     생성할 실행 파일 경로
                  -r, --rid <RID>         대상 OS/CPU RID (기본값: {{JMLCompiler.GetDefaultRid()}})
                  -S, --emit-asm          어셈블리 파일만 생성
                  -c, --emit-obj          오브젝트 파일만 생성
                      --keep-asm          오브젝트 생성 후에도 어셈블리 파일 유지
                      --list-rids         지원 RID 목록 출력

                RID 별칭:
                  mac, linux, windows     현재 CPU 아키텍처 기준으로 변환

                예시:
                  jaeminlang compile hello.jml -o hello --rid osx-x64
                  jaeminlang compile hello.jml -o hello --rid osx-arm64
                  jaeminlang compile hello.jml -o hello --rid linux-x64
                  jaeminlang compile hello.jml -o hello --rid linux-arm64
                  jaeminlang compile hello.jml -o hello.exe --rid win-x64
                  jaeminlang compile hello.jml -o hello.exe --rid win-arm64
                  jaeminlang compile hello.jml -c -o hello.o --rid linux-x64
                  jaeminlang compile hello.jml -S -o hello.s --rid linux-x64
                """);
        }

        private static void PrintLinkHelp()
        {
            Console.WriteLine($$"""
                사용법:
                  jaeminlang link <object.o...> [-o output] [--rid RID]

                옵션:
                  -o, --output <path>     생성할 실행 파일 경로
                  -r, --rid <RID>         대상 OS/CPU RID (기본값: {{JMLCompiler.GetDefaultRid()}})
                      --list-rids         지원 RID 목록 출력

                RID 별칭:
                  mac, linux, windows     현재 CPU 아키텍처 기준으로 변환

                예시:
                  jaeminlang compile hello.jml -c -o hello.o --rid linux-x64
                  jaeminlang link hello.o -o hello --rid linux-x64
                  jaeminlang link hello.o helper.o -o hello --rid osx-arm64
                  jaeminlang link hello.o -o hello.exe --rid win-x64
                """);
        }
    }
}
