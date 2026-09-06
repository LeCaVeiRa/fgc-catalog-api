namespace Fgc.Catalog.Application.DTOS.Orders
{
    public class OrderRequest
    {
        public required Guid UserId { get; set; }
        public required Guid GameId { get; set; }
    }
}
