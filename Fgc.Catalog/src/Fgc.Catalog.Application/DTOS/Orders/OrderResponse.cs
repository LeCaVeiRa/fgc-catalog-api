using Fgc.Catalog.Domain.Entities;

namespace Fgc.Catalog.Application.DTOS.Orders;

public class OrderResponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid GameId { get; set; }
    public required string Status { get; set; }
    public DateTime CreatedAt { get; set; }

    public static OrderResponse FromEntity(Order order)
    {
        return new OrderResponse
        {
            Id = order.Id,
            UserId = order.UserId,
            GameId = order.GameId,
            Status = order.Status,
            CreatedAt = order.CreatedAt
        };
    }
}
