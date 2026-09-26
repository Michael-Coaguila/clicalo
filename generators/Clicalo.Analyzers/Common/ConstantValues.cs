namespace Clicalo.Analyzers.Common;

/// <summary>Classification of the boxed values that <c>IOperation.ConstantValue</c> returns.</summary>
internal static class ConstantValues
{
    /// <summary>True for every numeric constant the compiler folds (integers, floating point and decimal).</summary>
    public static bool IsNumber(object? value) =>
        value
            is sbyte
                or byte
                or short
                or ushort
                or int
                or uint
                or long
                or ulong
                or float
                or double
                or decimal;

    /// <summary>Reads an integral constant (including the underlying value of an enum member) as a 64-bit integer.</summary>
    public static bool TryGetInteger(object? value, out long result)
    {
        switch (value)
        {
            case sbyte v:
                result = v;
                return true;
            case byte v:
                result = v;
                return true;
            case short v:
                result = v;
                return true;
            case ushort v:
                result = v;
                return true;
            case int v:
                result = v;
                return true;
            case uint v:
                result = v;
                return true;
            case long v:
                result = v;
                return true;
            case ulong v when v <= long.MaxValue:
                result = (long)v;
                return true;
            default:
                result = 0;
                return false;
        }
    }
}
