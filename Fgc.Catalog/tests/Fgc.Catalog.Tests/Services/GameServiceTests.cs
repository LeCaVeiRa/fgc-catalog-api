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

            _gameService = new GameService(_repositoryMock.Object, _cacheMock.Object, Mock.Of<ILogger<GameService>>());
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
        public async Task CreateAsync_ShouldPersist_WhenCacheInvalidationFails()
        {
            _repositoryMock.Setup(r => r.ExistsByTitleAsync("Zelda")).ReturnsAsync(false);

            var result = await _gameService.CreateAsync(new GameRequest { Title = "Zelda", Category = "Adventure", Price = 100m });

            Assert.Equal("Zelda", result.Title);
            _repositoryMock.Verify(r => r.AddAsync(It.IsAny<Game>()), Times.Once);
        }
    }
}
