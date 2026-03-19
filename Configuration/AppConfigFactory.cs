using System;

namespace SqlWebApi.Configuration
{
    /// <summary>
    /// Factory for creating ConfigProvider when the Azure Function
    /// already resolved the correct serviceName.
    /// </summary>
    public static class AppConfigFactory
    {
        /// <summary>
        /// serviceName = already parsed tenant + service
        /// e.g. "acme-sqlwebapi"
        /// </summary>
        public static ConfigProvider CreateConfigProvider(
            string serviceName,
            string localConfigPath = "appsettings.local.json")
        {
            if (string.IsNullOrWhiteSpace(serviceName))
                throw new ArgumentException("serviceName must be provided.");

            var options = ConfigProviderOptions.AutoDetect(serviceName, localConfigPath);

            // Optional: override remote API URL if env var exists
            var api = Environment.GetEnvironmentVariable("CONFIG_API_BASEURL");
            if (!string.IsNullOrWhiteSpace(api))
            {
                options.ApiBaseUrl = api;
            }

            return new ConfigProvider(options);
        }
    }
}