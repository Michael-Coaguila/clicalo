using System.Diagnostics.CodeAnalysis;

namespace Clicalo.Domain.Errors;

/// <summary>
/// The outcome of an operation that can fail in an expected way: a value or a <see cref="Errors.Failure"/>, never an
/// exception (blueprint §6.1, §13). Create it with <see cref="Results"/>. The default value is neither success nor
/// failure: it is a defect, and reading it throws.
/// </summary>
/// <typeparam name="T">Type of the value.</typeparam>
public readonly struct Result<T> : IEquatable<Result<T>>
{
    private readonly T? _value;
    private readonly Failure? _failure;

    internal Result(T value, Failure? failure)
    {
        _value = value;
        _failure = failure;
        IsSuccess = failure is null;
    }

    /// <summary>Whether the operation succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary>Whether the operation failed.</summary>
    public bool IsFailure => _failure is not null;

    /// <summary>The value.</summary>
    /// <exception cref="InvalidOperationException">The result is not a success (a defect in the caller).</exception>
    public T Value =>
        IsSuccess ? _value! : throw new InvalidOperationException("The result is not a success.");

    /// <summary>Why the operation failed.</summary>
    /// <exception cref="InvalidOperationException">The result is not a failure (a defect in the caller).</exception>
    public Failure Failure =>
        _failure ?? throw new InvalidOperationException("The result is not a failure.");

    /// <summary>Whether two results are equal.</summary>
    /// <param name="left">First result.</param>
    /// <param name="right">Second result.</param>
    public static bool operator ==(Result<T> left, Result<T> right) => left.Equals(right);

    /// <summary>Whether two results differ.</summary>
    /// <param name="left">First result.</param>
    /// <param name="right">Second result.</param>
    public static bool operator !=(Result<T> left, Result<T> right) => !left.Equals(right);

    /// <summary>The value when the result is a success.</summary>
    /// <param name="value">The value, or the default when the result is not a success.</param>
    public bool TryGetValue([MaybeNullWhen(false)] out T value)
    {
        value = IsSuccess ? _value! : default;
        return IsSuccess;
    }

    /// <summary>Transforms the value of a success; a failure passes through.</summary>
    /// <typeparam name="TOut">Type of the new value.</typeparam>
    /// <param name="map">Transformation.</param>
    public Result<TOut> Map<TOut>(Func<T, TOut> map)
    {
        ArgumentNullException.ThrowIfNull(map);
        return IsSuccess ? Results.Ok(map(_value!)) : Results.Fail<TOut>(Failure);
    }

    /// <summary>Chains an operation that can fail; a failure passes through.</summary>
    /// <typeparam name="TOut">Type of the new value.</typeparam>
    /// <param name="bind">Next operation.</param>
    public Result<TOut> Bind<TOut>(Func<T, Result<TOut>> bind)
    {
        ArgumentNullException.ThrowIfNull(bind);
        return IsSuccess ? bind(_value!) : Results.Fail<TOut>(Failure);
    }

    /// <inheritdoc />
    public bool Equals(Result<T> other) =>
        IsSuccess == other.IsSuccess
        && EqualityComparer<T?>.Default.Equals(_value, other._value)
        && Equals(_failure, other._failure);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Result<T> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(IsSuccess, _value, _failure);
}
