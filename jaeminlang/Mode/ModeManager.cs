namespace jaeminlang.Mode
{
    public enum ExecutionMode
    {
        Default = 0,
        Network = 1
    }

    public static class ModeManager
    {
        public static ExecutionMode Current { get; private set; } = ExecutionMode.Default;

        public static void SetMode(int mode)
        {
            if (!Enum.IsDefined(typeof(ExecutionMode), mode))
                throw new ArgumentException("이런 모드는 없잖아;;");

            Current = (ExecutionMode)mode;
        }

        public static void ExecuteSetMode(string[] args)
        {
            if (args.Length < 2)
                throw new NullReferenceException("메가커피에 모드가 없잖아;;");

            if (args.Length > 2)
                throw new ArgumentException("메가커피에 모드는 하나만 줘야지;;");

            double rawMode = Utils.ResolveNumberValue(args[1]);
            if (!double.IsFinite(rawMode) || rawMode != Math.Truncate(rawMode) || rawMode < int.MinValue || rawMode > int.MaxValue)
                throw new ArgumentException("메가커피 모드는 정수여야지;;");

            SetMode((int)rawMode);
        }

        public static void Reset()
        {
            Current = ExecutionMode.Default;
        }
    }
}
