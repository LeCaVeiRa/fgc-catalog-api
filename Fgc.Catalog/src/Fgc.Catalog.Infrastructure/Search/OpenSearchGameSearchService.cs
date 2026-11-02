using Fgc.Catalog.Application.DTOS.Games;
using Fgc.Catalog.Application.Interfaces;
using OpenSearch.Client;

namespace Fgc.Catalog.Infrastructure.Search
{
    public sealed class OpenSearchGameSearchService(IOpenSearchClient client) : IGameSearchService
    {
        public const string IndexName = "games";

        public async Task<bool> EnsureIndexAsync(CancellationToken cancellationToken = default)
        {
            var exists = await client.Indices.ExistsAsync(IndexName, ct: cancellationToken);
            if (exists.Exists)
                return false;
            // HEAD /games responde 404 quando o índice não existe; o client marca isso como inválido.
            if (exists.ApiCall?.HttpStatusCode != 404)
                EnsureSuccess(exists, "check index");

            var created = await client.Indices.CreateAsync(IndexName, c => c
                .Map<GameResponse>(m => m.Properties(p => p
                    .Keyword(k => k.Name(g => g.Id))
                    .Text(t => t.Name(g => g.Title).Fields(f => f.Keyword(k => k.Name("keyword"))))
                    .Text(t => t.Name(g => g.Category).Fields(f => f.Keyword(k => k.Name("keyword"))))
                    .Number(n => n.Name(g => g.Price).Type(NumberType.ScaledFloat).ScalingFactor(100)))),
                cancellationToken);

            // Outra réplica pode ter criado o índice entre o Exists e o Create.
            if (!created.IsValid && created.ServerError?.Error?.Type == "resource_already_exists_exception")
                return false;

            EnsureSuccess(created, "create index");
            return true;
        }

        public async Task IndexAsync(GameResponse game, CancellationToken cancellationToken = default)
        {
            // WaitFor: o jogo já aparece na busca quando a escrita retorna (volume baixo, consistência importa mais).
            var response = await client.IndexAsync(game, i => i.Index(IndexName).Id(game.Id).Refresh(OpenSearch.Net.Refresh.WaitFor), cancellationToken);
            EnsureSuccess(response, "index game");
        }

        public async Task IndexManyAsync(IReadOnlyCollection<GameResponse> games, CancellationToken cancellationToken = default)
        {
            if (games.Count == 0)
                return;

            var response = await client.BulkAsync(b => b
                .Index(IndexName)
                .IndexMany(games, (d, g) => d.Id(g.Id))
                .Refresh(OpenSearch.Net.Refresh.WaitFor), cancellationToken);
            EnsureSuccess(response, "bulk index games");
        }

        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var response = await client.DeleteAsync<GameResponse>(id, d => d.Index(IndexName).Refresh(OpenSearch.Net.Refresh.WaitFor), cancellationToken);
            if (response.Result == Result.NotFound)
                return;
            EnsureSuccess(response, "delete game");
        }

        public async Task<List<GameResponse>> SearchAsync(string query, int size, CancellationToken cancellationToken = default)
        {
            var response = await client.SearchAsync<GameResponse>(s => s
                .Index(IndexName)
                .Size(size)
                .Query(q => q.MultiMatch(m => m
                    .Query(query)
                    .Fields(f => f.Field(g => g.Title, boost: 3).Field(g => g.Category))
                    .Fuzziness(Fuzziness.Auto)))
                .Sort(so => so.Descending(SortSpecialField.Score)), cancellationToken);
            EnsureSuccess(response, "search games");
            return response.Documents.ToList();
        }

        private static void EnsureSuccess(IResponse response, string operation)
        {
            if (!response.IsValid)
                throw new SearchUnavailableException(
                    $"OpenSearch failed to {operation} (HTTP {response.ApiCall?.HttpStatusCode?.ToString() ?? "n/a"}): {response.ServerError?.ToString() ?? response.OriginalException?.Message}",
                    response.OriginalException);
        }
    }
}
