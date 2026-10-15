using FluentValidation;

namespace AppTemplate.Application.Common;

// Shape rules for text that is stored or looked up, shared by the validators. Postgres refuses a NUL in a text value
// (SQLSTATE 22021): without these, such input passes validation and fails as a 500 in the first query or insert.
public static class TextRules
{
    // No control character (NUL, a line break, a tab): for names and other one-line text.
    public static IRuleBuilderOptions<T, string?> WithoutControlCharacters<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(text => text is null || !text.Any(char.IsControl));

    // No control character and no white space at all: for identifiers a person types, such as an email address,
    // where a space is a typo that would otherwise be stored as part of the value.
    public static IRuleBuilderOptions<T, string?> WithoutWhiteSpace<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(text => text is null || !text.Any(c => char.IsControl(c) || char.IsWhiteSpace(c)));
}
