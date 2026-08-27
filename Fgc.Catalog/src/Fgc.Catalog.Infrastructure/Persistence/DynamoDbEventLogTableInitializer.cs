using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace Fgc.Catalog.Infrastructure.Persistence
{
    public static class DynamoDbEventLogTableInitializer
    {
        private const string TableName = "FgcEventLog";

        public static async Task EnsureTableExistsAsync(IAmazonDynamoDB dynamoDb)
        {
            try
            {
                await dynamoDb.DescribeTableAsync(TableName);
                return;
            }
            catch (ResourceNotFoundException)
            {
                // tabela ainda não existe, cria abaixo
            }

            await dynamoDb.CreateTableAsync(new CreateTableRequest
            {
                TableName = TableName,
                BillingMode = BillingMode.PAY_PER_REQUEST,
                AttributeDefinitions =
                [
                    new AttributeDefinition("ServiceName", ScalarAttributeType.S),
                    new AttributeDefinition("OccurredAtId", ScalarAttributeType.S)
                ],
                KeySchema =
                [
                    new KeySchemaElement("ServiceName", KeyType.HASH),
                    new KeySchemaElement("OccurredAtId", KeyType.RANGE)
                ]
            });
        }
    }
}
