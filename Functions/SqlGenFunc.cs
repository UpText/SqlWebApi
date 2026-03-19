using System.Net;
using Microsoft.AspNetCore.Routing;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Extensions;
using Microsoft.OpenApi.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using sqlwebapi;
using SqlWebApi.Configuration;

namespace SqlWebApi
{
    public class SqlGenFunc
    {
        private readonly ILogger _logger;
        private readonly IConfigProvider _config;
        public SqlGenFunc(ILoggerFactory loggerFactory, IConfigProvider configProvider)
        {
            _config = configProvider;
            //  _logger = loggerFactory.CreateLogger<OpenApi>();
        }


        [OpenApiOperation(operationId: "SqlGenerator", tags: new[] { "BuiltIn" }, Summary = "Generate SQL")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string),
            Summary = "Successful operation", Description = "Successful operation")]
        [OpenApiParameter("service", In = ParameterLocation.Query, Required = true, Type = typeof(string), 
            Summary = "The SQL schema of the generated stored procedure")]
        [OpenApiParameter("schema", In = ParameterLocation.Query, Required = true, Type = typeof(string),
            Summary = "The SQL schema of the table")]
        [OpenApiParameter("table", In = ParameterLocation.Query, Required = true, Type = typeof(string),
            Summary = "The name of the table")]
        [OpenApiParameter("verb", In = ParameterLocation.Query, Required = true, Type = typeof(string),
            Summary = "The http verb. (get,post,put,delete)")]
        [OpenApiParameter("paging", In = ParameterLocation.Query, Required = true, Type = typeof(bool),
            Summary = "Generate paging")]
        [OpenApiParameter("sort", In = ParameterLocation.Query, Required = true, Type = typeof(bool),
            Summary = "Generate orderBy from parameter ")]
        [OpenApiParameter("search", In = ParameterLocation.Query, Required = true, Type = typeof(bool),
            Summary = "Generate search functionality")]
        // [OpenApiParameter("exec", In = ParameterLocation.Query, Required = false, Type = typeof(bool),
        //     Summary = "Add/Update the database automatically")]
        [Function("SqlGenerator")]
        public async Task<HttpResponseData>  Run([HttpTrigger(AuthorizationLevel.Anonymous,
                "get", "post",
                Route = "SqlGenerator")]
            HttpRequestData req,
            string service = "api",
            string schema = "dbo",
            string table = "categories",
            string verb = "GET",
        bool paging = false,
        bool sort = false,
        bool search = false

            
         )
        {
            
            bool exec = false;
            string sqlSchema = await _config.GetAsync(service + ":SqlSchema");
            var connectionString = await _config.GetAsync(service + ":SqlConnectionString");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new Exception("Missing database connectionString");
            }

            var resourceName = table;
            var model = ModelBuilder.ConstructModel(connectionString, schema);
            var tableModel = ModelBuilder.ConstructTableModel(connectionString, schema, table, resourceName);
            var code = SqlCodeBuilder.BuildTableProc(sqlSchema, schema, tableModel, verb, search, paging, sort);
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain");
            response.WriteStringAsync(code);
            return response;
        }
    }
}