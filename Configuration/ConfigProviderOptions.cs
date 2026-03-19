using System;
using System.IO;

namespace SqlWebApi.Configuration
{
    public sealed class ConfigProviderOptions
    {
        public string ServiceName { get; set; }

        /// <summary>
        /// Path to a local JSON config file.
        /// For LocalFile mode: typically "appsettings.local.json".
        /// For LocalAzureFunctionsSettings: points to "local.settings.json".
        /// </summary>
        public string LocalConfigPath { get; set; } = "appsettings.local.json";

        /// <summary>
        /// Base URL of remote config API.
        /// </summary>
        public string ApiBaseUrl { get; set; }

        public ConfigProviderMode Mode { get; set; }

        /// <summary>
        /// Auto-detects how config should be loaded.
        ///
        /// Priority:
        ///   1. CONFIG_API_BASEURL set                 -> RemoteApi
        ///   2. DOTNET_RUNNING_IN_CONTAINER == "true"  -> Environment
        ///   3. local.settings.json exists (and not in container) -> LocalAzureFunctionsSettings
        ///   4. otherwise                              -> LocalFile
        /// </summary>
        public static ConfigProviderOptions AutoDetect(string serviceName, string localPath = "appsettings.local.json")
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentException("serviceName must be provided.", nameof(serviceName));

            // 1) Remote API
            var api = Environment.GetEnvironmentVariable("CONFIG_API_BASEURL");
            if (!string.IsNullOrWhiteSpace(api))
            {
                return new ConfigProviderOptions
                {
                    ServiceName = serviceName,
                    LocalConfigPath = localPath,
                    ApiBaseUrl = api,
                    Mode = ConfigProviderMode.RemoteApi
                };
            }

            // 2) Running in container => Environment
            var runningInContainer = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER");
            if (string.Equals(runningInContainer, "true", StringComparison.OrdinalIgnoreCase))
            {
                return new ConfigProviderOptions
                {
                    ServiceName = serviceName,
                    LocalConfigPath = localPath,
                    Mode = ConfigProviderMode.Environment
                };
            }

            // 3) Local Azure Functions debug: local.settings.json present
            var localSettingsPath = Path.Combine(Environment.CurrentDirectory, "local.settings.json");
            if (File.Exists(localSettingsPath))
            {
                return new ConfigProviderOptions
                {
                    ServiceName = serviceName,
                    LocalConfigPath = localSettingsPath,
                    Mode = ConfigProviderMode.LocalAzureFunctionsSettings
                };
            }

            // 4) Fallback: regular local file
            return new ConfigProviderOptions
            {
                ServiceName = serviceName,
                LocalConfigPath = localPath,
                Mode = ConfigProviderMode.LocalFile
            };
        }
    }
}
