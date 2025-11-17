namespace SW.PC.API.Backend.Models.TwinCAT
{
    /// <summary>
    /// Datos en tiempo real del PLC
    /// </summary>
    public class PlcDataSnapshot
    {
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        public string PlcId { get; set; } = string.Empty;
        
        public PlcConnectionStatus ConnectionStatus { get; set; }
        
        public Dictionary<string, object> Variables { get; set; } = new();
        
        public PlcState State { get; set; }
    }
    
    public enum PlcConnectionStatus
    {
        Disconnected = 0,
        Connecting = 1,
        Connected = 2,
        Error = 3
    }
    
    public enum PlcState
    {
        Invalid = 0,
        Idle = 1,
        Run = 2,
        Stop = 3,
        Config = 4,
        Reconfig = 5,
        Reset = 6
    }
    
    /// <summary>
    /// Solicitud de escritura de variable PLC
    /// </summary>
    public class PlcWriteRequest
    {
        public string VariableName { get; set; } = string.Empty;
        
        public object Value { get; set; } = null!;
        
        public string? DataType { get; set; }  // BOOL, INT, REAL, STRING, etc.
    }
    
    /// <summary>
    /// Respuesta de operación PLC
    /// </summary>
    public class PlcOperationResponse
    {
        public bool Success { get; set; }
        
        public string Message { get; set; } = string.Empty;
        
        public object? Data { get; set; }
        
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Configuración de conexión TwinCAT ADS
    /// </summary>
    public class AdsConfiguration
    {
        public string NetId { get; set; } = "127.0.0.1.1.1"; // AmsNetId del PLC
        
        public int Port { get; set; } = 851; // Puerto ADS (851 = PLC Runtime 1)
        
        public int Timeout { get; set; } = 5000; // Timeout en milisegundos
        
        public bool AutoReconnect { get; set; } = true;
        
        public int ReconnectIntervalMs { get; set; } = 5000;
    }
    
    /// <summary>
    /// Símbolo (variable) del PLC
    /// </summary>
    public class PlcSymbol
    {
        public string Name { get; set; } = string.Empty;
        
        public string InstancePath { get; set; } = string.Empty;
        
        public string TypeName { get; set; } = string.Empty;
        
        public int Size { get; set; }
        
        public uint IndexGroup { get; set; }
        
        public uint IndexOffset { get; set; }
        
        public string Comment { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// Notificación de cambio de variable PLC
    /// </summary>
    public class PlcNotification
    {
        public string VariableName { get; set; } = string.Empty;
        
        public object? OldValue { get; set; }
        
        public object? NewValue { get; set; }
        
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        public uint NotificationHandle { get; set; }
    }
}