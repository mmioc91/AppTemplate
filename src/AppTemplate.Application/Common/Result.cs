using System.Diagnostics.CodeAnalysis;

namespace AppTemplate.Application.Common;

// Outcome of a use case that returns nothing: success, or an expected failure (ADR 0008).
public sealed class Result
{
    private static readonly Result SuccessResult = new(null);

    private Result(Error? error) => Error = error;

    public Error? Error { get; }

    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccess => Error is null;

    public static Result Success() => SuccessResult;

    public static Result Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result(error);
    }

    public static implicit operator Result(Error error) => Failure(error);
}
