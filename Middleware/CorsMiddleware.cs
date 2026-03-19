using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;

namespace SqlWebApi.Middleware;

public sealed class CorsMiddleware : IFunctionsWorkerMiddleware
{
    private const string AllowedMethods = "GET,POST,PUT,DELETE,OPTIONS";
    private const string DefaultAllowedHeaders = "Content-Type,Authorization,X-Requested-With,Range";

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var request = await context.GetHttpRequestDataAsync();
        if (request is null)
        {
            await next(context);
            return;
        }

        var origin = GetAllowedOrigin(request);
        if (string.Equals(request.Method, "OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            var preflightResponse = request.CreateResponse(HttpStatusCode.NoContent);
            AddCorsHeaders(preflightResponse, request, origin);
            context.GetInvocationResult().Value = preflightResponse;
            return;
        }

        await next(context);

        if (context.GetInvocationResult().Value is HttpResponseData response)
            AddCorsHeaders(response, request, origin);
    }

    private static string? GetAllowedOrigin(HttpRequestData request)
    {
        var configuredOrigins = Environment.GetEnvironmentVariable("AllowedCorsOrigins")
                               ?? Environment.GetEnvironmentVariable("CORS_ALLOWED_ORIGINS")
                               ?? "http://localhost:8082";

        var allowedOrigins = configuredOrigins
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (!request.Headers.TryGetValues("Origin", out var originValues))
            return allowedOrigins.Contains("*", StringComparer.Ordinal)
                ? "*"
                : allowedOrigins.FirstOrDefault();

        var requestOrigin = originValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(requestOrigin))
            return null;

        if (allowedOrigins.Contains("*", StringComparer.Ordinal))
            return requestOrigin;

        return allowedOrigins.Any(allowedOrigin =>
            string.Equals(allowedOrigin, requestOrigin, StringComparison.OrdinalIgnoreCase))
            ? requestOrigin
            : null;
    }

    private static void AddCorsHeaders(HttpResponseData response, HttpRequestData request, string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
            return;

        response.Headers.Remove("Access-Control-Allow-Origin");
        response.Headers.Remove("Access-Control-Allow-Methods");
        response.Headers.Remove("Access-Control-Allow-Headers");
        response.Headers.Remove("Access-Control-Allow-Credentials");
        response.Headers.Remove("Vary");

        response.Headers.Add("Access-Control-Allow-Origin", origin);
        response.Headers.Add("Access-Control-Allow-Methods", AllowedMethods);
        response.Headers.Add("Access-Control-Allow-Headers", GetAllowedHeaders(request));
        response.Headers.Add("Access-Control-Allow-Credentials", "true");
        response.Headers.Add("Vary", "Origin");
    }

    private static string GetAllowedHeaders(HttpRequestData request)
    {
        if (!request.Headers.TryGetValues("Access-Control-Request-Headers", out var requestedHeaders))
            return DefaultAllowedHeaders;

        var headerValue = requestedHeaders.FirstOrDefault();
        return string.IsNullOrWhiteSpace(headerValue) ? DefaultAllowedHeaders : headerValue;
    }
}
