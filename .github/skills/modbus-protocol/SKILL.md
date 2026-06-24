---
name: modbus-protocol
description: 'Implement the Aquafrisch Modbus TCP integration — a NEW industrial protocol driver that COEXISTS with TwinCAT/ADS and OPC-UA, gated by Excel `System Config` flag `ModbusEnabled` exactly like OPC-UA (disabled = service does not exist, registered as `DisabledModbusService` stub). Dual role: (A) SERVER/Slave — Aquafrisch Supervisor exposes PLC data (read from TwinCAT over ADS) as Modbus registers so OTHER systems can consume them; (B) CLIENT/Master — Supervisor reads/writes up to 2 external Modbus TCP devices (sources). Variables and alarms are declared in two NEW Excel sheets `Modbus_Variables` and `Modbus_Alarms` (empty = unused, mirroring OPC_UA_Variables/OPC_UA_Alarms). Includes the FRONTEND mirror of OPC-UA: a dedicated `ModbusView` screen, a status row in the InfoPanel "external services" list, and a role permission key `ModbusView` in User Management — all shown ONLY when `ModbusEnabled=TRUE`. USE WHEN: adding `IModbusService`/`ModbusService`, the disabled stub, conditional DI in Program.cs, the Excel sheet parsers, register<->ADS mapping, the Modbus TCP server, the client polling of external devices, SignalR wiring, the ModbusController, the ModbusView React screen, the InfoPanel service status, or the `ModbusView` role permission. DO NOT USE FOR: TwinCAT/ADS internals (use ITwinCATService as-is), OPC-UA changes, Modbus RTU/serial (futuro), or adding PDF/Excel libs. Trigger phrases: "modbus", "protocolo modbus", "modbus tcp", "ModbusEnabled", "IModbusService", "Modbus_Variables", "Modbus_Alarms", "servidor modbus", "exponer registros modbus", "leer dispositivo modbus", "holding register", "coil", "FluentModbus", "vista modbus", "ModbusView", "permisos modbus".'
argument-hint: 'Indica rol y alcance, p.ej. "server: exponer alarmas ADS por holding registers" o "client: leer fuente 2"'
---

# Modbus TCP Integration — Implementation Skill

Aquafrisch Supervisor necesita un **driver Modbus TCP** nuevo. Filosofía y patrones son **idénticos a OPC-UA**: se activa/desactiva por proyecto desde Excel `System Config`, y cuando está deshabilitado **es como si no existiera** (se registra un stub `DisabledModbusService`). No hay interfaz común de protocolo en el repo: cada protocolo tiene la suya (`ITwinCATService`, `IOpcUaServerService`), así que Modbus tiene la suya: **`IModbusService`** (sin clase base compartida, pero con firmas alineadas a `ITwinCATService` por coherencia).

> **Origen siempre ADS.** TwinCAT ↔ Supervisor intercambian registros **siempre por ADS** (`ITwinCATService`). El Supervisor **transforma** esos valores a Modbus para otros sistemas (rol Server). El rol Client es para leer/escribir dispositivos Modbus externos. **Modbus nunca sustituye a ADS como fuente del PLC propio.**

## ⛔ REGLA DE ORO — CERO REGRESIÓN EN PRODUCCIÓN (lo más importante)

**Hay máquinas YA EN PRODUCCIÓN.** Esta funcionalidad es **100 % aditiva y opcional**. Si Modbus está `FALSE`, ausente o vacío en Excel, el software debe comportarse **exactamente como hoy, byte a byte de comportamiento**, sin que la máquina vieja se entere de que Modbus existe.

**Garantías obligatorias (verificar SIEMPRE):**
- ✅ **Default = desactivado.** Si la clave `ModbusEnabled` **no existe**, está **vacía**, es `FALSE`/`0`/`no` ⇒ Modbus OFF. Nunca asumir activado. Un Excel antiguo (sin la clave ni las hojas) es un caso válido y debe funcionar.
- ✅ **Hojas opcionales.** `Modbus_Variables`/`Modbus_Alarms` **ausentes** no deben provocar error, warning ruidoso ni excepción: lista vacía y seguir. Igual que OPC-UA cuando faltan sus hojas.
- ✅ **Stub real.** Con OFF se registra `DisabledModbusService`: **no abre sockets, no crea hilos/BackgroundService, no toca puertos (ni 502), no consume CPU**. Como `DisabledOpcUaServerService`.
- ✅ **No tocar caminos existentes.** Está **PROHIBIDO** modificar la lógica/firmas de `ITwinCATService`, `PlcPollingService`, `AlarmNotificationService`, `OpcUa*`, `ScadaHub`, el arranque actual o el parseo de las hojas existentes (`PLC_Variables`, etc.). Solo **añadir** (nuevos archivos, nuevas claves, nuevos `if (modbusEnabled)`), nunca **alterar** lo que ya funciona.
- ✅ **Aislamiento de fallos.** Cualquier error de Modbus (Excel mal puesto, fuente externa caída, puerto ocupado) **NO** puede tumbar el arranque ni afectar a ADS/OPC-UA/SignalR/HMI. Capturar y degradar SOLO el subsistema Modbus (loguear y seguir).
- ✅ **DI condicional desde el flag**, igual que OPC-UA: los `AddHostedService`/sockets se registran **solo** si `modbusEnabled==true`. Con OFF, ni se instancian.
- ✅ **Frontend invisible con OFF.** Sin menú, sin vista, sin fila en InfoPanel, sin warnings en consola. La UI antigua se ve y funciona idéntica.
- ✅ **Migración sin migración.** Actualizar el binario en una máquina vieja **sin** tocar su Excel debe dejarla funcionando igual. No requerir cambios de Excel, BD ni config para seguir operando.
- ✅ **DB/permisos compatibles.** La nueva clave de permiso `ModbusView` y los nuevos enums de log deben ser **aditivos**: claves/columnas ausentes heredan defaults (ver merge de permisos); nunca romper roles ni logs existentes.

**Antes de dar por terminado:** validar el caso `ModbusEnabled=FALSE` y el caso "Excel viejo sin nada de Modbus" ⇒ arranque y comportamiento **idénticos** a la versión previa (mismos logs de arranque salvo, como mucho, una línea informativa "Modbus disabled"). Si algo cambia para el usuario con Modbus OFF, **es un bug**.

## Decisiones cerradas con el usuario

| Tema | Decisión |
|------|----------|
| Variante | **Modbus TCP** únicamente. RTU/serie = **futuro**, no implementar ni mostrar. |
| Rol | **Ambos**: **Server/Slave** (exponer datos ADS a otros sistemas) **+** **Client/Master** (leer/escribir hasta **2** dispositivos Modbus externos). Alcance único (no por fases). |
| Coexistencia | **Driver adicional**. Convive con TwinCAT y OPC-UA. **No reemplaza** la fuente de datos del PLC propio (siempre ADS). |
| Activación | Flag `ModbusEnabled` en Excel `System Config` (TRUE/1/yes). **Deshabilitado = no existe**: registrar `DisabledModbusService` (stub), sin background services, sin endpoints activos. **Idéntico a `OpcUaEnabled`.** |
| Config de variables | Hoja Excel nueva **`Modbus_Variables`**. Vacía o ausente ⇒ no se usa. |
| Config de alarmas | Hoja Excel nueva **`Modbus_Alarms`**. Motivo: si hay que pasar alarmas a otro sistema, declararlas aquí evita re-leerlas/remapearlas. Vacía o ausente ⇒ no se usa. |
| Fuentes (Client) | Normalmente **máximo 2** dispositivos Modbus externos. Cada fuente = host + puerto + unitId, definida en `System Config`. |
| **Librería** | **Requiere aprobación** añadir NuGet (política CRA/SBOM del repo). Recomendada: **`FluentModbus`** (MIT, open source sin coste, server **y** client TCP, compatible net8.0). Alternativa: `NModbus` (MIT). **NO** añadir el paquete sin confirmación explícita del usuario. |
| Traducciones | Igual que el resto: en dev `t('clave', 'fallback ES')`, traducciones reales al final. |

## Arquitectura objetivo

```
                         ┌──────────────── TwinCAT PLC (Beckhoff) ────────────────┐
                         │                                                        │
                  ADS  ◄─┤  ITwinCATService  (única fuente del PLC propio)        │
                         │                                                        │
   ┌─────────────────────┴────────────────────────────────────────────────────────┐
   │                         AQUAFRISCH SUPERVISOR (backend)                        │
   │                                                                               │
   │  ROL SERVER (Slave) ───────────────────────────────────────────────────────  │
   │    ModbusServerService (BackgroundService)                                    │
   │      • Lee valores vía ITwinCATService (los mismos del polling/notify)        │
   │      • Mapea cada variable de `Modbus_Variables` (origen ADS) a un registro   │
   │      • Sirve un Modbus TCP Server (FC01/02/03/04/05/06/15/16)                 │
   │      • Otros sistemas (SCADA cliente, BMS…) leen/escriben esos registros      │
   │                                                                               │
   │  ROL CLIENT (Master) ──────────────────────────────────────────────────────  │
   │    ModbusClientService (BackgroundService)                                    │
   │      • Hasta 2 fuentes externas (host:port unitId) desde System Config        │
   │      • Lee/escribe registros declarados con Source=Modbus en sheets           │
   │      • Publica cambios a SignalR igual que el polling de ADS                  │
   │                                                                               │
   │            └────────────► ScadaHub (/hubs/scada) ──► React/Babylon            │
   └───────────────────────────────────────────────────────────────────────────────┘
        ▲ (server)                                    │ (client)
   Otros sistemas Modbus  ◄─────────────────────────► Dispositivos Modbus externos (máx 2)
```

## Patrón de registro (DI) — copiar el de OPC-UA en `Program.cs`

OPC-UA se registra condicionalmente leyendo el flag de Excel antes de construir el contenedor (ver bloque `opcUaEnabledInExcel` en `Program.cs`). **Replicar exactamente** para Modbus:

```csharp
// 1) Leer flag desde Excel "System Config" (clave ModbusEnabled / modbus_enabled)
bool modbusEnabledInExcel = /* misma lectura que opcUaEnabledInExcel */;

// 2) Registrar servicio según flag
if (modbusEnabledInExcel)
{
    builder.Services.AddSingleton<IModbusService, ModbusService>();
    // Background services SOLO si está habilitado
    builder.Services.AddHostedService(sp => (ModbusService)sp.GetRequiredService<IModbusService>());
    // (o servicios separados Server/Client si se dividen)
}
else
{
    builder.Services.AddSingleton<IModbusService, DisabledModbusService>(); // stub: IsEnabled=false, no-op
}
```

`DisabledModbusService` = stub equivalente a `DisabledOpcUaServerService`: `IsEnabled => false`, métodos devuelven vacío/`false`, **sin** abrir sockets ni hilos.

## Interfaz `IModbusService` (alinear con `ITwinCATService`)

Firmas sugeridas (ajustar al implementar; mantener coherencia con TwinCAT/OPC-UA):

```csharp
public interface IModbusService
{
    bool IsEnabled { get; }                 // false en el stub
    bool ServerRunning { get; }             // estado del Modbus TCP Server
    ModbusStatus GetStatus();               // como OpcUaServerStatus
    List<ModbusVariable> GetVariables();    // desde Modbus_Variables
    List<ModbusAlarm> GetAlarms();          // desde Modbus_Alarms
    Dictionary<string, object?> GetCurrentValues();

    // Rol Client (fuentes externas, máx 2)
    Task<object?> ReadAsync(string sourceId, ModbusVariable v);
    Task<bool>    WriteAsync(string sourceId, ModbusVariable v, object value);

    event EventHandler<PlcNotification>? OnVariableChanged; // reutiliza PlcNotification
}
```

> Reutilizar `PlcNotification` (Models/TwinCATModels.cs) para no inventar otro tipo de evento y encajar con el flujo SignalR existente.

## Hojas Excel nuevas (espejo de OPC-UA)

Las parsea `ExcelConfigService` (SINGLETON con caché). Si la hoja no existe o no tiene filas de datos ⇒ lista vacía ⇒ Modbus no expone/usa nada.

### `Modbus_Variables`
Igual que OPC-UA relaciona variables ADS con nodos, aquí **cada fila relaciona un símbolo ADS con un registro Modbus**. Ejemplo real:

```
GVL_Modbus.TLS_M3_MAL_RemoteMode  →  40001  (Holding Register, FC03/FC06)
GVL_Modbus.TLS_M3_MAL_Started     →  10001  (Discrete Input, FC02)
GVL_Modbus.TLS_M3_MAL_StartCmd    →  00001  (Coil, FC01/FC05)
```

| Columna | Significado |
|---------|-------------|
| `Name` | Nombre lógico (opcional si se da `AdsSymbol`). |
| `AdsSymbol` | **Ruta completa del símbolo ADS**, p.ej. `GVL_Modbus.TLS_M3_MAL_RemoteMode`. Es el vínculo con TwinCAT en rol Server (igual que la columna de símbolo en `OPC_UA_Variables`). |
| `ModbusRegister` | Registro destino en **notación clásica** `0xxxx`/`1xxxx`/`3xxxx`/`4xxxx`, p.ej. `40001`. El primer dígito implica el `RegisterType` (ver tabla abajo); el resto = dirección 1-based. Alternativamente, usar `RegisterType` + `Address` explícitos. |
| `Function` | Código(s) de función Modbus a usar, p.ej. `FC03/FC06` (read/write holding), `FC02` (read discrete), `FC01/FC05` (coil). Derivable de `ModbusRegister` pero explícito para claridad. |
| `RegisterType` | `Coil` (FC01/05/15) · `DiscreteInput` (FC02) · `InputRegister` (FC04) · `HoldingRegister` (FC03/06/16). Redundante si se usa `ModbusRegister`. |
| `Address` | Dirección 0-based del coil/registro (alternativa a `ModbusRegister`). |
| `DataType` | `BOOL`, `INT16`, `UINT16`, `INT32`, `UINT32`, `FLOAT32`, `STRING`… (multi-word para 32-bit). |
| `WordOrder` | Para tipos >16 bits: `ABCD`/`CDAB`/`BADC`/`DCBA` (big/little endian + swap). |
| `Scale` / `Offset` | Conversión a unidades de ingeniería (`eng = raw * Scale + Offset`). |
| `AccessMode` | `R` / `W` / `RW`. |
| `Source` | `ADS` (rol Server, valor viene de TwinCAT vía `AdsSymbol`) o id de fuente externa (rol Client, p.ej. `MB1`,`MB2`). En rol Server, `AdsSymbol` debe **coincidir EXACTAMENTE** (case-sensitive, ruta completa) con un `VariableName` ya declarado en `PLC_Variables`; si no existe ⇒ error de validación al cargar (no inventar ni crear la variable). |
| `ExcludeFromLog` | `TRUE`/`FALSE`. Igual que la columna de `OPC_UA_Variables`: si `TRUE`, los cambios de esa variable **no** generan log L2 (para watchdogs/vars de alta frecuencia). Por defecto `FALSE` ⇒ todo el intercambio se loguea. |
| `Description` / `Unit` | Documentación. |

**Convención de notación clásica `ModbusRegister`** (offset = nº − base):

| Prefijo | Rango | Tipo | Funciones |
|---------|-------|------|-----------|
| `0xxxx` | `00001…` | Coil | FC01 (R) · FC05/FC15 (W) |
| `1xxxx` | `10001…` | Discrete Input | FC02 (R) |
| `3xxxx` | `30001…` | Input Register | FC04 (R) |
| `4xxxx` | `40001…` | Holding Register | FC03 (R) · FC06/FC16 (W) |

> `40001` ⇒ Holding Register, dirección interna 0. `40002` ⇒ dirección 1, etc. El parser debe convertir la notación clásica a `RegisterType` + `Address` 0-based.

### `Modbus_Alarms`
Mismo patrón que `OPC_UA_Alarms`: nombre, índice (0-based, igual que `st_alarmPc[0..N]`), registro/coil donde se publica el estado, severidad, descripción. Permite re-publicar a terceros sin releer/remapear.

**Columnas:**

| Columna | Uso |
|---------|-----|
| `AlarmName` | Nombre de la alarma. **Obligatorio** (las filas se descartan si está vacío). El **sufijo numérico** (`..._001`) define el índice del array PLC. |
| `AlarmIndex` | Índice **Modbus** (posición del bit en modelo B / orden de visualización). **NO** se usa para leer el estado del PLC. Vacío ⇒ se deriva del nombre. |
| `ModbusRegister` | Registro destino en notación clásica `0xxxx`/`1xxxx`/`3xxxx`/`4xxxx`. El primer dígito implica el `RegisterType`. Alternativa a `RegisterType`+`Address`. |
| `RegisterType` | `Coil`/`DiscreteInput`/`HoldingRegister`/`InputRegister`. **Decide el modelo** (ver abajo). `UINT16`/`WORD` ⇒ HoldingRegister. |
| `Address` | Dirección 0-based del coil/registro (alternativa a `ModbusRegister`). |
| `Bit` | *(opcional, solo modelo B)* Posición del bit (0-15) dentro del registro. Vacío ⇒ `AlarmIndex % 16`. |
| `Severity` | `0`=Alarm · `1`=Notification · `2`=Info. Selecciona el sufijo en `st_alarmPc`. |
| `Description` | Texto descriptivo. |

> ⚠️ **Índice PLC vs índice Modbus (DESACOPLADOS).** El estado se lee de `st_alarmPc[N]` con **`N` derivado del sufijo del nombre** (`..._001` → `st_alarmPc[1]`), idéntico a OPC-UA (`ExtractAlarmIndex`). La columna `AlarmIndex` solo posiciona el bit/coil Modbus y la visualización. El modelo `ModbusAlarm` lleva dos campos: `PlcAlarmIndex` (estado, name-derived) y `AlarmIndex` (Modbus). Evita el desfase cuando nombre (`_001`) y posición Modbus (bit 0) no coinciden.

**Dos modelos (el cliente elige; lo decide `RegisterType` por fila):**

- **Modelo A — Coils / Discrete Inputs (un bit por alarma).** `RegisterType=Coil` (FC01) o `DiscreteInput` (FC02). `Address` = nº de coil 0-based, **único por alarma**. Helper `WriteBoolToBuffer`.
  ```
  AlarmName            AlarmIndex  RegisterType  Address  Severity
  TLS_M3_MAL_Alarm_001 0           Coil          0        0
  TLS_M3_MAL_Alarm_002 1           Coil          1        0
  TLS_M3_MAL_Alarm_003 2           Coil          2        1
  ```

- **Modelo B — Holding / Input Register (bits empaquetados en una palabra).** `RegisterType=HoldingRegister`/`InputRegister` (o `ModbusRegister=40100`). Varias alarmas comparten el **mismo** registro; cada una ocupa un bit = columna `Bit` o, por defecto, `AlarmIndex % 16`. Helper `WriteAlarmBitToRegister` (read-modify-write, big-endian). Es el modelo de la foto del cliente: `40100` con bits 0/1/2.
  ```
  AlarmName            AlarmIndex  ModbusRegister  RegisterType  Severity   → bit (AlarmIndex%16)
  TLS_M3_MAL_Alarm_001 0           40100           UINT16        0          → bit 0 de 40100
  TLS_M3_MAL_Alarm_002 1           40100           UINT16        0          → bit 1 de 40100
  TLS_M3_MAL_Alarm_003 2           40100           UINT16        1          → bit 2 de 40100
  ```
  > 16 bits por registro: si hay >16 alarmas en el mismo registro, `%16` colisiona ⇒ usar registros distintos (`40100`,`40101`,…) o la columna `Bit` explícita.

### `System Config` — claves nuevas (IP / puertos / conexiones)
La configuración de **IP y puertos** se declara aquí en Excel (no en `appsettings.json`), por proyecto, igual que OPC-UA declara `Port`/`ServerUri`.

| Clave | Ejemplo | Uso |
|-------|---------|-----|
| `ModbusEnabled` | `TRUE` | Activa todo el driver (igual que `OpcUaEnabled`). |
| `ModbusServerBindIp` | `0.0.0.0` | IP de escucha del Modbus TCP Server (rol Server). `0.0.0.0` = todas las interfaces. |
| `ModbusServerPort` | `502` | Puerto del Modbus TCP Server (rol Server). |
| `ModbusServerUnitId` | `1` | Unit/Slave id del server. |
| `ModbusClient1_Host` / `_Port` / `_UnitId` | `192.168.2.50` / `502` / `1` | **IP + puerto** de la fuente externa 1 (rol Client). |
| `ModbusClient2_Host` / `_Port` / `_UnitId` | `192.168.2.51` / `502` / `1` | Fuente externa 2 (**máx 2**). |
| `ModbusPollIntervalMs` | `1000` | Polling de fuentes externas (rol Client). |

> Los ids de fuente (`MB1`/`MB2` o `ModbusClient1`/`2`) usados en la columna `Source` de `Modbus_Variables` deben corresponder con estas claves.

## Flujo en tiempo real (encajar con lo existente)

- **Server**: leer valores con `ITwinCATService` (no abrir una segunda fuente para el PLC propio) y empujarlos al Modbus TCP Server. Cuando un tercero **escribe** un holding register/coil ⇒ propagar a ADS vía `ITwinCATService.WriteVariableAsync` **solo si** `AccessMode` lo permite.
- **Client**: `ModbusClientService` (BackgroundService) hace polling de las fuentes externas con `ModbusPollIntervalMs`, detecta cambios y emite `OnVariableChanged`/SignalR igual que `PlcPollingService`.
- Publicar a **`ScadaHub`** con los mismos eventos (`PlcDataUpdate`, etc.) para que el frontend no necesite lógica nueva.

## Alarmas — REUSAR el subsistema central, NUNCA duplicar polling (igual que OPC-UA)

⚠️ **Crítico para no afectar la máquina.** El sistema ya tiene **un único** subsistema de alarmas push-based; OPC-UA **no** vuelve a sondear alarmas, solo **lee la caché compartida**. Modbus debe hacer **exactamente lo mismo**.

**Cómo funciona hoy (no tocar):**
- [Services/AlarmNotificationService.cs](Services/AlarmNotificationService.cs) (Singleton + HostedService) mantiene la caché central `_alarmStates` (`ConcurrentDictionary<string,bool>`), alimentada **por notificaciones push ADS** suscribiéndose a `ITwinCATService.OnVariableChanged` (`_twinCATService.OnVariableChanged += OnAlarmChanged`). **Solo hay tráfico cuando una alarma cambia** (cero polling).
- Expone el estado a todos los consumidores con un único método:
  ```csharp
  public IReadOnlyDictionary<string, bool> GetCurrentAlarmStates() => _alarmStates;
  ```

**Cómo lo consume OPC-UA (patrón a copiar):** `OpcUaServerService` inyecta `AlarmNotificationService` y en su bucle hace **una** llamada `GetCurrentAlarmStates()` (comentario en código: *"push-based, 0 extra ADS reads"*), mapea por `AlarmIndex`/sufijo a `st_alarmPc[idx].{Alarm|Notification|Info}`, actualiza sus nodos y hace change-detection contra `_previousAlarmStates` para loguear `OpcUaAlarmChange` en L2. **Nunca** llama a `ReadVariableAsync` para alarmas.

**Blueprint Modbus (idéntico):**
1. Inyectar `AlarmNotificationService` (Singleton ya registrado) en `ModbusService`. **No** crear un segundo bucle de alarmas, **no** registrar notificaciones ADS propias, **no** sondear el PLC por alarmas.
2. En el ciclo Modbus: `var alarmStates = _alarmNotificationService.GetCurrentAlarmStates();` (una sola llamada) y, para cada fila de `Modbus_Alarms`, buscar su estado en la caché (por `AlarmIndex`/`PlcVariable`) y **escribir el coil/registro** correspondiente.
3. Change-detection con un `_previousAlarmStates` propio → log L2 `ModbusAlarmChange` solo cuando cambia (igual que OPC-UA).
4. Modelo: `Modbus_Alarms` mapea `AlarmIndex` (**0-based**, mismo del PLC/Excel `st_alarmPc[0..N]`) → coil (modelo A) o bit dentro de un registro (modelo B); reutilizar el `AlarmDefinition` central ([Models/ExcelModels.cs](Models/ExcelModels.cs)) como fuente de índices/textos, espejo de `OpcUaAlarm` ([Models/OpcUaModels.cs](Models/OpcUaModels.cs)).

**Por qué (no negociable):** ADS tiene un límite de suscripciones de notificación concurrentes; duplicar el polling de alarmas = lecturas redundantes al PLC, más CPU/red, posibles estados desincronizados entre HMI/OPC-UA/Modbus y riesgo de afectar a una máquina en producción. La única fuente de verdad es `AlarmNotificationService`.

## Controller (opcional, espejo de OPC-UA / EtherCAT)

`ModbusController` con endpoints de solo estado/diagnóstico (no exponer escritura arbitraria sin rol): `GET /api/modbus/status`, `GET /api/modbus/variables`, `GET /api/modbus/alarms`. Cuando `IsEnabled=false`, devolver 404/`{enabled:false}` de forma coherente con OPC-UA.

**Además**, exponer el estado para el frontend por los **dos canales que ya usa OPC-UA** (no inventar nuevos):
- `GET /api/config/system` debe devolver `modbusEnabled` (boolean), igual que `opcUaEnabled`. Lo lee `App.js` para gatear menú y vista.
- `GET /api/config/metrics` (`servicesStatus`) debe incluir `modbusEnabled`, `modbusRunning`, y opcional `modbusConnectedClients`/`modbusSources`, igual que `opcUaEnabled`/`opcUaRunning`/`opcUaConnectedClients`. Lo lee `InfoPanel` para la fila de servicio.

## Frontend (replicar EXACTAMENTE el patrón OPC-UA)

Todo lo de UI **solo aparece cuando `ModbusEnabled=TRUE` en Excel**. Patrón y archivos idénticos a OPC-UA (que ya está implementado):

### 1. Vista dedicada `ModbusView`
- Crear [my-3d-app/src/views/ModbusView.js](my-3d-app/src/views/ModbusView.js) **espejo de** [my-3d-app/src/views/OpcUaView.js](my-3d-app/src/views/OpcUaView.js). **Reutilizar EXACTAMENTE el mismo estilo de visualización** (mismos estilos/`styles`, tarjetas, badges, tabs, tablas y colores que OpcUaView). No inventar un look nuevo: copiar el patrón y solo cambiar datos/etiquetas.
- Ventanas/pestañas (mismas que OPC-UA):
  1. **Resumen (Estado + Configuración)**: una sola ventana con el estado del **Server** (running, puerto/IP, unitId, clientes conectados) y de las **fuentes Client** (host:port, conectado/timeout), más la **configuración** cargada de Excel (flags `System Config`, nº de variables/alarmas). Mismo formato de tarjetas de estado que la pestaña Status de OPC-UA.
  2. **Variables**: tabla con filtro de los registros mapeados (`AdsSymbol` ↔ `ModbusRegister`/`Function`, tipo, R/W, valor **live** vía SignalR), idéntica a la pestaña Variables de OPC-UA.
  3. **Alarmas**: tabla de alarmas mapeadas con valor/estado live, idéntica a la pestaña Alarms de OPC-UA.
  4. **Logs (SIEMPRE visibles abajo)**: **dos paneles inferiores** (L1 Audit + L2 Operations) idénticos a OPC-UA, con el mismo `renderSingleLogPanel` (colapsable, auto-scroll, refresco). Ver sección **Logs** más abajo — es **obligatorio**, no opcional.
- Usar `useAppTranslation()` (con `tLabel()`), `usePermissions()` y suscripción SignalR para los valores en vivo, igual que `OpcUaView`.
- Registrar en [my-3d-app/src/App.js](my-3d-app/src/App.js): `const ModbusView = lazy(() => import('./views/ModbusView'));`, entrada en `VIEW_LOADING_META` (`modbus: { icon:'📡', color:'#0066ff' }`), estado `const [modbusEnabled,setModbusEnabled]=useState(false)` cargado desde `api.getSystemConfiguration()` (`config.modbusEnabled`), y el render `currentView === 'modbus' && <ModbusView .../>`.
- Menú lateral [my-3d-app/src/components/EpicSideMenu.js](my-3d-app/src/components/EpicSideMenu.js): item `{ id:'modbus', labelId:'menu.modbus', color:'#0066ff', visible: modbusEnabled && canView('ModbusView') }`.

### 2. Fila en “Servicios externos” del Info Panel
- En [my-3d-app/src/components/InfoPanel.js](my-3d-app/src/components/InfoPanel.js): añadir a `EXTERNAL_SERVICES` `{ id:'modbusServer', label:'Modbus TCP', icon:'📡', iconColor:'#0066ff', protocol:'Modbus' }`.
- Filtrar para que **solo se muestre si habilitado**: `if (service.id==='modbusServer') return systemMetrics?.servicesStatus?.modbusEnabled === true;` (igual que `opcUaServer`).
- Determinación de estado espejo de OPC-UA: `disabled` si `!modbusEnabled`, `error`/`N/A` si SignalR caído, `connected` con métrica (clientes/fuentes) si `modbusRunning`, `Stopped` si no.

### 3. Permisos por rol (Gestión de usuarios) — clave única `ModbusView`
La misma string `ModbusView` (PascalCase ↔ camelCase automático en JSON) se registra en **5 sitios** para que un admin pueda conceder/denegar la vista por rol:
1. **Backend modelo**: en `ModulePermissions` de [Models/RolePermissions.cs](Models/RolePermissions.cs) añadir `public ViewPermission ModbusView { get; set; } = new();` (junto a `OpcUaView`).
2. **Backend defaults**: añadir `ModbusView = ...;` en las **6** factories `GetSuperAdminPermissions/GetAdministrator.../GetOperator.../GetMaintenance.../GetViewer.../GetAuditor...` del `DefaultRolePermissions`. Sugerido: Super/Admin `AllPermissions()`, Maintenance `CanView+CanEdit`, Viewer/Auditor `ReadOnlyPermission()`, Operator `NoPermission()` (ajustar a criterio del usuario).
3. **Frontend tabla de permisos**: entrada en `modulesData` de [my-3d-app/src/components/RolePermissionsConfig.js](my-3d-app/src/components/RolePermissionsConfig.js): `{ key:'ModbusView', name:t('rolePermissions.modules.modbusView',{}, '📡 Modbus TCP'), category:t('rolePermissions.categories.sideMenu'), hasEdit:true }`.
4. **Gateo de menú/vista**: ya cubierto por `canView('ModbusView')` en `EpicSideMenu.js` (punto 1).
5. **Traducciones**: claves `menu.modbus`, `rolePermissions.modules.modbusView`, `modbus.title`, etc. (en dev usar fallback ES).

> Comportamiento por defecto: SuperAdmin siempre ve; durante carga de permisos `canView` devuelve `true`; tras cargar, si la clave no está ⇒ `false` (oculto). Por eso hay que sembrar los defaults en las 6 factories.

## Logs (replicar el patrón de 2 niveles de OPC-UA) — TODO el intercambio logueado

**Regla**: todo lo relevante de Modbus se loguea, y especialmente **todo el intercambio de datos** (escrituras entrantes, cambios de valor, cambios de alarma, lecturas/escrituras a fuentes Client). Reusar el sistema de **2 niveles** existente, con sus **mismas categorías, niveles/severidad y persistencia** — NO crear un sistema de logs nuevo.

### Nivel L1 — Audit (seguridad / ciclo de vida)
- Servicio: [Services/AuditLogService.cs](Services/AuditLogService.cs) (JSON firmado SHA256 en `Projects/{id}/audit/`). Categoría **`AuditCategory.OtCommunication`** (la misma de OPC-UA). Resultado `AuditResult` (`Success`/`Warning`/`Failure`/`Error`).
- Añadir acciones nuevas en el enum `AuditAction` de [Models/AuditLogModels.cs](Models/AuditLogModels.cs) siguiendo el naming de OPC-UA:
  - `ModbusServerStart` / `ModbusServerStop`
  - `ModbusClientConnect` / `ModbusClientDisconnect` (terceros que se conectan al Server)
  - `ModbusSourceConnect` / `ModbusSourceDisconnect` (fuentes externas del rol Client)
  - `ModbusSecurityReject` / `ModbusConfigWarning`
- Llamadas desde `ModbusService` con el mismo patrón que `OpcUaServerService` (`_auditLogService.LogAsync(AuditCategory.OtCommunication, AuditAction.ModbusServerStart, AuditResult.Success, "...", userName:"System")`).

### Nivel L2 — Operation (intercambio de datos)
- Servicio: [Services/OperationLogService.cs](Services/OperationLogService.cs) (SQLite tabla `OperationLogs`). Añadir categoría **`OperationCategory.Modbus`** (espejo de `OperationCategory.OpcUa`) y acciones en `OperationAction` de [Models/OperationLogModels.cs](Models/OperationLogModels.cs):
  - `ModbusRegisterWrite` (tercero → registro → ADS, rol Server)
  - `ModbusValueChange` (ADS → registro, rol Server)
  - `ModbusAlarmChange` (cambio de estado de alarma)
  - `ModbusSourceRead` / `ModbusSourceWrite` (rol Client hacia fuentes externas)
- Detalle con el mismo formato OPC-UA: `"{Name}: {prev} → {value}"`, `user:"PLC"` o el cliente/fuente. Respetar `ExcludeFromLog=TRUE` de `Modbus_Variables` para no loguear vars de alta frecuencia (igual que OPC-UA).
- **Toda escritura entrante** (tercero→ADS) va a L2 **y además** debe auditarse (trazabilidad CRA, ver "Detalles que suelen escaparse" #1).

### Frontend — paneles inferiores en `ModbusView` (idénticos a OPC-UA)
- Reusar `renderSingleLogPanel(...)` de [my-3d-app/src/views/OpcUaView.js](my-3d-app/src/views/OpcUaView.js): dos paneles colapsables **siempre abajo**, auto-scroll (`logsEndRef`), con sus mismos `styles` (`logPanel`, `logPanelBody`, `logEntry(level)`…) y color por nivel.
- Datos:
  - **L1**: `GET /api/audit/logs/category/OtCommunication?take=50` filtrando por una lista `MODBUS_LEVEL1_ACTIONS` (las acciones `Modbus*` de arriba), igual que OPC-UA usa `LEVEL1_ACTIONS`.
  - **L2**: `GET /api/operationlogs?category=Modbus&pageSize=50&fromDate=...`.
- Refresco como OPC-UA: estado ~5 s, logs ~10 s.
- i18n: claves `modbus.log.action.{accion}` y `modbus.log.detail.{accion_lower}` (mismo convenio que `opcua.log.*`).

## Checklist de implementación

1. (Si se aprueba) añadir `FluentModbus` al `.csproj` — **pedir confirmación antes**.
2. `Models/ModbusModels.cs`: `ModbusVariable`, `ModbusAlarm`, `ModbusStatus`, `ModbusConfig`, enum `ModbusRegisterType`.
3. `Services/ExcelConfigService.cs`: parsear `Modbus_Variables`, `Modbus_Alarms` y claves `System Config` (vacío ⇒ no usar).
4. `Services/IModbusService` + `ModbusService` (server) + `DisabledModbusService` (stub) + (client).
5. `Program.cs`: lectura del flag + registro condicional (copiar bloque OPC-UA).
6. Mapeo registro↔ADS (Server) y polling de fuentes (Client) → `ScadaHub`.
7. **Alarmas**: inyectar `AlarmNotificationService` y leer `GetCurrentAlarmStates()` (caché push compartida); escribir coils/registros de `Modbus_Alarms`. **Prohibido** crear un 2º polling de alarmas o notificaciones ADS propias.
8. `Controllers/ModbusController.cs` (diagnóstico) + exponer `modbusEnabled`/`modbusRunning` en `/api/config/system` y `/api/config/metrics`.
9. **Frontend**: `views/ModbusView.js`, registro en `App.js`, item en `EpicSideMenu.js`, fila en `InfoPanel.js` (`EXTERNAL_SERVICES`), todo gateado por `modbusEnabled` (+ `canView('ModbusView')`).
10. **Permisos**: clave `ModbusView` en `Models/RolePermissions.cs` (modelo + 6 defaults) y en `RolePermissionsConfig.js`.
11. **Logs**: acciones `Modbus*` en `AuditAction` (L1) y `OperationAction` (L2) + categoría `OperationCategory.Modbus`; llamadas de log en `ModbusService` (intercambio completo); dos paneles inferiores en `ModbusView` (L1/L2) reusando `renderSingleLogPanel`.
12. Probar con `ModbusEnabled=FALSE` ⇒ el sistema arranca **idéntico** a hoy (driver inexistente, sin menú, sin fila en info panel, sin entrada de permisos visible).

## ⚠️ Detalles que suelen escaparse (cerrar antes/durante la implementación)

Cosas que el cliente normalmente no piensa pero que hay que resolver:

1. **Modbus TCP es texto plano y SIN autenticación/cifrado.** Cualquiera con acceso de red al puerto puede leer/escribir. Para CRA/seguridad:
   - `ModbusServerBindIp` debería apuntar a la interfaz/VLAN concreta del otro sistema, **no** `0.0.0.0` en producción salvo necesidad.
   - Documentar que la protección es **a nivel de red** (firewall/segmentación), no del protocolo.
   - Toda **escritura** entrante (tercero → register → ADS) debe registrarse en `AuditLogService` (trazabilidad CRA), igual que otras escrituras a PLC.
2. **Puerto 502 es privilegiado en Windows.** Puede requerir permisos/reserva (`netsh http`/firewall) o ejecutar como servicio. Permitir puerto alternativo (p.ej. `1502`) vía `ModbusServerPort`.
3. **Calidad/frescura del dato si ADS cae.** Decidir qué sirve el Modbus Server cuando `ITwinCATService` está desconectado: último valor conocido (stale), valor por defecto, o excepción Modbus. Recomendado: marcar `ServerRunning` pero exponer estado de conexión ADS en `GetStatus()` y en SignalR; no servir ceros silenciosos.
4. **No duplicar lecturas de ADS.** El rol Server debe **reusar la caché del `PlcPollingService`** (o su mismo ciclo) en vez de abrir lecturas ADS propias en paralelo — evita doblar carga sobre el PLC.
5. **Tipos multi-registro y solapes.** `INT32`/`FLOAT32` ocupan **2 registros** consecutivos; `STRING` varios. Validar al cargar Excel que **no se solapan** direcciones (dos variables sobre el mismo registro/words) y avisar con error claro.
6. **Word order / endianness por dispositivo.** Distintos PLC/dispositivos usan `ABCD`/`CDAB`/… Hacerlo **configurable por variable** (columna `WordOrder`) y, si conviene, un default por fuente en `System Config`.
7. **Escalado para enteros.** Si un `REAL` ADS se expone en un holding register de 16 bits, hay pérdida → usar `Scale`/`Offset` (p.ej. `bar*100 → INT16`) y documentarlo; o exponer como `FLOAT32` (2 registros).
8. **Límites por petición Modbus.** Lecturas: máx **125** holding/input registers y **2000** coils por request; escrituras FC16 máx **123** registros. En rol Client, **batch/segmentar** las lecturas de cada fuente respetando estos límites.
9. **Reconexión y timeouts (rol Client).** Igual que `TwinCAT.AutoReconnect`: reconexión automática, timeout configurable, backoff, y no tumbar el servicio si una fuente externa no responde (degradar esa fuente, seguir con la otra).
10. **Mapa de bits dentro de registros.** A veces el otro sistema espera varios BOOL empaquetados en un holding register (bit 0..15) en lugar de coils. Si surge, soportar `DataType=BIT` con `Address=40010.3` (registro.bit). Dejarlo previsto, implementar solo si se pide.
11. **Coherencia con modo simulado.** Cuando el PLC está en modo simulado (`ITwinCATService.IsSimulated`), el Modbus Server debe seguir funcionando sirviendo los valores simulados (útil para pruebas sin PLC real).
12. **Unicidad de `Name`/`AdsSymbol`.** Validar que no haya filas duplicadas en `Modbus_Variables` apuntando al mismo símbolo con registros distintos sin querer.

## NO hacer

- ❌ **NO romper lo que ya funciona en producción** (ver REGLA DE ORO). Con Modbus OFF/ausente/vacío, comportamiento idéntico a hoy. Solo añadir, nunca alterar caminos existentes.
- ❌ NO tocar `ITwinCATService`/ADS como fuente del PLC propio (Modbus Server lee de ahí, no la sustituye).
- ❌ NO Modbus RTU/serie (futuro).
- ❌ NO añadir el NuGet sin aprobación explícita (CRA/SBOM).
- ❌ NO crear una interfaz "común de protocolos": el repo no la tiene; mantener `IModbusService` independiente.
- ❌ NO dejar sockets/hilos vivos cuando `ModbusEnabled=FALSE`.
- ❌ NO inventar más de 2 fuentes Client salvo que el usuario lo pida.
