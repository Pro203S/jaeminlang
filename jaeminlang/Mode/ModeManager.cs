using System.Globalization;
using System.Threading;

namespace jaeminlang.Mode
{
    public enum ExecutionMode
    {
        Default = 0,
        Network = 1
    }

    public static class ModeManager
    {
        private static readonly AsyncLocal<ExecutionMode?> CurrentMode = new();

        public static ExecutionMode Current => CurrentMode.Value ?? ExecutionMode.Default;

        public static void SetMode(int mode)
        {
            if (!Enum.IsDefined(typeof(ExecutionMode), mode))
                throw new ArgumentException("이런 모드는 없잖아;;");

            CurrentMode.Value = (ExecutionMode)mode;
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

        internal static ExecutionMode? GetDeclaredMode(string[] args)
        {
            if (args.Length != 2 ||
                !double.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double rawMode))
            {
                return null;
            }

            if (!double.IsFinite(rawMode) || rawMode != Math.Truncate(rawMode) || rawMode < int.MinValue || rawMode > int.MaxValue)
                throw new ArgumentException("메가커피 모드는 정수여야지;;");

            int mode = (int)rawMode;
            if (!Enum.IsDefined(typeof(ExecutionMode), mode))
                throw new ArgumentException("이런 모드는 없잖아;;");

            return (ExecutionMode)mode;
        }

        internal static IDisposable EnterMode(ExecutionMode mode)
        {
            ExecutionMode previousMode = Current;
            CurrentMode.Value = mode;
            return new ModeScope(previousMode);
        }

        public static void Reset()
        {
            CurrentMode.Value = ExecutionMode.Default;
        }

        private sealed class ModeScope(ExecutionMode previousMode) : IDisposable
        {
            private bool disposed;

            public void Dispose()
            {
                if (disposed)
                    return;

                CurrentMode.Value = previousMode;
                disposed = true;
            }
        }
    }
}
