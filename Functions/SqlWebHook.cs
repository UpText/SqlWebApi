using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using SqlWebApi.Services;

namespace SqlWebApi;

public class SqlWebHook
{
    private readonly ILogger _logger;
    private readonly ISqlWebApiExecutor _executor;

    public SqlWebHook(ILoggerFactory loggerFactory, ISqlWebApiExecutor executor)
    {
        _logger = loggerFactory.CreateLogger<SqlWebHook>();
        _executor = executor;
    }

    [Function("SqlWebHook")]
    public Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", "options",
            Route = "webhook/{Service}/{Resource}/{Id?}/{Details?}")]
        HttpRequestData req,
        string Service,
        string Resource,
        string? Id = null,
        string? Details = null)
    {
        // Separate seam for webhook-specific validation, signature checks, or payload normalization.
        _logger.LogInformation("SqlWebHook received request for {Service}/{Resource}", Service, Resource);
        return _executor.ExecuteAsync(req, Service, Resource, Id, Details);
    }
}
