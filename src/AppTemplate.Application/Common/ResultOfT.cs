using System.Diagnostics.CodeAnalysis;

namespace AppTemplate.Application.Common;

// Outcome of a use case: a value, or an expected failure (ADR 0008). Invariant breaches stay DomainExceptions.
public sealed class Result<T>
{
    private Result(T? value, Error? error)
    {
        StoredValue = value;
        Error = error;
    }

    public Error? Error { get; }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public T Value => IsSuccess
        ? StoredValue!
        : throw new InvalidOperationException($"A failed result has no value (error {Error.Code}).");

    private T? StoredValue { get; }

    public static Result<T> Success(T value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value), "A successful result needs a value.");
        }

        return new Result<T>(value, null);
    }

    public static Result<T> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(default, error);
    }

    public static implicit operator Result<T>(T value) => Success(value);

    public static implicit operator Result<T>(Error error) => Failure(error);
}
