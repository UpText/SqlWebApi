using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SqlWebApi.Services;

namespace SqlWebApi
{ 
    public class SqlWebApiFunc
    {
        private readonly ISqlWebApiExecutor _executor;

        public SqlWebApiFunc(ISqlWebApiExecutor executor)
        {
            _executor = executor;
        }
#if OIDAUTH
        [Authorize]
        [OpenApiSecurity("bearer_auth",
            SecuritySchemeType.Http,
            Scheme = OpenApiSecuritySchemeType.Bearer,
            BearerFormat = "JWT")]
#endif
        [Function("SqlWebApi")]
        public Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", "put", "delete", "options",
                Route = "swa/{Service}/{Resource}/{Id?}/{Details?}")]
            HttpRequestData req,
            string Service,
            string Resource,
            string? Id = null,
            string? Details = null,
            FunctionContext context = null!)
        {
            return _executor.ExecuteAsync(req, Service, Resource, Id, Details);
        }
    }
}
