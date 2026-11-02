using Amazon.DynamoDBv2;
using Fgc.Catalog.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Fgc.Catalog.Api.HealthChecks
{
    // /health/live: o processo responde (sem dependências) - usado como liveness probe.
    // /health/ready: banco, Redis, DynamoDB e broker (check do MassTransit, tag "ready") - readiness probe.
    // /health: alias de live, mantido por compatibilidade com o compose e o Kong.
    public static class HealthCheckExtensions
    {
        private const string ReadyTag = "ready";

        public static IServiceCollection AddFgcHealthChecks(this IServiceCollection services, params string[] dynamoDbTables)
        {
            services.AddHealthChecks()
                .AddCheck<DatabaseHealthCheck>("database", tags: [ReadyTag])
                .AddCheck<RedisCacheHealthCheck>("redis", tags: [ReadyTag])
                .Add(new HealthCheckRegistration(
                    "dynamodb",
                    sp => new DynamoDbHealthCheck(sp.GetRequiredService<IAmazonDynamoDB>(), dynamoDbTables),
                    failureStatus: null,
                    tags: [ReadyTag]));
            return services;
        }

        public static WebApplication MapFgcHealthChecks(this WebApplication app)
        {
            var live = new HealthCheckOptions { Predicate = _ => false };
            app.MapHealthChecks("/health", live);
            app.MapHealthChecks("/health/live", live);
            app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains(ReadyTag) });
            return app;
        }
    }

    internal sealed class DatabaseHealthCheck(CatalogDbContext dbContext) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            return await dbContext.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy()
                : new HealthCheckResult(context.Registration.FailureStatus, "Database unavailable");
        }
    }

    internal sealed class RedisCacheHealthCheck(IDistributedCache cache) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                await cache.GetAsync("health:ping", cancellationToken);
                return HealthCheckResult.Healthy();
            }
            catch (Exception ex)
            {
                return new HealthCheckResult(context.Registration.FailureStatus, "Redis unavailable", ex);
            }
        }
    }

    internal sealed class DynamoDbHealthCheck(IAmazonDynamoDB dynamoDb, string[] tables) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                foreach (var table in tables)
                {
                    await dynamoDb.DescribeTableAsync(table, cancellationToken);
                }
                return HealthCheckResult.Healthy();
            }
            catch (Exception ex)
            {
                return new HealthCheckResult(context.Registration.FailureStatus, "DynamoDB unavailable", ex);
            }
        }
    }
}
