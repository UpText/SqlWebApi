using Microsoft.Azure.Functions.Worker.Http;

namespace SqlWebApi.Services;

public interface ISqlWebApiExecutor
{
    Task<HttpResponseData> ExecuteAsync(
        HttpRequestData req,
        string service,
        string resource,
        string? id = null,
        string? details = null);
}
