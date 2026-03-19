using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Caching.Memory;
using SqlWebApi;
using SqlWebApi.Configuration;
using SqlWebApi.Middleware;
using SqlWebApi.Services;

var logConnectionString = Environment.GetEnvironmentVariable("SqlServerLogDb");
if (!string.IsNullOrEmpty(logConnectionString))
    SqlLog.CheckOrCreateTable("dbo", "log",
        logConnectionString);

var host = Host.CreateDefaultBuilder(args)
    .ConfigureFunctionsWorkerDefaults(worker => { worker.UseMiddleware<CorsMiddleware>(); })
    .ConfigureServices(services =>
    {
        services.AddMemoryCache();
        var serviceName = Environment.GetEnvironmentVariable("SqlWebApiServiceName") ?? "SqlWebApi";
        var configProvider = AppConfigFactory.CreateConfigProvider(serviceName);
        services.AddSingleton<IConfigProvider>(configProvider);
        services.AddSingleton<ISqlWebApiExecutor, SqlWebApiExecutor>();
    })
    .Build();

host.Run();
