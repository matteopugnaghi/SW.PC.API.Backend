// ==================================================================
// Services/BackupSchedulerService.cs
// DATA MANAGEMENT - Servicio de Backup Automático Programado
// Versión: 1.0.0
// Cumplimiento: EU CRA Anexo I, Parte I, 2f (Integridad de Datos)
// ==================================================================

using SW.PC.API.Backend.Models;

namespace SW.PC.API.Backend.Services
{
    /// <summary>
    /// Background service que ejecuta backups automáticos según la configuración
    /// </summary>
    public class BackupSchedulerService : BackgroundService
    {
        private readonly ILogger<BackupSchedulerService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(15); // Revisar cada 15 minutos

        public BackupSchedulerService(
            ILogger<BackupSchedulerService> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("BackupSchedulerService started");
            
            // Esperar un poco antes de iniciar para que la app arranque completamente
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckAndRunScheduledBackupsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in backup scheduler");
                }
                
                await Task.Delay(_checkInterval, stoppingToken);
            }
            
            _logger.LogInformation("BackupSchedulerService stopped");
        }

        private async Task CheckAndRunScheduledBackupsAsync(CancellationToken stoppingToken)
        {
            using var scope = _serviceProvider.CreateScope();
            
            var projectContext = scope.ServiceProvider.GetRequiredService<IProjectContextService>();
            var backupService = scope.ServiceProvider.GetRequiredService<IBackupService>();
            
            // Obtener todos los proyectos disponibles
            var projects = projectContext.GetAvailableProjects().Select(p => p.Id).ToList();
            
            foreach (var projectId in projects)
            {
                if (stoppingToken.IsCancellationRequested)
                    break;
                
                try
                {
                    await CheckProjectBackupAsync(projectId, backupService);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking backup for project {ProjectId}", projectId);
                }
            }
        }

        private async Task CheckProjectBackupAsync(string projectId, IBackupService backupService)
        {
            // Obtener configuración
            var config = await backupService.GetBackupConfigAsync(projectId);
            
            // Si backup no está habilitado o intervalo es 0, saltar
            if (!config.Enabled || config.IntervalHours <= 0)
            {
                return;
            }
            
            // Obtener estado del sistema
            var status = await backupService.GetSystemStatusAsync(projectId);
            
            // Determinar si necesita backup
            bool needsBackup = false;
            
            if (status.LastBackup == null)
            {
                // Nunca se ha hecho backup
                needsBackup = true;
                _logger.LogInformation("Project {ProjectId}: No previous backup found, scheduling backup", projectId);
            }
            else
            {
                var timeSinceLastBackup = DateTime.Now - status.LastBackup.CreatedAt;
                var interval = TimeSpan.FromHours(config.IntervalHours);
                
                if (timeSinceLastBackup >= interval)
                {
                    needsBackup = true;
                    _logger.LogInformation(
                        "Project {ProjectId}: Last backup was {Hours:F1} hours ago (interval: {Interval}h), scheduling backup",
                        projectId, timeSinceLastBackup.TotalHours, config.IntervalHours);
                }
            }
            
            if (needsBackup)
            {
                _logger.LogInformation("Starting scheduled backup for project {ProjectId}", projectId);
                
                var request = new CreateBackupRequest
                {
                    Name = $"Scheduled Backup {DateTime.Now:yyyy-MM-dd HH:mm}",
                    Description = "Automatic scheduled backup",
                    IncludeConfig = true,
                    IncludeModels = true,
                    IncludeDatabase = true
                };
                
                var result = await backupService.CreateBackupAsync(projectId, request, "scheduler");
                
                if (result.Success)
                {
                    _logger.LogInformation(
                        "Scheduled backup completed for project {ProjectId}: {BackupId}",
                        projectId, result.BackupInfo?.Id);
                }
                else
                {
                    _logger.LogWarning(
                        "Scheduled backup failed for project {ProjectId}: {Message}",
                        projectId, result.Message);
                }
            }
        }
    }
}
