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
        private readonly IGameSearchService _search;

        public GameService(IGameRepository repository, IDistributedCache cache, ILogger<GameService> logger, IGameSearchService search)
        {
            _repository = repository;
            _cache = cache;
            _logger = logger;
            _search = search;
        }

        public async Task<GameResponse> CreateAsync(GameRequest request)
        {
            if (await _repository.ExistsByTitleAsync(request.Title))
                throw new CatalogDomainException("Game already exists.");

            var game = Game.Create(request.Title, request.Category, request.Price);

            await _repository.AddAsync(game);

            await _cache.TryRemoveAsync(AllGamesCacheKey, _logger);

            var response = GameResponse.FromEntity(game);
            await TrySearchIndexAsync(() => _search.IndexAsync(response), game.Id);

            return response;
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

            var response = GameResponse.FromEntity(game);
            await TrySearchIndexAsync(() => _search.IndexAsync(response), game.Id);

            return response;
        }

        public async Task DeleteAsync(Guid id)
        {
            var game = await _repository.GetByIdAsync(id)
                ?? throw new NotFoundException("Game not found.");
            await _repository.DeleteAsync(game);

            await _cache.TryRemoveAsync(AllGamesCacheKey, _logger);

            await TrySearchIndexAsync(() => _search.DeleteAsync(id), id);
        }

        /// <summary>Busca textual com tolerância a erros de digitação, ordenada por relevância.</summary>
        public Task<List<GameResponse>> SearchAsync(string query, int size = 20)
        {
            return _search.SearchAsync(query.Trim(), Math.Clamp(size, 1, 100));
        }

        /// <summary>Reconstrói o índice de busca a partir do banco. Retorna quantos jogos foram indexados.</summary>
        public async Task<int> ReindexAsync()
        {
            await _search.EnsureIndexAsync();
            var games = (await _repository.GetAllAsync()).Select(GameResponse.FromEntity).ToList();
            await _search.IndexManyAsync(games);
            return games.Count;
        }

        /// <summary>
        /// Chamado na subida: garante o índice e, se ele acabou de ser criado (ambiente novo ou índice apagado),
        /// popula com os jogos do banco. OpenSearch fora do ar não impede a API de subir.
        /// </summary>
        public async Task InitializeSearchIndexAsync()
        {
            try
            {
                if (await _search.EnsureIndexAsync())
                {
                    var count = await ReindexAsync();
                    _logger.LogInformation("Search index created and populated | Games={Count}", count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Search index initialization failed; use POST /games/reindex once OpenSearch is available");
            }
        }

        // O banco é a fonte da verdade: falha no índice não desfaz a escrita, só fica registrada
        // (corrigível com POST /games/reindex).
        private async Task TrySearchIndexAsync(Func<Task> operation, Guid gameId)
        {
            try
            {
                await operation();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Search index update failed | GameId={GameId}", gameId);
            }
        }
    }
}

