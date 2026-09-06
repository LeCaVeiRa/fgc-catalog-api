using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Fgc.Catalog.Application.Interfaces;
using Fgc.Catalog.Domain.Entities;
using System.Globalization;

namespace Fgc.Catalog.Infrastructure.Repositories
{
    public class OrderRepository(IAmazonDynamoDB dynamoDb) : IOrderRepository
    {
        private const string TableName = "Orders";

        public async Task AddAsync(Order order)
        {
            await dynamoDb.PutItemAsync(new PutItemRequest
            {
                TableName = TableName,
                Item = new Dictionary<string, AttributeValue>
                {
                    ["Id"] = new AttributeValue { S = order.Id.ToString() },
                    ["UserId"] = new AttributeValue { S = order.UserId.ToString() },
                    ["GameId"] = new AttributeValue { S = order.GameId.ToString() },
                    ["Status"] = new AttributeValue { S = order.Status },
                    ["CreatedAt"] = new AttributeValue { S = order.CreatedAt.ToString("O") }
                }
            });
        }

        public async Task<Order?> GetByIdAsync(Guid id)
        {
            var response = await dynamoDb.GetItemAsync(new GetItemRequest
            {
                TableName = TableName,
                Key = new Dictionary<string, AttributeValue>
                {
                    ["Id"] = new AttributeValue { S = id.ToString() }
                }
            });

            if (!response.IsItemSet)
                return null;

            return Order.Rehydrate(
                Guid.Parse(response.Item["Id"].S),
                Guid.Parse(response.Item["UserId"].S),
                Guid.Parse(response.Item["GameId"].S),
                response.Item["Status"].S,
                DateTime.Parse(response.Item["CreatedAt"].S, null, DateTimeStyles.RoundtripKind));
        }
    }
}
