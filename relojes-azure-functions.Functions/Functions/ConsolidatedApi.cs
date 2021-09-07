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

            var contAdded = 0;
            var contUpdated = 0;
            foreach (TimeManagerEntity item in times)
            {
                foreach (TimeManagerEntity itemCompare in times)
                {
                    if (item.EmployeId == itemCompare.EmployeId && item.Type == 0 && itemCompare.Type == 1 && item.IsConsolidated == false && itemCompare.IsConsolidated == false)
                    {
                        item.IsConsolidated = true;
                        TableOperation addOperation = TableOperation.Replace(item);
                        await timesTable.ExecuteAsync(addOperation);
                        itemCompare.IsConsolidated = true;
                        TableOperation addOperationCompare = TableOperation.Replace(itemCompare);
                        await timesTable.ExecuteAsync(addOperationCompare);

                        // validate times EmployedId
                        TableQuery<ConsolidatedEntity> queryConsolidate = new TableQuery<ConsolidatedEntity>().Where(TableQuery.GenerateFilterConditionForInt("EmployeId", QueryComparisons.Equal, item.EmployeId));
                        TableQuerySegment<ConsolidatedEntity> consolidate = await consolidatedTable.ExecuteQuerySegmentedAsync(queryConsolidate, null);
                        if (consolidate.Results.Count > 0)
                        {
                            // foreach (ConsolidatedEntity itemConsolidate in consolidate.Results)
                            // {
                            // }
                            
                            int myIndex = consolidate.Results.FindIndex(p => {
                                return p.DateJob.Date == item.TimeMarker.Date;
                             });

                            if (myIndex > -1)
                            {
                                consolidate.Results[myIndex].MinutesWorked = consolidate.Results[myIndex].MinutesWorked + Convert.ToInt32((itemCompare.TimeMarker - item.TimeMarker).TotalMinutes);
                                TableOperation addOperationUpdate = TableOperation.Replace(consolidate.Results[myIndex]);
                                await consolidatedTable.ExecuteAsync(addOperationUpdate);
                                contUpdated = contUpdated + 1;
                            }
                            else
                            {
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
                                contAdded = contAdded + 1;
                            }

                        }
                        else
                        {
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
                            contAdded = contAdded + 1;
                        }
                        break;
                    }
                }
            }

            string message = $"Consolidation sumary, Records added: {contAdded}, records update {contUpdated}";
            log.LogInformation(message);

            return new OkObjectResult(new Response
            {
                IsSuccess = true,
                Message = message,
                Result = times
            });
        }

        [FunctionName(nameof(GetAllConsolidates))]
        public static async Task<IActionResult> GetAllConsolidates(
           [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "consolidated")] HttpRequest req,
           [Table("consolidated", Connection = "AzureWebJobsStorage")] CloudTable consolidatedTable,
           ILogger log)
        {
            log.LogInformation("Get all consolidate received");

            TableQuery<ConsolidatedEntity> query = new TableQuery<ConsolidatedEntity>();
            TableQuerySegment<ConsolidatedEntity> consolidate = await consolidatedTable.ExecuteQuerySegmentedAsync(query, null);

            string message = "Retrieved all consolidated";
            log.LogInformation(message);

            return new OkObjectResult(new Response
            {
                IsSuccess = true,
                Message = message,
                Result = consolidate
            });
        }

        [FunctionName(nameof(GetConsolidateByDate))]
        public static async Task<IActionResult> GetConsolidateByDate(
          [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "consolidated/{date}")] HttpRequest req,
          [Table("consolidated", Connection = "AzureWebJobsStorage")] CloudTable consolidatedTable,
          string date,
          ILogger log)
        {
            log.LogInformation($"Get consolidated by date: {date}, received");

            // validate times EmployedId
            TableQuery<ConsolidatedEntity> queryConsolidate = new TableQuery<ConsolidatedEntity>();
            TableQuerySegment<ConsolidatedEntity> consolidate = await consolidatedTable.ExecuteQuerySegmentedAsync(queryConsolidate, null);
            if (consolidate.Results.Count <= 0)
            {
                return new BadRequestObjectResult(new Response
                {
                    IsSuccess = false,
                    Message = "Consolidated register not found."
                });
            }

            int myIndex = consolidate.Results.FindIndex(p => {
                return p.DateJob.Date == Convert.ToDateTime(date).Date;
            });

            string message = $"Consolidated {date}, retrieved";
            log.LogInformation(message);

            return new OkObjectResult(new Response
            {
                IsSuccess = true,
                Message = message,
                Result = consolidate.Results[myIndex]
            });
        }
    }
}
