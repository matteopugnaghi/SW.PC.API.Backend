using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SW.PC.API.Backend.Data;
using SW.PC.API.Backend.Hubs;
using SW.PC.API.Backend.Models;

namespace SW.PC.API.Backend.Services
{
    /// <summary>
    /// Servicio de background que monitorea las variables de auto-carga de recetas de lavado.
    /// Cuando TwinCAT escribe un número != 0 en las variables configuradas, 
    /// el servicio automáticamente carga la receta de esa línea y resetea la variable a 0.
    /// 
    /// Configuración desde Excel (System Config):
    /// - WashRecipeEnabled: Habilita/deshabilita el módulo completo
    /// - WashRecipeAutoLoadVar: Variable PLC para auto-carga PLC1 (ej: "GVL.nAutoLoadRecipe")
    /// - WashRecipeAutoLoadVar2: Variable PLC para auto-carga PLC2 (ej: "GVL.nAutoLoadRecipe_2")
    /// </summary>
    public class WashRecipeAutoLoadService : BackgroundService
    {
        private readonly ITwinCATService _twinCATService;
        private readonly IHubContext<ScadaHub> _hubContext;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<WashRecipeAutoLoadService> _logger;
        
        private string _autoLoadVar1 = "";
        private string _autoLoadVar2 = "";
        private bool _isEnabled = false;
        private DateTime _lastConfigReload = DateTime.MinValue;
        private const int CONFIG_RELOAD_INTERVAL_SECONDS = 60;
        private const int POLLING_INTERVAL_MS = 500; // Polling cada 500ms

        public WashRecipeAutoLoadService(
            ITwinCATService twinCATService,
            IHubContext<ScadaHub> hubContext,
            IServiceProvider serviceProvider,
            ILogger<WashRecipeAutoLoadService> logger)
        {
            _twinCATService = twinCATService;
            _hubContext = hubContext;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🚿 WashRecipeAutoLoadService iniciado");

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
                    _logger.LogError(ex, "❌ Error en WashRecipeAutoLoadService");
                    await Task.Delay(5000, stoppingToken);
                }
            }

            _logger.LogInformation("🛑 WashRecipeAutoLoadService detenido");
        }

        private async Task ReloadConfigurationAsync()
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var excelConfigService = scope.ServiceProvider.GetRequiredService<IExcelConfigService>();
                var projectContext = scope.ServiceProvider.GetRequiredService<IProjectContextService>();
                
                var systemConfig = await excelConfigService.LoadSystemConfigurationAsync(projectContext.ExcelConfigPath);
                
                _isEnabled = systemConfig.WashRecipeEnabled;
                _autoLoadVar1 = systemConfig.WashRecipeAutoLoadVar ?? "";
                _autoLoadVar2 = systemConfig.WashRecipeAutoLoadVar2 ?? "";
                _lastConfigReload = DateTime.Now;

                if (_isEnabled && (!string.IsNullOrEmpty(_autoLoadVar1) || !string.IsNullOrEmpty(_autoLoadVar2)))
                {
                    _logger.LogInformation("🚿 WashRecipeAutoLoad configuración cargada:");
                    _logger.LogInformation("  - Enabled: {Enabled}", _isEnabled);
                    if (!string.IsNullOrEmpty(_autoLoadVar1))
                        _logger.LogInformation("  - AutoLoadVar (PLC1): {Var}", _autoLoadVar1);
                    if (!string.IsNullOrEmpty(_autoLoadVar2))
                        _logger.LogInformation("  - AutoLoadVar2 (PLC2): {Var}", _autoLoadVar2);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Error cargando configuración de WashRecipeAutoLoad");
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

                _logger.LogInformation("🚿 Auto-carga detectada desde {PlcId}: línea {LineNumber} (variable: {Variable})", 
                    plcId, lineNumber, variableName);

                // Procesar la auto-carga
                var success = await ProcessAutoLoadAsync(lineNumber, plcId, stoppingToken);

                if (success)
                {
                    // Resetear la variable a 0
                    await _twinCATService.WriteVariableAsync(variableName, 0, typeof(int));
                    _logger.LogInformation("✅ Variable {Variable} reseteada a 0", variableName);

                    // Notificar via SignalR
                    await _hubContext.Clients.All.SendAsync("WashRecipeAutoLoaded", new
                    {
                        lineNumber,
                        plcId,
                        timestamp = DateTime.Now,
                        success = true
                    }, stoppingToken);
                }
                else
                {
                    _logger.LogWarning("⚠️ Fallo al auto-cargar receta línea {LineNumber}", lineNumber);
                    
                    // Resetear igualmente para evitar loop infinito
                    await _twinCATService.WriteVariableAsync(variableName, 0, typeof(int));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error procesando auto-carga para {Variable}", variableName);
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
                
                // Buscar el tipo de lavado en la base de datos por DisplayOrder o Id
                var washType = await dbContext.WashTypes
                    .Include(w => w.Parameters)
                    .Where(w => w.IsActive && (w.DisplayOrder == lineNumber || w.Id == lineNumber))
                    .OrderByDescending(w => w.DisplayOrder == lineNumber) // Priorizar DisplayOrder match
                    .FirstOrDefaultAsync(stoppingToken);
                
                if (washType == null)
                {
                    _logger.LogWarning("⚠️ No se encontró tipo de lavado con línea/id {LineNumber}", lineNumber);
                    return false;
                }

                _logger.LogInformation("🚿 Auto-cargando receta '{Name}' (línea {Line}) al {PlcId}", 
                    washType.Name, lineNumber, plcId);

                // Cargar configuración de WashRecipe desde Excel para obtener variables PLC base
                var excelPath = projectContext.ExcelConfigPath;
                var washRecipeConfig = await excelConfigService.LoadWashRecipeConfigAsync(excelPath);

                // Determinar si es PLC1 o PLC2
                bool useAlternate = plcId == "PLC2";
                string? alternatePrefix = washRecipeConfig?.AlternateWritePlcPrefix;
                string? recipeNameVar = useAlternate 
                    ? GetAlternateVariable(washRecipeConfig?.RecipeNamePlcVariable, alternatePrefix)
                    : washRecipeConfig?.RecipeNamePlcVariable;
                string? recipeLineVar = useAlternate
                    ? GetAlternateVariable(washRecipeConfig?.RecipeLineNumberPlcVariable, alternatePrefix)
                    : washRecipeConfig?.RecipeLineNumberPlcVariable;

                int parametersWritten = 0;

                // Escribir nombre de receta
                if (!string.IsNullOrEmpty(recipeNameVar))
                {
                    await _twinCATService.WriteVariableAsync(recipeNameVar, washType.Name, typeof(string));
                    parametersWritten++;
                    _logger.LogDebug("✅ Nombre de receta escrito: {Var} = {Value}", recipeNameVar, washType.Name);
                }

                // Escribir número de línea
                if (!string.IsNullOrEmpty(recipeLineVar))
                {
                    await _twinCATService.WriteVariableAsync(recipeLineVar, lineNumber, typeof(int));
                    parametersWritten++;
                    _logger.LogDebug("✅ Línea de receta escrita: {Var} = {Value}", recipeLineVar, lineNumber);
                }

                // Escribir parámetros del WashType
                foreach (var param in washType.Parameters.Where(p => !string.IsNullOrEmpty(p.PlcVariable)))
                {
                    try
                    {
                        string plcVar = useAlternate 
                            ? GetAlternateVariable(param.PlcVariable, alternatePrefix) ?? param.PlcVariable!
                            : param.PlcVariable!;

                        object? valueToWrite = param.DataType?.ToUpper() switch
                        {
                            "BOOL" => (object)(bool.TryParse(param.Value, out var b) ? b : false),
                            "INT" or "INTEGER" => (object)(int.TryParse(param.Value, out var i) ? i : 0),
                            "LREAL" or "REAL" or "DOUBLE" => (object)(double.TryParse(param.Value,
                                System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture,
                                out var d) ? d : 0.0),
                            _ => param.Value
                        };

                        Type dataType = param.DataType?.ToUpper() switch
                        {
                            "BOOL" => typeof(bool),
                            "INT" or "INTEGER" => typeof(int),
                            "LREAL" or "REAL" or "DOUBLE" => typeof(double),
                            _ => typeof(string)
                        };

                        await _twinCATService.WriteVariableAsync(plcVar, valueToWrite!, dataType);
                        parametersWritten++;
                        _logger.LogDebug("✅ Parámetro escrito: {Var} = {Value}", plcVar, param.Value);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("⚠️ Error escribiendo parámetro {Param}: {Error}", 
                            param.Name, ex.Message);
                    }
                }

                _logger.LogInformation("✅ Auto-carga completada: {ParamsWritten} parámetros escritos al {PlcId}", 
                    parametersWritten, plcId);

                // 📋 Registrar en Operation Log (PlcCommand)
                try
                {
                    var operationLog = scope.ServiceProvider.GetRequiredService<IOperationLogService>();
                    await operationLog.LogAsync(
                        OperationCategory.PlcCommand,
                        OperationAction.PlcCommandWashChange,
                        $"{plcId}: L{lineNumber} → '{washType.Name}' ({parametersWritten})",
                        user: "PLC"
                    );
                }
                catch (Exception logEx)
                {
                    _logger.LogWarning(logEx, "⚠️ No se pudo registrar en Operation Log");
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error en ProcessAutoLoadAsync");
                return false;
            }
        }

        private string? GetAlternateVariable(string? originalVar, string? alternatePrefix)
        {
            if (string.IsNullOrEmpty(originalVar) || string.IsNullOrEmpty(alternatePrefix))
                return originalVar;

            // Reemplazar el prefijo st_WashRecipe con el alternativo
            return originalVar.Replace("st_WashRecipe", alternatePrefix);
        }
    }
}
