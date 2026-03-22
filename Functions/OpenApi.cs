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
using System.Collections.Generic;
using sqlwebapi;
using SqlWebApi.Configuration;

namespace SqlWebApi
{
    public class OpenApi
    {
        private readonly ILogger _logger;
        private  IConfigProvider _config; 
        

        public OpenApi(ILoggerFactory loggerFactory,
            IConfigProvider configProvider)
        {
            _logger = loggerFactory.CreateLogger<OpenApi>();
            _config = configProvider;
        }

        private static string? GetFirstHeaderValue(HttpHeadersCollection headers, params string[] names)
        {
            foreach (var name in names)
            {
                if (headers.TryGetValues(name, out IEnumerable<string>? values))
                {
                    var value = values.FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(value))
                        return value.Split(',')[0].Trim();
                }
            }

            return null;
        }

        private static string GetServerUrl(HttpRequestData req, string service)
        {
            var configuredBaseUrl = ConfigDefaults.GetValue("OPENAPI_PUBLIC_BASEURL");
            if (!string.IsNullOrWhiteSpace(configuredBaseUrl))
            {
                return $"{configuredBaseUrl.TrimEnd('/')}/swa/{Uri.EscapeDataString(service)}";
            }

            var forwardedProto = GetFirstHeaderValue(req.Headers, "X-Forwarded-Proto", "X-Original-Proto");
            var forwardedHost = GetFirstHeaderValue(req.Headers, "X-Forwarded-Host", "X-Original-Host", "Host");
            var forwardedPrefix = GetFirstHeaderValue(req.Headers, "X-Forwarded-Prefix", "X-Original-PathBase");

            var builder = new UriBuilder(req.Url);

            if (!string.IsNullOrWhiteSpace(forwardedProto))
                builder.Scheme = forwardedProto;

            if (!string.IsNullOrWhiteSpace(forwardedHost))
            {
                if (forwardedHost.Contains(':', StringComparison.Ordinal))
                {
                    var hostParts = forwardedHost.Split(':', 2);
                    builder.Host = hostParts[0];
                    if (int.TryParse(hostParts[1], out var forwardedPort))
                        builder.Port = forwardedPort;
                }
                else
                {
                    builder.Host = forwardedHost;
                    builder.Port = builder.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ? 443 : 80;
                }
            }

            var pathBase = string.IsNullOrWhiteSpace(forwardedPrefix) ? string.Empty : "/" + forwardedPrefix.Trim('/');
            builder.Path = $"{pathBase}/swa/{Uri.EscapeDataString(service)}".Replace("//", "/");
            builder.Query = string.Empty;
            builder.Fragment = string.Empty;

            if ((builder.Scheme == "https" && builder.Port == 443) ||
                (builder.Scheme == "http" && builder.Port == 80))
            {
                builder.Port = -1;
            }

            return builder.Uri.ToString().TrimEnd('/');
        }

        [Function("OpenApi")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", "options",
            Route =  "swa/{Service}/swagger.json" )] HttpRequestData req, 
            string service)
        {
            string? connectionString = null;

            string schema = // "api";
                await _config.GetAsync(service + ":SqlSchema");

            
            connectionString = await _config.GetAsync(service + ":SqlConnectionString");
                //??= Environment.GetEnvironmentVariable("SqlConnectionString");

            if (connectionString == null)
            {
                throw new Exception("Missing database connectionString");
            }

            var model = ModelBuilder.ConstructModel(connectionString,schema);            
            
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");
            
            var path = GetServerUrl(req, service);
            var document = new OpenApiDocument
            {
                Info = new OpenApiInfo
                {
                    Version = "1.0.0",
                    Title = $"OpenApi document generated for service: {service} by SqlWebApi ",
                },
                Servers = new List<OpenApiServer>
                {
                      new OpenApiServer {  Url = path}
                },
                Paths = new OpenApiPaths
                {
                }
                
            };
            document.AddJwtBearer(
                schemeName: "Bearer",
                alsoApplyToOperations: false, // set true if your tooling ignores top-level requirements
                description: "Enter: Bearer {token}"
            );
            
            
            foreach (var controller in model.controllers)
            {
                var operations = new Dictionary<OperationType, OpenApiOperation>();
                foreach (var proc in controller.procs)
                {
                    var operation = new OpenApiOperation();
                    operation.Tags.Add(new OpenApiTag { Name = controller.name });
                    operation.Responses = new OpenApiResponses
                    {
                        ["200"] = new OpenApiResponse
                        {
                            Description = "OK"
                        }
                    };
                    foreach (var openApiUpdate in controller.openApiUpdates)
                        if (openApiUpdate.operation == "*" || proc.name.EndsWith(openApiUpdate.operation))
                            if (openApiUpdate.className == "response" && openApiUpdate.property == "description")
                                operation.Responses.Add(openApiUpdate.name, new OpenApiResponse { Description = openApiUpdate.value } );
                    
                    operation.Parameters = new List<OpenApiParameter>();
                    foreach (var param in proc.parameters)
                    {
                        if (!param.name.ToLower().StartsWith("@auth_"))
                        {
                            var p = new OpenApiParameter();
                            p.Name = param.name?.Substring(1); // Remove @
                            p.In = ParameterLocation.Query;
                            operation.Parameters.Add(p); 
                            // Add attributes from openApiUpdates to openApiParameter
                            foreach (var openApiUpdate in controller.openApiUpdates)
                                if (openApiUpdate.operation == "*" || proc.name.EndsWith(openApiUpdate.operation))
                                    if (openApiUpdate.className == "parameter" && p.Name == openApiUpdate.name && openApiUpdate.property == "description")
                                        p.Description = openApiUpdate.value;
                        }
                    }
                    // Add attributes from openApiUpdates to operation
                    foreach (var openApiUpdate in controller.openApiUpdates)
                        if (proc.name.EndsWith(openApiUpdate.operation))
                            if (openApiUpdate.className == "operation") 
                                if ( openApiUpdate.property == "description")
                                    operation.Description = openApiUpdate.value;
                                else if (openApiUpdate.property == "summary")
                                    operation.Summary = openApiUpdate.value;
                                else if (openApiUpdate.property == "tag")
                                    operation.Tags.Add(new OpenApiTag { Name = openApiUpdate.name, Description = openApiUpdate.value });                                   
                                    
                    
                    if (proc.name.EndsWith("get"))
                        operations.Add(OperationType.Get,operation);
                    if (proc.name.EndsWith("post"))
                        operations.Add(OperationType.Post,operation);
                    if (proc.name.EndsWith("put"))
                        operations.Add(OperationType.Put,operation);
                    if (proc.name.EndsWith("delete"))
                        operations.Add(OperationType.Delete,operation);
                    
                }

                var pathItem = new OpenApiPathItem();
                pathItem.Operations = operations;
                document.Paths.Add("/" + controller.name,pathItem);
            }
            
            
            var outputString = document.Serialize(OpenApiSpecVersion.OpenApi3_0, OpenApiFormat.Json);
            response.WriteStringAsync(outputString);
            return response;
        }
    }
}
