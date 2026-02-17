// ============================================================================
// OperationLogService.cs - Servicio de Registro de Operaciones (SQLite)
// ============================================================================
// Registra acciones operativas y historial de alarmas PLC en base de datos SQLite.
// - Nivel 2 de logging (complementa Audit Log Nivel 1 para EU CRA)
// - Historial de alarmas PLC (st_alarmHistPc) con textos multiidioma desde Excel
// - Soporte para consultas eficientes, paginación y filtros
// ============================================================================

using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SW.PC.API.Backend.Data;
using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Models.Excel;

namespace SW.PC.API.Backend.Services;

/// <summary>
/// Interface del servicio de logs de operación
/// </summary>
public interface IOperationLogService
{
    /// <summary>
    /// Registrar una operación general
    /// </summary>
    Task<OperationLog> LogAsync(
        OperationCategory category, 
        OperationAction action, 
        string description, 
        string? user = null, 
        Dictionary<string, object>? details = null,
        string? ipAddress = null, 
        string? sessionId = null);
    
    /// <summary>
    /// Registrar una alarma del historial del PLC (st_alarmHistPc)
    /// Obtiene el texto de la alarma desde Excel según el índice y idioma
    /// </summary>
    Task<OperationLog?> LogPlcAlarmHistoryAsync(string plcVariable, bool isActive);
    
    /// <summary>
    /// Registrar un mensaje enviado desde el PLC (LogFromTwincat WSTRING).
    /// Formato esperado: "ID|CATEGORIA|MENSAJE" (ej: "001|PROCESS|Motor arrancado")
    /// Categorías: PROCESS, ALARM, INFO, WARNING, COMMAND (default: INFO si no especificada)
    /// </summary>
    Task<OperationLog?> LogPlcMessageAsync(string rawMessage);
    
    /// <summary>
    /// Obtener logs con filtros y paginación
    /// </summary>
    Task<OperationLogPagedResponse> GetLogsAsync(OperationLogFilter filter);
    
    /// <summary>
    /// Obtener logs recientes
    /// </summary>
    Task<List<OperationLogDto>> GetRecentLogsAsync(int count = 50, string language = "SPA");
    
    /// <summary>
    /// Obtener resumen de logs para dashboard
    /// </summary>
    Task<OperationLogSummary> GetSummaryAsync(string language = "SPA");
    
    /// <summary>
    /// Reconocer un log (marcar como visto/atendido)
    /// </summary>
    Task<bool> AcknowledgeLogAsync(int logId, string acknowledgedBy);
    
    /// <summary>
    /// Reconocer múltiples logs
    /// </summary>
    Task<int> AcknowledgeLogsAsync(IEnumerable<int> logIds, string acknowledgedBy);
    
    /// <summary>
    /// Obtener información de ayuda
    /// </summary>
    Task<OperationLogHelp> GetHelpAsync(string language = "es");
    
    /// <summary>
    /// Limpiar logs antiguos según configuración de retención
    /// </summary>
    Task<int> CleanupOldLogsAsync(int retentionDays = 365);
}

/// <summary>
/// Servicio de logging de operaciones usando SQLite (Nivel 2)
/// </summary>
public class OperationLogService : IOperationLogService
{
    private readonly ILogger<OperationLogService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IExcelConfigService _excelConfigService;
    
    // Cache de configuración de alarmas
    private AlarmConfiguration? _alarmConfigCache;
    private DateTime _alarmConfigCacheTime = DateTime.MinValue;
    private static readonly TimeSpan AlarmCacheExpiration = TimeSpan.FromMinutes(5);
    private readonly object _alarmCacheLock = new();
    
    // Flag para evitar verificar la tabla en cada operación
    private static bool _tableVerified = false;
    private static readonly object _tableVerifyLock = new();
    
    // Regex para parsear variable PLC de alarma histórica
    // Formato: MAIN.fbMachine.st_alarmHistPc[X].Alarm/Notification/Info
    private static readonly Regex AlarmHistRegex = new(
        @"st_alarmHistPc\[(\d+)\]\.(Alarm|Notification|Info)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly INxLogFileService _nxLogFileService;

    public OperationLogService(
        ILogger<OperationLogService> logger,
        IServiceScopeFactory scopeFactory,
        IExcelConfigService excelConfigService,
        INxLogFileService nxLogFileService)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _excelConfigService = excelConfigService;
        _nxLogFileService = nxLogFileService;
        
        _logger.LogInformation("📋 OperationLogService initialized (SQLite mode)");
    }

    /// <summary>
    /// Registrar una operación general en la base de datos
    /// </summary>
    public async Task<OperationLog> LogAsync(
        OperationCategory category, 
        OperationAction action, 
        string description, 
        string? user = null, 
        Dictionary<string, object>? details = null,
        string? ipAddress = null, 
        string? sessionId = null)
    {
        // 🔐 SuperAdmin aparece como "Administrator" en logs (oculta nombre real)
        if (IsSuperAdminIdentity(user))
        {
            user = "Administrator";
        }

        var entry = new OperationLog
        {
            Timestamp = DateTime.Now,
            Category = category,
            Action = action,
            Severity = OperationLog.GetSeverityFromAction(action),
            User = user ?? "System",
            Description = description,
            IpAddress = ipAddress,
            SessionId = sessionId,
            DetailsJson = details != null ? JsonSerializer.Serialize(details) : null
        };

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AquafrischDbContext>();
            
            dbContext.OperationLogs.Add(entry);
            await dbContext.SaveChangesAsync();
            
            // 📋 NxLog: Escribir evento L2 a fichero JSONL (para SOC PIVOT TISSEO)
            _ = _nxLogFileService.WriteOperationEventAsync(new NxLogOperationEntry
            {
                Timestamp = entry.Timestamp,
                Level = entry.Severity == OperationSeverity.Critical ? "ERROR" :
                        entry.Severity == OperationSeverity.Warning ? "WARNING" : "INFO",
                Category = category.ToString(),
                Action = action.ToString(),
                Severity = entry.Severity.ToString(),
                User = entry.User,
                Description = description,
                IpAddress = ipAddress
            });
            
            _logger.LogDebug("📋 Operation logged: {Category}.{Action} by {User}: {Description}",
                category, action, user, description);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging operation: {Action}", action);
            throw;
        }

        return entry;
    }

    /// <summary>
    /// Registrar una alarma del historial del PLC (st_alarmHistPc)
    /// </summary>
    public async Task<OperationLog?> LogPlcAlarmHistoryAsync(string plcVariable, bool isActive)
    {
        try
        {
            // Parsear la variable PLC para extraer índice y tipo
            var match = AlarmHistRegex.Match(plcVariable);
            if (!match.Success)
            {
                _logger.LogWarning("Variable PLC no es de historial de alarma: {Variable}", plcVariable);
                return null;
            }
            
            var alarmIndex = int.Parse(match.Groups[1].Value);
            var alarmTypeStr = match.Groups[2].Value; // "Alarm", "Notification", "Info"
            
            // Determinar acción según tipo y estado
            var action = (alarmTypeStr.ToLower(), isActive) switch
            {
                ("alarm", true) => OperationAction.PlcAlarmActivated,
                ("alarm", false) => OperationAction.PlcAlarmDeactivated,
                ("notification", true) => OperationAction.PlcNotificationActivated,
                ("notification", false) => OperationAction.PlcNotificationDeactivated,
                ("info", true) => OperationAction.PlcInfoActivated,
                ("info", false) => OperationAction.PlcInfoDeactivated,
                _ => OperationAction.PlcAlarmActivated
            };
            
            // Obtener código de la alarma desde Excel
            var alarmCode = await GetAlarmCodeFromExcelAsync(alarmIndex, alarmTypeStr);
            
            // Crear entrada de log
            var entry = new OperationLog
            {
                Timestamp = DateTime.Now,
                Category = OperationCategory.PlcAlarmHistory,
                Action = action,
                Severity = OperationLog.GetSeverityFromAction(action),
                User = "PLC",
                Description = $"{alarmTypeStr} #{alarmIndex}",
                PlcVariable = plcVariable,
                AlarmIndex = alarmIndex,
                AlarmCode = alarmCode ?? alarmIndex.ToString(),
                AlarmType = alarmTypeStr,
                // ActionKey no se usa para PlcAlarmHistory (texto viene de Excel al consultar)
                NewValue = isActive ? "Active" : "Inactive",
                DetailsJson = JsonSerializer.Serialize(new Dictionary<string, object>
                {
                    ["plcVariable"] = plcVariable,
                    ["alarmIndex"] = alarmIndex,
                    ["alarmType"] = alarmTypeStr,
                    ["isActive"] = isActive
                })
            };
            
            // Guardar en base de datos
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AquafrischDbContext>();
            
            // 🔧 Asegurar que la tabla existe antes de insertar
            await EnsureOperationLogsTableExistsAsync(dbContext);
            
            dbContext.OperationLogs.Add(entry);
            await dbContext.SaveChangesAsync();
            
            // 📋 NxLog: Escribir alarma PLC L2 a fichero JSONL (para SOC PIVOT TISSEO)
            _ = _nxLogFileService.WriteOperationEventAsync(new NxLogOperationEntry
            {
                Timestamp = entry.Timestamp,
                Level = alarmTypeStr == "Alarm" ? "ERROR" :
                        alarmTypeStr == "Notification" ? "WARNING" : "INFO",
                Category = entry.Category.ToString(),
                Action = entry.Action.ToString(),
                Severity = entry.Severity.ToString(),
                User = "PLC",
                Description = entry.Description,
                PlcVariable = plcVariable,
                AlarmCode = alarmCode,
                AlarmType = alarmTypeStr,
                NewValue = isActive ? "Active" : "Inactive"
            });
            
            _logger.LogInformation(
                "🔔 Alarma PLC registrada: [{Type}] Index={Index} Active={Active} - Code={Code}",
                alarmTypeStr, alarmIndex, isActive, alarmCode ?? "N/A");
            
            return entry;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registrando alarma PLC: {Variable}", plcVariable);
            return null;
        }
    }

    /// <summary>
    /// Registrar un mensaje enviado desde el PLC (LogFromTwincat WSTRING).
    /// Formato esperado: "ID|CATEGORIA|MENSAJE" (ej: "001|PROCESS|Motor arrancado")
    /// </summary>
    public async Task<OperationLog?> LogPlcMessageAsync(string rawMessage)
    {
        if (string.IsNullOrWhiteSpace(rawMessage))
        {
            _logger.LogDebug("📋 [PlcMessage] Mensaje vacío recibido, ignorando");
            return null;
        }

        try
        {
            // Parsear mensaje: "ID|CATEGORIA|MENSAJE" o "MENSAJE" simple
            string messageId = "";
            string category = "INFO";
            string message = rawMessage;

            var parts = rawMessage.Split('|', 3);
            if (parts.Length >= 3)
            {
                // Formato completo: ID|CATEGORIA|MENSAJE
                messageId = parts[0].Trim();
                category = parts[1].Trim().ToUpperInvariant();
                message = parts[2].Trim();
            }
            else if (parts.Length == 2)
            {
                // Formato: CATEGORIA|MENSAJE (sin ID)
                category = parts[0].Trim().ToUpperInvariant();
                message = parts[1].Trim();
            }
            // Si solo hay una parte, message ya tiene el valor correcto

            // Crear entrada de log
            var entry = new OperationLog
            {
                Timestamp = DateTime.Now,
                Category = OperationCategory.PlcCommand, // Usamos PlcCommand para mensajes del PLC
                Action = OperationAction.PlcLogReceived,
                Severity = OperationSeverity.Info,
                User = "PLC",
                Description = message,
                NewValue = messageId, // Guardamos el ID del mensaje para referencia
                DetailsJson = JsonSerializer.Serialize(new Dictionary<string, object>
                {
                    ["rawMessage"] = rawMessage,
                    ["messageId"] = messageId,
                    ["category"] = category,
                    ["message"] = message
                })
            };

            // Guardar en base de datos
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AquafrischDbContext>();
            
            await EnsureOperationLogsTableExistsAsync(dbContext);
            
            dbContext.OperationLogs.Add(entry);
            await dbContext.SaveChangesAsync();
            
            // 📋 NxLog: Escribir mensaje PLC L2 a fichero JSONL (para SOC PIVOT TISSEO)
            _ = _nxLogFileService.WriteOperationEventAsync(new NxLogOperationEntry
            {
                Timestamp = entry.Timestamp,
                Level = category == "ERROR" || category == "ALARM" ? "ERROR" :
                        category == "WARNING" || category == "NOTIFICATION" ? "WARNING" : "INFO",
                Category = entry.Category.ToString(),
                Action = entry.Action.ToString(),
                Severity = entry.Severity.ToString(),
                User = "PLC",
                Description = message,
                NewValue = messageId
            });

            _logger.LogInformation(
                "📋 Mensaje PLC registrado: [{Category}] ID={Id} - {Message}",
                category, messageId, message);

            return entry;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registrando mensaje PLC: {Message}", rawMessage);
            return null;
        }
    }

    /// <summary>
    /// Obtener logs con filtros y paginación
    /// </summary>
    public async Task<OperationLogPagedResponse> GetLogsAsync(OperationLogFilter filter)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AquafrischDbContext>();
        
        // 🔧 Asegurar que la tabla existe antes de consultar
        await EnsureOperationLogsTableExistsAsync(dbContext);
        
        var query = dbContext.OperationLogs.AsNoTracking().AsQueryable();
        
        // Aplicar filtros
        if (filter.FromDate.HasValue)
            query = query.Where(l => l.Timestamp >= filter.FromDate.Value);
        
        if (filter.ToDate.HasValue)
            query = query.Where(l => l.Timestamp <= filter.ToDate.Value);
        
        if (filter.Category.HasValue)
            query = query.Where(l => l.Category == filter.Category.Value);
        
        if (filter.Action.HasValue)
            query = query.Where(l => l.Action == filter.Action.Value);
        
        if (filter.MinSeverity.HasValue)
            query = query.Where(l => l.Severity >= filter.MinSeverity.Value);
        
        if (filter.OnlyPlcAlarms == true)
            query = query.Where(l => l.Category == OperationCategory.PlcAlarmHistory);
        
        if (!string.IsNullOrEmpty(filter.User))
            query = query.Where(l => l.User.Contains(filter.User));
        
        if (filter.OnlyUnacknowledged == true)
            query = query.Where(l => !l.IsAcknowledged);
        
        if (!string.IsNullOrEmpty(filter.SearchText))
        {
            var search = filter.SearchText.ToLower();
            query = query.Where(l => 
                l.Description.ToLower().Contains(search) ||
                (l.AlarmCode != null && l.AlarmCode.ToLower().Contains(search)) ||
                (l.ActionKey != null && l.ActionKey.ToLower().Contains(search)));
        }
        
        // Contar total
        var totalCount = await query.CountAsync();
        
        // Ordenar y paginar
        var items = await query
            .OrderByDescending(l => l.Timestamp)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync();
        
        // Convertir a DTOs con mensajes de alarma desde Excel
        var dtos = await ConvertToDtosWithAlarmTextAsync(items, filter.Language);
        
        return new OperationLogPagedResponse
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }
    
    /// <summary>
    /// Convierte entidades a DTOs, obteniendo texto de alarma desde Excel para PlcAlarmHistory
    /// </summary>
    private async Task<List<OperationLogDto>> ConvertToDtosWithAlarmTextAsync(List<OperationLog> items, string language)
    {
        // Obtener configuración de alarmas desde Excel (con caché)
        var alarmConfig = await GetAlarmConfigurationAsync();
        
        // Debug: mostrar cuántas alarmas se cargaron
        if (alarmConfig != null)
        {
            _logger.LogDebug("🔔 Alarmas cargadas desde Excel: {Total} (Alarms={A}, Notifications={N}, Infos={I})",
                alarmConfig.TotalCount, alarmConfig.Alarms.Count, alarmConfig.Notifications.Count, alarmConfig.Infos.Count);
        }
        else
        {
            _logger.LogWarning("⚠️ No se pudo cargar configuración de alarmas desde Excel");
        }
        
        var dtos = new List<OperationLogDto>();
        foreach (var item in items)
        {
            string? alarmMessage = null;
            
            // Si es PlcAlarmHistory, obtener texto desde Excel
            if (item.Category == OperationCategory.PlcAlarmHistory && item.AlarmIndex.HasValue)
            {
                _logger.LogDebug("🔍 Buscando alarma: Index={Index}, Type={Type}", item.AlarmIndex, item.AlarmType);
                
                var alarmDef = alarmConfig?.GetAll()
                    .FirstOrDefault(a => a.Index == item.AlarmIndex.Value && 
                                        string.Equals(a.Type.ToString(), item.AlarmType ?? "Alarm", StringComparison.OrdinalIgnoreCase));
                
                if (alarmDef != null)
                {
                    alarmMessage = alarmDef.GetText(language);
                    _logger.LogDebug("✅ Encontrada alarma: Index={Index}, Type={Type}, Text='{Text}'", 
                        alarmDef.Index, alarmDef.Type, alarmMessage);
                }
                else
                {
                    _logger.LogWarning("⚠️ Alarma NO encontrada en Excel: Index={Index}, Type={Type}. " +
                        "Alarmas disponibles: [{Available}]", 
                        item.AlarmIndex, item.AlarmType,
                        alarmConfig != null 
                            ? string.Join(", ", alarmConfig.GetAll().Take(10).Select(a => $"{a.Type}[{a.Index}]"))
                            : "ninguna");
                }
            }
            
            dtos.Add(OperationLogDto.FromEntity(item, alarmMessage));
        }
        
        return dtos;
    }
    
    /// <summary>
    /// Obtener configuración de alarmas con caché
    /// </summary>
    private async Task<AlarmConfiguration?> GetAlarmConfigurationAsync()
    {
        // Verificar caché
        lock (_alarmCacheLock)
        {
            if (_alarmConfigCache != null && 
                DateTime.Now - _alarmConfigCacheTime < AlarmCacheExpiration)
            {
                return _alarmConfigCache;
            }
        }
        
        try
        {
            // Cargar desde Excel
            var config = await _excelConfigService.LoadAlarmsAsync("ProjectConfig.xlsm");
            
            lock (_alarmCacheLock)
            {
                _alarmConfigCache = config;
                _alarmConfigCacheTime = DateTime.Now;
            }
            
            return config;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error loading alarm configuration from Excel");
            return _alarmConfigCache; // Devolver caché anterior si hay error
        }
    }

    /// <summary>
    /// Obtener logs recientes
    /// </summary>
    public async Task<List<OperationLogDto>> GetRecentLogsAsync(int count = 50, string language = "SPA")
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AquafrischDbContext>();
        
        // 🔧 Asegurar que la tabla existe
        await EnsureOperationLogsTableExistsAsync(dbContext);
        
        var items = await dbContext.OperationLogs
            .AsNoTracking()
            .OrderByDescending(l => l.Timestamp)
            .Take(count)
            .ToListAsync();
        
        return await ConvertToDtosWithAlarmTextAsync(items, language);
    }

    /// <summary>
    /// Obtener resumen para dashboard
    /// </summary>
    public async Task<OperationLogSummary> GetSummaryAsync(string language = "SPA")
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AquafrischDbContext>();
        
        // 🔧 Asegurar que la tabla existe
        await EnsureOperationLogsTableExistsAsync(dbContext);
        
        var today = DateTime.Now.Date;
        var todayLogs = dbContext.OperationLogs
            .AsNoTracking()
            .Where(l => l.Timestamp >= today);
        
        var summary = new OperationLogSummary
        {
            TotalToday = await todayLogs.CountAsync(),
            AlarmsToday = await todayLogs.CountAsync(l => 
                l.Category == OperationCategory.PlcAlarmHistory && 
                l.AlarmType == "Alarm"),
            NotificationsToday = await todayLogs.CountAsync(l => 
                l.Category == OperationCategory.PlcAlarmHistory && 
                l.AlarmType == "Notification"),
            InfosToday = await todayLogs.CountAsync(l => 
                l.Category == OperationCategory.PlcAlarmHistory && 
                l.AlarmType == "Info"),
            UnacknowledgedCount = await dbContext.OperationLogs
                .CountAsync(l => !l.IsAcknowledged && 
                    l.Severity >= OperationSeverity.Warning),
            CriticalCount = await dbContext.OperationLogs
                .CountAsync(l => l.Severity >= OperationSeverity.Critical && 
                    !l.IsAcknowledged),
            LastUpdate = DateTime.Now
        };
        
        // Última alarma
        var lastAlarm = await dbContext.OperationLogs
            .AsNoTracking()
            .Where(l => l.Category == OperationCategory.PlcAlarmHistory)
            .OrderByDescending(l => l.Timestamp)
            .FirstOrDefaultAsync();
        
        if (lastAlarm != null)
        {
            summary.LastAlarm = OperationLogDto.FromEntity(lastAlarm, language);
        }
        
        return summary;
    }

    /// <summary>
    /// Reconocer un log
    /// </summary>
    public async Task<bool> AcknowledgeLogAsync(int logId, string acknowledgedBy)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AquafrischDbContext>();
        
        var log = await dbContext.OperationLogs.FindAsync(logId);
        if (log == null) return false;
        
        log.IsAcknowledged = true;
        log.AcknowledgedBy = acknowledgedBy;
        log.AcknowledgedAt = DateTime.Now;
        
        await dbContext.SaveChangesAsync();
        
        _logger.LogInformation("✅ Log {Id} acknowledged by {User}", logId, acknowledgedBy);
        return true;
    }

    /// <summary>
    /// Reconocer múltiples logs
    /// </summary>
    public async Task<int> AcknowledgeLogsAsync(IEnumerable<int> logIds, string acknowledgedBy)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AquafrischDbContext>();
        
        var now = DateTime.Now;
        var count = await dbContext.OperationLogs
            .Where(l => logIds.Contains(l.Id) && !l.IsAcknowledged)
            .ExecuteUpdateAsync(s => s
                .SetProperty(l => l.IsAcknowledged, true)
                .SetProperty(l => l.AcknowledgedBy, acknowledgedBy)
                .SetProperty(l => l.AcknowledgedAt, now));
        
        _logger.LogInformation("✅ {Count} logs acknowledged by {User}", count, acknowledgedBy);
        return count;
    }

    /// <summary>
    /// Obtener información de ayuda
    /// </summary>
    public Task<OperationLogHelp> GetHelpAsync(string language = "es")
    {
        var help = language == "en" ? GetEnglishHelp() : GetSpanishHelp();
        return Task.FromResult(help);
    }

    /// <summary>
    /// Limpiar logs antiguos
    /// </summary>
    public async Task<int> CleanupOldLogsAsync(int retentionDays = 365)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AquafrischDbContext>();
        
        var cutoffDate = DateTime.Now.AddDays(-retentionDays);
        
        var deletedCount = await dbContext.OperationLogs
            .Where(l => l.Timestamp < cutoffDate)
            .ExecuteDeleteAsync();
        
        if (deletedCount > 0)
        {
            _logger.LogInformation("🗑️ Deleted {Count} old operation logs (before {Date:yyyy-MM-dd})", 
                deletedCount, cutoffDate);
        }
        
        return deletedCount;
    }

    // ============================================================================
    // Métodos privados
    // ============================================================================
    
    /// <summary>
    /// Obtener código de alarma basado en tipo e índice
    /// El texto de la alarma se obtiene en tiempo de consulta via GetAlarmConfigurationAsync
    /// </summary>
    private Task<string?> GetAlarmCodeFromExcelAsync(int index, string type)
    {
        try
        {
            // Generar código de alarma basado en tipo e índice
            var prefix = type.ToLower() switch
            {
                "alarm" => "ALM",
                "notification" => "NTF",
                "info" => "INF",
                _ => "OPR"
            };
            var code = $"{prefix}{index:D3}";
            
            return Task.FromResult<string?>(code);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generando código de alarma {Type}[{Index}]", type, index);
            return Task.FromResult<string?>(null);
        }
    }
    
    /// <summary>
    /// Obtener configuración de alarmas con cache
    /// </summary>
    private async Task<AlarmConfiguration?> GetCachedAlarmConfigAsync()
    {
        lock (_alarmCacheLock)
        {
            if (_alarmConfigCache != null && 
                DateTime.Now - _alarmConfigCacheTime < AlarmCacheExpiration)
            {
                return _alarmConfigCache;
            }
        }
        
        try
        {
            var excelPath = _excelConfigService.GetExcelConfigPath();
            var config = await _excelConfigService.LoadAlarmsAsync(excelPath);
            
            lock (_alarmCacheLock)
            {
                _alarmConfigCache = config;
                _alarmConfigCacheTime = DateTime.Now;
            }
            
            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cargando configuración de alarmas");
            return null;
        }
    }
    
    // ============================================================================
    // Textos de ayuda
    // ============================================================================
    
    private OperationLogHelp GetSpanishHelp() => new()
    {
        Title = "Registro de Operaciones (Nivel 2)",
        Description = "Esta vista muestra todas las acciones realizadas por los operadores en la aplicación. " +
            "A diferencia del Audit Log (Nivel 1) que registra eventos de seguridad, " +
            "el Operation Log registra las acciones operativas del día a día.",
        Sections = new List<HelpSection>
        {
            new() {
                Title = "¿Qué se registra?",
                Content = "• Cambios de vista en la aplicación\n" +
                    "• Reconocimiento de alarmas\n" +
                    "• Carga y ejecución de recetas\n" +
                    "• Cambios de setpoints\n" +
                    "• Exportaciones de datos\n" +
                    "• Acciones sobre el proceso\n" +
                    "• Historial de alarmas PLC (st_alarmHistPc)"
            },
            new() {
                Title = "Retención de datos",
                Content = "Los logs de operación se conservan durante 1 año por defecto. " +
                    "Este período es configurable en el sistema. Los datos se almacenan en SQLite " +
                    "para consultas eficientes y exportación."
            },
            new() {
                Title = "Filtros disponibles",
                Content = "• Por categoría (Alarmas PLC, Navegación, Recetas, etc.)\n" +
                    "• Por severidad (Info, Notificación, Advertencia, Error, Crítico)\n" +
                    "• Por fecha/hora\n" +
                    "• Por usuario\n" +
                    "• Búsqueda de texto\n" +
                    "• Solo no reconocidos"
            }
        },
        Compliance = new ComplianceInfo
        {
            Title = "Cumplimiento Normativo",
            Content = "El Operation Log (Nivel 2) complementa al Audit Log (Nivel 1) que es obligatorio por EU CRA. " +
                "Mientras el Audit Log registra eventos de seguridad, el Operation Log proporciona " +
                "trazabilidad operativa útil para diagnóstico y mejora continua."
        }
    };

    private OperationLogHelp GetEnglishHelp() => new()
    {
        Title = "Operation Logs (Level 2)",
        Description = "This view shows all actions performed by operators in the application. " +
            "Unlike the Audit Log (Level 1) which records security events, " +
            "the Operation Log records day-to-day operational actions.",
        Sections = new List<HelpSection>
        {
            new() {
                Title = "What is logged?",
                Content = "• View changes in the application\n" +
                    "• Alarm acknowledgments\n" +
                    "• Recipe loading and execution\n" +
                    "• Setpoint changes\n" +
                    "• Data exports\n" +
                    "• Process actions\n" +
                    "• PLC alarm history (st_alarmHistPc)"
            },
            new() {
                Title = "Data retention",
                Content = "Operation logs are kept for 1 year by default. " +
                    "This period is configurable in the system. Data is stored in SQLite " +
                    "for efficient querying and export."
            },
            new() {
                Title = "Available filters",
                Content = "• By category (PLC Alarms, Navigation, Recipes, etc.)\n" +
                    "• By severity (Info, Notice, Warning, Error, Critical)\n" +
                    "• By date/time\n" +
                    "• By user\n" +
                    "• Text search\n" +
                    "• Unacknowledged only"
            }
        },
        Compliance = new ComplianceInfo
        {
            Title = "Regulatory Compliance",
            Content = "The Operation Log (Level 2) complements the Audit Log (Level 1) which is mandatory under EU CRA. " +
                "While the Audit Log records security events, the Operation Log provides " +
                "operational traceability useful for diagnostics and continuous improvement."
        }
    };
    
    // ============================================================================
    // Utilidades de base de datos
    // ============================================================================
    
    /// <summary>
    /// Asegura que la tabla OperationLogs existe en la base de datos.
    /// Solo verifica una vez por ejecución de la aplicación.
    /// </summary>
    private async Task EnsureOperationLogsTableExistsAsync(AquafrischDbContext context)
    {
        // ✅ OPTIMIZACIÓN: Solo verificar la tabla una vez
        if (_tableVerified)
            return;
            
        lock (_tableVerifyLock)
        {
            if (_tableVerified)
                return;
        }
        
        try
        {
            _logger.LogInformation("📋 Verificando tabla OperationLogs (una sola vez)...");
            
            // Crear tabla si no existe
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS OperationLogs (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Timestamp TEXT NOT NULL,
                    Category INTEGER NOT NULL,
                    Action INTEGER NOT NULL,
                    Severity INTEGER NOT NULL DEFAULT 0,
                    User TEXT NOT NULL DEFAULT 'System',
                    Description TEXT NOT NULL DEFAULT '',
                    PlcVariable TEXT,
                    AlarmIndex INTEGER,
                    AlarmCode TEXT,
                    AlarmType TEXT,
                    ActionKey TEXT,
                    OldValue TEXT,
                    NewValue TEXT,
                    IpAddress TEXT,
                    SessionId TEXT,
                    DetailsJson TEXT,
                    IsAcknowledged INTEGER NOT NULL DEFAULT 0,
                    AcknowledgedBy TEXT,
                    AcknowledgedAt TEXT
                )");
            
            // Crear índices básicos
            await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS IX_OperationLogs_Timestamp ON OperationLogs(Timestamp)");
            await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS IX_OperationLogs_Category ON OperationLogs(Category)");
            
            // Marcar como verificada
            lock (_tableVerifyLock)
            {
                _tableVerified = true;
            }
            
            _logger.LogInformation("✅ Tabla OperationLogs verificada correctamente");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Tabla OperationLogs verificada (puede que ya exista)");
            // Marcar como verificada incluso si falla (la tabla probablemente existe)
            lock (_tableVerifyLock)
            {
                _tableVerified = true;
            }
        }
    }

    /// <summary>
    /// 🔐 Detecta si el usuario es SuperAdmin para enmascararlo como "Administrator"
    /// SuperAdmin aparece en los registros pero con nombre genérico
    /// </summary>
    private static bool IsSuperAdminIdentity(string? userName)
    {
        if (string.IsNullOrEmpty(userName)) return false;
        var nameLower = userName.ToLowerInvariant();
        return nameLower == "superadmin" || nameLower == "super_admin" || nameLower == "super admin";
    }
}

/// <summary>
/// Información de ayuda para Operation Log
/// </summary>
public class OperationLogHelp
{
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public List<HelpSection> Sections { get; set; } = new();
    public ComplianceInfo? Compliance { get; set; }
}

public class HelpSection
{
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
}

public class ComplianceInfo
{
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
}
