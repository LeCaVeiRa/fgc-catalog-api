using Fgc.Catalog.Domain.Entities;

namespace Fgc.Catalog.Application.Interfaces
{
    public interface IOrderRepository
    {
        Task AddAsync(Order order);
        Task<Order?> GetByIdAsync(Guid id);
    }
}
