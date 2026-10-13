using AppTemplate.SharedKernel;
using Shouldly;

namespace AppTemplate.Domain.Tests;

// Enumeration<TEnum> lives in SharedKernel, which has no test project of its own; every smart enum and the EF
// converter (stored by Name, ADR 0005) rely on these lookups.
public class EnumerationTests
{
    [Fact]
    public void FromValue_KnownValue_ReturnsThatInstance()
    {
        // Act
        var size = Size.FromValue(2);

        // Assert
        size.ShouldBeSameAs(Size.Large);
    }

    [Fact]
    public void FromValue_UnknownValue_ReturnsNull()
    {
        // Act
        var size = Size.FromValue(99);

        // Assert
        size.ShouldBeNull();
    }

    [Theory]
    [InlineData("Large")]
    [InlineData("large")]
    [InlineData("LARGE")]
    public void FromName_KnownNameInAnyCase_ReturnsThatInstance(string name)
    {
        // Act
        var size = Size.FromName(name);

        // Assert
        size.ShouldBeSameAs(Size.Large);
    }

    // A lookup miss, not an error: callers map null to their own error (e.g. the validator's INVALID).
    [Theory]
    [InlineData("Huge")]
    [InlineData("")]
    [InlineData(null)]
    public void FromName_NoMatch_ReturnsNull(string? name)
    {
        // Act
        var size = Size.FromName(name);

        // Assert
        size.ShouldBeNull();
    }

    [Fact]
    public void GetAll_ReturnsEveryDeclaredValueOnce()
    {
        // Act
        IReadOnlyCollection<Size> all = Size.GetAll();

        // Assert
        all.ShouldBe([Size.Small, Size.Large], ignoreOrder: true);
    }

    [Fact]
    public void Equals_SameValueAndType_IsEqual()
    {
        // Arrange
        Size fromLookup = Size.FromValue(1)!;

        // Act & Assert
        fromLookup.Equals(Size.Small).ShouldBeTrue();
        fromLookup.Equals((object)Size.Small).ShouldBeTrue();
        fromLookup.GetHashCode().ShouldBe(Size.Small.GetHashCode());
    }

    [Fact]
    public void Equals_DifferentValue_IsNotEqual()
    {
        // Act & Assert
        Size.Small.Equals(Size.Large).ShouldBeFalse();
    }

    [Fact]
    public void Equals_SameValueButDifferentType_IsNotEqual()
    {
        // Act & Assert: Small (1) and Red (1) share a value, not a meaning.
        Size.Small.Equals(Color.Red).ShouldBeFalse();
    }

    [Fact]
    public void Equals_Null_IsNotEqual()
    {
        // Act & Assert
        Size.Small.Equals(null).ShouldBeFalse();
        Size.Small.Equals((object?)null).ShouldBeFalse();
    }

    [Fact]
    public void ToString_ReturnsName()
    {
        // Act & Assert
        Size.Large.ToString().ShouldBe("Large");
    }

    // Each broken enum is its own type: the runtime caches a failed type initialization, so one type per test.
    [Fact]
    public void FirstLookup_DuplicateName_ThrowsTypeInitializationException()
    {
        // Act
        TypeInitializationException exception =
            Should.Throw<TypeInitializationException>(() => DuplicateName.GetAll());

        // Assert
        exception.InnerException.ShouldBeOfType<InvalidOperationException>();
    }

    [Fact]
    public void FirstLookup_NamesDifferingOnlyInCase_ThrowsTypeInitializationException()
    {
        // Act
        TypeInitializationException exception =
            Should.Throw<TypeInitializationException>(() => DuplicateNameIgnoringCase.GetAll());

        // Assert
        exception.InnerException.ShouldBeOfType<InvalidOperationException>();
    }

    [Fact]
    public void FirstLookup_DuplicateValue_ThrowsTypeInitializationException()
    {
        // Act
        TypeInitializationException exception =
            Should.Throw<TypeInitializationException>(() => DuplicateValue.GetAll());

        // Assert
        exception.InnerException.ShouldBeOfType<InvalidOperationException>();
    }

    private sealed class Size(int value, string name) : Enumeration<Size>(value, name)
    {
        public static readonly Size Small = new(1, nameof(Small));
        public static readonly Size Large = new(2, nameof(Large));
    }

    private sealed class Color(int value, string name) : Enumeration<Color>(value, name)
    {
        public static readonly Color Red = new(1, nameof(Red));
    }

    private sealed class DuplicateName(int value, string name) : Enumeration<DuplicateName>(value, name)
    {
        public static readonly DuplicateName First = new(1, "Same");
        public static readonly DuplicateName Second = new(2, "Same");
    }

    private sealed class DuplicateNameIgnoringCase(int value, string name)
        : Enumeration<DuplicateNameIgnoringCase>(value, name)
    {
        public static readonly DuplicateNameIgnoringCase First = new(1, "Same");
        public static readonly DuplicateNameIgnoringCase Second = new(2, "SAME");
    }

    private sealed class DuplicateValue(int value, string name) : Enumeration<DuplicateValue>(value, name)
    {
        public static readonly DuplicateValue First = new(1, nameof(First));
        public static readonly DuplicateValue Second = new(1, nameof(Second));
    }
}
