using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace Fgc.Catalog.Infrastructure.Persistence
{
    public static class DynamoDbOrderTableInitializer
    {
        private const string TableName = "Orders";

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
                    new AttributeDefinition("Id", ScalarAttributeType.S)
                ],
                KeySchema =
                [
                    new KeySchemaElement("Id", KeyType.HASH)
                ]
            });
        }
    }
}
