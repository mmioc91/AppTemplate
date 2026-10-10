using FluentValidation;

namespace AppTemplate.Application.Tests.Common;

// Input for the validation decorator tests, with one validator per rule so tests can combine them.
public sealed record TestCommand(string? Name, int Count);

internal sealed class NameValidator : AbstractValidator<TestCommand>
{
    public NameValidator() => RuleFor(c => c.Name).NotEmpty().WithErrorCode("NAME_REQUIRED");
}

internal sealed class NameLengthValidator : AbstractValidator<TestCommand>
{
    public NameLengthValidator() => RuleFor(c => c.Name).MinimumLength(3).WithErrorCode("NAME_TOO_SHORT");
}

internal sealed class CountValidator : AbstractValidator<TestCommand>
{
    public CountValidator() => RuleFor(c => c.Count).GreaterThan(0).WithErrorCode("COUNT_NOT_POSITIVE");
}
