using Fgc.Catalog.Application.DTOS.Games;

namespace Fgc.Catalog.Application.Interfaces
{
    // Índice de busca dos jogos (OpenSearch). O banco relacional continua sendo a fonte da verdade;
    // o índice é uma projeção que pode ser reconstruída a qualquer momento (POST /games/reindex).
    public interface IGameSearchService
    {
        /// <summary>Cria o índice se não existir. Retorna true quando acabou de criá-lo (índice vazio).</summary>
        Task<bool> EnsureIndexAsync(CancellationToken cancellationToken = default);
        Task IndexAsync(GameResponse game, CancellationToken cancellationToken = default);
        Task IndexManyAsync(IReadOnlyCollection<GameResponse> games, CancellationToken cancellationToken = default);
        Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
        Task<List<GameResponse>> SearchAsync(string query, int size, CancellationToken cancellationToken = default);
    }

    public class SearchUnavailableException(string message, Exception? innerException = null)
        : Exception(message, innerException);
}
