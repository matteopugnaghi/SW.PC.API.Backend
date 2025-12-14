using Microsoft.AspNetCore.SignalR;
using SW.PC.API.Backend.Models.TwinCAT;
using SW.PC.API.Backend.Models.Database;
using SW.PC.API.Backend.Models.Excel;
using SW.PC.API.Backend.Services;

namespace SW.PC.API.Backend.Hubs
{
    /// <summary>
    /// Hub SignalR para comunicación en tiempo real entre backend y frontend
    /// </summary>
    public class ScadaHub : Hub
    {
        private readonly ILogger<ScadaHub> _logger;
        private readonly ITwinCATService _twinCATService;
        private readonly IMetricsService _metricsService;
        private static int _activeConnections = 0;
        private static readonly object _lockObj = new object();
        
        public ScadaHub(
            ILogger<ScadaHub> _logger, 
            ITwinCATService twinCATService,
            IMetricsService metricsService)
        {
            this._logger = _logger;
            _twinCATService = twinCATService;
            _metricsService = metricsService;
        }
        
        public override async Task OnConnectedAsync()
        {
            lock (_lockObj)
            {
                _activeConnections++;
                _metricsService.SetSignalRActiveConnections(_activeConnections);
                _metricsService.SetSignalRStatus(true, true, $"OK - {_activeConnections} conexiones");
            }
            
            _logger.LogInformation("Client connected: {ConnectionId} (Total: {Count})", 
                Context.ConnectionId, _activeConnections);
            
            await base.OnConnectedAsync();
            
            // Enviar estado inicial del PLC
            await Clients.Caller.SendAsync("PlcConnectionStatus", new 
            { 
                isConnected = _twinCATService.IsConnected,
                timestamp = DateTime.UtcNow
            });
        }
        
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            lock (_lockObj)
            {
                _activeConnections--;
                if (_activeConnections < 0) _activeConnections = 0; // Evitar negativos
                _metricsService.SetSignalRActiveConnections(_activeConnections);
                _metricsService.SetSignalRStatus(true, _activeConnections > 0, 
                    _activeConnections > 0 ? $"OK - {_activeConnections} conexiones" : "Esperando conexiones...");
            }
            
            _logger.LogInformation("Client disconnected: {ConnectionId} (Total: {Count})", 
                Context.ConnectionId, _activeConnections);
            
            await base.OnDisconnectedAsync(exception);
        }
        
        /// <summary>
        /// El cliente se suscribe a una variable PLC específica
        /// </summary>
        public async Task SubscribeToVariable(string variableName)
        {
            _logger.LogInformation("Client {ConnectionId} subscribed to variable {VariableName}", 
                Context.ConnectionId, variableName);
            
            await Groups.AddToGroupAsync(Context.ConnectionId, $"var_{variableName}");
        }
        
        /// <summary>
        /// El cliente se desuscribe de una variable PLC
        /// </summary>
        public async Task UnsubscribeFromVariable(string variableName)
        {
            _logger.LogInformation("Client {ConnectionId} unsubscribed from variable {VariableName}", 
                Context.ConnectionId, variableName);
            
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"var_{variableName}");
        }
        
        /// <summary>
        /// El cliente solicita escribir una variable PLC
        /// </summary>
        public async Task<PlcOperationResponse> WriteVariable(PlcWriteRequest request)
        {
            try
            {
                _logger.LogInformation("Write request for variable {VariableName} with value {Value}", 
                    request.VariableName, request.Value);
                
                var dataType = GetTypeFromString(request.DataType ?? "object");
                var success = await _twinCATService.WriteVariableAsync(
                    request.VariableName, 
                    request.Value, 
                    dataType
                );
                
                return new PlcOperationResponse
                {
                    Success = success,
                    Message = success ? "Variable written successfully" : "Failed to write variable",
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error writing variable {VariableName}", request.VariableName);
                return new PlcOperationResponse
                {
                    Success = false,
                    Message = ex.Message,
                    Timestamp = DateTime.UtcNow
                };
            }
        }
        
        /// <summary>
        /// El cliente solicita leer una variable PLC
        /// </summary>
        public async Task<PlcOperationResponse> ReadVariable(string variableName, string? dataType = null)
        {
            try
            {
                var type = GetTypeFromString(dataType ?? "object");
                var value = await _twinCATService.ReadVariableAsync(variableName, type);
                
                return new PlcOperationResponse
                {
                    Success = true,
                    Message = "Variable read successfully",
                    Data = value,
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading variable {VariableName}", variableName);
                return new PlcOperationResponse
                {
                    Success = false,
                    Message = ex.Message,
                    Timestamp = DateTime.UtcNow
                };
            }
        }
        
        #region Alarm System Methods
        
        /// <summary>
        /// Suscribirse al grupo de alarmas para recibir actualizaciones de estado
        /// </summary>
        public async Task SubscribeToAlarms()
        {
            _logger.LogInformation("🔔 Client {ConnectionId} subscribed to alarms", Context.ConnectionId);
            await Groups.AddToGroupAsync(Context.ConnectionId, "alarms");
        }
        
        /// <summary>
        /// Desuscribirse del grupo de alarmas
        /// </summary>
        public async Task UnsubscribeFromAlarms()
        {
            _logger.LogInformation("🔔 Client {ConnectionId} unsubscribed from alarms", Context.ConnectionId);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, "alarms");
        }
        
        /// <summary>
        /// Suscribirse a un tipo específico de alarma (Alarm, Notification, Info)
        /// </summary>
        public async Task SubscribeToAlarmType(string alarmType)
        {
            var groupName = $"alarm_{alarmType.ToLower()}";
            _logger.LogInformation("🔔 Client {ConnectionId} subscribed to {GroupName}", Context.ConnectionId, groupName);
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        }
        
        /// <summary>
        /// Desuscribirse de un tipo específico de alarma
        /// </summary>
        public async Task UnsubscribeFromAlarmType(string alarmType)
        {
            var groupName = $"alarm_{alarmType.ToLower()}";
            _logger.LogInformation("🔔 Client {ConnectionId} unsubscribed from {GroupName}", Context.ConnectionId, groupName);
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName);
        }
        
        #endregion
        
        private Type GetTypeFromString(string typeName)
        {
            return typeName.ToUpper() switch
            {
                "BOOL" or "BOOLEAN" => typeof(bool),
                "INT" or "INT16" or "SHORT" => typeof(short),
                "DINT" or "INT32" or "INT" => typeof(int),
                "REAL" or "FLOAT" or "SINGLE" => typeof(float),
                "LREAL" or "DOUBLE" => typeof(double),
                "STRING" => typeof(string),
                _ => typeof(object)
            };
        }
    }
    
    /// <summary>
    /// Servicio de fondo para enviar actualizaciones de PLC a los clientes conectados
    /// </summary>
    public class PlcNotificationService : BackgroundService
    {
        private readonly ILogger<PlcNotificationService> _logger;
        private readonly IHubContext<ScadaHub> _hubContext;
        private readonly ITwinCATService _twinCATService;
        
        public PlcNotificationService(
            ILogger<PlcNotificationService> logger,
            IHubContext<ScadaHub> hubContext,
            ITwinCATService twinCATService)
        {
            _logger = logger;
            _hubContext = hubContext;
            _twinCATService = twinCATService;
            
            // Suscribirse a cambios de variables PLC
            _twinCATService.OnVariableChanged += OnPlcVariableChanged;
        }
        
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("PLC Notification Service started");
            
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Enviar estado de conexión PLC cada 5 segundos
                    await _hubContext.Clients.All.SendAsync("PlcConnectionStatus", new
                    {
                        isConnected = _twinCATService.IsConnected,
                        timestamp = DateTime.UtcNow
                    }, stoppingToken);
                    
                    await Task.Delay(5000, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in PLC notification service");
                }
            }
            
            _logger.LogInformation("PLC Notification Service stopped");
        }
        
        private async void OnPlcVariableChanged(object? sender, PlcNotification notification)
        {
            try
            {
                // Enviar notificación a todos los clientes suscritos a esta variable
                await _hubContext.Clients.Group($"var_{notification.VariableName}")
                    .SendAsync("VariableChanged", new
                    {
                        variableName = notification.VariableName,
                        value = notification.NewValue,
                        timestamp = notification.Timestamp
                    });
                
                // 🔔 Detectar si es una variable de alarma y notificar al grupo de alarmas
                if (IsAlarmVariable(notification.VariableName))
                {
                    await SendAlarmNotification(notification);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending variable change notification for {VariableName}", 
                    notification.VariableName);
            }
        }
        
        /// <summary>
        /// Verifica si una variable es de tipo alarma
        /// </summary>
        private bool IsAlarmVariable(string variableName)
        {
            return variableName.Contains("st_alarmPc[") && 
                   (variableName.EndsWith("].Alarm") || 
                    variableName.EndsWith("].Notification") || 
                    variableName.EndsWith("].Info"));
        }
        
        /// <summary>
        /// Envía notificación de cambio de alarma a los clientes suscritos
        /// </summary>
        private async Task SendAlarmNotification(PlcNotification notification)
        {
            try
            {
                // Determinar tipo de alarma
                string alarmType = "unknown";
                if (notification.VariableName.EndsWith("].Alarm")) alarmType = "alarm";
                else if (notification.VariableName.EndsWith("].Notification")) alarmType = "notification";
                else if (notification.VariableName.EndsWith("].Info")) alarmType = "info";
                
                var alarmUpdate = new
                {
                    variableName = notification.VariableName,
                    alarmType = alarmType,
                    isActive = Convert.ToBoolean(notification.NewValue),
                    timestamp = notification.Timestamp
                };
                
                // Notificar al grupo general de alarmas
                await _hubContext.Clients.Group("alarms")
                    .SendAsync("AlarmStateChanged", alarmUpdate);
                
                // Notificar al grupo específico del tipo de alarma
                await _hubContext.Clients.Group($"alarm_{alarmType}")
                    .SendAsync("AlarmStateChanged", alarmUpdate);
                
                _logger.LogDebug("🔔 Alarm notification sent: {Variable} = {Value}", 
                    notification.VariableName, notification.NewValue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🔔 Error sending alarm notification for {VariableName}", 
                    notification.VariableName);
            }
        }
    }
}