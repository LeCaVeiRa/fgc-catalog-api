using Fgc.Catalog.Application.Interfaces;
using Fgc.Catalog.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Fgc.Catalog.IntegrationTests.Infrastructure
{
    public class CustomWebApplicationFactory<TStartup> : WebApplicationFactory<TStartup> where TStartup : class
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll(typeof(DbContextOptions<CatalogDbContext>));
                services.RemoveAll(typeof(DbContextOptions));

                services.AddScoped<DbContextOptions<CatalogDbContext>>(provider =>
                {
                    return new DbContextOptionsBuilder<CatalogDbContext>()
                        .UseInMemoryDatabase("InMemoryCatalogTestDb")
                        .Options;
                });

                // Sem broker/DynamoDB real disponível em teste de integração.
                services.RemoveAll(typeof(IEventLogRepository));
                services.AddScoped<IEventLogRepository, NoOpEventLogRepository>();

                // Sem Redis real disponível em teste de integração - cache em memória equivalente.
                services.RemoveAll(typeof(IDistributedCache));
                services.AddDistributedMemoryCache();
            });

        }
    }

    internal class NoOpEventLogRepository : IEventLogRepository
    {
        public Task LogAsync(string eventType, object payload, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
