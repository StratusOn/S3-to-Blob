using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading.Tasks;

public static class AzureTableUtil
{
    public static async Task<IActionResult> UpdateLastProcessedTimestampInTable(HttpRequest req, TraceWriter log)
    {
        string tableConnectionString = Environment.GetEnvironmentVariable("LastProcessedDateTimeTableConnectionString", EnvironmentVariableTarget.Process);
        string tableName = Environment.GetEnvironmentVariable("LastProcessedDateTimeTableName", EnvironmentVariableTarget.Process);
        string tablePropertyName = Environment.GetEnvironmentVariable("LastProcessedTablePropertyName", EnvironmentVariableTarget.Process);

        log.Info($"[Setting]: Table Connection String: {tableConnectionString}");
        log.Info($"[Setting]: Table Name: {tableName}");
        log.Info($"[Setting]: Table Property Name: {tablePropertyName}");

        string newLastProcessedDateTimeUtcAsString = req.Query["newLastProcessedDateTimeUtc"]; // GET
        string requestBody = new StreamReader(req.Body).ReadToEnd(); // POST
        dynamic data = JsonConvert.DeserializeObject(requestBody);
        newLastProcessedDateTimeUtcAsString = newLastProcessedDateTimeUtcAsString ?? data?.newLastProcessedDateTimeUtc;
        if (string.IsNullOrWhiteSpace(newLastProcessedDateTimeUtcAsString))
        {
            string errorMessage = "A 'newLastProcessedDateTimeUtc' querystring parameter or a request body containing a JSON object with a 'newLastProcessedDateTimeUtc' property was expected but not found.";
            log.Info(errorMessage);
            return new BadRequestObjectResult(errorMessage);
        }
        var newLastProcessedDateTimeUtc = DateTime.Parse(newLastProcessedDateTimeUtcAsString);
        log.Info($"New Last Processed DateTimeUtc: {newLastProcessedDateTimeUtcAsString}.");

        try
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(tableConnectionString);
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
            CloudTable cloudTable = tableClient.GetTableReference(tableName);
            bool tableExists = await cloudTable.ExistsAsync();

            if (!tableExists)
            {
                log.Info($"Table {tableName} was not found in storage account. Creating table...");
                bool createdTableSuccessfully = await cloudTable.CreateIfNotExistsAsync();
                if (!createdTableSuccessfully)
                {
                    log.Info($"Failed to create table {tableName}. Create the table manually in storage account '{storageAccount.Credentials.AccountName}' with PartionKey/RowKey of 1/1 and a property '{tablePropertyName}' with an empty value.");
                    return new BadRequestObjectResult("Operation failed: Specified table not found. Check function's log for details.");
                }

                // Add the first entity / default entity:
                log.Info($"Adding a default entity to newly created table '{tableName}'...");
                DynamicTableEntity entity = new DynamicTableEntity("1", "1");
                entity.Properties[tablePropertyName] = new EntityProperty("1/1/0001 12:00:00 AM");
                TableOperation insertOperation = TableOperation.Insert(entity);
                await cloudTable.ExecuteAsync(insertOperation);
                log.Info($"Added default entity to newly created table '{tableName}'.");
            }
            else
            {
                // Update the existing entity:
                TableQuery<DynamicTableEntity> query = new TableQuery<DynamicTableEntity>().Take(1); // Get the first entity in the table. We don't care what the partition or row keys are.
                TableQuerySegment<DynamicTableEntity> resultSegment = await cloudTable.ExecuteQuerySegmentedAsync(query, null);
                DynamicTableEntity entity = resultSegment.Results[0];
                log.Info($"PartitionKey/RowKey values of the first entity in table '{tableName}' are: {entity.PartitionKey}/{entity.RowKey}.");
                EntityProperty lastProcessedTableEntityProperty = entity.Properties[tablePropertyName];
                log.Info($"Current value of '{tablePropertyName}' property in table '{tableName}' is: {lastProcessedTableEntityProperty.StringValue}.");

                // Set the property value to the new one:
                lastProcessedTableEntityProperty.StringValue = newLastProcessedDateTimeUtcAsString;
                entity.Properties[tablePropertyName] = lastProcessedTableEntityProperty;
                TableOperation updateOperation = TableOperation.Replace(entity);
                await cloudTable.ExecuteAsync(updateOperation);
            }

            dynamic payload = new System.Dynamic.ExpandoObject();
            payload.Result = "Operation succeeded.";
            return new OkObjectResult(JsonConvert.SerializeObject(payload, Formatting.Indented));
        }
        catch (Exception exception)
        {
            log.Info($"Exception [UpdateLastProcessedTimestampInTable]: {exception.ToString()}.");
            return new BadRequestObjectResult("Operation failed. Check function's log for details.");
        }
    }
}
