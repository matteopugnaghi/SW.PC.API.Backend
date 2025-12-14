using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using SW.PC.API.Backend.Hubs;
using SW.PC.API.Backend.Models.Excel;
using SW.PC.API.Backend.Services;

namespace SW.PC.API.Backend.Controllers
{
    /// <summary>
    /// Controller para gestión de alarmas del sistema SCADA.
    /// Proporciona endpoints para definiciones de alarmas multilenguaje y estados en tiempo real.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AlarmsController : ControllerBase
    {
        private readonly IExcelConfigService _excelConfigService;
        private readonly IRequestProjectContext _projectContext;
        private readonly ITwinCATService _twinCATService;
        private readonly IHubContext<ScadaHub> _hubContext;
        private readonly ILogger<AlarmsController> _logger;
        
        // Cache de configuración de alarmas (se recarga con invalidación manual o reinicio)
        private static AlarmConfiguration? _cachedAlarmConfig;
        private static DateTime _cacheTimestamp = DateTime.MinValue;
        private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(5);
        private static readonly object _cacheLock = new();
        
        public AlarmsController(
            IExcelConfigService excelConfigService,
            IRequestProjectContext projectContext,
            ITwinCATService twinCATService,
            IHubContext<ScadaHub> hubContext,
            ILogger<AlarmsController> logger)
        {
            _excelConfigService = excelConfigService;
            _projectContext = projectContext;
            _twinCATService = twinCATService;
            _hubContext = hubContext;
            _logger = logger;
        }
        
        /// <summary>
        /// Obtiene la configuración completa de alarmas desde Excel.
        /// Incluye definiciones de Alarms, Notifications e Infos con textos multilenguaje.
        /// </summary>
        /// <returns>Configuración de alarmas con soporte ES/EN</returns>
        [HttpGet("configuration")]
        [ProducesResponseType(typeof(AlarmConfiguration), 200)]
        public async Task<ActionResult<AlarmConfiguration>> GetAlarmConfiguration()
        {
            try
            {
                var config = await GetCachedAlarmConfigurationAsync();
                return Ok(config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🔔 Error al obtener configuración de alarmas");
                return StatusCode(500, new { error = "Error al cargar configuración de alarmas", message = ex.Message });
            }
        }
        
        /// <summary>
        /// Obtiene las definiciones de alarmas con textos en el idioma especificado.
        /// </summary>
        /// <param name="language">Código de idioma ISO 639-2 (SPA, ENG, ITA, FRA, etc.) o ISO 639-1 (ES, EN). Por defecto: SPA</param>
        /// <returns>Lista de definiciones de alarmas en el idioma solicitado</returns>
        [HttpGet("definitions")]
        [ProducesResponseType(typeof(AlarmDefinitionResponse), 200)]
        public async Task<ActionResult<AlarmDefinitionResponse>> GetAlarmDefinitions([FromQuery] string language = "SPA")
        {
            try
            {
                var config = await GetCachedAlarmConfigurationAsync();
                var langCode = NormalizeLanguageCode(language);
                
                var response = new AlarmDefinitionResponse
                {
                    Language = langCode,
                    AvailableLanguages = config.AvailableLanguages,
                    Alarms = config.Alarms.Select(a => new AlarmDefinitionDto
                    {
                        Index = a.Index,
                        Type = "Alarm",
                        PlcVariable = a.PlcVariable,
                        Text = a.GetText(langCode)
                    }).ToList(),
                    Notifications = config.Notifications.Select(n => new AlarmDefinitionDto
                    {
                        Index = n.Index,
                        Type = "Notification",
                        PlcVariable = n.PlcVariable,
                        Text = n.GetText(langCode)
                    }).ToList(),
                    Infos = config.Infos.Select(i => new AlarmDefinitionDto
                    {
                        Index = i.Index,
                        Type = "Info",
                        PlcVariable = i.PlcVariable,
                        Text = i.GetText(langCode)
                    }).ToList(),
                    TotalCount = config.TotalCount
                };
                
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🔔 Error al obtener definiciones de alarmas");
                return StatusCode(500, new { error = "Error al cargar definiciones de alarmas", message = ex.Message });
            }
        }
        
        /// <summary>
        /// Obtiene la lista de variables PLC de alarmas que deben ser monitoreadas.
        /// Útil para que el servicio de polling sepa qué variables leer.
        /// </summary>
        /// <returns>Lista de nombres de variables PLC de alarmas</returns>
        [HttpGet("plc-variables")]
        [ProducesResponseType(typeof(AlarmPlcVariablesResponse), 200)]
        public async Task<ActionResult<AlarmPlcVariablesResponse>> GetAlarmPlcVariables()
        {
            try
            {
                var config = await GetCachedAlarmConfigurationAsync();
                
                var response = new AlarmPlcVariablesResponse
                {
                    AlarmVariables = config.Alarms.Select(a => a.PlcVariable).ToList(),
                    NotificationVariables = config.Notifications.Select(n => n.PlcVariable).ToList(),
                    InfoVariables = config.Infos.Select(i => i.PlcVariable).ToList(),
                    TotalCount = config.TotalCount
                };
                
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🔔 Error al obtener variables PLC de alarmas");
                return StatusCode(500, new { error = "Error al cargar variables PLC", message = ex.Message });
            }
        }
        
        /// <summary>
        /// Invalida el cache de alarmas para forzar recarga desde Excel.
        /// Útil después de modificar el archivo Excel.
        /// </summary>
        /// <returns>Confirmación de invalidación</returns>
        [HttpPost("invalidate-cache")]
        [ProducesResponseType(200)]
        public ActionResult InvalidateCache()
        {
            try
            {
                lock (_cacheLock)
                {
                    _cachedAlarmConfig = null;
                    _cacheTimestamp = DateTime.MinValue;
                }
                
                _logger.LogInformation("🔔 Cache de alarmas invalidado manualmente");
                return Ok(new { message = "Cache de alarmas invalidado correctamente", timestamp = DateTime.UtcNow });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🔔 Error al invalidar cache de alarmas");
                return StatusCode(500, new { error = "Error al invalidar cache", message = ex.Message });
            }
        }
        
        /// <summary>
        /// [DESARROLLO] Simula activar/desactivar una alarma para testing.
        /// Esto cambia el valor en el TwinCATService simulado y envía el cambio por SignalR.
        /// </summary>
        /// <param name="request">Tipo de alarma, índice y estado</param>
        /// <returns>Confirmación del cambio</returns>
        [HttpPost("simulate")]
        [ProducesResponseType(200)]
        public async Task<ActionResult> SimulateAlarm([FromBody] SimulateAlarmRequest request)
        {
            try
            {
                // Construir nombre de variable según tipo
                var variableName = request.Type.ToLower() switch
                {
                    "alarm" => $"MAIN.fbMachine.st_alarmPc[{request.Index}].Alarm",
                    "notification" => $"MAIN.fbMachine.st_alarmPc[{request.Index}].Notification",
                    "info" => $"MAIN.fbMachine.st_alarmPc[{request.Index}].Info",
                    _ => $"MAIN.fbMachine.st_alarmPc[{request.Index}].Alarm"
                };
                
                // Enviar cambio por SignalR a todos los clientes (sin escribir al PLC)
                await _hubContext.Clients.All.SendAsync("PlcVariableUpdated", new
                {
                    variableName = variableName,
                    value = request.IsActive,
                    timestamp = DateTime.UtcNow.ToString("O"),
                    source = "simulation"
                });
                
                _logger.LogInformation("🔔 Alarma simulada: {Variable} = {Value}", variableName, request.IsActive);
                
                return Ok(new 
                { 
                    message = "Alarma simulada correctamente",
                    variableName = variableName,
                    isActive = request.IsActive,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🔔 Error al simular alarma");
                return StatusCode(500, new { error = "Error al simular alarma", message = ex.Message });
            }
        }
        
        /// <summary>
        /// Obtiene un resumen rápido del estado de alarmas (para InfoPanel).
        /// Incluye conteos y las alarmas más recientes.
        /// </summary>
        /// <param name="language">Código de idioma ISO 639-2 (SPA, ENG) o ISO 639-1 (ES, EN). Por defecto: SPA</param>
        /// <returns>Resumen de alarmas para el panel de información</returns>
        [HttpGet("summary")]
        [ProducesResponseType(typeof(AlarmSummaryResponse), 200)]
        public async Task<ActionResult<AlarmSummaryResponse>> GetAlarmSummary([FromQuery] string language = "SPA")
        {
            try
            {
                var config = await GetCachedAlarmConfigurationAsync();
                var langCode = NormalizeLanguageCode(language);
                
                // Por ahora devolvemos solo las definiciones disponibles
                // Los estados activos vendrán de SignalR/PLC cuando se integre
                var response = new AlarmSummaryResponse
                {
                    Language = langCode,
                    TotalAlarmDefinitions = config.Alarms.Count,
                    TotalNotificationDefinitions = config.Notifications.Count,
                    TotalInfoDefinitions = config.Infos.Count,
                    // TODO: Cuando se integre con PLC real, estos se llenarán con estados activos
                    ActiveAlarms = new List<ActiveAlarmDto>(),
                    ActiveNotifications = new List<ActiveAlarmDto>(),
                    ActiveInfos = new List<ActiveAlarmDto>(),
                    LastUpdate = DateTime.UtcNow
                };
                
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🔔 Error al obtener resumen de alarmas");
                return StatusCode(500, new { error = "Error al cargar resumen de alarmas", message = ex.Message });
            }
        }
        
        /// <summary>
        /// Obtiene el estado actual de todas las alarmas leyendo directamente del PLC.
        /// Útil para sincronizar el estado al cargar la página.
        /// </summary>
        /// <returns>Lista de estados de alarmas activas</returns>
        [HttpGet("current-states")]
        [ProducesResponseType(typeof(AlarmCurrentStatesResponse), 200)]
        public async Task<ActionResult<AlarmCurrentStatesResponse>> GetCurrentAlarmStates()
        {
            try
            {
                var config = await GetCachedAlarmConfigurationAsync();
                var activeStates = new List<AlarmStateDto>();
                
                // Leer todas las variables de alarma del PLC
                var allVariables = config.Alarms.Select(a => a.PlcVariable)
                    .Concat(config.Notifications.Select(n => n.PlcVariable))
                    .Concat(config.Infos.Select(i => i.PlcVariable))
                    .ToList();
                
                foreach (var variable in allVariables)
                {
                    try
                    {
                        var value = await _twinCATService.ReadVariableAsync(variable, typeof(bool));
                        if (value != null && (bool)value == true)
                        {
                            activeStates.Add(new AlarmStateDto
                            {
                                VariableName = variable,
                                IsActive = true,
                                Timestamp = DateTime.UtcNow
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug("No se pudo leer {Variable}: {Error}", variable, ex.Message);
                    }
                }
                
                _logger.LogInformation("🔔 Estados actuales: {Active} alarmas activas de {Total} variables", 
                    activeStates.Count, allVariables.Count);
                
                return Ok(new AlarmCurrentStatesResponse
                {
                    ActiveStates = activeStates,
                    TotalVariables = allVariables.Count,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🔔 Error al obtener estados actuales de alarmas");
                return StatusCode(500, new { error = "Error al leer estados de alarmas", message = ex.Message });
            }
        }
        
        #region Private Methods
        
        /// <summary>
        /// Obtiene la configuración de alarmas con cache
        /// </summary>
        private async Task<AlarmConfiguration> GetCachedAlarmConfigurationAsync()
        {
            lock (_cacheLock)
            {
                if (_cachedAlarmConfig != null && 
                    DateTime.UtcNow - _cacheTimestamp < CacheExpiration)
                {
                    return _cachedAlarmConfig;
                }
            }
            
            // Cargar desde Excel
            var excelPath = _excelConfigService.GetExcelConfigPath();
            var config = await _excelConfigService.LoadAlarmsAsync(excelPath);
            
            lock (_cacheLock)
            {
                _cachedAlarmConfig = config;
                _cacheTimestamp = DateTime.UtcNow;
            }
            
            return config;
        }
        
        /// <summary>
        /// Normaliza código de idioma a ISO 639-2 (3 letras)
        /// </summary>
        private static string NormalizeLanguageCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return "SPA";
                
            var upperCode = code.ToUpperInvariant();
            
            // Si ya tiene 3 letras, devolver tal cual
            if (upperCode.Length == 3)
                return upperCode;
            
            // Convertir de ISO 639-1 (2 letras) a ISO 639-2 (3 letras)
            return upperCode switch
            {
                "ES" => "SPA",
                "EN" => "ENG",
                "IT" => "ITA",
                "FR" => "FRA",
                "RU" => "RUS",
                "CS" => "CZE",
                "DA" => "DAN",
                "VI" => "VIE",
                "TH" => "TAI",
                "ID" => "IND",
                "MS" => "MAY",
                "EL" => "GRE",
                "DE" => "DEU",
                "PT" => "POR",
                _ => upperCode
            };
        }
        
        #endregion
    }
    
    #region DTOs for API Responses
    
    /// <summary>
    /// Respuesta con definiciones de alarmas en un idioma específico
    /// </summary>
    public class AlarmDefinitionResponse
    {
        public string Language { get; set; } = "SPA";
        public List<string> AvailableLanguages { get; set; } = new();
        public List<AlarmDefinitionDto> Alarms { get; set; } = new();
        public List<AlarmDefinitionDto> Notifications { get; set; } = new();
        public List<AlarmDefinitionDto> Infos { get; set; } = new();
        public int TotalCount { get; set; }
    }
    
    /// <summary>
    /// DTO para una definición de alarma
    /// </summary>
    public class AlarmDefinitionDto
    {
        public int Index { get; set; }
        public string Type { get; set; } = string.Empty;
        public string PlcVariable { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// Respuesta con variables PLC de alarmas
    /// </summary>
    public class AlarmPlcVariablesResponse
    {
        public List<string> AlarmVariables { get; set; } = new();
        public List<string> NotificationVariables { get; set; } = new();
        public List<string> InfoVariables { get; set; } = new();
        public int TotalCount { get; set; }
    }
    
    /// <summary>
    /// Respuesta con resumen de alarmas para InfoPanel
    /// </summary>
    public class AlarmSummaryResponse
    {
        public string Language { get; set; } = "SPA";
        public int TotalAlarmDefinitions { get; set; }
        public int TotalNotificationDefinitions { get; set; }
        public int TotalInfoDefinitions { get; set; }
        public List<ActiveAlarmDto> ActiveAlarms { get; set; } = new();
        public List<ActiveAlarmDto> ActiveNotifications { get; set; } = new();
        public List<ActiveAlarmDto> ActiveInfos { get; set; } = new();
        public DateTime LastUpdate { get; set; }
    }
    
    /// <summary>
    /// DTO para una alarma activa
    /// </summary>
    public class ActiveAlarmDto
    {
        public int Index { get; set; }
        public string Type { get; set; } = string.Empty;
        public string PlcVariable { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime? ActivatedAt { get; set; }
        public bool IsAcknowledged { get; set; }
    }
    
    /// <summary>
    /// Request para simular cambio de alarma
    /// </summary>
    public class SimulateAlarmRequest
    {
        public string Type { get; set; } = "Alarm"; // Alarm, Notification, Info
        public int Index { get; set; } = 1;
        public bool IsActive { get; set; } = true;
    }
    
    /// <summary>
    /// Respuesta con estados actuales de alarmas
    /// </summary>
    public class AlarmCurrentStatesResponse
    {
        public List<AlarmStateDto> ActiveStates { get; set; } = new();
        public int TotalVariables { get; set; }
        public DateTime Timestamp { get; set; }
    }
    
    /// <summary>
    /// DTO para un estado de alarma
    /// </summary>
    public class AlarmStateDto
    {
        public string VariableName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime Timestamp { get; set; }
    }
    
    #endregion
}
