namespace SW.PC.API.Backend.Models
{
    /// <summary>
    /// Configuración del servicio de polling de variables PLC
    /// </summary>
    public class PlcPollingConfiguration
    {
        public bool Enabled { get; set; } = true;
        public int PollingIntervalMs { get; set; } = 1000;
        public string ExcelFileName { get; set; } = "ProjectConfig.xlsm";
        public bool AutoLoadFromExcel { get; set; } = true;
    }

    /// <summary>
    /// Estado interno de una variable monitoreada
    /// </summary>
    public class PlcVariableState
    {
        public string Name { get; set; } = string.Empty;
        public object? LastValue { get; set; }
        public DateTime LastUpdate { get; set; } = DateTime.UtcNow;
        public int ReadErrorCount { get; set; } = 0;
    }
}
