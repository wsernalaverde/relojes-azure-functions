using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;
using relojes_azure_functions.Functions.Entities;

namespace relojes_azure_functions.Functions.Functions
{
    public static class ScheduleConsolidated
    {
        [FunctionName("ScheduleConsolidated")]
        public static async Task Run(
            [TimerTrigger("0 */20 * * * *")] TimerInfo myTimer,
            [Table("times", Connection = "AzureWebJobsStorage")] CloudTable timesTable,
            [Table("consolidated", Connection = "AzureWebJobsStorage")] CloudTable consolidatedTable,
            ILogger log)
        {
            log.LogInformation($"Consolidated task completed function executed at: {DateTime.Now}");

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
                            int myIndex = consolidate.Results.FindIndex(p => p.DateJob.Day == item.TimeMarker.Day);
                            if (myIndex > -1)
                            {
                                consolidate.Results[0].MinutesWorked = consolidate.Results[0].MinutesWorked + Convert.ToInt32((itemCompare.TimeMarker - item.TimeMarker).TotalMinutes);
                                TableOperation addOperationUpdate = TableOperation.Replace(consolidate.Results[0]);
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
                        Console.Write($"hola {(itemCompare.TimeMarker - item.TimeMarker).TotalMinutes}");
                        Console.Write($"ini {item.TimeMarker}");
                        Console.Write($"end {itemCompare.TimeMarker}");
                        break;
                    }
                }
            }

            string message = $"Consolidation sumary, Records added: {contAdded}, records update {contUpdated}, at: {DateTime.Now}";
            log.LogInformation(message);
        }
    }
}
