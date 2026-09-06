using Fgc.Catalog.Application.DTOS.Games;
using Fgc.Catalog.Application.DTOS.Orders;
using Fgc.Catalog.Infrastructure.Persistence;
using Fgc.Catalog.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;

namespace Fgc.Catalog.IntegrationTests.Controllers
{
    public class OrderControllerTests : IClassFixture<CustomWebApplicationFactory<Program>>
    {
        private readonly HttpClient _client;
        private readonly CustomWebApplicationFactory<Program> _factory;

        public OrderControllerTests(CustomWebApplicationFactory<Program> factory)
        {
            _client = factory.CreateClient();
            _factory = factory;

            ResetState();
        }

        private void ResetState()
        {
            using var scope = _factory.Services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            context.Database.EnsureDeleted();
            context.Database.EnsureCreated();

            var orderRepository = (InMemoryOrderRepository)scope.ServiceProvider.GetRequiredService<Application.Interfaces.IOrderRepository>();
            orderRepository.Clear();
        }

        private static string GenerateToken(Guid userId, string role = "User")
        {
            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes("FgcUsers-Auth-JWT-Key-2026-Strong-And-Secure"));

            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.Role, role),
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            };

            var token = new JwtSecurityToken(
                issuer: "Fgc.UsersAPI",
                audience: "Fgc.CatalogAPI",
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private async Task<GameResponse> CreateGameAsync()
        {
            var adminToken = GenerateToken(Guid.NewGuid(), "Admin");
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

            var request = new GameRequest { Title = "Super Mario", Category = "Plataforma", Price = 199.90m };
            var response = await _client.PostAsJsonAsync("/games", request);
            var game = await response.Content.ReadFromJsonAsync<GameResponse>();

            _client.DefaultRequestHeaders.Authorization = null;
            return game!;
        }

        [Fact]
        public async Task PurchaseGame_WithExistingGameAndToken_Returns201WithCompletedOrder()
        {
            // Arrange
            var game = await CreateGameAsync();
            var userId = Guid.NewGuid();
            var token = GenerateToken(userId);
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await _client.PostAsJsonAsync("/orders", new OrderRequest { UserId = userId, GameId = game.Id });

            // Assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            var order = await response.Content.ReadFromJsonAsync<OrderResponse>();
            Assert.NotNull(order);
            Assert.Equal(userId, order.UserId);
            Assert.Equal(game.Id, order.GameId);
            Assert.Equal("Completed", order.Status);
        }

        [Fact]
        public async Task PurchaseGame_WithNonExistentGame_Returns404()
        {
            // Arrange
            var token = GenerateToken(Guid.NewGuid());
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await _client.PostAsJsonAsync("/orders", new OrderRequest { UserId = Guid.NewGuid(), GameId = Guid.NewGuid() });

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task PurchaseGame_WhenUserAlreadyOwnsGame_Returns409()
        {
            // Arrange
            var game = await CreateGameAsync();
            var userId = Guid.NewGuid();
            var token = GenerateToken(userId);
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var firstPurchase = await _client.PostAsJsonAsync("/orders", new OrderRequest { UserId = userId, GameId = game.Id });
            Assert.Equal(HttpStatusCode.Created, firstPurchase.StatusCode);

            using var scope = _factory.Services.CreateScope();
            var userLibraryRepository = scope.ServiceProvider.GetRequiredService<Application.Interfaces.IUserLibraryRepository>();
            await userLibraryRepository.AddAsync(
                Domain.Entities.UserLibrary.Create(userId, game.Id, 10m));

            // Act
            var response = await _client.PostAsJsonAsync("/orders", new OrderRequest { UserId = userId, GameId = game.Id });

            // Assert
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        [Fact]
        public async Task PurchaseGame_WithoutToken_Returns401()
        {
            // Arrange - sem token
            var request = new OrderRequest { UserId = Guid.NewGuid(), GameId = Guid.NewGuid() };

            // Act
            var response = await _client.PostAsJsonAsync("/orders", request);

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task GetOrderById_WhenOrderExists_Returns200WithOrder()
        {
            // Arrange
            var game = await CreateGameAsync();
            var userId = Guid.NewGuid();
            var token = GenerateToken(userId);
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var createResponse = await _client.PostAsJsonAsync("/orders", new OrderRequest { UserId = userId, GameId = game.Id });
            var createdOrder = await createResponse.Content.ReadFromJsonAsync<OrderResponse>();
            Assert.NotNull(createdOrder);

            // Act
            var response = await _client.GetAsync($"/orders/{createdOrder.Id}");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var order = await response.Content.ReadFromJsonAsync<OrderResponse>();
            Assert.NotNull(order);
            Assert.Equal(createdOrder.Id, order.Id);
        }

        [Fact]
        public async Task GetOrderById_WhenOrderDoesNotExist_Returns404()
        {
            // Arrange
            var token = GenerateToken(Guid.NewGuid());
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Act
            var response = await _client.GetAsync($"/orders/{Guid.NewGuid()}");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }
}
