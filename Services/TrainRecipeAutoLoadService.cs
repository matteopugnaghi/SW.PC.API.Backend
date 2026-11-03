using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SW.PC.API.Backend.Data;
using SW.PC.API.Backend.Hubs;
using SW.PC.API.Backend.Models;

namespace SW.PC.API.Backend.Services
{
    /// <summary>
    /// Servicio de background que monitorea las variables de auto-carga de tipos de tren.
    /// Cuando TwinCAT escribe un número != 0 en las variables configuradas, 
    /// el servicio automáticamente carga el tipo de tren de esa línea y resetea la variable a 0.
    /// 
    /// Configuración desde Excel (System Config):
    /// - TrainRecipeEnabled: Habilita/deshabilita el módulo completo
    /// - TrainRecipeAutoLoadVar: Variable PLC para auto-carga PLC1 (ej: "GVL.nAutoLoadTrainType")
    /// - TrainRecipeAutoLoadVar2: Variable PLC para auto-carga PLC2 (ej: "GVL.nAutoLoadTrainType_2")
    /// </summary>
    public class TrainRecipeAutoLoadService : BackgroundService
    {
        private readonly ITwinCATService _twinCATService;
        private readonly IHubContext<ScadaHub> _hubContext;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<TrainRecipeAutoLoadService> _logger;
        
        private string _autoLoadVar1 = "";
        private string _autoLoadVar2 = "";
        private bool _isEnabled = false;
        private DateTime _lastConfigReload = DateTime.MinValue;
        private const int CONFIG_RELOAD_INTERVAL_SECONDS = 60;
        private const int POLLING_INTERVAL_MS = 500; // Polling cada 500ms

        public TrainRecipeAutoLoadService(
            ITwinCATService twinCATService,
            IHubContext<ScadaHub> hubContext,
            IServiceProvider serviceProvider,
            ILogger<TrainRecipeAutoLoadService> logger)
        {
            _twinCATService = twinCATService;
            _hubContext = hubContext;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🚆 TrainRecipeAutoLoadService iniciado");

            // Esperar un poco al inicio para que otros servicios se inicialicen
            await Task.Delay(5000, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Recargar configuración periódicamente
                    if ((DateTime.Now - _lastConfigReload).TotalSeconds > CONFIG_RELOAD_INTERVAL_SECONDS)
                    {
                        await ReloadConfigurationAsync();
                    }

                    // Si no está habilitado o no hay variables configuradas, esperar
                    if (!_isEnabled || (string.IsNullOrEmpty(_autoLoadVar1) && string.IsNullOrEmpty(_autoLoadVar2)))
                    {
                        await Task.Delay(5000, stoppingToken);
                        continue;
                    }

                    // Monitorear variable 1 (PLC1)
                    if (!string.IsNullOrEmpty(_autoLoadVar1))
                    {
                        await CheckAndProcessAutoLoad(_autoLoadVar1, "PLC1", stoppingToken);
                    }

                    // Monitorear variable 2 (PLC2)
                    if (!string.IsNullOrEmpty(_autoLoadVar2))
                    {
                        await CheckAndProcessAutoLoad(_autoLoadVar2, "PLC2", stoppingToken);
                    }

                    await Task.Delay(POLLING_INTERVAL_MS, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Error en TrainRecipeAutoLoadService");
                    await Task.Delay(5000, stoppingToken);
                }
            }

            _logger.LogInformation("🛑 TrainRecipeAutoLoadService detenido");
        }

        private async Task ReloadConfigurationAsync()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var excelConfigService = scope.ServiceProvider.GetRequiredService<IExcelConfigService>();
                var projectContext = scope.ServiceProvider.GetRequiredService<IProjectContextService>();
                
                var systemConfig = await excelConfigService.LoadSystemConfigurationAsync(projectContext.ExcelConfigPath);
                
                _isEnabled = systemConfig.TrainRecipeEnabled;
                _autoLoadVar1 = systemConfig.TrainRecipeAutoLoadVar ?? "";
                _autoLoadVar2 = systemConfig.TrainRecipeAutoLoadVar2 ?? "";
                _lastConfigReload = DateTime.Now;

                if (_isEnabled && (!string.IsNullOrEmpty(_autoLoadVar1) || !string.IsNullOrEmpty(_autoLoadVar2)))
                {
                    _logger.LogInformation("🚆 TrainRecipeAutoLoad configuración cargada:");
                    _logger.LogInformation("  - Enabled: {Enabled}", _isEnabled);
                    if (!string.IsNullOrEmpty(_autoLoadVar1))
                        _logger.LogInformation("  - AutoLoadVar (PLC1): {Var}", _autoLoadVar1);
                    if (!string.IsNullOrEmpty(_autoLoadVar2))
                        _logger.LogInformation("  - AutoLoadVar2 (PLC2): {Var}", _autoLoadVar2);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Error cargando configuración de TrainRecipeAutoLoad");
            }
        }

        private async Task CheckAndProcessAutoLoad(string variableName, string plcId, CancellationToken stoppingToken)
        {
            try
            {
                // Leer el valor de la variable
                var result = await _twinCATService.ReadVariableAsync(variableName, typeof(int));
                
                if (result is not int lineNumber || lineNumber == 0)
                {
                    return; // Nada que hacer
                }

                _logger.LogInformation("🚆 Auto-carga de tren detectada desde {PlcId}: línea {LineNumber} (variable: {Variable})", 
                    plcId, lineNumber, variableName);

                // Procesar la auto-carga
                var success = await ProcessAutoLoadAsync(lineNumber, plcId, stoppingToken);

                if (success)
                {
                    // Resetear la variable a 0
                    await _twinCATService.WriteVariableAsync(variableName, 0, typeof(int));
                    _logger.LogInformation("✅ Variable {Variable} reseteada a 0", variableName);

                    // Notificar via SignalR
                    await _hubContext.Clients.All.SendAsync("TrainRecipeAutoLoaded", new
                    {
                        lineNumber,
                        plcId,
                        timestamp = DateTime.Now,
                        success = true
                    }, stoppingToken);
                }
                else
                {
                    _logger.LogWarning("⚠️ Fallo al auto-cargar tipo de tren línea {LineNumber}", lineNumber);
                    
                    // Resetear igualmente para evitar loop infinito
                    await _twinCATService.WriteVariableAsync(variableName, 0, typeof(int));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error procesando auto-carga de tren para {Variable}", variableName);
            }
        }

        private async Task<bool> ProcessAutoLoadAsync(int lineNumber, string plcId, CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AquafrischDbContext>();
                var excelConfigService = scope.ServiceProvider.GetRequiredService<IExcelConfigService>();
                var projectContext = scope.ServiceProvider.GetRequiredService<IProjectContextService>();
                
                // Buscar el tipo de tren en la base de datos por DisplayOrder o Id
                var trainType = await dbContext.TrainTypes
                    .Include(t => t.Parameters)
                    .Include(t => t.GantryData)
                    .Where(t => t.IsActive && (t.DisplayOrder == lineNumber || t.Id == lineNumber))
                    .OrderByDescending(t => t.DisplayOrder == lineNumber) // Priorizar DisplayOrder match
                    .FirstOrDefaultAsync(stoppingToken);
                
                if (trainType == null)
                {
                    _logger.LogWarning("⚠️ No se encontró tipo de tren con línea/id {LineNumber}", lineNumber);
                    return false;
                }

                _logger.LogInformation("🚆 Auto-cargando tipo de tren '{Name}' (línea {Line}) al {PlcId}", 
                    trainType.Name, lineNumber, plcId);

                // Cargar configuración de TrainRecipe desde Excel para obtener variables PLC base
                var excelPath = projectContext.ExcelConfigPath;
                var trainRecipeConfig = await excelConfigService.LoadTrainRecipeConfigAsync(excelPath);

                // PLC2 → variables con prefijo alternativo (Excel A13 ON + A14). Tablas Gantry según A15.
                bool useAlternate = plcId == "PLC2";
                string? alternatePrefix = trainRecipeConfig?.AlternatePlcPrefix;
                if (useAlternate && (trainRecipeConfig == null || !trainRecipeConfig.AlternateWriteEnabled || string.IsNullOrEmpty(alternatePrefix)))
                {
                    _logger.LogError("❌ Auto-carga {PlcId} solicitada pero PLC2 no está habilitado en Excel TrainRecipe (A13=ON y prefijo en A14). No se escribe nada.", plcId);
                    return false;
                }

                var plcWriter = scope.ServiceProvider.GetRequiredService<ITrainTypePlcWriter>();
                var writeResult = await plcWriter.WriteAsync(trainType, trainRecipeConfig, new TrainTypePlcWriteOptions
                {
                    IncludeGantryTables = !useAlternate || (trainRecipeConfig?.AlternateIncludeGantryTables ?? false),
                    VariableMapper = useAlternate ? (v => TrainTypePlcWriter.ApplyAlternatePrefix(v, alternatePrefix)) : null,
                    ContextLabel = $"{plcId} auto-carga",
                    LineNumberOverride = lineNumber
                }, "PLC");

                int parametersWritten = writeResult.SuccessCount;
                _logger.Log(writeResult.Success ? LogLevel.Information : LogLevel.Error,
                    "{Icon} Auto-carga de tren '{Name}' al {PlcId}: {ParamsWritten} escritos, {Errors} errores. {Verification}",
                    writeResult.Success ? "✅" : "❌", trainType.Name, plcId, parametersWritten, writeResult.ErrorCount, writeResult.Verification?.Summary);

                // 📋 Registrar en Operation Log (PlcCommand)
                try
                {
                    var operationLog = scope.ServiceProvider.GetRequiredService<IOperationLogService>();
                    await operationLog.LogAsync(
                        OperationCategory.PlcCommand,
                        OperationAction.PlcCommandTrainChange,
                        writeResult.Success
                            ? $"{plcId}: L{lineNumber} → '{trainType.Name}' ({parametersWritten})"
                            : $"{plcId}: L{lineNumber} → '{trainType.Name}' ({parametersWritten} OK, {writeResult.ErrorCount} ERRORES)",
                        user: "PLC",
                        details: writeResult.Success ? null : new Dictionary<string, object> { ["errors"] = writeResult.Errors.Take(50).ToList() }
                    );
                }
                catch (Exception logEx)
                {
                    _logger.LogWarning(logEx, "⚠️ No se pudo registrar en Operation Log");
                }

                return writeResult.Success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error en ProcessAutoLoadAsync para tipo de tren");
                return false;
            }
        }
    }
}
