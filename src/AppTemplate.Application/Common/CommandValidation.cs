using FluentValidation;
using FluentValidation.Results;

namespace AppTemplate.Application.Common;

// Shared by both validation decorators: runs every validator for the command and turns the failures into one
// Validation error (field → error codes, ADR 0012), or null when the command is valid.
internal static class CommandValidation
{
    public static async Task<Error?> ValidateAsync<TCommand>(
        TCommand command,
        IEnumerable<IValidator<TCommand>> validators,
        CancellationToken cancellationToken)
    {
        List<ValidationFailure> failures = [];

        foreach (IValidator<TCommand> validator in validators)
        {
            ValidationResult result = await validator.ValidateAsync(command, cancellationToken);
            failures.AddRange(result.Errors);
        }

        if (failures.Count == 0)
        {
            return null;
        }

        var fieldErrors = failures
            .GroupBy(f => f.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorCode).Distinct().ToArray());

        return Error.Validation(fieldErrors);
    }
}
