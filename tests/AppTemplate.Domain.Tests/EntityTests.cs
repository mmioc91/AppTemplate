using AppTemplate.SharedKernel;
using Shouldly;

namespace AppTemplate.Domain.Tests;

// Entity<TId> lives in SharedKernel, which has no test project of its own; every aggregate relies on this equality.
public class EntityTests
{
    private static readonly Guid SomeId = Guid.CreateVersion7();

    [Fact]
    public void Equals_SameIdAndType_IsEqual()
    {
        // Arrange
        Parcel first = new(SomeId);
        Parcel second = new(SomeId);

        // Act & Assert
        first.Equals(second).ShouldBeTrue();
        (first == second).ShouldBeTrue();
        (first != second).ShouldBeFalse();
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Equals_SameInstance_IsEqual()
    {
        // Arrange
        Parcel parcel = new(SomeId);

        // Act & Assert
        parcel.Equals(parcel).ShouldBeTrue();
    }

    [Fact]
    public void Equals_DifferentId_IsNotEqual()
    {
        // Arrange
        Parcel first = new(SomeId);
        Parcel second = new(Guid.CreateVersion7());

        // Act & Assert
        first.Equals(second).ShouldBeFalse();
        (first == second).ShouldBeFalse();
        (first != second).ShouldBeTrue();
    }

    [Fact]
    public void Equals_SameIdButDifferentType_IsNotEqual()
    {
        // Arrange
        Parcel parcel = new(SomeId);
        Letter letter = new(SomeId);

        // Act & Assert
        parcel.Equals(letter).ShouldBeFalse();
        (parcel == letter).ShouldBeFalse();
    }

    [Fact]
    public void Equals_ObjectThatIsNotAnEntity_IsNotEqual()
    {
        // Arrange
        Parcel parcel = new(SomeId);

        // Act & Assert: not even the entity's own id.
        parcel.Equals(SomeId).ShouldBeFalse();
    }

    [Fact]
    public void Equals_Null_IsNotEqual()
    {
        // Arrange
        Parcel parcel = new(SomeId);
        Entity<Guid>? nothing = null;

        // Act & Assert
        parcel.Equals(nothing).ShouldBeFalse();
        (parcel == nothing).ShouldBeFalse();
        (nothing == parcel).ShouldBeFalse();
        (parcel != nothing).ShouldBeTrue();
    }

    [Fact]
    public void EqualityOperator_BothNull_IsEqual()
    {
        // Arrange
        Entity<Guid>? left = null;
        Entity<Guid>? right = null;

        // Act & Assert
        (left == right).ShouldBeTrue();
        (left != right).ShouldBeFalse();
    }

    private sealed class Parcel(Guid id) : Entity<Guid>(id);

    private sealed class Letter(Guid id) : Entity<Guid>(id);
}
