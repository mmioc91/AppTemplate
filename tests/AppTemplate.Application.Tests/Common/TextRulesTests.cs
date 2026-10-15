using AppTemplate.Application.Common;
using FluentValidation;
using FluentValidation.TestHelper;

namespace AppTemplate.Application.Tests.Common;

public class TextRulesTests
{
    [Theory]
    [InlineData("Line one\nLine two")]
    [InlineData("Column\tvalue\r\n")]
    public void WithoutNulCharacters_MultiLineText_IsAccepted(string text)
    {
        // Act
        TestValidationResult<Note> result = new NoteValidator().TestValidate(new Note(text));

        // Assert: multi-line text keeps its line breaks and tabs.
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void WithoutNulCharacters_TextWithANul_IsInvalid()
    {
        // Act
        TestValidationResult<Note> result = new NoteValidator().TestValidate(new Note("Line one\0"));

        // Assert
        result.ShouldHaveValidationErrorFor(n => n.Text).WithErrorCode(ValidationCodes.Invalid).Only();
    }

    [Theory]
    [InlineData("Ana\n")]
    [InlineData("Ana\tAnić")]
    public void WithoutControlCharacters_OneLineTextWithALineBreakOrTab_IsInvalid(string text)
    {
        // Act
        TestValidationResult<Note> result = new TitleValidator().TestValidate(new Note(text));

        // Assert
        result.ShouldHaveValidationErrorFor(n => n.Text).WithErrorCode(ValidationCodes.Invalid).Only();
    }

    [Fact]
    public void WithoutControlCharacters_OneLineTextWithSpaces_IsAccepted()
    {
        // Act
        TestValidationResult<Note> result = new TitleValidator().TestValidate(new Note("Ana Marija Anić"));

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    public sealed record Note(string? Text);

    private sealed class NoteValidator : AbstractValidator<Note>
    {
        public NoteValidator() => RuleFor(n => n.Text).WithoutNulCharacters().WithErrorCode(ValidationCodes.Invalid);
    }

    private sealed class TitleValidator : AbstractValidator<Note>
    {
        public TitleValidator() => RuleFor(n => n.Text).WithoutControlCharacters().WithErrorCode(ValidationCodes.Invalid);
    }
}
