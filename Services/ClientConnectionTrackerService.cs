using Microsoft.Extensions.Hosting;
using SW.PC.API.Backend.Models.Excel;

namespace SW.PC.API.Backend.Services
{
    /// <summary>
    /// Servicio de background que actualiza el contador de clientes conectados al PLC cada segundo
    /// </summary>
    public class ClientConnectionTrackerService : BackgroundService
    {
        private readonly ILogger<ClientConnectionTrackerService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly ITwinCATService _twinCATService;
        
        // Referencias estáticas compartidas con ScadaHub
        public static int ActiveConnections { get; set; } = 0;
        public static int CycleCounter { get; set; } = 0;
        public static Dictionary<string, (string Username, string IPAddress)> ConnectedClients { get; } = new();
        public static readonly object LockObj = new object();

        // 🔄 Estado anterior de conexión al PLC para detectar transición disconnected->connected
        // y reenviar UserLogged/ClientsIdConnected (que solo se escriben en login/logout).
        private bool _previousPlcConnected = false;
        
        public ClientConnectionTrackerService(
            ILogger<ClientConnectionTrackerService> logger,
            IServiceProvider serviceProvider,
            ITwinCATService twinCATService)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _twinCATService = twinCATService;
        }
        
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("⏱️ ClientConnectionTrackerService iniciado");
            
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(1000, stoppingToken); // Cada 1 segundo
                    
                    int currentCounter = 0;
                    bool hasClients = false;
                    List<(string Username, string IPAddress)> clientsList;
                    
                    lock (LockObj)
                    {
                        if (ActiveConnections > 0)
                        {
                            CycleCounter++;
                            currentCounter = CycleCounter;
                            hasClients = true;
                            clientsList = ConnectedClients.Values.Take(6).ToList();
                        }
                        else
                        {
                            CycleCounter = 0;
                            clientsList = new List<(string, string)>();
                        }
                    }
                    
                    // 📤 Escribir al PLC (siempre, para que el contador se resetee a 0)
                    await UpdatePlcCounterAsync(currentCounter, clientsList);

                    // 🔄 Detectar reconexión del PLC (estaba desconectado y ahora sí):
                    // si hay clientes conectados, reenviar UserLogged/ClientsIdConnected porque
                    // el ScadaHub.OnConnectedAsync de esos clientes pudo haberse ejecutado mientras
                    // el PLC no estaba conectado y la escritura se omitió silenciosamente.
                    // Casos cubiertos:
                    //  - Primer arranque: PC arranca antes que el PLC esté en RUN.
                    //  - PLC se desconecta (descarga de software, reinicio) y vuelve mientras
                    //    los clientes siguen conectados.
                    bool currentlyConnected = _twinCATService.IsConnected;
                    if (currentlyConnected && !_previousPlcConnected && hasClients)
                    {
                        _logger.LogInformation("🔄 PLC reconectado con {Count} cliente(s) ya conectado(s) - reenviando UserLogged/ClientsIdConnected", clientsList.Count);
                        await UpdatePlcClientsAsync(_serviceProvider, _twinCATService, _logger);
                    }
                    _previousPlcConnected = currentlyConnected;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "⚠️ Error en ClientConnectionTrackerService");
                }
            }
            
            _logger.LogInformation("⏱️ ClientConnectionTrackerService detenido");
        }
        
        private async Task UpdatePlcCounterAsync(int counter, List<(string Username, string IPAddress)> clients)
        {
            try
            {
                if (!_twinCATService.IsConnected)
                    return;
                
                using var scope = _serviceProvider.CreateScope();
                var excelConfigService = scope.ServiceProvider.GetRequiredService<IExcelConfigService>();
                var projectContext = scope.ServiceProvider.GetRequiredService<IProjectContextService>();
                
                var excelPath = projectContext.ExcelConfigPath;
                var systemConfig = await excelConfigService.LoadSystemConfigurationAsync(excelPath);
                
                // Escribir solo el contador (cada segundo)
                if (!string.IsNullOrEmpty(systemConfig.CounterCycleLive))
                {
                    await _twinCATService.WriteVariableAsync(systemConfig.CounterCycleLive, counter, typeof(int));
                    _logger.LogDebug("⏱️ CounterCycleLive: {Counter}", counter);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "⚠️ Error actualizando contador en PLC");
            }
        }
        
        /// <summary>
        /// Actualiza usuarios e IPs en el PLC (llamado desde ScadaHub en conexión/desconexión)
        /// </summary>
        public static async Task UpdatePlcClientsAsync(
            IServiceProvider serviceProvider, 
            ITwinCATService twinCATService,
            ILogger logger)
        {
            try
            {
                if (!twinCATService.IsConnected)
                    return;
                
                List<(string Username, string IPAddress)> clientsList;
                int currentCounter;
                
                lock (LockObj)
                {
                    clientsList = ConnectedClients.Values.Take(6).ToList();
                    currentCounter = CycleCounter;
                }
                
                using var scope = serviceProvider.CreateScope();
                var excelConfigService = scope.ServiceProvider.GetRequiredService<IExcelConfigService>();
                var projectContext = scope.ServiceProvider.GetRequiredService<IProjectContextService>();
                
                var excelPath = projectContext.ExcelConfigPath;
                var systemConfig = await excelConfigService.LoadSystemConfigurationAsync(excelPath);
                
                var writeTasks = new List<Task<bool>>();
                
                // Escribir contador
                if (!string.IsNullOrEmpty(systemConfig.CounterCycleLive))
                {
                    writeTasks.Add(twinCATService.WriteVariableAsync(systemConfig.CounterCycleLive, currentCounter, typeof(int)));
                }
                
                // Lista paralela de nombres para poder reportar cuál falla
                var writeNames = new List<string>();
                if (!string.IsNullOrEmpty(systemConfig.CounterCycleLive))
                    writeNames.Add(systemConfig.CounterCycleLive);

                // Escribir UserLogged[0..5]
                if (!string.IsNullOrEmpty(systemConfig.UserLogged))
                {
                    for (int i = 0; i < 6; i++)
                    {
                        string username = i < clientsList.Count ? clientsList[i].Username : "";
                        string arrayVarName = $"{systemConfig.UserLogged}[{i}]";
                        writeTasks.Add(twinCATService.WriteVariableAsync(arrayVarName, username, typeof(string)));
                        writeNames.Add(arrayVarName);
                    }
                }
                
                // Escribir ClientsIdConnected[0..5]
                if (!string.IsNullOrEmpty(systemConfig.ClientsIdConnected))
                {
                    for (int i = 0; i < 6; i++)
                    {
                        string ipAddress = i < clientsList.Count ? clientsList[i].IPAddress : "";
                        string arrayVarName = $"{systemConfig.ClientsIdConnected}[{i}]";
                        writeTasks.Add(twinCATService.WriteVariableAsync(arrayVarName, ipAddress, typeof(string)));
                        writeNames.Add(arrayVarName);
                    }
                }
                
                var results = await Task.WhenAll(writeTasks);

                // 🔍 Detectar fallos silenciosos (WriteVariableAsync devuelve false sin lanzar excepción)
                var failed = new List<string>();
                for (int i = 0; i < results.Length && i < writeNames.Count; i++)
                {
                    if (!results[i]) failed.Add(writeNames[i]);
                }

                if (failed.Count > 0)
                {
                    logger.LogWarning("⚠️ PLC actualizado parcialmente: {Failed} variable(s) fallaron: {Names}. " +
                        "Posible handle ADS obsoleto tras reconexión - se reintentará en la próxima actualización.",
                        failed.Count, string.Join(", ", failed));
                }
                else
                {
                    logger.LogInformation("✅ PLC actualizado: {Count} clientes, contador={Counter}", clientsList.Count, currentCounter);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "⚠️ Error actualizando clientes en PLC");
            }
        }
    }
}
