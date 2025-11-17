using SW.PC.API.Backend.Models;
using System.Text.Json;

namespace SW.PC.API.Backend.Services
{
    public interface IConfigurationService
    {
        Task<AppConfiguration> GetConfigurationAsync();
        Task<bool> UpdateConfigurationAsync(AppConfiguration configuration);
        Task<bool> UpdateColorConfigurationAsync(ColorConfiguration colorConfig);
    }
    
    public class ConfigurationService : IConfigurationService
    {
        private readonly string _configPath;
        private readonly ILogger<ConfigurationService> _logger;
        private AppConfiguration _cachedConfig;
        private readonly object _lock = new object();
        
        public ConfigurationService(IWebHostEnvironment environment, ILogger<ConfigurationService> logger)
        {
            _configPath = Path.Combine(environment.ContentRootPath, "app-config.json");
            _logger = logger;
            _cachedConfig = LoadConfiguration();
        }
        
        public async Task<AppConfiguration> GetConfigurationAsync()
        {
            return await Task.FromResult(_cachedConfig);
        }
        
        public async Task<bool> UpdateConfigurationAsync(AppConfiguration configuration)
        {
            try
            {
                lock (_lock)
                {
                    var json = JsonSerializer.Serialize(configuration, new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
                    
                    File.WriteAllText(_configPath, json);
                    _cachedConfig = configuration;
                }
                
                _logger.LogInformation("Configuration updated successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating configuration");
                return false;
            }
        }
        
        public async Task<bool> UpdateColorConfigurationAsync(ColorConfiguration colorConfig)
        {
            try
            {
                lock (_lock)
                {
                    _cachedConfig.Colors = colorConfig;
                    
                    var json = JsonSerializer.Serialize(_cachedConfig, new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
                    
                    File.WriteAllText(_configPath, json);
                }
                
                _logger.LogInformation("Color configuration updated successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating color configuration");
                return false;
            }
        }
        
        private AppConfiguration LoadConfiguration()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var json = File.ReadAllText(_configPath);
                    var config = JsonSerializer.Deserialize<AppConfiguration>(json, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
                    
                    if (config != null)
                    {
                        _logger.LogInformation("Configuration loaded from file: {ConfigPath}", _configPath);
                        return config;
                    }
                }
                
                _logger.LogInformation("Creating default configuration");
                var defaultConfig = new AppConfiguration();
                
                // Save default configuration
                var defaultJson = JsonSerializer.Serialize(defaultConfig, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                
                File.WriteAllText(_configPath, defaultJson);
                return defaultConfig;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading configuration, using defaults");
                return new AppConfiguration();
            }
        }
    }
}