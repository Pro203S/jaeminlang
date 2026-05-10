namespace jaeminlang
{
    public sealed class JMLReturnSignal : Exception
    {
        public JMLReturnSignal(object?[] values)
        {
            Values = values;
        }

        public object?[] Values { get; }
    }
}
