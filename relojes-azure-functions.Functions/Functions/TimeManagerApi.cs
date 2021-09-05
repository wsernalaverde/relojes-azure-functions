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
           [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "times")] HttpRequest req,
           [Table("times", Connection = "AzureWebJobsStorage")] CloudTable timesTable,
           ILogger log)
        {
            log.LogInformation("Recieved a new Employe's time.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            TimeManager time = JsonConvert.DeserializeObject<TimeManager>(requestBody);

            if (time?.EmployeId == null || time?.EmployeId == 0)
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
                EmployeId = time.EmployeId,
                TimeMarker = DateTime.UtcNow,
                Type = time.Type
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

        [FunctionName(nameof(UpdateTime))]
        public static async Task<IActionResult> UpdateTime(
          [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "times/{id}")] HttpRequest req,
          [Table("times", Connection = "AzureWebJobsStorage")] CloudTable timesTable,
          string id,
          ILogger log)
        {
            log.LogInformation($"Update time for employe: {id}, received");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            TimeManager time = JsonConvert.DeserializeObject<TimeManager>(requestBody);

            // validate todo id
            TableOperation finOperation = TableOperation.Retrieve<TimeManagerEntity>("TIMES", id);
            TableResult findResult = await timesTable.ExecuteAsync(finOperation);
            if (findResult.Result == null)
            {
                return new BadRequestObjectResult(new Response
                {
                    IsSuccess = false,
                    Message = "Time for employe not found."
                });
            }

            //Update todo
            TimeManagerEntity timeEntity = (TimeManagerEntity)findResult.Result;
            timeEntity.IsConsolidated = time.IsConsolidated;
            if (time.TimeMarker != DateTime.MinValue)
            {
                timeEntity.TimeMarker = time.TimeMarker;      
            }

            TableOperation addOperation = TableOperation.Replace(timeEntity);
            await timesTable.ExecuteAsync(addOperation);

            string message = $"Register time: {id}, updated in table";
            log.LogInformation(message);

            return new OkObjectResult(new Response
            {
                IsSuccess = true,
                Message = message,
                Result = timeEntity
            });
        }

        [FunctionName(nameof(GetAllTimes))]
        public static async Task<IActionResult> GetAllTimes(
           [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "times")] HttpRequest req,
           [Table("times", Connection = "AzureWebJobsStorage")] CloudTable timesTable,
           ILogger log)
        {
            log.LogInformation("Get all times received");

            TableQuery<TimeManagerEntity> query = new TableQuery<TimeManagerEntity>();
            TableQuerySegment<TimeManagerEntity> times = await timesTable.ExecuteQuerySegmentedAsync(query, null);

            string message = "Retrieved all times";
            log.LogInformation(message);

            return new OkObjectResult(new Response
            {
                IsSuccess = true,
                Message = message,
                Result = times
            });
        }

        [FunctionName(nameof(GetTimeById))]
        public static IActionResult GetTimeById(
           [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "times/{id}")] HttpRequest req,
           [Table("times", "TIMES", "{id}", Connection = "AzureWebJobsStorage")] TimeManagerEntity timesEntity,
           string id,
           ILogger log)
        {
            log.LogInformation($"Get time register by id: {id}, received");

            if (timesEntity == null)
            {
                return new BadRequestObjectResult(new Response
                {
                    IsSuccess = false,
                    Message = "Time register not found."
                });
            }

            string message = $"Time register {id}, retrieved";
            log.LogInformation(message);

            return new OkObjectResult(new Response
            {
                IsSuccess = true,
                Message = message,
                Result = timesEntity
            });
        }

        [FunctionName(nameof(DeleteTodo))]
        public static async Task<IActionResult> DeleteTodo(
           [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "times/{id}")] HttpRequest req,
           [Table("times", "TIMES", "{id}", Connection = "AzureWebJobsStorage")] TimeManagerEntity timesEntity,
           [Table("times", Connection = "AzureWebJobsStorage")] CloudTable timesTable,
           string id,
           ILogger log)
        {
            log.LogInformation($"Delete todo: {id}, received");

            if (timesEntity == null)
            {
                return new BadRequestObjectResult(new Response
                {
                    IsSuccess = false,
                    Message = "Time register not found."
                });
            }

            await timesTable.ExecuteAsync(TableOperation.Delete(timesEntity));
            string message = $"Time register {id}, deleted";
            log.LogInformation(message);

            return new OkObjectResult(new Response
            {
                IsSuccess = true,
                Message = message,
                Result = timesEntity
            });
        }
    }
}
