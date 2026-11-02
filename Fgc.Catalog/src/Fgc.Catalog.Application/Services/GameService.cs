using Fgc.Catalog.Application.DTOS.Games;
using Fgc.Catalog.Application.Helpers;
using Fgc.Catalog.Application.Interfaces;
using Fgc.Catalog.Domain.Entities;
using Fgc.Catalog.Domain.Exceptions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Fgc.Catalog.Application.Services
{
    public class GameService
    {
        private const string AllGamesCacheKey = "games:all";
        private static readonly DistributedCacheEntryOptions CacheOptions = new()
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60)
        };

        private readonly IGameRepository _repository;
        private readonly IDistributedCache _cache;
        private readonly ILogger<GameService> _logger;

        public GameService(IGameRepository repository, IDistributedCache cache, ILogger<GameService> logger)
        {
            _repository = repository;
            _cache = cache;
            _logger = logger;
        }

        public async Task<GameResponse> CreateAsync(GameRequest request)
        {
            if (await _repository.ExistsByTitleAsync(request.Title))
                throw new CatalogDomainException("Game already exists.");

            var game = Game.Create(request.Title, request.Category, request.Price);

            await _repository.AddAsync(game);

            await _cache.TryRemoveAsync(AllGamesCacheKey, _logger);

            return GameResponse.FromEntity(game);
        }

        public async Task<List<GameResponse>> GetAllAsync()
        {
            var cached = await _cache.TryGetStringAsync(AllGamesCacheKey, _logger);
            if (cached is not null)
            {
                return JsonSerializer.Deserialize<List<GameResponse>>(cached) ?? [];
            }

            var games = await _repository.GetAllAsync();
            var response = games.Select(GameResponse.FromEntity).ToList();

            await _cache.TrySetStringAsync(AllGamesCacheKey, JsonSerializer.Serialize(response), CacheOptions, _logger);

            return response;
        }

        public async Task<GameResponse?> GetByIdAsync(Guid id)
        {
            var game = await _repository.GetByIdAsync(id);
            return game is null?null : GameResponse.FromEntity(game);
        }

        public async Task<GameResponse>UpdateAsync(Guid id, GameRequest request)
        {
            var game = await _repository.GetByIdAsync(id)
                ?? throw new NotFoundException("Game not found.");

            game.Update(request.Title, request.Category, request.Price);

            await _repository.UpdateAsync(game);

            await _cache.TryRemoveAsync(AllGamesCacheKey, _logger);

            return GameResponse.FromEntity(game);
        }

        public async Task DeleteAsync(Guid id)
        {
            var game = await _repository.GetByIdAsync(id)
                ?? throw new NotFoundException("Game not found.");
            await _repository.DeleteAsync(game);

            await _cache.TryRemoveAsync(AllGamesCacheKey, _logger);
        }
    }
}

