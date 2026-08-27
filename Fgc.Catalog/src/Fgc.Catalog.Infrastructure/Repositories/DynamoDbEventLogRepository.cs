using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Fgc.Catalog.Application.Interfaces;
using System.Text.Json;

namespace Fgc.Catalog.Infrastructure.Repositories
{
    public class DynamoDbEventLogRepository(IAmazonDynamoDB dynamoDb) : IEventLogRepository
    {
        private const string TableName = "FgcEventLog";
        private const string ServiceName = "CatalogApi";

        public async Task LogAsync(string eventType, object payload, CancellationToken cancellationToken)
        {
            var occurredAt = DateTime.UtcNow;
            var eventId = Guid.NewGuid();

            var item = new Dictionary<string, AttributeValue>
            {
                ["ServiceName"] = new AttributeValue { S = ServiceName },
                ["OccurredAtId"] = new AttributeValue { S = $"{occurredAt:O}#{eventId}" },
                ["EventType"] = new AttributeValue { S = eventType },
                ["Payload"] = new AttributeValue { S = JsonSerializer.Serialize(payload) }
            };

            await dynamoDb.PutItemAsync(new PutItemRequest
            {
                TableName = TableName,
                Item = item
            }, cancellationToken);
        }
    }
}
