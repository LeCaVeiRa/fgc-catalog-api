using Fgc.Catalog.Application.DTOS.Games;
using Fgc.Catalog.Application.Interfaces;
using Fgc.Catalog.Application.Services;
using Fgc.Catalog.Domain.Entities;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Moq;

namespace Fgc.Catalog.Tests.Services
{
    public class GameServiceTests
    {
        private readonly Mock<IGameRepository> _repositoryMock = new();
        private readonly Mock<IDistributedCache> _cacheMock = new();
        private readonly Mock<IGameSearchService> _searchMock = new();
        private readonly GameService _gameService;

        public GameServiceTests()
        {
            // Simula Redis fora do ar: toda operação de cache lança exceção.
            _cacheMock.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("redis down"));
            _cacheMock.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("redis down"));
            _cacheMock.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("redis down"));

            _gameService = new GameService(_repositoryMock.Object, _cacheMock.Object, Mock.Of<ILogger<GameService>>(), _searchMock.Object);
        }

        [Fact]
        public async Task GetAllAsync_ShouldFallBackToRepository_WhenCacheIsUnavailable()
        {
            _repositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync([Game.Create("Zelda", "Adventure", 100m)]);

            var result = await _gameService.GetAllAsync();

            Assert.Single(result);
            _repositoryMock.Verify(r => r.GetAllAsync(), Times.Once);
        }

        [Fact]
        public async Task GetAllAsync_ShouldSkipCache_AfterFailure()
        {
            _repositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync([]);

            await _gameService.GetAllAsync();
            await _gameService.GetAllAsync();

            // Primeira chamada falha e abre o disjuntor; a segunda nem tenta o Redis.
            _cacheMock.Verify(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            _repositoryMock.Verify(r => r.GetAllAsync(), Times.Exactly(2));
        }

        [Fact]
        public async Task CreateAsync_ShouldPersist_WhenCacheInvalidationFails()
        {
            _repositoryMock.Setup(r => r.ExistsByTitleAsync("Zelda")).ReturnsAsync(false);

            var result = await _gameService.CreateAsync(new GameRequest { Title = "Zelda", Category = "Adventure", Price = 100m });

            Assert.Equal("Zelda", result.Title);
            _repositoryMock.Verify(r => r.AddAsync(It.IsAny<Game>()), Times.Once);
        }

        [Fact]
        public async Task CreateAsync_ShouldIndexGame()
        {
            _repositoryMock.Setup(r => r.ExistsByTitleAsync("Zelda")).ReturnsAsync(false);

            var result = await _gameService.CreateAsync(new GameRequest { Title = "Zelda", Category = "Adventure", Price = 100m });

            _searchMock.Verify(s => s.IndexAsync(It.Is<GameResponse>(g => g.Id == result.Id && g.Title == "Zelda"), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateAsync_ShouldPersist_WhenSearchIndexFails()
        {
            _repositoryMock.Setup(r => r.ExistsByTitleAsync("Zelda")).ReturnsAsync(false);
            _searchMock.Setup(s => s.IndexAsync(It.IsAny<GameResponse>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new SearchUnavailableException("opensearch down"));

            var result = await _gameService.CreateAsync(new GameRequest { Title = "Zelda", Category = "Adventure", Price = 100m });

            Assert.Equal("Zelda", result.Title);
            _repositoryMock.Verify(r => r.AddAsync(It.IsAny<Game>()), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_ShouldReindexGame()
        {
            var game = Game.Create("Zelda", "Adventure", 100m);
            _repositoryMock.Setup(r => r.GetByIdAsync(game.Id)).ReturnsAsync(game);

            await _gameService.UpdateAsync(game.Id, new GameRequest { Title = "Zelda BOTW", Category = "Adventure", Price = 150m });

            _searchMock.Verify(s => s.IndexAsync(It.Is<GameResponse>(g => g.Id == game.Id && g.Title == "Zelda BOTW" && g.Price == 150m), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DeleteAsync_ShouldRemoveFromIndex()
        {
            var game = Game.Create("Zelda", "Adventure", 100m);
            _repositoryMock.Setup(r => r.GetByIdAsync(game.Id)).ReturnsAsync(game);

            await _gameService.DeleteAsync(game.Id);

            _searchMock.Verify(s => s.DeleteAsync(game.Id, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ReindexAsync_ShouldIndexAllGamesFromRepository()
        {
            _repositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync([Game.Create("Zelda", "Adventure", 100m), Game.Create("Mario", "Platform", 80m)]);

            var count = await _gameService.ReindexAsync();

            Assert.Equal(2, count);
            _searchMock.Verify(s => s.IndexManyAsync(It.Is<IReadOnlyCollection<GameResponse>>(g => g.Count == 2), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SearchAsync_ShouldTrimQueryAndClampSize()
        {
            _searchMock.Setup(s => s.SearchAsync("zeld", 100, It.IsAny<CancellationToken>())).ReturnsAsync([]);

            await _gameService.SearchAsync("  zeld ", 1000);

            _searchMock.Verify(s => s.SearchAsync("zeld", 100, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task InitializeSearchIndexAsync_ShouldPopulateIndex_WhenJustCreated()
        {
            _searchMock.Setup(s => s.EnsureIndexAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _repositoryMock.Setup(r => r.GetAllAsync()).ReturnsAsync([Game.Create("Zelda", "Adventure", 100m)]);

            await _gameService.InitializeSearchIndexAsync();

            _searchMock.Verify(s => s.IndexManyAsync(It.Is<IReadOnlyCollection<GameResponse>>(g => g.Count == 1), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task InitializeSearchIndexAsync_ShouldNotThrow_WhenOpenSearchIsDown()
        {
            _searchMock.Setup(s => s.EnsureIndexAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new SearchUnavailableException("opensearch down"));

            await _gameService.InitializeSearchIndexAsync();
        }
    }
}
