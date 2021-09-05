using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using relojes_azure_functions.Common.Models;
using relojes_azure_functions.Common.Responses;
using relojes_azure_functions.Functions.Entities;
using System;
using System.IO;
using System.Threading.Tasks;

namespace relojes_azure_functions.Functions.Functions
{
    public static class TimeManagerApi
    {
        [FunctionName(nameof(CreateTimeManager))]
        public static async Task<IActionResult> CreateTimeManager(
           [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "todo")] HttpRequest req,
           [Table("times", Connection = "AzureWebJobsStorage")] CloudTable timesTable,
           ILogger log)
        {
            log.LogInformation("Recieved a new Employe's time.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            TimeManager times = JsonConvert.DeserializeObject<TimeManager>(requestBody);

            if (times?.EmployeId == null || times?.EmployeId == 0)
            {
                return new BadRequestObjectResult(new Response
                {
                    IsSuccess = false,
                    Message = "The request must have a TimeMarker for employe"
                });
            }

            TimeManagerEntity timeEntity = new TimeManagerEntity
            {
                ETag = "*",
                IsConsolidated = false,
                PartitionKey = "TIMES",
                RowKey = Guid.NewGuid().ToString(),
                EmployeId = times.EmployeId,
                TimeMarker = DateTime.UtcNow,
                Type = times.Type
            };

            TableOperation addOperation = TableOperation.Insert(timeEntity);
            await timesTable.ExecuteAsync(addOperation);

            string message = "New time stored in table";
            log.LogInformation(message);

            return new OkObjectResult(new Response
            {
                IsSuccess = true,
                Message = message,
                Result = timeEntity
            });
        }
    }
}
