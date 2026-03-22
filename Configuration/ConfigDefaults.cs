using System;
using System.Collections.Generic;

namespace SqlWebApi.Configuration
{
    public static class ConfigDefaults
    {
        private static readonly IReadOnlyDictionary<string, string> Defaults =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["SqlWebApiServiceName"] = "SqlWebApi",
                ["AllowedCorsOrigins"] = "http://localhost:8082",
                ["CORS_ALLOWED_ORIGINS"] = "http://localhost:8082",
                ["JWT_HOURS"] = "8"
            };

        public static string? GetDefault(string key)
        {
            return Defaults.TryGetValue(key, out var value) ? value : null;
        }

        public static string? GetValue(params string[] keys)
        {
            foreach (var key in keys)
            {
                var value = Environment.GetEnvironmentVariable(key);
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            foreach (var key in keys)
            {
                var value = GetDefault(key);
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return null;
        }

        public static string GetRequiredValue(string key)
        {
            return GetValue(key) ??
                   throw new Exception($"{key} environment variable is not set.");
        }
    }
}
