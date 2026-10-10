using AppTemplate.SharedKernel;
using Shouldly;

namespace AppTemplate.Domain.Tests;

// Enumeration<TEnum> lives in SharedKernel, which has no test project of its own; every smart enum relies on it.
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

    [Fact]
    public void FromName_UnknownName_ReturnsNull()
    {
        // Act
        var size = Size.FromName("Huge");

        // Assert
        size.ShouldBeNull();
    }

    [Fact]
    public void GetAll_ReturnsEveryDeclaredValue()
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

    private sealed class Size(int value, string name) : Enumeration<Size>(value, name)
    {
        public static readonly Size Small = new(1, nameof(Small));
        public static readonly Size Large = new(2, nameof(Large));
    }

    private sealed class Color(int value, string name) : Enumeration<Color>(value, name)
    {
        public static readonly Color Red = new(1, nameof(Red));
    }
}
