using Fgc.Catalog.Application.DTOS.Orders;
using Fgc.Catalog.Application.Interfaces;
using Fgc.Catalog.Domain.Entities;
using Fgc.Catalog.Domain.Exceptions;
using Fgc.MessageContracts.Events;
using MassTransit;

namespace Fgc.Catalog.Application.Services
{
    public class OrderService(
        IOrderRepository orderRepository,
        IGameRepository gameRepository,
        IUserLibraryRepository userLibraryRepository,
        IEventLogRepository eventLogRepository,
        IPublishEndpoint publishEndpoint)
    {
        public async Task<OrderResponse> PurchaseAsync(Guid userId, Guid gameId, CancellationToken cancellationToken = default)
        {
            var game = await gameRepository.GetByIdAsync(gameId)
                ?? throw new NotFoundException("Game not found.");

            if (await userLibraryRepository.ExistsAsync(userId, gameId))
                throw new ConflictException("User already owns this game.");

            var order = Order.Create(userId, gameId);
            await orderRepository.AddAsync(order);

            await eventLogRepository.LogAsync(
                "GamePurchased",
                new { OrderId = order.Id, UserId = userId, GameId = gameId },
                cancellationToken);

            await publishEndpoint.Publish(new OrderPlacedEvent(order.Id, userId, gameId, game.Price), cancellationToken);

            return OrderResponse.FromEntity(order);
        }

        public async Task<OrderResponse?> GetByIdAsync(Guid id)
        {
            var order = await orderRepository.GetByIdAsync(id);
            return order is null ? null : OrderResponse.FromEntity(order);
        }
    }
}
