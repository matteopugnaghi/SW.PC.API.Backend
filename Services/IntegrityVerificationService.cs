using SW.PC.API.Backend.Models;
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
                    _nextVerificationTime = DateTime.Now.AddSeconds(_verificationIntervalSeconds);
                    
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
            var auditLog = scope.ServiceProvider.GetRequiredService<IAuditLogService>();
            var projectContext = scope.ServiceProvider.GetRequiredService<IProjectContextService>();

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

            // Obtener detalles de cada componente para mensajes detallados
            var versionInfo = integrityService.GetSoftwareVersionInfo();
            var componentDetails = GetComponentDetailsMessage(versionInfo);

            if (result)
            {
                _logger.LogInformation("✅ Integrity verification PASSED in {ElapsedMs}ms - {Details}", 
                    stopwatch.ElapsedMilliseconds, componentDetails);
                
                // 📝 Audit Log: Register successful auto-verification in ALL projects
                await LogToAllProjectsAsync(auditLog, projectContext,
                    AuditCategory.Integrity,
                    AuditAction.IntegrityAutoVerify,
                    AuditResult.Success,
                    $"Automatic integrity verification PASSED - {componentDetails}",
                    stopwatch.ElapsedMilliseconds);
            }
            else
            {
                // Construir mensaje detallado de warnings
                var warningDetails = GetWarningDetailsMessage(versionInfo);
                
                _logger.LogWarning("⚠️ Integrity verification completed with WARNINGS in {ElapsedMs}ms - {Details}", 
                    stopwatch.ElapsedMilliseconds, warningDetails);
                    
                // 📝 Audit Log: Register verification with warnings in ALL projects
                await LogToAllProjectsAsync(auditLog, projectContext,
                    AuditCategory.Integrity,
                    AuditAction.IntegrityAutoVerify,
                    AuditResult.Warning,
                    $"Automatic integrity verification with WARNINGS - {warningDetails}",
                    stopwatch.ElapsedMilliseconds);
            }
        }

        /// <summary>
        /// Escribe el log de integridad a TODOS los proyectos disponibles.
        /// Los eventos de integridad son globales y deben verse en cada proyecto.
        /// </summary>
        private async Task LogToAllProjectsAsync(
            IAuditLogService auditLog,
            IProjectContextService projectContext,
            AuditCategory category,
            AuditAction action,
            AuditResult result,
            string details,
            long durationMs)
        {
            var projects = projectContext.GetAvailableProjects().ToList();
            
            foreach (var project in projects)
            {
                try
                {
                    await auditLog.LogAsync(
                        category,
                        action,
                        result,
                        details,
                        "System",
                        durationMs: durationMs,
                        projectId: project.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "⚠️ Could not write integrity log to project {ProjectId}", project.Id);
                }
            }
        }

        /// <summary>
        /// Genera mensaje resumido del estado de cada componente
        /// </summary>
        private string GetComponentDetailsMessage(SoftwareVersionInfo versionInfo)
        {
            var parts = new List<string>();

            if (versionInfo.Backend != null)
                parts.Add($"Backend: {versionInfo.Backend.Integrity ?? "unknown"}");
            
            if (versionInfo.Frontend != null)
                parts.Add($"Frontend: {versionInfo.Frontend.Integrity ?? "unknown"}");
            
            if (versionInfo.TwinCatPlc != null)
                parts.Add($"TwinCAT: {versionInfo.TwinCatPlc.Integrity ?? "unknown"}");

            return string.Join(" | ", parts);
        }

        /// <summary>
        /// Genera mensaje detallado de warnings con archivos modificados
        /// </summary>
        private string GetWarningDetailsMessage(SoftwareVersionInfo versionInfo)
        {
            var warnings = new List<string>();

            // Backend
            if (versionInfo.Backend != null)
            {
                if (versionInfo.Backend.Integrity == "modified")
                {
                    var files = versionInfo.Backend.ModifiedFiles > 0 
                        ? $"{versionInfo.Backend.ModifiedFiles} files" 
                        : "uncommitted changes";
                    warnings.Add($"Backend: MODIFIED ({files})");
                }
                else if (versionInfo.Backend.Integrity == "unknown")
                {
                    warnings.Add("Backend: UNKNOWN (repo not found)");
                }
                else
                {
                    warnings.Add($"Backend: {versionInfo.Backend.Integrity}");
                }
            }

            // Frontend
            if (versionInfo.Frontend != null)
            {
                if (versionInfo.Frontend.Integrity == "modified")
                {
                    var files = versionInfo.Frontend.ModifiedFiles > 0 
                        ? $"{versionInfo.Frontend.ModifiedFiles} files" 
                        : "uncommitted changes";
                    warnings.Add($"Frontend: MODIFIED ({files})");
                }
                else if (versionInfo.Frontend.Integrity == "unknown")
                {
                    warnings.Add("Frontend: UNKNOWN (repo not found)");
                }
                else
                {
                    warnings.Add($"Frontend: {versionInfo.Frontend.Integrity}");
                }
            }

            // TwinCAT PLC
            if (versionInfo.TwinCatPlc != null)
            {
                if (versionInfo.TwinCatPlc.Integrity == "modified")
                {
                    var files = versionInfo.TwinCatPlc.ModifiedFiles > 0 
                        ? $"{versionInfo.TwinCatPlc.ModifiedFiles} files" 
                        : "uncommitted changes";
                    warnings.Add($"TwinCAT: MODIFIED ({files})");
                }
                else if (versionInfo.TwinCatPlc.Integrity == "unknown")
                {
                    warnings.Add("TwinCAT: UNKNOWN (repo not found)");
                }
                else
                {
                    warnings.Add($"TwinCAT: {versionInfo.TwinCatPlc.Integrity}");
                }
            }

            return string.Join(" | ", warnings);
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
