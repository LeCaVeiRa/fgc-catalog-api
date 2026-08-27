using Fgc.Catalog.Application.Interfaces;
using Fgc.MessageContracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Fgc.Catalog.Application.Consumers
{
    public class UserCreatedEventConsumer : IConsumer<UserCreatedEvent>
    {
        private readonly ILogger<UserCreatedEventConsumer> _logger;
        private readonly IEventLogRepository _eventLogRepository;

        public UserCreatedEventConsumer(ILogger<UserCreatedEventConsumer> logger, IEventLogRepository eventLogRepository)
        {
            _logger = logger;
            _eventLogRepository = eventLogRepository;
        }
        public async Task Consume(ConsumeContext<UserCreatedEvent> context)
        {
            var message = context.Message;

            _logger.LogInformation(
                "UserCreatedEvent received: UserId={UserId}, Username={Username}, Email={Email}",
                message.Id,
                message.Name,
                message.Email
                );

            await _eventLogRepository.LogAsync("UserCreatedEvent", message, context.CancellationToken);
        }
    }
}
