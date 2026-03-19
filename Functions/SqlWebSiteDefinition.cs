using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using SqlWebApi;
using Humanizer;
using Microsoft.OpenApi.Models;
using SqlWebApi.Configuration;

namespace sqlwebapi;

public class SqlWebSiteDefinition
{
    private static string version = "1.12";

    private  ILogger _logger;    
    private  IConfigProvider _config;

    public SqlWebSiteDefinition(ILoggerFactory loggerFactory,
        IConfigProvider configProvider)
    {
        _logger = loggerFactory.CreateLogger<SqlWebSiteDefinition>();
        _config = configProvider;
    }

    [OpenApiOperation(operationId: "SqlWebSiteDefinition", tags: new[] { "BuiltIn" },
        Summary = "Return json descriibing site")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/json", bodyType: typeof(string),
        Summary = "Successful operation", Description = "Successful operation")]
    [Function("SqlWebSiteDefinition")]
    [OpenApiParameter("service", In = ParameterLocation.Query, Required = false, Type = typeof(string),
        Summary = "The SQL schema of the generated stored procedure")]

    public async Task<HttpResponseData>
        Run([HttpTrigger(AuthorizationLevel.Anonymous,
                "get", "post", "options",
                Route = "swa/SqlWebSiteDefinition")]
            HttpRequestData req,
            string Service = "api")
    {
;
        string SqlSchema = 
            await _config.GetAsync(Service + ":SqlSchema");

        var connectionString = await _config.GetAsync(Service + ":SqlConnectionString");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new Exception("Missing database connectionString");



        if (connectionString == null)
        {
            throw new Exception("Missing database connectionString");
        }

        var model = ModelBuilder.ConstructModel(connectionString, SqlSchema);

        var document = new SqlSiteDocument() { Name = "SqlWebUI", Resources = [] };

        foreach (var controller in model.controllers)
        {
            string? err = "";
            bool hasJson = false;
            JsonElement je = JsonDocument.Parse("{}").RootElement.Clone();
            if (!string.IsNullOrWhiteSpace(controller.ui))
                hasJson = JsonHelpers.TryParseJsonElement(controller.ui, out je, out err);
            var resource = new UiResource()
            {
                Name = controller.name,
                RecordRepresentation = controller.name.Humanize(LetterCasing.Title),
                Fields = [],
                Options = new Options()
                {
                    Label = controller.name, HasEdit = true,
                    HasDelete = false, HasCreate = false, HasPagination = false,
                    HasSearch = false, HasSort = false
                },
                ui = je,
            };
            int i = 0;
            foreach (var column in controller.columns)
            {
                if (column.name == "total_rows")
                    resource.Options.HasPagination = true;
                else
                {
                    List<string> views = [];
                    if (i++ < 30)
                        views.Add("list");
                    var inPost = false;
                    {
                        var proc = controller.procs.FirstOrDefault(p => p.name.EndsWith("post"));
                        inPost = proc?.parameters.FirstOrDefault(param => param.name == "@" + column.name) != null;
                    }
                    if (inPost)
                        views.Add("create");

                    views.Add("show");

                    var inPut = false;
                    {
                        var p = controller.procs.FirstOrDefault(p => p.name.EndsWith("put"));
                        inPut = p?.parameters.FirstOrDefault(p => p.name == "@" + column.name) != null;
                    }
                    if (inPut)
                        views.Add("edit");
                    var reference = "";

                    //              Console.WriteLine("fieldType:" + column.sqlType);
                    var fieldType = "string";
                    if (column.name.EndsWith("_url"))
                        fieldType = "image";
                    else if (column.name.EndsWith("_id"))
                    {
                        fieldType = "reference";
                        reference = column.name.Substring(0, column.name.IndexOf("_id", StringComparison.Ordinal));
                    }
                    else if (column.sqlType == "Int32")
                        fieldType = "number";
                    else if (column.sqlType == "Boolean")
                        fieldType = "boolean";
                    else if (column.sqlType == "Money" || column.sqlType == "Decimal")
                        fieldType = "number";
                    else if (column.sqlType == "DateTime" || column.sqlType == "DateTime2")
                        fieldType = "date";

                    var validators = new List<string>();
                    if (!column.isNullable)
                        validators.Add("required");
                    var field = new Field()
                    {
                        Source = column.name,
                        Type = fieldType,
                        View = views,
                        Validators = validators,
                        Reference = reference,
                    };
                    resource.Fields.Add(field);
                }

                resource.Options.HasEdit = (controller.procs.FirstOrDefault(p => p.name.EndsWith("put")) != null);
                ;
                resource.Options.HasDelete = (controller.procs.FirstOrDefault(p => p.name.EndsWith("delete")) != null);
                ;
                resource.Options.HasCreate = (controller.procs.FirstOrDefault(p => p.name.EndsWith("post")) != null);
                ;
            }

            var getProc = controller.procs.FirstOrDefault(p => p.name.EndsWith("get"));
            if (getProc != null)
                foreach (var param in getProc.parameters)
                    if (param.name == "@search")
                        resource.Options.HasSearch = true;
                    else if (param.name == "@sort_field")
                        resource.Options.HasSort = true;

            if (resource.Name != "login")
                document.Resources.Add(resource);
        }

        var outputString = document.Serialize();

        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");

        await response.WriteStringAsync(outputString, Encoding.UTF8);

        return response;
    }
}
