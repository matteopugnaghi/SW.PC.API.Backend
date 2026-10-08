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

        // 🔁 CounterCycleLive es un INT de TwinCAT (Int16, máx. 32767). El contador se escribe
        // con Convert.ToInt16: si superase 32767 lanzaría OverflowException en cada ciclo y el
        // latido dejaría de llegar al PLC de forma permanente (≈9 h de conexión ininterrumpida),
        // con lo que el watchdog del PLC daría el HMI por muerto y deshabilitaría los mandos
        // externos (p. ej. modo manual) aunque el usuario siguiera operando. Envolvemos a 1
        // (nunca a 0: 0 significa "sin clientes") con margen antes del límite.
        private const int MaxCycleCounter = 32000;

        // 🕐 Gracia de reconexión para los arrays del PLC (UserLogged/ClientsIdConnected/
        // CurrentScreen/ClientsHostName). Una reconexión SignalR (watchdog zombi, micro-corte
        // de red, timeout del servidor) produce OnDisconnected(vieja) + OnConnected(nueva), y
        // ambas reescribían el slot del cliente con "" antes de que el frontend pudiera reenviar
        // SetActiveView. El PLC interpreta CurrentScreen <> "manual" como "el operador salió de
        // la página de manuales" y RESETEA los manuales, aunque el usuario siguiera en el panel
        // con el botón activo. Con la gracia, el slot se conserva ReconnectGraceSeconds tras la
        // desconexión y la nueva conexión del mismo usuario@IP hereda su pantalla: el PLC nunca
        // ve el slot vacío por una reconexión. Si no reconecta, el slot se vacía en el
        // siguiente reenvío periódico (<= ReconnectGraceSeconds + ResendEverySeconds).
        // ActiveConnections/CounterCycleLive NO se ven afectados por la gracia.
        private const int ReconnectGraceSeconds = 20;
        private static readonly Dictionary<string, ((string Username, string IPAddress, string CurrentScreen, string HostName) Info, DateTime DisconnectedAtUtc)> _recentlyDisconnected = new();

        /// <summary>Fuente de tiempo (sustituible en tests para simular la expiración de la gracia).</summary>
        public static Func<DateTime> UtcNowProvider { get; set; } = () => DateTime.UtcNow;

        /// <summary>Limpia todo el estado estático compartido. SOLO para tests.</summary>
        public static void ResetStateForTesting()
        {
            lock (LockObj)
            {
                ConnectedClients.Clear();
                _recentlyDisconnected.Clear();
                _slotByClient.Clear();
                ActiveConnections = 0;
                CycleCounter = 0;
                PendingResend = false;
            }
        }

        private static string ClientKey(string username, string ipAddress) => $"{username}|{ipAddress}";

        /// <summary>
        /// Recuerda un cliente recién desconectado para conservar su slot en el PLC durante la gracia.
        /// Debe llamarse dentro de lock(LockObj).
        /// </summary>
        public static void RememberDisconnectedClient((string Username, string IPAddress, string CurrentScreen, string HostName) info)
        {
            _recentlyDisconnected[ClientKey(info.Username, info.IPAddress)] = (info, UtcNowProvider());
        }

        /// <summary>
        /// Si el mismo usuario@IP se desconectó hace menos de ReconnectGraceSeconds, devuelve la
        /// pantalla que tenía (y olvida la entrada); si no, "". Debe llamarse dentro de lock(LockObj).
        /// </summary>
        public static string TakeInheritedScreen(string username, string ipAddress)
        {
            var key = ClientKey(username, ipAddress);
            if (_recentlyDisconnected.TryGetValue(key, out var entry))
            {
                _recentlyDisconnected.Remove(key);
                if ((UtcNowProvider() - entry.DisconnectedAtUtc).TotalSeconds <= ReconnectGraceSeconds)
                    return entry.Info.CurrentScreen;
            }
            return "";
        }

        /// <summary>
        /// Slots [0..5] a reflejar en los arrays paralelos del PLC (UserLogged / ClientsIdConnected /
        /// CurrentScreen / ClientsHostName). Entradas vacías = ("","","",""). Incluye las conexiones
        /// vivas y las desconectadas dentro de la gracia (cuyo usuario@IP no tenga ya una viva).
        ///
        /// 📌 ASIGNACIÓN ESTABLE DE SLOTS (el programa PLC razona por índice):
        ///  • [0] está RESERVADO al cliente LOCAL (kiosco, loopback). El PLC evalúa CurrentScreen[0]
        ///    para decidir si el operador está en la página de manuales. Si no hay kiosco conectado,
        ///    [0] queda vacío (ningún remoto lo ocupa).
        ///  • Los remotos reciben el menor índice libre en [1..5] al aparecer y lo CONSERVAN mientras
        ///    estén vivos o en gracia; al reconectar recuperan el mismo índice. Conectar/desconectar
        ///    otros clientes NUNCA desplaza a los demás (antes el orden era el de inserción de un
        ///    diccionario y cualquier cambio podía mover a un usuario de índice, lo que el PLC
        ///    interpretaba como un cambio de pantalla).
        /// Debe llamarse dentro de lock(LockObj).
        /// </summary>
        public static (string Username, string IPAddress, string CurrentScreen, string HostName)[] GetPlcClientsSnapshot()
        {
            var now = UtcNowProvider();
            foreach (var key in _recentlyDisconnected
                         .Where(kv => (now - kv.Value.DisconnectedAtUtc).TotalSeconds > ReconnectGraceSeconds)
                         .Select(kv => kv.Key).ToList())
            {
                _recentlyDisconnected.Remove(key);
            }

            // Clientes presentes: vivos primero (una entrada por usuario@IP), luego en gracia.
            var present = new List<(string Key, (string Username, string IPAddress, string CurrentScreen, string HostName) Info, bool IsLive)>();
            var seen = new HashSet<string>();
            foreach (var c in ConnectedClients.Values)
            {
                var key = ClientKey(c.Username, c.IPAddress);
                if (seen.Add(key)) present.Add((key, c, true));
            }
            foreach (var kv in _recentlyDisconnected)
            {
                if (seen.Add(kv.Key)) present.Add((kv.Key, kv.Value.Info, false));
            }

            // Liberar slots de clientes que ya no están (ni vivos ni en gracia).
            foreach (var key in _slotByClient.Keys.Where(k => !seen.Contains(k)).ToList())
            {
                _slotByClient.Remove(key);
            }

            var slots = new (string Username, string IPAddress, string CurrentScreen, string HostName)[MaxPlcSlots];
            Array.Fill(slots, ("", "", "", ""));
            var taken = new bool[MaxPlcSlots];

            // [0] → local. Prioridad: vivo que ya tenía [0] > cualquier vivo local > local en gracia.
            string? localKey = null;
            foreach (var p in present.Where(p => IsLocalClient(p.Info.IPAddress))
                                     .OrderBy(p => p.IsLive ? 0 : 1)
                                     .ThenBy(p => _slotByClient.TryGetValue(p.Key, out var s) && s == 0 ? 0 : 1))
            {
                localKey = p.Key;
                slots[0] = p.Info;
                taken[0] = true;
                _slotByClient[p.Key] = 0;
                break;
            }

            // Otros locales en gracia distintos del titular de [0] (p. ej. el usuario anterior del
            // kiosco tras un cambio de login) quedan superados: no se representan.
            foreach (var p in present.Where(p => p.Key != localKey && !p.IsLive && IsLocalClient(p.Info.IPAddress)).ToList())
            {
                _recentlyDisconnected.Remove(p.Key);
                _slotByClient.Remove(p.Key);
                present.Remove(p);
            }

            var remaining = present.Where(p => p.Key != localKey).ToList();

            // Pasada 1: quien ya tenía un slot [1..5] lo recupera (vivos y en gracia).
            foreach (var p in remaining)
            {
                if (_slotByClient.TryGetValue(p.Key, out var prev) && prev >= 1 && prev < MaxPlcSlots && !taken[prev])
                {
                    slots[prev] = p.Info;
                    taken[prev] = true;
                }
                else
                {
                    _slotByClient.Remove(p.Key);
                }
            }

            // Pasada 2: los nuevos reciben el menor slot libre en [1..5] (vivos antes que en gracia).
            foreach (var p in remaining.Where(p => !_slotByClient.ContainsKey(p.Key)).OrderBy(p => p.IsLive ? 0 : 1))
            {
                int slot = Array.FindIndex(taken, 1, t => !t);
                if (slot < 0) continue; // sin hueco: no representado (máx. 6 clientes)
                slots[slot] = p.Info;
                taken[slot] = true;
                _slotByClient[p.Key] = slot;
            }

            return slots;
        }

        private const int MaxPlcSlots = 6;

        // Slot asignado a cada usuario@IP presente (vivo o en gracia). Se libera al desaparecer.
        private static readonly Dictionary<string, int> _slotByClient = new();

        /// <summary>true si la IP de la conexión es loopback (::1, 127.x.x.x, ::ffff:127.x.x.x).</summary>
        public static bool IsLocalClient(string? ipAddress) =>
            OriginPermissionEvaluator.NormalizeIp(ipAddress) == "127.0.0.1";

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

        // 🔒 Serializa UpdatePlcClientsAsync. Se invoca desde varios sitios en paralelo
        // (OnConnected, OnDisconnected, SetActiveView, reenvío periódico): sin serializar,
        // dos invocaciones toman snapshots distintos y sus escrituras ADS pueden llegar al
        // PLC en orden inverso (un snapshot antiguo con CurrentScreen='principal' pisando al
        // nuevo con 'manual' hasta el siguiente reenvío). Tomando el snapshot DENTRO de la
        // sección crítica, la última escritura refleja siempre el último estado.
        private static readonly SemaphoreSlim _plcClientsWriteLock = new(1, 1);
        
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
                    
                    lock (LockObj)
                    {
                        if (ActiveConnections > 0)
                        {
                            CycleCounter = CycleCounter >= MaxCycleCounter ? 1 : CycleCounter + 1;
                            currentCounter = CycleCounter;
                            hasClients = true;
                        }
                        else
                        {
                            CycleCounter = 0;
                        }
                    }
                    
                    // 📤 Escribir al PLC (siempre, para que el contador se resetee a 0)
                    await UpdatePlcCounterAsync(currentCounter);

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
                            _logger.LogInformation("🔄 PLC reconectado con {Count} cliente(s) ya conectado(s) - reenviando UserLogged/ClientsIdConnected", ActiveConnections);
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
        
        private async Task UpdatePlcCounterAsync(int counter)
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
            bool? isConnected = null;
            try
            {
                isConnected = twinCATService.IsConnected;
                if (isConnected == false)
                {
                    // No se pudo escribir: reintentar en cuanto el PLC vuelva a estar conectado.
                    PendingResend = true;
                    return;
                }
            }
            catch (Exception ex)
            {
                PendingResend = true;
                logger.LogWarning(ex, "⚠️ Error comprobando conexión PLC antes de actualizar clientes");
                return;
            }

            await _plcClientsWriteLock.WaitAsync();
            try
            {
                (string Username, string IPAddress, string CurrentScreen, string HostName)[] slots;
                int currentCounter;
                
                // Snapshot DENTRO del lock de escritura: refleja el estado más reciente.
                lock (LockObj)
                {
                    slots = GetPlcClientsSnapshot();
                    currentCounter = CycleCounter;
                }
                int presentClients = slots.Count(s => !string.IsNullOrEmpty(s.Username));
                
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
                    for (int i = 0; i < MaxPlcSlots; i++)
                    {
                        string arrayVarName = $"{systemConfig.UserLogged}[{i}]";
                        writeTasks.Add(twinCATService.WriteVariableAsync(arrayVarName, slots[i].Username, typeof(string)));
                        writeNames.Add(arrayVarName);
                    }
                }
                
                // Escribir ClientsIdConnected[0..5]
                if (!string.IsNullOrEmpty(systemConfig.ClientsIdConnected))
                {
                    for (int i = 0; i < MaxPlcSlots; i++)
                    {
                        string arrayVarName = $"{systemConfig.ClientsIdConnected}[{i}]";
                        writeTasks.Add(twinCATService.WriteVariableAsync(arrayVarName, slots[i].IPAddress, typeof(string)));
                        writeNames.Add(arrayVarName);
                    }
                }
                
                // 📺 Escribir CurrentScreenPlcVariable[0..5] - pantalla activa de CADA usuario
                // Array paralelo a UserLogged/ClientsIdConnected: mismo índice = mismo usuario.
                if (!string.IsNullOrEmpty(systemConfig.CurrentScreenPlcVariable))
                {
                    for (int i = 0; i < MaxPlcSlots; i++)
                    {
                        string arrayVarName = $"{systemConfig.CurrentScreenPlcVariable}[{i}]";
                        writeTasks.Add(twinCATService.WriteVariableAsync(arrayVarName, slots[i].CurrentScreen, typeof(string)));
                        writeNames.Add(arrayVarName);
                    }
                }
                
                // 🖥️ Escribir ClientsHostName[0..5] - nombre de equipo de CADA usuario
                // Array paralelo (mismo índice = mismo usuario). Origen: CN del certificado
                // cliente mTLS o nombre de dispositivo por token (ambos verificados), o
                // Environment.MachineName si es el kiosco local.
                // Sin identidad verificable → "" (nunca se adivina por DNS inverso).
                if (!string.IsNullOrEmpty(systemConfig.ClientsHostName))
                {
                    for (int i = 0; i < MaxPlcSlots; i++)
                    {
                        string arrayVarName = $"{systemConfig.ClientsHostName}[{i}]";
                        writeTasks.Add(twinCATService.WriteVariableAsync(arrayVarName, slots[i].HostName, typeof(string)));
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
                        logger.LogDebug("✅ PLC actualizado (reenvío periódico): {Count} clientes, contador={Counter}", presentClients, currentCounter);
                    else
                        logger.LogInformation("✅ PLC actualizado: {Count} clientes, contador={Counter}", presentClients, currentCounter);
                }
            }
            catch (Exception ex)
            {
                PendingResend = true;
                logger.LogWarning(ex, "⚠️ Error actualizando clientes en PLC");
            }
            finally
            {
                _plcClientsWriteLock.Release();
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
