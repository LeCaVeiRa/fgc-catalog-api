using System.Runtime.CompilerServices;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Fgc.Catalog.Application.Helpers
{
    // Cache é otimização: indisponibilidade do Redis não pode derrubar a requisição.
    // Falhas viram warning e o chamador segue para o banco (leitura) ou ignora (escrita/invalidação).
    // Após uma falha o cache é ignorado por BreakDuration, para que cada requisição não pague o timeout
    // de conexão enquanto o Redis estiver fora (disjuntor por instância de IDistributedCache).
    public static class SafeDistributedCacheExtensions
    {
        private static readonly TimeSpan BreakDuration = TimeSpan.FromSeconds(30);
        private static readonly ConditionalWeakTable<IDistributedCache, Breaker> Breakers = new();

        public static async Task<string?> TryGetStringAsync(this IDistributedCache cache, string key, ILogger logger)
        {
            var breaker = Breakers.GetOrCreateValue(cache);
            if (breaker.IsOpen)
                return null;
            try
            {
                return await cache.GetStringAsync(key);
            }
            catch (Exception ex)
            {
                breaker.Trip();
                logger.LogWarning(ex, "Cache read failed, falling back to database | Key={Key}", key);
                return null;
            }
        }

        public static async Task TrySetStringAsync(this IDistributedCache cache, string key, string value, DistributedCacheEntryOptions options, ILogger logger)
        {
            var breaker = Breakers.GetOrCreateValue(cache);
            if (breaker.IsOpen)
                return;
            try
            {
                await cache.SetStringAsync(key, value, options);
            }
            catch (Exception ex)
            {
                breaker.Trip();
                logger.LogWarning(ex, "Cache write failed | Key={Key}", key);
            }
        }

        public static async Task TryRemoveAsync(this IDistributedCache cache, string key, ILogger logger)
        {
            var breaker = Breakers.GetOrCreateValue(cache);
            if (breaker.IsOpen)
                return;
            try
            {
                await cache.RemoveAsync(key);
            }
            catch (Exception ex)
            {
                breaker.Trip();
                logger.LogWarning(ex, "Cache invalidation failed | Key={Key}", key);
            }
        }

        private sealed class Breaker
        {
            private long _openUntil;

            public bool IsOpen => Environment.TickCount64 < Interlocked.Read(ref _openUntil);

            public void Trip() => Interlocked.Exchange(ref _openUntil, Environment.TickCount64 + (long)BreakDuration.TotalMilliseconds);
        }
    }
}
