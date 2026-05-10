using System.Globalization;

namespace jaeminlang
{
    [Flags]
    internal enum NativeRuntimeFeatures
    {
        None = 0,
        Print = 1 << 0,
        Input = 1 << 1,
        Array = 1 << 2,
        Pow = 1 << 3,
        ValueToNumber = 1 << 4
    }

    internal sealed record JmlNativeProgram(
        string[] Lines,
        IReadOnlyDictionary<int, JmlStmt> Statements,
        IReadOnlyDictionary<string, NativeFunctionDefinition> Functions,
        IReadOnlyDictionary<int, int> FunctionBlocks,
        IReadOnlyDictionary<string, HashSet<int>> LabelLines,
        IReadOnlyDictionary<string, HashSet<int>> DeadLines,
        NativeRuntimeFeatures RuntimeFeatures);

    internal sealed record NativeFunctionDefinition(
        string Name,
        int DeclarationLine,
        int Start,
        int End,
        string Label,
        string[] Parameters,
        int ReturnCount);

    internal abstract record JmlStmt(int Line, string[] Args);

    internal sealed record PrintStmt(int Line, string[] Args, IReadOnlyList<Expr> Values) : JmlStmt(Line, Args);

    internal sealed record InputStmt(int Line, string[] Args, string Name) : JmlStmt(Line, Args);

    internal sealed record AssignStmt(int Line, string[] Args, LValue Target, IReadOnlyList<Expr> Values) : JmlStmt(Line, Args);

    internal sealed record BranchNotEqualStmt(int Line, string[] Args, Expr Left, Expr Right, int TargetLine) : JmlStmt(Line, Args);

    internal sealed record FunctionStmt(int Line, string[] Args, string Name, IReadOnlyList<Expr> Arguments, bool IsDeclaration) : JmlStmt(Line, Args);

    internal sealed record ReturnStmt(int Line, string[] Args, IReadOnlyList<Expr> Values) : JmlStmt(Line, Args);

    internal abstract record LValue;

    internal sealed record VariableLValue(string Name) : LValue;

    internal sealed record ArrayLValue(string Name) : LValue;

    internal sealed record ArrayElementLValue(string Name, IReadOnlyList<int> Indices) : LValue;

    internal abstract record Expr;

    internal sealed record NumberExpr(long ScaledValue) : Expr;

    internal sealed record StringExpr(string Value) : Expr;

    internal sealed record NullExpr() : Expr;

    internal sealed record VarExpr(string Name) : Expr;

    internal sealed record ArrayAccessExpr(string Name, IReadOnlyList<int> Indices) : Expr;

    internal sealed record CallExpr(string Name, IReadOnlyList<Expr> Args) : Expr;

    internal sealed record PrefixBinaryExpr(string Op, Expr Right) : Expr;

    internal sealed record BinaryExpr(string Op, Expr Left, Expr Right) : Expr;

    internal static class JMLNativeParser
    {
        public const long NumberScale = 1_000_000;
        public const string MainContext = "main";

        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

        public static JmlNativeProgram Parse(string[] lines)
        {
            Dictionary<string, NativeFunctionDefinition> functions = CollectFunctions(lines);
            Dictionary<int, int> functionBlocks = functions.Values.ToDictionary(function => function.DeclarationLine, function => function.End);
            Dictionary<int, JmlStmt> statements = ParseStatements(lines, functions);
            NativeRuntimeFeatures features = CollectRuntimeFeatures(statements.Values);

            Dictionary<int, string> lineContexts = BuildLineContexts(functions);
            ValidateBranches(lines, statements, functions, functionBlocks, lineContexts);

            (Dictionary<string, HashSet<int>> labelLines, Dictionary<string, HashSet<int>> deadLines) =
                BuildControlFlow(lines, statements, functions, functionBlocks, lineContexts);

            return new JmlNativeProgram(
                lines,
                statements,
                functions,
                functionBlocks,
                labelLines,
                deadLines,
                features);
        }

        private static Dictionary<string, NativeFunctionDefinition> CollectFunctions(string[] lines)
        {
            Dictionary<string, NativeFunctionDefinition> functions = [];
            int functionIndex = 0;

            for (int i = 0; i < lines.Length; i++)
            {
                if (Utils.ShouldSkipLine(lines[i]))
                    continue;

                string[] args = Utils.GetArguments(lines[i]);
                if (args.Length < 2 || args[0] != "엘릭서")
                    continue;

                string name = RequireArg(args, 1, i);
                if (functions.ContainsKey(name))
                {
                    if (LooksLikeDuplicateFunctionBlock(lines, i + 1))
                        throw new ArgumentException($"{i + 1}번째 줄 {name} 함수가 이미 정의됐잖아;;");

                    continue;
                }

                int returnIndex = Utils.FindFunctionReturnLine(lines, i + 1);
                int declarationLine = i + 1;
                int start = declarationLine + 1;
                int end = returnIndex + 1;
                string label = "jml_func_" + functionIndex++;
                string[] parameters = args.Skip(2).ToArray();
                string[] returnArgs = Utils.GetArguments(lines[returnIndex]);
                int returnCount = Math.Max(0, returnArgs.Length - 1);

                functions[name] = new NativeFunctionDefinition(name, declarationLine, start, end, label, parameters, returnCount);
                i = returnIndex;
            }

            foreach (NativeFunctionDefinition function in functions.Values.ToArray())
            {
                string[] returnArgs = Utils.GetArguments(lines[function.End - 1]);
                string[] returnTokens = returnArgs.Skip(1).ToArray();
                if (LooksLikeFunctionCall(returnTokens, functions))
                {
                    NativeFunctionDefinition returnedFunction = functions[returnTokens[0]];
                    functions[function.Name] = function with { ReturnCount = returnedFunction.ReturnCount };
                }
            }

            return functions;
        }

        private static bool LooksLikeDuplicateFunctionBlock(string[] lines, int startIndex)
        {
            for (int i = startIndex; i < lines.Length; i++)
            {
                if (Utils.ShouldSkipLine(lines[i]))
                    continue;

                string[] args = Utils.GetArguments(lines[i]);
                if (args.Length == 0)
                    continue;

                if (args[0] == "음...")
                    return true;

                if (args[0] == "엘릭서")
                    return false;
            }

            return false;
        }

        private static Dictionary<int, JmlStmt> ParseStatements(string[] lines, IReadOnlyDictionary<string, NativeFunctionDefinition> functions)
        {
            Dictionary<int, JmlStmt> statements = [];

            for (int i = 0; i < lines.Length; i++)
            {
                int line = i + 1;
                if (Utils.ShouldSkipLine(lines[i]))
                    continue;

                string[] args = Utils.GetArguments(lines[i]);
                if (args.Length == 0 || string.IsNullOrEmpty(args[0]))
                    continue;

                statements[line] = args[0] switch
                {
                    "안산" => ParsePrint(line, args, functions),
                    "재민" => new InputStmt(line, args, RequireArg(args, 1, i)),
                    "그램" => ParseAssign(line, args, functions),
                    "러스트" => ParseBranch(line, args, functions),
                    "엘릭서" => ParseFunction(line, args, functions),
                    "음..." => ParseReturn(line, args, functions),
                    _ => throw new ArgumentException($"{line}번째 줄에 모르는 명령어가 있잖아;; {args[0]}")
                };
            }

            return statements;
        }

        private static PrintStmt ParsePrint(int line, string[] args, IReadOnlyDictionary<string, NativeFunctionDefinition> functions)
        {
            if (args.Length < 2)
                throw new ArgumentException($"{line}번째 줄 안산에 인수가 없잖아;;");

            string[] tokens = args.Skip(1).ToArray();
            IReadOnlyList<Expr> values = LooksLikeFunctionCall(tokens, functions)
                ? [new CallExpr(tokens[0], tokens.Skip(1).Select(token => ParseExpr(token, functions, false)).ToArray())]
                : tokens.Select(token => ParseExpr(token, functions, false)).ToArray();

            return new PrintStmt(line, args, values);
        }

        private static AssignStmt ParseAssign(int line, string[] args, IReadOnlyDictionary<string, NativeFunctionDefinition> functions)
        {
            string key = RequireArg(args, 1, line - 1);
            string[] valueTokens = args.Skip(2).ToArray();
            LValue target;

            if (key.StartsWith('[') && key.EndsWith(']'))
                target = new ArrayLValue(key[1..^1]);
            else if (TryParseArrayAccess(key, out string arrayName, out int[] arrayIndices))
                target = new ArrayElementLValue(arrayName, arrayIndices);
            else
                target = new VariableLValue(key);

            IReadOnlyList<Expr> values = LooksLikeFunctionCall(valueTokens, functions)
                ? [new CallExpr(valueTokens[0], valueTokens.Skip(1).Select(token => ParseExpr(token, functions, false)).ToArray())]
                : valueTokens.Select(token => ParseExpr(token, functions, true)).ToArray();

            return new AssignStmt(line, args, target, values);
        }

        private static BranchNotEqualStmt ParseBranch(int line, string[] args, IReadOnlyDictionary<string, NativeFunctionDefinition> functions)
        {
            string left = RequireArg(args, 1, line - 1);
            string right = RequireArg(args, 2, line - 1);
            int targetLine = ParseLineNumber(RequireArg(args, 3, line - 1), line - 1);

            return new BranchNotEqualStmt(line, args, ParseExpr(left, functions, false), ParseExpr(right, functions, false), targetLine);
        }

        private static FunctionStmt ParseFunction(int line, string[] args, IReadOnlyDictionary<string, NativeFunctionDefinition> functions)
        {
            string name = RequireArg(args, 1, line - 1);
            bool isDeclaration = functions.TryGetValue(name, out NativeFunctionDefinition? function) &&
                function.DeclarationLine == line;

            return new FunctionStmt(
                line,
                args,
                name,
                args.Skip(2).Select(token => ParseExpr(token, functions, false)).ToArray(),
                isDeclaration);
        }

        private static ReturnStmt ParseReturn(int line, string[] args, IReadOnlyDictionary<string, NativeFunctionDefinition> functions)
        {
            string[] tokens = args.Skip(1).ToArray();
            IReadOnlyList<Expr> values = LooksLikeFunctionCall(tokens, functions)
                ? [new CallExpr(tokens[0], tokens.Skip(1).Select(token => ParseExpr(token, functions, false)).ToArray())]
                : tokens.Select(token => ParseExpr(token, functions, false)).ToArray();

            return new ReturnStmt(line, args, values);
        }

        private static Expr ParseExpr(string token, IReadOnlyDictionary<string, NativeFunctionDefinition> functions, bool allowLegacyPrefix)
        {
            if (token == "여친")
                return new NullExpr();

            if (IsStringLiteral(token))
                return new StringExpr(token[1..^1]);

            if (allowLegacyPrefix && Utils.IsExpression(token) && token.Length > 1)
                return new PrefixBinaryExpr(token[..1], ParseExpr(token[1..], functions, false));

            if (TryParseNumber(token, out long number))
                return new NumberExpr(number);

            if (PrattExpressionParser.LooksLikeExpression(token))
                return PrattExpressionParser.Parse(token);

            if (TryParseArrayAccess(token, out string arrayName, out int[] arrayIndices))
                return new ArrayAccessExpr(arrayName, arrayIndices);

            return new VarExpr(token);
        }

        private static NativeRuntimeFeatures CollectRuntimeFeatures(IEnumerable<JmlStmt> statements)
        {
            NativeRuntimeFeatures features = NativeRuntimeFeatures.None;

            foreach (JmlStmt statement in statements)
            {
                switch (statement)
                {
                    case PrintStmt:
                        features |= NativeRuntimeFeatures.Print;
                        break;
                    case InputStmt:
                        features |= NativeRuntimeFeatures.Input;
                        break;
                    case AssignStmt assign:
                        if (assign.Target is ArrayLValue or ArrayElementLValue)
                            features |= NativeRuntimeFeatures.Array;

                        foreach (Expr expr in assign.Values)
                            features |= CollectRuntimeFeatures(expr);
                        break;
                    case BranchNotEqualStmt branch:
                        features |= NativeRuntimeFeatures.ValueToNumber;
                        features |= CollectRuntimeFeatures(branch.Left);
                        features |= CollectRuntimeFeatures(branch.Right);
                        break;
                    case FunctionStmt function:
                        foreach (Expr expr in function.Arguments)
                            features |= CollectRuntimeFeatures(expr);
                        break;
                    case ReturnStmt ret:
                        foreach (Expr expr in ret.Values)
                            features |= CollectRuntimeFeatures(expr);
                        break;
                }
            }

            return features;
        }

        private static NativeRuntimeFeatures CollectRuntimeFeatures(Expr expr)
        {
            return expr switch
            {
                ArrayAccessExpr => NativeRuntimeFeatures.Array,
                CallExpr call => call.Args.Aggregate(NativeRuntimeFeatures.None, (features, arg) => features | CollectRuntimeFeatures(arg)),
                PrefixBinaryExpr prefix => NativeRuntimeFeatures.ValueToNumber |
                    (prefix.Op == "^" ? NativeRuntimeFeatures.Pow : NativeRuntimeFeatures.None) |
                    CollectRuntimeFeatures(prefix.Right),
                BinaryExpr binary => NativeRuntimeFeatures.ValueToNumber |
                    (binary.Op == "^" ? NativeRuntimeFeatures.Pow : NativeRuntimeFeatures.None) |
                    CollectRuntimeFeatures(binary.Left) |
                    CollectRuntimeFeatures(binary.Right),
                _ => NativeRuntimeFeatures.None
            };
        }

        private static Dictionary<int, string> BuildLineContexts(IReadOnlyDictionary<string, NativeFunctionDefinition> functions)
        {
            Dictionary<int, string> contexts = [];

            foreach (NativeFunctionDefinition function in functions.Values)
            {
                for (int line = function.Start; line <= function.End; line++)
                    contexts[line] = function.Label;
            }

            return contexts;
        }

        private static void ValidateBranches(
            string[] lines,
            IReadOnlyDictionary<int, JmlStmt> statements,
            IReadOnlyDictionary<string, NativeFunctionDefinition> functions,
            IReadOnlyDictionary<int, int> functionBlocks,
            IReadOnlyDictionary<int, string> lineContexts)
        {
            foreach (BranchNotEqualStmt branch in statements.Values.OfType<BranchNotEqualStmt>())
            {
                if (branch.TargetLine <= 0 || branch.TargetLine > lines.Length)
                    throw new ArgumentException($"{branch.Line}번째 줄 이동할 줄 번호가 이상하잖아;;");

                if (!statements.ContainsKey(branch.TargetLine))
                    throw new ArgumentException($"{branch.Line}번째 줄 러스트 대상은 실행 가능한 줄이어야 하잖아;;");

                string sourceContext = lineContexts.TryGetValue(branch.Line, out string? functionContext)
                    ? functionContext
                    : MainContext;
                string targetContext = lineContexts.TryGetValue(branch.TargetLine, out string? targetFunctionContext)
                    ? targetFunctionContext
                    : MainContext;

                if (sourceContext != targetContext)
                    throw new ArgumentException($"{branch.Line}번째 줄 러스트가 다른 함수/메인 영역으로 점프하려고 하잖아;;");

                if (sourceContext == MainContext && IsInsideFunctionBlock(branch.TargetLine, functionBlocks))
                    throw new ArgumentException($"{branch.Line}번째 줄 러스트가 함수 선언 안으로 점프하려고 하잖아;;");

                if (functions.Values.Any(function => function.DeclarationLine == branch.TargetLine))
                    throw new ArgumentException($"{branch.Line}번째 줄 러스트가 함수 선언 줄로 점프하려고 하잖아;;");
            }
        }

        private static (Dictionary<string, HashSet<int>> LabelLines, Dictionary<string, HashSet<int>> DeadLines) BuildControlFlow(
            string[] lines,
            IReadOnlyDictionary<int, JmlStmt> statements,
            IReadOnlyDictionary<string, NativeFunctionDefinition> functions,
            IReadOnlyDictionary<int, int> functionBlocks,
            IReadOnlyDictionary<int, string> lineContexts)
        {
            Dictionary<string, HashSet<int>> labelLines = [];
            Dictionary<string, HashSet<int>> deadLines = [];
            BuildControlFlowForContext(MainContext, GetContextLines(lines, statements, functionBlocks, lineContexts, MainContext), statements, labelLines, deadLines);

            foreach (NativeFunctionDefinition function in functions.Values)
                BuildControlFlowForContext(function.Label, GetContextLines(lines, statements, functionBlocks, lineContexts, function.Label), statements, labelLines, deadLines);

            return (labelLines, deadLines);
        }

        private static List<int> GetContextLines(
            string[] lines,
            IReadOnlyDictionary<int, JmlStmt> statements,
            IReadOnlyDictionary<int, int> functionBlocks,
            IReadOnlyDictionary<int, string> lineContexts,
            string context)
        {
            List<int> result = [];

            for (int line = 1; line <= lines.Length; line++)
            {
                if (!statements.ContainsKey(line))
                    continue;

                if (context == MainContext)
                {
                    if (lineContexts.ContainsKey(line) || functionBlocks.ContainsKey(line))
                        continue;
                }
                else if (!lineContexts.TryGetValue(line, out string? lineContext) || lineContext != context)
                {
                    continue;
                }

                result.Add(line);
            }

            return result;
        }

        private static void BuildControlFlowForContext(
            string context,
            List<int> lines,
            IReadOnlyDictionary<int, JmlStmt> statements,
            Dictionary<string, HashSet<int>> labelLines,
            Dictionary<string, HashSet<int>> deadLines)
        {
            labelLines[context] = [];
            deadLines[context] = [];

            if (lines.Count == 0)
                return;

            HashSet<int> leaders = [lines[0]];
            Dictionary<int, int> nextLine = [];

            for (int i = 0; i < lines.Count - 1; i++)
                nextLine[lines[i]] = lines[i + 1];

            foreach (int line in lines)
            {
                if (statements[line] is BranchNotEqualStmt branch)
                {
                    leaders.Add(branch.TargetLine);
                    if (nextLine.TryGetValue(line, out int fallthrough))
                        leaders.Add(fallthrough);
                }
            }

            List<int> sortedLeaders = leaders.Order().ToList();
            Dictionary<int, int> lineToBlock = [];
            List<BasicBlock> blocks = [];

            for (int i = 0; i < sortedLeaders.Count; i++)
            {
                int start = sortedLeaders[i];
                int endExclusive = i + 1 < sortedLeaders.Count ? sortedLeaders[i + 1] : int.MaxValue;
                BasicBlock block = new(i, start);

                foreach (int line in lines.Where(candidate => candidate >= start && candidate < endExclusive))
                {
                    block.Lines.Add(line);
                    lineToBlock[line] = block.Id;
                }

                if (block.Lines.Count > 0)
                    blocks.Add(block);
            }

            foreach (BasicBlock block in blocks)
            {
                int lastLine = block.Lines[^1];
                JmlStmt last = statements[lastLine];

                if (last is ReturnStmt)
                    continue;

                if (last is BranchNotEqualStmt branch)
                {
                    bool? constantResult = TryEvaluateBranch(branch);
                    if (constantResult != false && lineToBlock.TryGetValue(branch.TargetLine, out int targetBlock))
                        block.Successors.Add(targetBlock);

                    if (constantResult != true &&
                        nextLine.TryGetValue(lastLine, out int fallthroughLine) &&
                        lineToBlock.TryGetValue(fallthroughLine, out int fallthroughBlock))
                    {
                        block.Successors.Add(fallthroughBlock);
                    }

                    continue;
                }

                if (nextLine.TryGetValue(lastLine, out int next) && lineToBlock.TryGetValue(next, out int nextBlock))
                    block.Successors.Add(nextBlock);
            }

            HashSet<int> reachableBlocks = [];
            Stack<int> work = new();
            work.Push(blocks[0].Id);

            while (work.Count > 0)
            {
                int id = work.Pop();
                if (!reachableBlocks.Add(id))
                    continue;

                foreach (int successor in blocks.First(block => block.Id == id).Successors)
                    work.Push(successor);
            }

            foreach (BasicBlock block in blocks)
            {
                if (reachableBlocks.Contains(block.Id))
                    labelLines[context].Add(block.StartLine);
                else
                    foreach (int line in block.Lines)
                        deadLines[context].Add(line);
            }
        }

        private static bool? TryEvaluateBranch(BranchNotEqualStmt branch)
        {
            if (branch.Left is NumberExpr left && branch.Right is NumberExpr right)
                return left.ScaledValue != right.ScaledValue;

            if (ExpressionPlanner.TryFoldToNumber(branch.Left, out long foldedLeft) &&
                ExpressionPlanner.TryFoldToNumber(branch.Right, out long foldedRight))
            {
                return foldedLeft != foldedRight;
            }

            return null;
        }

        private static bool IsInsideFunctionBlock(int line, IReadOnlyDictionary<int, int> functionBlocks)
        {
            return functionBlocks.Any(block => line >= block.Key && line <= block.Value);
        }

        private static bool LooksLikeFunctionCall(string[] tokens, IReadOnlyDictionary<string, NativeFunctionDefinition> functions)
        {
            return tokens.Length > 0 && functions.ContainsKey(tokens[0]);
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

        private static bool TryParseNumber(string token, out long value)
        {
            if (double.TryParse(token, NumberStyles.Float, Culture, out double parsed))
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

        private sealed class BasicBlock(int id, int startLine)
        {
            public int Id { get; } = id;
            public int StartLine { get; } = startLine;
            public List<int> Lines { get; } = [];
            public List<int> Successors { get; } = [];
        }
    }

    internal sealed record VReg(int Id);

    internal abstract record ExprIrInstr(int Index, VReg Dest);

    internal sealed record LoadExprIrInstr(int Index, VReg Dest, Expr Source) : ExprIrInstr(Index, Dest);

    internal sealed record BinaryExprIrInstr(int Index, VReg Dest, string Op, VReg Left, VReg Right) : ExprIrInstr(Index, Dest);

    internal sealed record ExpressionPlan(
        IReadOnlyList<ExprIrInstr> Instructions,
        VReg Result,
        IReadOnlyDictionary<VReg, string> Registers);

    internal static class ExpressionPlanner
    {
        public static ExpressionPlan Build(Expr expr, IReadOnlyList<string> physicalRegisters)
        {
            Expr folded = Fold(expr);
            List<ExprIrInstr> instructions = [];
            int nextReg = 0;
            VReg result = Lower(folded, instructions, ref nextReg);
            IReadOnlyDictionary<VReg, string> registers = Allocate(instructions, physicalRegisters);
            return new ExpressionPlan(instructions, result, registers);
        }

        public static bool TryFoldToNumber(Expr expr, out long value)
        {
            Expr folded = Fold(expr);
            if (folded is NumberExpr number)
            {
                value = number.ScaledValue;
                return true;
            }

            value = 0;
            return false;
        }

        private static VReg Lower(Expr expr, List<ExprIrInstr> instructions, ref int nextReg)
        {
            if (expr is BinaryExpr binary)
            {
                VReg left = Lower(binary.Left, instructions, ref nextReg);
                VReg right = Lower(binary.Right, instructions, ref nextReg);
                VReg dest = new(nextReg++);
                instructions.Add(new BinaryExprIrInstr(instructions.Count, dest, binary.Op, left, right));
                return dest;
            }

            VReg loadDest = new(nextReg++);
            instructions.Add(new LoadExprIrInstr(instructions.Count, loadDest, expr));
            return loadDest;
        }

        private static IReadOnlyDictionary<VReg, string> Allocate(IReadOnlyList<ExprIrInstr> instructions, IReadOnlyList<string> physicalRegisters)
        {
            Dictionary<VReg, LiveInterval> intervals = [];

            foreach (ExprIrInstr instruction in instructions)
            {
                EnsureInterval(intervals, instruction.Dest, instruction.Index).Start = instruction.Index;

                if (instruction is BinaryExprIrInstr binary)
                {
                    EnsureInterval(intervals, binary.Left, instruction.Index).End = instruction.Index;
                    EnsureInterval(intervals, binary.Right, instruction.Index).End = instruction.Index;
                }
            }

            foreach (ExprIrInstr instruction in instructions)
                EnsureInterval(intervals, instruction.Dest, instruction.Index).End = Math.Max(intervals[instruction.Dest].End, instruction.Index);

            List<LiveInterval> sorted = intervals.Values.OrderBy(interval => interval.Start).ToList();
            List<LiveInterval> active = [];
            Dictionary<VReg, string> assignments = [];
            Queue<string> free = new(physicalRegisters);

            foreach (LiveInterval current in sorted)
            {
                foreach (LiveInterval expired in active.Where(interval => interval.End < current.Start).ToArray())
                {
                    active.Remove(expired);
                    free.Enqueue(assignments[expired.Reg]);
                }

                if (free.Count == 0)
                    throw new InvalidOperationException("표현식이 너무 복잡해서 레지스터가 부족하잖아;;");

                assignments[current.Reg] = free.Dequeue();
                active.Add(current);
                active.Sort((left, right) => left.End.CompareTo(right.End));
            }

            return assignments;
        }

        private static LiveInterval EnsureInterval(Dictionary<VReg, LiveInterval> intervals, VReg reg, int index)
        {
            if (intervals.TryGetValue(reg, out LiveInterval? interval))
                return interval;

            interval = new LiveInterval(reg, index, index);
            intervals[reg] = interval;
            return interval;
        }

        private static Expr Fold(Expr expr)
        {
            if (expr is not BinaryExpr binary)
                return expr;

            Expr left = Fold(binary.Left);
            Expr right = Fold(binary.Right);

            if (left is NumberExpr leftNumber && right is NumberExpr rightNumber)
                return new NumberExpr(FoldBinary(binary.Op, leftNumber.ScaledValue, rightNumber.ScaledValue));

            return binary with { Left = left, Right = right };
        }

        private static long FoldBinary(string op, long left, long right)
        {
            return op switch
            {
                "+" => checked(left + right),
                "-" => checked(left - right),
                "*" => checked(left * right / JMLNativeParser.NumberScale),
                "/" => checked(left * JMLNativeParser.NumberScale / right),
                "^" => PowFixed(left, right),
                _ => throw new ArgumentException("이런 수식은 안산에도 없어;; " + op)
            };
        }

        private static long PowFixed(long baseValue, long exponentValue)
        {
            long exp = exponentValue / JMLNativeParser.NumberScale;
            bool negative = exp < 0;
            if (negative)
                exp = -exp;

            long result = JMLNativeParser.NumberScale;
            long current = baseValue;

            while (exp > 0)
            {
                if ((exp & 1) != 0)
                    result = checked(result * current / JMLNativeParser.NumberScale);

                current = checked(current * current / JMLNativeParser.NumberScale);
                exp >>= 1;
            }

            return negative
                ? checked(JMLNativeParser.NumberScale * JMLNativeParser.NumberScale / result)
                : result;
        }

        private sealed class LiveInterval(VReg reg, int start, int end)
        {
            public VReg Reg { get; } = reg;
            public int Start { get; set; } = start;
            public int End { get; set; } = end;
        }
    }

    internal sealed class PrattExpressionParser
    {
        private readonly List<Token> _tokens;
        private int _position;

        private PrattExpressionParser(string source)
        {
            _tokens = Tokenize(source);
        }

        public static bool LooksLikeExpression(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
                return false;

            bool hasOperator = false;
            bool hasOperandBeforeOperator = false;
            bool seenOperand = false;

            foreach (char c in source)
            {
                if (char.IsWhiteSpace(c))
                    continue;

                if ("+-*/^()".Contains(c))
                {
                    hasOperator = true;
                    if (seenOperand)
                        hasOperandBeforeOperator = true;
                    continue;
                }

                seenOperand = true;
            }

            return hasOperator && hasOperandBeforeOperator;
        }

        public static Expr Parse(string source)
        {
            PrattExpressionParser parser = new(source);
            Expr expr = parser.ParseExpression(0);
            if (parser.Peek().Kind != TokenKind.End)
                throw new ArgumentException("표현식 파싱이 이상하잖아;; " + source);

            return expr;
        }

        private Expr ParseExpression(int minPrecedence)
        {
            Expr left = ParsePrefix();

            while (true)
            {
                Token token = Peek();
                if (token.Kind != TokenKind.Operator)
                    break;

                int precedence = GetPrecedence(token.Text);
                if (precedence < minPrecedence)
                    break;

                string op = Consume().Text;
                int nextMin = op == "^" ? precedence : precedence + 1;
                Expr right = ParseExpression(nextMin);
                left = new BinaryExpr(op, left, right);
            }

            return left;
        }

        private Expr ParsePrefix()
        {
            Token token = Consume();
            return token.Kind switch
            {
                TokenKind.Number => new NumberExpr(checked((long)Math.Round(double.Parse(token.Text, CultureInfo.InvariantCulture) * JMLNativeParser.NumberScale))),
                TokenKind.Identifier => ParseIdentifier(token.Text),
                TokenKind.Operator when token.Text == "-" => new BinaryExpr("-", new NumberExpr(0), ParseExpression(GetPrecedence("-") + 1)),
                TokenKind.Operator when token.Text == "+" => ParseExpression(GetPrecedence("+") + 1),
                TokenKind.LeftParen => ParseParenthesized(),
                _ => throw new ArgumentException("표현식이 이상하잖아;;")
            };
        }

        private Expr ParseParenthesized()
        {
            Expr expr = ParseExpression(0);
            if (Consume().Kind != TokenKind.RightParen)
                throw new ArgumentException("괄호가 안 닫혔잖아;;");

            return expr;
        }

        private static Expr ParseIdentifier(string text)
        {
            string[] parts = text.Split('.');
            if (parts.Length > 1 && parts.Skip(1).All(part => int.TryParse(part, out _)))
                return new ArrayAccessExpr(parts[0], parts.Skip(1).Select(int.Parse).ToArray());

            return new VarExpr(text);
        }

        private Token Peek()
        {
            return _tokens[_position];
        }

        private Token Consume()
        {
            return _tokens[_position++];
        }

        private static int GetPrecedence(string op)
        {
            return op switch
            {
                "+" or "-" => 10,
                "*" or "/" => 20,
                "^" => 30,
                _ => -1
            };
        }

        private static List<Token> Tokenize(string source)
        {
            List<Token> tokens = [];

            for (int i = 0; i < source.Length;)
            {
                char c = source[i];
                if (char.IsWhiteSpace(c))
                {
                    i++;
                    continue;
                }

                if ("+-*/^".Contains(c))
                {
                    tokens.Add(new Token(TokenKind.Operator, c.ToString()));
                    i++;
                    continue;
                }

                if (c == '(')
                {
                    tokens.Add(new Token(TokenKind.LeftParen, "("));
                    i++;
                    continue;
                }

                if (c == ')')
                {
                    tokens.Add(new Token(TokenKind.RightParen, ")"));
                    i++;
                    continue;
                }

                if (char.IsDigit(c) || c == '.')
                {
                    int start = i;
                    while (i < source.Length && (char.IsDigit(source[i]) || source[i] == '.'))
                        i++;

                    tokens.Add(new Token(TokenKind.Number, source[start..i]));
                    continue;
                }

                int identifierStart = i;
                while (i < source.Length && !char.IsWhiteSpace(source[i]) && !"+-*/^()".Contains(source[i]))
                    i++;

                tokens.Add(new Token(TokenKind.Identifier, source[identifierStart..i]));
            }

            tokens.Add(new Token(TokenKind.End, ""));
            return tokens;
        }

        private enum TokenKind
        {
            Number,
            Identifier,
            Operator,
            LeftParen,
            RightParen,
            End
        }

        private sealed record Token(TokenKind Kind, string Text);
    }
}
