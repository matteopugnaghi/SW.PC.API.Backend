using System.Globalization;
using System.Xml.Linq;

namespace SW.PC.API.Backend.Services
{
    /// <summary>
    /// Servicio de background que lee temperaturas de sensores Papouch TME vía HTTP
    /// y las escribe al PLC vía ADS como LREAL.
    /// Polling: una lectura al arrancar y luego cada 60 segundos.
    /// Los dos sensores se leen con desfase (no simultáneamente).
    /// Timeout HTTP de 5 segundos para evitar bloqueos.
    /// </summary>
    public class TmeSensorService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IMetricsService _metricsService;
        private readonly ILogger<TmeSensorService> _logger;

        private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan SensorStaggerDelay = TimeSpan.FromSeconds(5);

        public TmeSensorService(
            IServiceProvider serviceProvider,
            IMetricsService metricsService,
            ILogger<TmeSensorService> logger)
        {
            _serviceProvider = serviceProvider;
            _metricsService = metricsService;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🌡️ TmeSensorService starting...");

            // Esperar a que el resto de servicios arranquen
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            // Cargar configuración
            var (enableTme1, uriTme1, adsTme1, enableTme2, uriTme2, adsTme2) = await LoadConfigAsync();

            if (!enableTme1 && !enableTme2)
            {
                _logger.LogInformation("🌡️ TME sensors disabled in configuration. Service idle.");
                _metricsService.SetTmeSensorStatus(1, false, false, "Disabled");
                _metricsService.SetTmeSensorStatus(2, false, false, "Disabled");
                return;
            }

            _logger.LogInformation("🌡️ TME config: TME1={Enable1} URI={Uri1} ADS={Ads1} | TME2={Enable2} URI={Uri2} ADS={Ads2}",
                enableTme1, uriTme1, adsTme1, enableTme2, uriTme2, adsTme2);

            // Marcar estado inicial
            if (enableTme1)
                _metricsService.SetTmeSensorStatus(1, true, false, "Starting...");
            else
                _metricsService.SetTmeSensorStatus(1, false, false, "Disabled");

            if (enableTme2)
                _metricsService.SetTmeSensorStatus(2, true, false, "Starting...");
            else
                _metricsService.SetTmeSensorStatus(2, false, false, "Disabled");

            // Crear HttpClient con timeout
            using var httpClient = new HttpClient { Timeout = HttpTimeout };

            // Bucle de polling
            bool firstRun = true;
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (!firstRun)
                    {
                        await Task.Delay(PollingInterval, stoppingToken);
                    }
                    firstRun = false;

                    // Leer TME 1
                    if (enableTme1 && !string.IsNullOrEmpty(uriTme1))
                    {
                        await ReadAndWriteSensorAsync(httpClient, 1, uriTme1, adsTme1, stoppingToken);
                    }

                    // Desfase entre sensores
                    if (enableTme1 && enableTme2)
                    {
                        await Task.Delay(SensorStaggerDelay, stoppingToken);
                    }

                    // Leer TME 2
                    if (enableTme2 && !string.IsNullOrEmpty(uriTme2))
                    {
                        await ReadAndWriteSensorAsync(httpClient, 2, uriTme2, adsTme2, stoppingToken);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "🌡️ TME polling cycle error");
                }
            }

            _logger.LogInformation("🌡️ TmeSensorService stopped.");
        }

        /// <summary>
        /// Lee temperatura del sensor TME vía HTTP y la escribe al PLC vía ADS.
        /// </summary>
        private async Task ReadAndWriteSensorAsync(HttpClient httpClient, int sensorIndex, string uri, string adsVariable, CancellationToken ct)
        {
            try
            {
                var temperature = await ReadTemperatureAsync(httpClient, uri, ct);

                if (temperature.HasValue)
                {
                    _logger.LogDebug("🌡️ TME{Index}: {Temp:F1}°C from {URI}", sensorIndex, temperature.Value, uri);
                    _metricsService.SetTmeSensorStatus(sensorIndex, true, true,
                        $"OK ({temperature.Value:F1}°C)", temperature.Value);

                    // Escribir al PLC si hay variable ADS configurada
                    if (!string.IsNullOrEmpty(adsVariable))
                    {
                        await WriteTemperatureToPlcAsync(sensorIndex, adsVariable, temperature.Value);
                    }
                }
                else
                {
                    _logger.LogWarning("🌡️ TME{Index}: No valid temperature from {URI}", sensorIndex, uri);
                    _metricsService.SetTmeSensorStatus(sensorIndex, true, false, "No data");
                }
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning("🌡️ TME{Index}: HTTP timeout ({Timeout}s) connecting to {URI}", sensorIndex, HttpTimeout.TotalSeconds, uri);
                _metricsService.SetTmeSensorStatus(sensorIndex, true, false, $"Timeout ({HttpTimeout.TotalSeconds}s)");
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning("🌡️ TME{Index}: HTTP error from {URI}: {Error}", sensorIndex, uri, ex.Message);
                _metricsService.SetTmeSensorStatus(sensorIndex, true, false, $"HTTP Error: {ex.Message}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🌡️ TME{Index}: Unexpected error reading {URI}", sensorIndex, uri);
                _metricsService.SetTmeSensorStatus(sensorIndex, true, false, $"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Lee la temperatura del sensor Papouch TME desde su endpoint HTTP XML.
        /// El XML tiene formato: <tmr><t>25.3</t></tmr> o similar.
        /// </summary>
        private async Task<double?> ReadTemperatureAsync(HttpClient httpClient, string uri, CancellationToken ct)
        {
            var response = await httpClient.GetStringAsync(uri, ct);

            if (string.IsNullOrWhiteSpace(response))
                return null;

            // Parse XML del TME: formato <root><sns><id>0</id><t>25.3</t><h>...</h></sns></root>
            // o <tmr><t>25.3</t></tmr>
            var doc = XDocument.Parse(response);

            // Buscar elemento <t> (temperature) en cualquier nivel
            var tempElement = doc.Descendants("t").FirstOrDefault();
            if (tempElement == null)
            {
                _logger.LogWarning("🌡️ No <t> element found in TME XML response: {Response}", 
                    response.Length > 200 ? response[..200] : response);
                return null;
            }

            var tempText = tempElement.Value.Trim();
            if (double.TryParse(tempText, NumberStyles.Float, CultureInfo.InvariantCulture, out double temperature))
            {
                return temperature;
            }

            _logger.LogWarning("🌡️ Cannot parse temperature value: '{Value}'", tempText);
            return null;
        }

        /// <summary>
        /// Escribe el valor de temperatura al PLC como LREAL vía ADS.
        /// </summary>
        private async Task WriteTemperatureToPlcAsync(int sensorIndex, string adsVariable, double temperature)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var twinCatService = scope.ServiceProvider.GetRequiredService<ITwinCATService>();

                if (!twinCatService.IsConnected)
                {
                    _logger.LogDebug("🌡️ TME{Index}: PLC not connected, skipping ADS write to {Var}", sensorIndex, adsVariable);
                    return;
                }

                await twinCatService.WriteVariableAsync(adsVariable, temperature, typeof(double));
                _logger.LogDebug("🌡️ TME{Index}: Wrote {Temp:F1}°C to PLC {Var}", sensorIndex, temperature, adsVariable);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "🌡️ TME{Index}: Failed to write temperature to PLC {Var}", sensorIndex, adsVariable);
            }
        }

        /// <summary>
        /// Carga la configuración TME desde el Excel del proyecto activo.
        /// </summary>
        private async Task<(bool enableTme1, string uriTme1, string adsTme1, bool enableTme2, string uriTme2, string adsTme2)> LoadConfigAsync()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var projectContext = scope.ServiceProvider.GetRequiredService<IProjectContextService>();
                var excelConfigService = scope.ServiceProvider.GetRequiredService<IExcelConfigService>();

                var excelPath = projectContext.ExcelConfigPath;
                var systemConfig = await excelConfigService.LoadSystemConfigurationAsync(excelPath);

                return (
                    systemConfig.EnableTME1, systemConfig.UriTME1, systemConfig.AdsTME1,
                    systemConfig.EnableTME2, systemConfig.UriTME2, systemConfig.AdsTME2
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🌡️ Failed to load TME configuration from Excel");
                return (false, "", "", false, "", "");
            }
        }
    }
}
