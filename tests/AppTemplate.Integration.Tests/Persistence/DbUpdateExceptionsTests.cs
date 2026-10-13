using AppTemplate.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shouldly;

namespace AppTemplate.Integration.Tests.Persistence;

// No database needed: the question is answered from the exception alone (ADR 0013).
public class DbUpdateExceptionsTests
{
    public static TheoryData<Exception?> OtherCauses() => new()
    {
        new PostgresException("violates a foreign key", "ERROR", "ERROR", PostgresErrorCodes.ForeignKeyViolation),
        new InvalidOperationException("not from the database"),
        null,
    };

    [Fact]
    public void IsUniqueViolation_PostgresUniqueViolation_IsTrue()
    {
        // Arrange
        DbUpdateException exception = new("save failed", new PostgresException("duplicate key", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation));

        // Act & Assert
        exception.IsUniqueViolation().ShouldBeTrue();
    }

    [Theory]
    [MemberData(nameof(OtherCauses))]
    public void IsUniqueViolation_AnythingElse_IsFalse(Exception? inner)
    {
        // Arrange
        DbUpdateException exception = new("save failed", inner);

        // Act & Assert
        exception.IsUniqueViolation().ShouldBeFalse();
    }
}
