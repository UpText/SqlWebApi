using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace swa.Functions;

public class HttpPing
{
    private readonly ILogger<HttpPing> _logger;

    public HttpPing(ILogger<HttpPing> logger)
    {
        _logger = logger;
    }

    [Function("HttpPing")]
    public HttpResponseData Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "ping")] HttpRequestData req)
    {
        _logger.LogInformation("Ping function processed a request.");
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
        response.WriteString("pong");
        return response;
    }
}