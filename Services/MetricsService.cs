using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Models.Excel;
using System.Diagnostics;

namespace SW.PC.API.Backend.Services
{
    /// <summary>
    /// Servicio para recopilar y almacenar métricas de rendimiento del sistema
    /// </summary>
    public interface IMetricsService
    {
        /// <summary>
        /// Registrar el tiempo de un ciclo de polling PLC
        /// </summary>
        void RecordPlcPollingScanTime(double milliseconds);

        /// <summary>
        /// Registrar el número de variables monitoreadas
        /// </summary>
        void SetPlcMonitoredVariables(int count);

        /// <summary>
        /// Registrar el tiempo de un broadcast SignalR
        /// </summary>
        void RecordSignalRBroadcastTime(double milliseconds);

        /// <summary>
        /// Registrar el número de conexiones SignalR activas
        /// </summary>
        void SetSignalRActiveConnections(int count);

        /// <summary>
        /// Registrar el tiempo de carga de un archivo Excel
        /// </summary>
        void RecordExcelLoadTime(double milliseconds);

        /// <summary>
        /// Obtener las métricas actuales del sistema
        /// </summary>
        SystemMetrics GetCurrentMetrics();
    }

    public class MetricsService : IMetricsService
    {
        private readonly object _lock = new object();
        private readonly List<double> _plcScanTimes = new List<double>();
        private readonly List<double> _signalRBroadcastTimes = new List<double>();
        private readonly int _maxSamples = 100; // Mantener últimas 100 muestras
        private readonly DateTime _serverStartTime;

        private double _lastPlcScanTime;
        private int _plcMonitoredVariables;
        private int _signalRActiveConnections;
        private double _lastSignalRBroadcastTime;
        private double _lastExcelLoadTime;

        public MetricsService()
        {
            _serverStartTime = DateTime.UtcNow;
        }

        public void RecordPlcPollingScanTime(double milliseconds)
        {
            lock (_lock)
            {
                _lastPlcScanTime = milliseconds;
                _plcScanTimes.Add(milliseconds);

                // Mantener solo las últimas N muestras
                if (_plcScanTimes.Count > _maxSamples)
                {
                    _plcScanTimes.RemoveAt(0);
                }
            }
        }

        public void SetPlcMonitoredVariables(int count)
        {
            lock (_lock)
            {
                _plcMonitoredVariables = count;
            }
        }

        public void RecordSignalRBroadcastTime(double milliseconds)
        {
            lock (_lock)
            {
                _lastSignalRBroadcastTime = milliseconds;
                _signalRBroadcastTimes.Add(milliseconds);

                // Mantener solo las últimas N muestras
                if (_signalRBroadcastTimes.Count > _maxSamples)
                {
                    _signalRBroadcastTimes.RemoveAt(0);
                }
            }
        }

        public void SetSignalRActiveConnections(int count)
        {
            lock (_lock)
            {
                _signalRActiveConnections = count;
            }
        }

        public void RecordExcelLoadTime(double milliseconds)
        {
            lock (_lock)
            {
                _lastExcelLoadTime = milliseconds;
            }
        }

        public SystemMetrics GetCurrentMetrics()
        {
            lock (_lock)
            {
                var uptime = DateTime.UtcNow - _serverStartTime;
                
                return new SystemMetrics
                {
                    PlcPollingScanTime = Math.Round(_lastPlcScanTime, 2),
                    PlcPollingAvgScanTime = _plcScanTimes.Count > 0 
                        ? Math.Round(_plcScanTimes.Average(), 2) 
                        : 0,
                    PlcMonitoredVariables = _plcMonitoredVariables,
                    SignalRActiveConnections = _signalRActiveConnections,
                    SignalRLastBroadcastTime = Math.Round(_lastSignalRBroadcastTime, 2),
                    SignalRAvgBroadcastTime = _signalRBroadcastTimes.Count > 0 
                        ? Math.Round(_signalRBroadcastTimes.Average(), 2) 
                        : 0,
                    ExcelLastLoadTime = Math.Round(_lastExcelLoadTime, 2),
                    LastUpdate = DateTime.UtcNow,
                    ServerUptime = $"{uptime.Days:00}:{uptime.Hours:00}:{uptime.Minutes:00}:{uptime.Seconds:00}"
                };
            }
        }
    }
}
