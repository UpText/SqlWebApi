using System;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using SqlWebApi;
public class SqlConnectionTestFunction
{
    private readonly IConfiguration _configuration;

    public SqlConnectionTestFunction(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [Function("SqlConnectionTest")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", 
            Route = "sqltest/{Service}")]
        HttpRequestData req,
        string Service,
        FunctionContext context)
    {
        CancellationToken cancellationToken = context.CancellationToken;

        // Try both patterns: ConnectionStrings:SqlDb or SqlConnectionString
        string connectionString =Environment.GetEnvironmentVariable("SqlConnectionString");

        var result = await SqlConnectionTester.TestAsync(
            connectionString,
            getServerTime: true,
            cancellationToken: cancellationToken);

        var response = req.CreateResponse(
            result.Success ? HttpStatusCode.OK : HttpStatusCode.InternalServerError);

        var payload = new
        {
            success = result.Success,
            message = result.Message,
            serverTime = result.ServerTime,
            exceptionMessage = result.Exception != null
                ? result.Exception.Message
                : null
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await response.WriteStringAsync(json, cancellationToken);
        return response;
    }
}