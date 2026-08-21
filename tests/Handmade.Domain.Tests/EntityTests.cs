using Handmade.Domain.Common;
using Handmade.Domain.Exceptions;

namespace Handmade.Domain.Tests;

public sealed class EntityTests
{
    [Fact]
    public void CreateId_ReturnsVersion7Guid()
    {
        TestEntity entity = TestEntity.Create();

        Assert.Equal(7, entity.Id.Version);
        Assert.NotEqual(Guid.Empty, entity.Id);
    }

    [Fact]
    public void Equals_SameIdAndType_ReturnsTrue()
    {
        Guid id = Guid.CreateVersion7();
        TestEntity left = TestEntity.Create(id);
        TestEntity right = TestEntity.Create(id);

        Assert.Equal(left, right);
        Assert.True(left == right);
    }

    [Fact]
    public void Equals_DifferentId_ReturnsFalse()
    {
        TestEntity left = TestEntity.Create();
        TestEntity right = TestEntity.Create();

        Assert.NotEqual(left, right);
    }

    private sealed class TestEntity : AggregateRoot
    {
        private TestEntity(Guid id)
            : base(id)
        {
        }

        public static TestEntity Create() => new(CreateId());

        public static TestEntity Create(Guid id) => new(id);
    }
}

public sealed class DomainExceptionTests
{
    [Fact]
    public void NotFoundException_SetsCode()
    {
        NotFoundException exception = new("Artwork", Guid.CreateVersion7());

        Assert.Equal("not_found", exception.Code);
        Assert.Equal("Artwork", exception.ResourceName);
    }

    [Fact]
    public void ConflictException_SetsCode()
    {
        ConflictException exception = new("Already exists");

        Assert.Equal("conflict", exception.Code);
        Assert.Equal("Already exists", exception.Message);
    }
}
