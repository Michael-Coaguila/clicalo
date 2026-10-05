namespace Clicalo.Domain.Errors;

/// <summary>Creates <see cref="Result{T}"/> values (static members cannot live on the generic type, CA1000).</summary>
public static class Results
{
    /// <summary>A success.</summary>
    /// <typeparam name="T">Type of the value.</typeparam>
    /// <param name="value">The value.</param>
    public static Result<T> Ok<T>(T value) => new(value, null);

    /// <summary>A failure.</summary>
    /// <typeparam name="T">Type of the value the operation would have produced.</typeparam>
    /// <param name="failure">Why it failed.</param>
    public static Result<T> Fail<T>(Failure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new(default!, failure);
    }
}
