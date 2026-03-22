using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;

namespace SqlWebApi.Configuration
{
    public sealed class ConfigProvider : IConfigProvider, IDisposable
    {
        private readonly ConfigProviderOptions _options;
        private readonly HttpClient _httpClient;
        
        private readonly Dictionary<string, (string Value, DateTimeOffset ExpiresAt)> _cache
            = new();
        private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);
        private readonly object _cacheLock = new();
        
        
        private Dictionary<string, string> _fileConfig;
        private bool _fileLoaded;

        public ConfigProvider(ConfigProviderOptions options)
            : this(options, new HttpClient())
        {
        //    _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        public ConfigProvider(ConfigProviderOptions options, HttpClient httpClient)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

            if (string.IsNullOrWhiteSpace(_options.ServiceName))
                throw new ArgumentException("ServiceName must be provided.");
        }

        // ------------------------------------------------------------
        // PUBLIC API
        // ------------------------------------------------------------
        public async Task<string> GetAsync(string key, CancellationToken cancellationToken = default)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            var service = key.Split(':')[0];
            key = key.Split(':')[1];

            switch (_options.Mode)
            {
                case ConfigProviderMode.RemoteApi:
                    return await GetFromApiAsync(service, key, cancellationToken).ConfigureAwait(false);

                case ConfigProviderMode.Environment:
                    return GetFromEnvironment(service, key);

                case ConfigProviderMode.LocalFile:
                case ConfigProviderMode.LocalAzureFunctionsSettings:
                    return await GetFromFileAsync(key, cancellationToken).ConfigureAwait(false);

                default:
                    throw new InvalidOperationException("Unknown config mode: " + _options.Mode);
            }
        }
        
        public async Task<string> GetCachedAsync(string url, CancellationToken ct)
        {
            // Try cache
            lock (_cacheLock)
            {
                if (_cache.TryGetValue(url, out var entry))
                {
                    if (entry.ExpiresAt > DateTime.UtcNow)
                        return entry.Value;

                    // expired → remove
                    _cache.Remove(url);
                }
            }

            // Fetch from remote
            var response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            // Store in cache
            lock (_cacheLock)
            {
                _cache[url] = (body, DateTime.UtcNow.Add(_cacheDuration));
            }

            return body;
        }
        
        public async Task<T> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
     //       var value = await GetAsync(key, cancellationToken).ConfigureAwait(false);
            var value = await GetCachedAsync(key, cancellationToken).ConfigureAwait(false);
            if (value == null)
                return default;

            return (T)Convert.ChangeType(value, typeof(T));
        }

        // ------------------------------------------------------------
        // ENVIRONMENT MODE
        // ------------------------------------------------------------
        private string GetFromEnvironment(string service, string key)
        {
            string value = null;
            // service + key:   API__CONNECTIONSTRING
            var envKey = (service + "__" + key)
                .Replace('.', '_')
                .Replace('-', '_')
                .ToUpperInvariant();
            value = Environment.GetEnvironmentVariable(envKey);
            if (value != null) return value;
            
            value = Environment.GetEnvironmentVariable(key);
            if (value != null) return value;
            value = ConfigDefaults.GetDefault(key);
            if (value != null) return value;
            
            // SERVICE-NAME__KEY  (normalized)
            envKey = (_options.ServiceName + "__" + key)
                .Replace('.', '_')
                .Replace('-', '_')
                .ToUpperInvariant();

            value = Environment.GetEnvironmentVariable(envKey);
            if (value != null) return value;

            return ConfigDefaults.GetDefault(envKey);
        }

        // ------------------------------------------------------------
        // FILE MODES (LocalFile + LocalAzureFunctionsSettings)
        // ------------------------------------------------------------
        private async Task<string> GetFromFileAsync(string key, CancellationToken cancellationToken)
        {
            await EnsureLocalFileLoadedAsync(cancellationToken).ConfigureAwait(false);

            if (_fileConfig == null)
                return null;

            string v;
            return _fileConfig.TryGetValue(key, out v) ? v : null;
        }

        /// <summary>
        /// Loads JSON config into _fileConfig.
        /// Supports:
        ///   - Flat JSON: { "Key": "Value", ... }
        ///   - Azure Functions local.settings.json:
        ///       { "IsEncrypted": false, "Values": { "Key": "Value" } }
        /// </summary>
        private async Task EnsureLocalFileLoadedAsync(CancellationToken cancellationToken)
        {
            if (_fileLoaded) return;

            _fileConfig = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var path = _options.LocalConfigPath;
            if (!File.Exists(path))
            {
                // Optional: you can throw here if you want it to be required
                _fileLoaded = true;
                return;
            }

            using (var stream = File.OpenRead(path))
            {
                var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
                var root = doc.RootElement;

                // Case 1: local.settings.json shape
                if (_options.Mode == ConfigProviderMode.LocalAzureFunctionsSettings &&
                    root.TryGetProperty("Values", out var valuesElement) &&
                    valuesElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in valuesElement.EnumerateObject())
                    {
                        if (prop.Value.ValueKind == JsonValueKind.String)
                        {
                            _fileConfig[prop.Name] = prop.Value.GetString();
                        }
                        else
                        {
                            _fileConfig[prop.Name] = prop.Value.GetRawText();
                        }
                    }
                }
                else
                {
                    // Case 2: flat JSON object (e.g. appsettings.local.json)
                    foreach (var prop in root.EnumerateObject())
                    {
                        if (prop.Value.ValueKind == JsonValueKind.String)
                        {
                            _fileConfig[prop.Name] = prop.Value.GetString();
                        }
                        else
                        {
                            _fileConfig[prop.Name] = prop.Value.GetRawText();
                        }
                    }
                }
            }

            _fileLoaded = true;
        }

        // ------------------------------------------------------------
        // REMOTE API MODE
        // ------------------------------------------------------------
        private async Task<string> GetFromApiAsync(string service, string key, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_options.ApiBaseUrl))
                throw new InvalidOperationException("ApiBaseUrl must be set for RemoteApi mode.");

            var url = _options.ApiBaseUrl.TrimEnd('/') +
                      "/config/" + Uri.EscapeDataString(service);
//                      "?configname=" + Uri.EscapeDataString(key);

            // 1) Try cache
            lock (_cacheLock)
            {
                if (_cache.TryGetValue(url, out var entry))
                {
                    if (entry.ExpiresAt > DateTimeOffset.UtcNow)
                    {
                        // Cache hit
                        return GetValue(key, entry.Value);
                    }

                    // Expired -> remove
                    _cache.Remove(url);
                }
            }

            // 2) Call remote API
            using (var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false))
            {
                if (!response.IsSuccessStatusCode)
                    return null; // you may or may not want to cache failures/nulls

                var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);



                // 3) Store in cache (only if we have a non-null result, optional)
                if (json is not null)
                {
                    lock (_cacheLock)
                    {
                        _cache[url] = (json, DateTimeOffset.UtcNow.Add(_cacheDuration));
                    }
                }

                var value = GetValue(key, json);
                return value;
            }
        }

        public string GetValue(string key, string json)
        {
            string result = null;
            using (var doc = JsonDocument.Parse(json))
            {
                var root = doc.RootElement;

                if (root.ValueKind == JsonValueKind.Object &&
                    root.TryGetProperty(key, out var valueProp))
                {
                    result = valueProp.GetString();
                }
                else if (root.ValueKind == JsonValueKind.String)
                {
                    result = root.GetString();
                }
                else
                {
                    result = root.GetRawText();
                }
            }
            return result;
        }
        public void Dispose()
        {
            _httpClient?.Dispose();
        }

        public string GetMode()
        {
            switch (_options.Mode)
            {
                case ConfigProviderMode.RemoteApi:
                    return "RemoteApi";

                case ConfigProviderMode.Environment:
                    return "Environment";

                case ConfigProviderMode.LocalFile:
                    return "LocalFile";
                case ConfigProviderMode.LocalAzureFunctionsSettings:
                    return "LocalAzureFunctionsSettings";

                default:
                    return "Unknown";
            }

        }
    }
}
