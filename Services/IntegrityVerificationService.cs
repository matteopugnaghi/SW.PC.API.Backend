using SW.PC.API.Backend.Models.Excel;

namespace SW.PC.API.Backend.Services
{
    /// <summary>
    /// 🔐 Servicio de verificación periódica de integridad del software
    /// Re-verifica la integridad Git de todos los componentes cada 2 minutos
    /// Para cumplimiento CRA (Cyber Resilience Act)
    /// </summary>
    public class IntegrityVerificationService : BackgroundService
    {
        private readonly ILogger<IntegrityVerificationService> _logger;
        private readonly IServiceProvider _serviceProvider;
        
        // Intervalo de verificación (2 minutos por defecto)
        private readonly int _verificationIntervalSeconds;
        private DateTime _nextVerificationTime;
        private bool _isFirstRun = true;

        public IntegrityVerificationService(
            ILogger<IntegrityVerificationService> logger,
            IServiceProvider serviceProvider,
            IConfiguration configuration)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            
            // Leer intervalo de configuración o usar 120 segundos (2 min) por defecto
            _verificationIntervalSeconds = configuration.GetValue<int>("Security:VerificationIntervalSeconds", 120);
            
            _logger.LogInformation("🔐 IntegrityVerificationService initialized - Interval: {Interval}s", 
                _verificationIntervalSeconds);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🔐 IntegrityVerificationService starting...");

            // Esperar 10 segundos antes de la primera verificación para que el sistema arranque
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PerformVerificationAsync();
                    
                    // Calcular próxima verificación
                    _nextVerificationTime = DateTime.UtcNow.AddSeconds(_verificationIntervalSeconds);
                    
                    // Actualizar la info de próxima verificación en el servicio
                    UpdateNextVerificationInfo();

                    _logger.LogInformation("🔐 Next integrity verification at: {NextTime} (in {Seconds}s)", 
                        _nextVerificationTime.ToString("HH:mm:ss"), _verificationIntervalSeconds);

                    // Esperar hasta la próxima verificación
                    await Task.Delay(TimeSpan.FromSeconds(_verificationIntervalSeconds), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Cancelación normal, salir del loop
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error during integrity verification");
                    
                    // Esperar un poco antes de reintentar en caso de error
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                }
            }

            _logger.LogInformation("🔐 IntegrityVerificationService stopped");
        }

        private async Task PerformVerificationAsync()
        {
            using var scope = _serviceProvider.CreateScope();
            var integrityService = scope.ServiceProvider.GetRequiredService<ISoftwareIntegrityService>();

            if (_isFirstRun)
            {
                _logger.LogInformation("🔐 Performing INITIAL integrity verification...");
                _isFirstRun = false;
            }
            else
            {
                _logger.LogInformation("🔐 Performing PERIODIC integrity verification...");
            }

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            var result = await integrityService.VerifyAllIntegrityAsync();
            
            stopwatch.Stop();

            if (result)
            {
                _logger.LogInformation("✅ Integrity verification PASSED in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
            }
            else
            {
                _logger.LogWarning("⚠️ Integrity verification completed with warnings in {ElapsedMs}ms", 
                    stopwatch.ElapsedMilliseconds);
            }
        }

        private void UpdateNextVerificationInfo()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var integrityService = scope.ServiceProvider.GetRequiredService<ISoftwareIntegrityService>();
                
                // Actualizar información de próxima verificación
                integrityService.UpdateVerificationSchedule(
                    _nextVerificationTime, 
                    _verificationIntervalSeconds);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not update verification schedule info");
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("🔐 IntegrityVerificationService stopping...");
            await base.StopAsync(cancellationToken);
        }
    }
}
