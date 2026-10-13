using FluentValidation;
using FluentValidation.Results;

namespace AppTemplate.Application.Common;

// Shared by the validation decorators: runs every validator for a command or query and turns the failures into one
// Validation error (field → error codes, ADR 0012), or null when the input is valid.
internal static class InputValidation
{
    public static async Task<Error?> ValidateAsync<TInput>(
        TInput input,
        IEnumerable<IValidator<TInput>> validators,
        CancellationToken cancellationToken)
    {
        List<ValidationFailure> failures = [];

        foreach (IValidator<TInput> validator in validators)
        {
            ValidationResult result = await validator.ValidateAsync(input, cancellationToken);
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
