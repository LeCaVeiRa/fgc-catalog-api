namespace Fgc.Catalog.Application.Interfaces
{
    public interface IEventLogRepository
    {
        Task LogAsync(string eventType, object payload, CancellationToken cancellationToken);
    }
}
