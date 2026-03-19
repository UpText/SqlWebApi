using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace SqlWebApi;

public class ServiceSwaggerUI
{
    private const string DefaultSwaggerUrl = "http://api.uptext.com/swagger.json";

    [Function("ServiceSwaggerUI")]
    public HttpResponseData Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "docs")] HttpRequestData req)
    {
        return RenderSwaggerUi(req, null);
    }

    [Function("ServiceSwaggerUIByService")]
    public HttpResponseData RunByService(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "docs/{service}")] HttpRequestData req,
        string service)
    {
        return RenderSwaggerUi(req, service);
    }

    private static HttpResponseData RenderSwaggerUi(HttpRequestData req, string? service)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        response.Headers.Add("Content-Type", "text/html; charset=utf-8");

        var swaggerUrl = string.IsNullOrWhiteSpace(service)
            ? DefaultSwaggerUrl
            : $"/swa/{Uri.EscapeDataString(service)}/swagger.json";
        var pageTitle = string.IsNullOrWhiteSpace(service)
            ? "Swagger UI"
            : $"Swagger UI - {WebUtility.HtmlEncode(service)}";
        var headerText = string.IsNullOrWhiteSpace(service)
            ? $"Swagger UI loading {WebUtility.HtmlEncode(swaggerUrl)}"
            : $"Swagger UI for <code>{WebUtility.HtmlEncode(service)}</code> loading <code>{WebUtility.HtmlEncode(swaggerUrl)}</code>";

        response.WriteString($$"""
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>{{pageTitle}}</title>
  <link rel="stylesheet" href="https://unpkg.com/swagger-ui-dist@5/swagger-ui.css" />
  <style>
    html { box-sizing: border-box; overflow-y: scroll; }
    *, *:before, *:after { box-sizing: inherit; }
    body { margin: 0; background: #faf7f1; }
    .topbar {
      padding: 12px 20px;
      border-bottom: 1px solid #ddd4c8;
      background: #f3ecdf;
      font-family: system-ui, sans-serif;
    }
    .topbar code {
      padding: 2px 6px;
      border-radius: 6px;
      background: #fffaf2;
    }
  </style>
</head>
<body>
  <div class="topbar">
    {{headerText}}
  </div>
  <div id="swagger-ui"></div>
  <script src="https://unpkg.com/swagger-ui-dist@5/swagger-ui-bundle.js" crossorigin></script>
  <script src="https://unpkg.com/swagger-ui-dist@5/swagger-ui-standalone-preset.js" crossorigin></script>
  <script>
    window.onload = function () {
      window.ui = SwaggerUIBundle({
        url: {{System.Text.Json.JsonSerializer.Serialize(swaggerUrl)}},
        dom_id: '#swagger-ui',
        deepLinking: true,
        presets: [
          SwaggerUIBundle.presets.apis,
          SwaggerUIStandalonePreset
        ],
        layout: "StandaloneLayout"
      });
    };
  </script>
</body>
</html>
""");

        return response;
    }
}
