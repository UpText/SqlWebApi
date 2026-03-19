using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace swa.Functions;

public class ServerTime
{
    private readonly ILogger<ServerTime> _logger;

    public ServerTime(ILogger<ServerTime> logger)
    {
        _logger = logger;
    }

    [Function("ServerTime")]
    public HttpResponseData Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "time")] HttpRequestData req)
    {
        var nowLocal = DateTimeOffset.Now;
        var nowUtc = nowLocal.ToUniversalTime();

        _logger.LogInformation("ServerTime requested at {UtcNow}", nowUtc);

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        response.WriteString($$"""
                              {
                                "serverTimeLocal": "{{nowLocal:O}}",
                                "serverTimeUtc": "{{nowUtc:O}}",
                                "timeZone": "{{TimeZoneInfo.Local.Id}}"
                              }
                              """);
        return response;
    }
}
