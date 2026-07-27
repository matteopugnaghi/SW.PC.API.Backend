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
        public static Dictionary<string, (string Username, string IPAddress, string CurrentScreen, string HostName)> ConnectedClients { get; } = new();
        public static readonly object LockObj = new object();

        // 🔄 Estado anterior de conexión al PLC para detectar transición disconnected->connected
        // y reenviar UserLogged/ClientsIdConnected (que solo se escriben en login/logout).
        private bool _previousPlcConnected = false;

        // 🔁 Reenvío periódico de UserLogged/ClientsIdConnected (autocuración): las escrituras
        // por evento (login/logout) pueden fallar silenciosamente (handle ADS obsoleto tras
        // reconexión, timeout transitorio) o el PLC puede resetear las variables a '' con una
        // descarga/online-change SIN que la conexión ADS llegue a caer. Reenviando cada pocos
        // segundos, el PLC siempre refleja el estado real en <= ResendEverySeconds aunque un
        // intento puntual falle. Crítico: st_InfoUserLogged habilita comandos externos
        // (selectores, botones) en la máquina.
        private const int ResendEverySeconds = 5;
        private int _cyclesSinceResend = 0;

        // Marcado cuando una escritura de UserLogged/ClientsIdConnected falla (o el PLC no
        // estaba conectado) para reintentar en el siguiente ciclo sin esperar al periodo.
        public static volatile bool PendingResend = false;
        
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
                    List<(string Username, string IPAddress, string CurrentScreen, string HostName)> clientsList;
                    
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
                            clientsList = new List<(string, string, string, string)>();
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
                    bool plcReconnected = currentlyConnected && !_previousPlcConnected && hasClients;
                    _previousPlcConnected = currentlyConnected;

                    // 🔁 Reenviar UserLogged/ClientsIdConnected si:
                    //  - el PLC acaba de reconectar con clientes ya conectados, o
                    //  - una escritura anterior falló (PendingResend), o
                    //  - toca el reenvío periódico (autocuración cada ResendEverySeconds).
                    _cyclesSinceResend++;
                    if (currentlyConnected && (plcReconnected || PendingResend || _cyclesSinceResend >= ResendEverySeconds))
                    {
                        if (plcReconnected)
                        {
                            _logger.LogInformation("🔄 PLC reconectado con {Count} cliente(s) ya conectado(s) - reenviando UserLogged/ClientsIdConnected", clientsList.Count);
                        }
                        else if (PendingResend)
                        {
                            _logger.LogInformation("🔁 Reintentando escritura de UserLogged/ClientsIdConnected tras fallo anterior");
                        }

                        bool quiet = !plcReconnected && !PendingResend; // reenvío rutinario → log Debug
                        _cyclesSinceResend = 0;
                        await UpdatePlcClientsAsync(_serviceProvider, _twinCATService, _logger, quiet);
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "⚠️ Error en ClientConnectionTrackerService");
                }
            }
            
            _logger.LogInformation("⏱️ ClientConnectionTrackerService detenido");
        }
        
        private async Task UpdatePlcCounterAsync(int counter, List<(string Username, string IPAddress, string CurrentScreen, string HostName)> clients)
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
            ILogger logger,
            bool quiet = false)
        {
            try
            {
                if (!twinCATService.IsConnected)
                {
                    // No se pudo escribir: reintentar en cuanto el PLC vuelva a estar conectado.
                    PendingResend = true;
                    return;
                }
                
                List<(string Username, string IPAddress, string CurrentScreen, string HostName)> clientsList;
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
                
                // 📺 Escribir CurrentScreenPlcVariable[0..5] - pantalla activa de CADA usuario
                // Array paralelo a UserLogged/ClientsIdConnected: mismo índice = mismo usuario.
                if (!string.IsNullOrEmpty(systemConfig.CurrentScreenPlcVariable))
                {
                    for (int i = 0; i < 6; i++)
                    {
                        string screen = i < clientsList.Count ? clientsList[i].CurrentScreen : "";
                        string arrayVarName = $"{systemConfig.CurrentScreenPlcVariable}[{i}]";
                        writeTasks.Add(twinCATService.WriteVariableAsync(arrayVarName, screen, typeof(string)));
                        writeNames.Add(arrayVarName);
                    }
                }
                
                // 🖥️ Escribir ClientsHostName[0..5] - nombre de equipo de CADA usuario
                // Array paralelo (mismo índice = mismo usuario). Origen: CN del certificado
                // cliente mTLS (verificado) o Environment.MachineName si es el kiosco local.
                // Sin identidad verificable → "" (nunca se adivina por DNS inverso).
                if (!string.IsNullOrEmpty(systemConfig.ClientsHostName))
                {
                    for (int i = 0; i < 6; i++)
                    {
                        string hostName = i < clientsList.Count ? clientsList[i].HostName : "";
                        string arrayVarName = $"{systemConfig.ClientsHostName}[{i}]";
                        writeTasks.Add(twinCATService.WriteVariableAsync(arrayVarName, hostName, typeof(string)));
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
                    PendingResend = true;
                    logger.LogWarning("⚠️ PLC actualizado parcialmente: {Failed} variable(s) fallaron: {Names}. " +
                        "Posible handle ADS obsoleto tras reconexión - se reintentará en el próximo ciclo (1s).",
                        failed.Count, string.Join(", ", failed));
                }
                else
                {
                    PendingResend = false;
                    if (quiet)
                        logger.LogDebug("✅ PLC actualizado (reenvío periódico): {Count} clientes, contador={Counter}", clientsList.Count, currentCounter);
                    else
                        logger.LogInformation("✅ PLC actualizado: {Count} clientes, contador={Counter}", clientsList.Count, currentCounter);
                }
            }
            catch (Exception ex)
            {
                PendingResend = true;
                logger.LogWarning(ex, "⚠️ Error actualizando clientes en PLC");
            }
        }

        /// <summary>
        /// 📺 Actualiza la pantalla activa de UNA conexión concreta.
        /// La escritura al PLC la hace luego UpdatePlcClientsAsync (array CurrentScreenPlcVariable[0..5]).
        /// Devuelve true si la conexión estaba registrada.
        /// </summary>
        public static bool SetClientScreen(string connectionId, string screenName)
        {
            lock (LockObj)
            {
                if (ConnectedClients.TryGetValue(connectionId, out var info))
                {
                    ConnectedClients[connectionId] = (info.Username, info.IPAddress, screenName ?? "", info.HostName);
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// 📺 Limpia el array de pantallas en el PLC (CurrentScreenPlcVariable[0..5] = "").
        /// Usado en el shutdown del backend para indicar que el HMI está offline.
        /// </summary>
        public static async Task ClearPlcScreensAsync(
            IServiceProvider serviceProvider,
            ITwinCATService twinCATService,
            ILogger logger)
        {
            try
            {
                if (!twinCATService.IsConnected) return;
                
                using var scope = serviceProvider.CreateScope();
                var excelConfigService = scope.ServiceProvider.GetRequiredService<IExcelConfigService>();
                var projectContext = scope.ServiceProvider.GetRequiredService<IProjectContextService>();
                
                var systemConfig = await excelConfigService.LoadSystemConfigurationAsync(projectContext.ExcelConfigPath);
                if (string.IsNullOrEmpty(systemConfig?.CurrentScreenPlcVariable)) return;
                
                var writeTasks = new List<Task<bool>>();
                for (int i = 0; i < 6; i++)
                {
                    writeTasks.Add(twinCATService.WriteVariableAsync(
                        $"{systemConfig.CurrentScreenPlcVariable}[{i}]", "", typeof(string)));
                }
                
                await Task.WhenAll(writeTasks);
                logger.LogInformation("📺 ✅ Pantallas de clientes limpiadas en el PLC ({Var}[0..5] = '')", systemConfig.CurrentScreenPlcVariable);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "⚠️ No se pudieron limpiar las pantallas en el PLC");
            }
        }
    }
}
