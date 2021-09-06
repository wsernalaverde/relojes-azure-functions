using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.Storage.Table;
using relojes_azure_functions.Functions.Entities;
using relojes_azure_functions.Common.Responses;

namespace relojes_azure_functions.Functions.Functions
{
    public static class ConsolidatedApi
    {
        [FunctionName(nameof(ConsolidatedTimes))]
        public static async Task<IActionResult> ConsolidatedTimes(
           [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "consolidated")] HttpRequest req,
           [Table("times", Connection = "AzureWebJobsStorage")] CloudTable timesTable,
           [Table("consolidated", Connection = "AzureWebJobsStorage")] CloudTable consolidatedTable,
           ILogger log)
        {
            log.LogInformation("Recieved a new consolidated times process.");

            TableQuery<TimeManagerEntity> query = new TableQuery<TimeManagerEntity>().Where(TableQuery.GenerateFilterConditionForBool("IsConsolidated", QueryComparisons.Equal, false));
            TableQuerySegment<TimeManagerEntity> times = await timesTable.ExecuteQuerySegmentedAsync(query, null);

            foreach (TimeManagerEntity item in times)
            {
                foreach (TimeManagerEntity itemCompare in times)
                {
                    if (item.EmployeId == itemCompare.EmployeId && item.Type == 0 && itemCompare.Type == 1)
                    {
                        item.IsConsolidated = true;
                        TableOperation addOperation = TableOperation.Replace(item);
                        await timesTable.ExecuteAsync(addOperation);
                        itemCompare.IsConsolidated = true;
                        TableOperation addOperationCompare = TableOperation.Replace(itemCompare);
                        await timesTable.ExecuteAsync(addOperationCompare);

                        ConsolidatedEntity consolidatedEntity = new ConsolidatedEntity
                        {
                            ETag = "*",
                            PartitionKey = "CONSOLIDATED",
                            RowKey = Guid.NewGuid().ToString(),
                            EmployeId = item.EmployeId,
                            MinutesWorked = Convert.ToInt32((itemCompare.TimeMarker - item.TimeMarker).TotalMinutes),
                            DateJob = item.TimeMarker
                        };

                        TableOperation addOperationInsert = TableOperation.Insert(consolidatedEntity);
                        await consolidatedTable.ExecuteAsync(addOperationInsert);

                        Console.Write($"hola {(itemCompare.TimeMarker - item.TimeMarker).TotalMinutes}");
                    }
                }
            }

            string message = "New time stored in table";
            log.LogInformation(message);

            return new OkObjectResult(new Response
            {
                IsSuccess = true,
                Message = message,
                Result = times
            });
        }
    }
}
