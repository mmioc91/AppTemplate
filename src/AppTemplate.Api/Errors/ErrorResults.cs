using System.Text.Json;
using AppTemplate.Application.Common;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace AppTemplate.Api.Errors;

// The one place a Result failure becomes HTTP (ADR 0008, 0018): endpoints return error.ToProblem(), never build
// ProblemDetails themselves. Clients get the code; Error.Message is English text for logs only.
internal static class ErrorResults
{
    public static ProblemHttpResult ToProblem(this Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        if (error.Kind == ErrorKind.Validation)
        {
            HttpValidationProblemDetails validation = new(ToJsonFieldNames(error.FieldErrors))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "The request is invalid.",
            };
            validation.Extensions[ProblemCodes.Key] = error.Code;
            return TypedResults.Problem(validation);
        }

        (int status, string title) = error.Kind switch
        {
            ErrorKind.Unauthorized => (StatusCodes.Status401Unauthorized, "Authentication failed."),
            ErrorKind.NotFound => (StatusCodes.Status404NotFound, "The resource was not found."),
            ErrorKind.Conflict => (StatusCodes.Status409Conflict, "The request conflicts with the current state."),
            ErrorKind.Forbidden => (StatusCodes.Status403Forbidden, "The request is not allowed."),
            _ => throw new ArgumentOutOfRangeException(nameof(error), error.Kind, "Unknown error kind."),
        };

        ProblemDetails problem = new() { Status = status, Title = title };
        problem.Extensions[ProblemCodes.Key] = error.Code;
        return TypedResults.Problem(problem);
    }

    // Application reports C# property paths ("Address.Street"); the client sees JSON names ("address.street"), so it
    // can attach each error to its form field. Minimal APIs serialize with camelCase (JsonSerializerDefaults.Web).
    private static Dictionary<string, string[]> ToJsonFieldNames(IReadOnlyDictionary<string, string[]> fieldErrors) =>
        fieldErrors.ToDictionary(
            field => string.Join('.', field.Key.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName)),
            field => field.Value);
}
