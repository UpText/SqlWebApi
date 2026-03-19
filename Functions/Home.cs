using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

public class Home
{
    [Function("Home")]
    public HttpResponseData Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "/")] HttpRequestData req)
    {
        var res = req.CreateResponse(HttpStatusCode.OK);
        res.Headers.Add("Content-Type", "text/html; charset=utf-8");

        res.WriteString("""
                        <!doctype html>
                        <html>
                        <head>
                          <meta charset="utf-8" />
                          <title>SqlWebApi</title>
                          <style>
                            body { font-family: system-ui, sans-serif; margin: 40px; }
                            .card { max-width: 720px; padding: 24px; border: 1px solid #ddd; border-radius: 12px; }
                            a { display:block; margin: 8px 0; }
                          </style>
                        </head>
                        <body>
                          <div class="card">
                            <h1>SqlWebApi 🚀</h1>
                            <p>Azure Functions isolated — Docker hosted</p>

                            <a href="/docs">Swagger UI</a>
                            <a href="/docs/crmapi">Swagger UI for crmapi</a>
                            <a href="/docs/{service}">Swagger UI template</a>
                            <a href="/swagger.json">OpenAPI</a>
                            <a href="/health">Health</a>
                          </div>
                        </body>
                        </html>
                        """);

        return res;
    }
}
