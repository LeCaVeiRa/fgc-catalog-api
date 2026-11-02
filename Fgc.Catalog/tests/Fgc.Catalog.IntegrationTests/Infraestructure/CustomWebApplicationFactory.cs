using Fgc.Catalog.Application.Consumers;
using Fgc.Catalog.Application.DTOS.Games;
using Fgc.Catalog.Application.Interfaces;
using Fgc.Catalog.Domain.Entities;
using Fgc.Catalog.Infrastructure.Persistence;
using MassTransit;
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
        // Nome único por instância de fábrica - evita que duas classes de teste (cada uma com
        // sua própria IClassFixture<CustomWebApplicationFactory<Program>>) rodando em paralelo
        // colidam no mesmo banco InMemory compartilhado (EF Core InMemory reusa o mesmo store
        // para o mesmo nome entre providers diferentes).
        private readonly string _databaseName = Guid.NewGuid().ToString();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll(typeof(DbContextOptions<CatalogDbContext>));
                services.RemoveAll(typeof(DbContextOptions));

                services.AddScoped<DbContextOptions<CatalogDbContext>>(provider =>
                {
                    return new DbContextOptionsBuilder<CatalogDbContext>()
                        .UseInMemoryDatabase(_databaseName)
                        .Options;
                });

                // Sem broker/DynamoDB real disponível em teste de integração.
                services.RemoveAll(typeof(IEventLogRepository));
                services.AddScoped<IEventLogRepository, NoOpEventLogRepository>();

                // Sem Redis real disponível em teste de integração - cache em memória equivalente.
                services.RemoveAll(typeof(IDistributedCache));
                services.AddDistributedMemoryCache();

                // Sem DynamoDB real disponível em teste de integração - fake em memória equivalente.
                services.RemoveAll(typeof(IOrderRepository));
                services.AddSingleton<IOrderRepository, InMemoryOrderRepository>();

                // Sem OpenSearch real disponível em teste de integração - fake em memória (match por substring).
                services.RemoveAll(typeof(IGameSearchService));
                services.AddSingleton<IGameSearchService, InMemoryGameSearchService>();

                // Sem RabbitMQ real disponível em teste de integração - substitui o bus do Program
                // por um transporte em memória (publicações ficam inspecionáveis via ITestHarness).
                services.AddMassTransitTestHarness(x =>
                {
                    x.AddConsumer<UserCreatedEventConsumer>();
                    x.AddConsumer<PaymentProcessedEventConsumer>();
                });
            });

        }
    }

    internal class NoOpEventLogRepository : IEventLogRepository
    {
        public Task LogAsync(string eventType, object payload, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    internal class InMemoryOrderRepository : IOrderRepository
    {
        private readonly Dictionary<Guid, Order> _orders = [];

        public Task AddAsync(Order order)
        {
            _orders[order.Id] = order;
            return Task.CompletedTask;
        }

        public Task<Order?> GetByIdAsync(Guid id)
        {
            _orders.TryGetValue(id, out var order);
            return Task.FromResult(order);
        }

        // Chamado pelos testes para isolar cada [Fact] - o repositório é singleton
        // (precisa sobreviver entre requests HTTP dentro do mesmo teste), então não
        // é limpo automaticamente como o DbContext em ResetDatabase().
        public void Clear() => _orders.Clear();
    }

    internal class InMemoryGameSearchService : IGameSearchService
    {
        private readonly Dictionary<Guid, GameResponse> _games = [];

        public Task<bool> EnsureIndexAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);

        public Task IndexAsync(GameResponse game, CancellationToken cancellationToken = default)
        {
            lock (_games) _games[game.Id] = game;
            return Task.CompletedTask;
        }

        public async Task IndexManyAsync(IReadOnlyCollection<GameResponse> games, CancellationToken cancellationToken = default)
        {
            foreach (var game in games)
                await IndexAsync(game, cancellationToken);
        }

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            lock (_games) _games.Remove(id);
            return Task.CompletedTask;
        }

        public Task<List<GameResponse>> SearchAsync(string query, int size, CancellationToken cancellationToken = default)
        {
            lock (_games)
                return Task.FromResult(_games.Values
                    .Where(g => g.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                             || g.Category.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .Take(size)
                    .ToList());
        }
    }
}
