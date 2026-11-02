using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Fgc.Catalog.Application.Helpers
{
    // Cache é otimização: indisponibilidade do Redis não pode derrubar a requisição.
    // Falhas viram warning e o chamador segue para o banco (leitura) ou ignora (escrita/invalidação).
    public static class SafeDistributedCacheExtensions
    {
        public static async Task<string?> TryGetStringAsync(this IDistributedCache cache, string key, ILogger logger)
        {
            try
            {
                return await cache.GetStringAsync(key);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Cache read failed, falling back to database | Key={Key}", key);
                return null;
            }
        }

        public static async Task TrySetStringAsync(this IDistributedCache cache, string key, string value, DistributedCacheEntryOptions options, ILogger logger)
        {
            try
            {
                await cache.SetStringAsync(key, value, options);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Cache write failed | Key={Key}", key);
            }
        }

        public static async Task TryRemoveAsync(this IDistributedCache cache, string key, ILogger logger)
        {
            try
            {
                await cache.RemoveAsync(key);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Cache invalidation failed | Key={Key}", key);
            }
        }
    }
}
