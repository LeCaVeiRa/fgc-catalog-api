using Fgc.Catalog.Domain.Exceptions;

namespace Fgc.Catalog.Domain.Entities
{
    public class Order
    {
        public Guid Id { get; private set; }
        public Guid UserId { get; private set; }
        public Guid GameId { get; private set; }
        public string Status { get; private set; } = null!;
        public DateTime CreatedAt { get; private set; }

        private Order() { }

        public static Order Create(Guid userId, Guid gameId)
        {
            Validate(userId, gameId);

            return new Order
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                GameId = gameId,
                Status = "Completed",
                CreatedAt = DateTime.UtcNow
            };
        }

        // Reconstrução a partir do DynamoDB (OrderRepository) - sem EF Core não há
        // materialização automática via reflection sobre o construtor privado.
        public static Order Rehydrate(Guid id, Guid userId, Guid gameId, string status, DateTime createdAt)
            => new() { Id = id, UserId = userId, GameId = gameId, Status = status, CreatedAt = createdAt };

        private static void Validate(Guid userId, Guid gameId)
        {
            if (userId == Guid.Empty)
                throw new CatalogDomainException("UserId is required.");

            if (gameId == Guid.Empty)
                throw new CatalogDomainException("GameId is required.");
        }
    }
}
