using Fgc.Catalog.Domain.Entities;
using Fgc.Catalog.Domain.Exceptions;

namespace Fgc.Catalog.Tests.Entities
{
    public class OrderTests
    {
        [Fact]
        public void Create_ValidUserIdAndGameId_ReturnsCompletedOrder()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var gameId = Guid.NewGuid();

            // Act
            var order = Order.Create(userId, gameId);

            // Assert
            Assert.NotNull(order);
            Assert.NotEqual(Guid.Empty, order.Id);
            Assert.Equal(userId, order.UserId);
            Assert.Equal(gameId, order.GameId);
            Assert.Equal("Completed", order.Status);
        }

        [Fact]
        public void Create_EmptyUserId_ThrowsCatalogDomainException()
        {
            var ex = Assert.Throws<CatalogDomainException>(() => Order.Create(Guid.Empty, Guid.NewGuid()));
            Assert.Equal("UserId is required.", ex.Message);
        }

        [Fact]
        public void Create_EmptyGameId_ThrowsCatalogDomainException()
        {
            var ex = Assert.Throws<CatalogDomainException>(() => Order.Create(Guid.NewGuid(), Guid.Empty));
            Assert.Equal("GameId is required.", ex.Message);
        }

        [Fact]
        public void Rehydrate_ReturnsOrderWithGivenValues()
        {
            // Arrange
            var id = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var gameId = Guid.NewGuid();
            var createdAt = DateTime.UtcNow;

            // Act
            var order = Order.Rehydrate(id, userId, gameId, "Approved", createdAt);

            // Assert
            Assert.Equal(id, order.Id);
            Assert.Equal(userId, order.UserId);
            Assert.Equal(gameId, order.GameId);
            Assert.Equal("Approved", order.Status);
            Assert.Equal(createdAt, order.CreatedAt);
        }
    }
}
