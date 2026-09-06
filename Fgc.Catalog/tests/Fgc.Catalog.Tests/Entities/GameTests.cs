using Fgc.Catalog.Domain.Entities;
using Fgc.Catalog.Domain.Exceptions;

namespace Fgc.Catalog.Tests.Entities
{
    public class GameTests
    {
        [Fact]
        public void Create_ValidTitleAndCategory_ReturnsGame()
        {
            // Arrange
            var title = "Super Mario";
            var category = "Plataforma";
            var price = 199.90m;

            // Act
            var game = Game.Create(title, category, price);

            // Assert
            Assert.NotNull(game);
            Assert.NotEqual(Guid.Empty, game.Id);
            Assert.Equal(title, game.Title);
            Assert.Equal(category, game.Category);
            Assert.Equal(price, game.Price);
        }

        [Fact]
        public void Create_EmptyTitle_ThrowsCatalogDomainException()
        {
            var ex = Assert.Throws<CatalogDomainException>(() => Game.Create(string.Empty, "Plataforma", 199.90m));
            Assert.Equal("Title is required.", ex.Message);
        }

        [Fact]
        public void Create_NullTitle_ThrowsCatalogDomainException()
        {
            var ex = Assert.Throws<CatalogDomainException>(() => Game.Create(null!, "Plataforma", 199.90m));
            Assert.Equal("Title is required.", ex.Message);
        }

        [Fact]
        public void Create_EmptyCategory_ThrowsCatalogDomainException()
        {
            var ex = Assert.Throws<CatalogDomainException>(() => Game.Create("Super Mario", string.Empty, 199.90m));
            Assert.Equal("Category is required.", ex.Message);
        }

        [Fact]
        public void Create_ZeroOrNegativePrice_ThrowsCatalogDomainException()
        {
            var ex = Assert.Throws<CatalogDomainException>(() => Game.Create("Super Mario", "Plataforma", 0m));
            Assert.Equal("Price must be greater than zero.", ex.Message);
        }
    }
}
