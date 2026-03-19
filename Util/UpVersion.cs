using System.Net;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;

namespace SqlWebApi
{
    public class UpVersion
    {
        private readonly ILogger _logger;

        public UpVersion(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<UpVersion>();
        }

        private static string version = "1.13"; 
        [OpenApiOperation(operationId: "Version", tags: new[] { "BuiltIn" }, Summary = "Return version of SqlWebAPi")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Summary = "Successful operation", Description = "Successful operation")]
        [Function("Version")]
        // public HttpResponseData 
        public async Task<IActionResult> 
            Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req)
        {
            var resp = $"SqlWebApi Version: {version}";
            // var headers = req.Headers;
            // var response = req.CreateResponse(HttpStatusCode.OK);
            // response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
            // await response.WriteStringAsync(resp);
            // // _logger.LogInformation($"Version called. Return: {version} ");
            // var ret = response;
            return new OkObjectResult(resp);
        }
    }
}


