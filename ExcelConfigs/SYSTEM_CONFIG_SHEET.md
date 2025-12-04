# 📋 Hoja "System Config" - Configuración del Sistema Backend

## 🎯 Descripción

La hoja **"System Config"** en `ProjectConfig.xlsm` permite configurar completamente el comportamiento del backend sin tocar código. Todos los parámetros se cargan al iniciar el servidor.

## 📐 Formato de la Hoja

### Estructura

| Columna A (Parámetro) | Columna B (Valor) |
|-----------------------|-------------------|
| EnablePlcPolling      | true              |
| PlcPollingInterval    | 1000              |
| EnableSignalR         | true              |
| ...                   | ...               |

- **Fila 1**: Encabezados (ej: "Parámetro" | "Valor")
- **Fila 2+**: Datos (Nombre del parámetro | Valor)

### Nombres Alternativos de la Hoja

El servicio busca automáticamente estos nombres:
- `System Config` (preferido)
- `SystemConfig`
- `Config`
- `Settings`

## ⚙️ Parámetros Disponibles

### 🔌 SERVICIOS

| Parámetro | Tipo | Default | Descripción |
|-----------|------|---------|-------------|
| **EnablePlcPolling** | bool | `true` | Habilitar polling automático del PLC |
| **PlcPollingInterval** | int | `1000` | Intervalo de polling en milisegundos |
| **EnableSignalR** | bool | `true` | Habilitar comunicación en tiempo real SignalR |
| **EnableVerboseLogging** | bool | `false` | Activar logs detallados (desarrollo) |

### 🏭 TWINCAT / PLC

| Parámetro | Tipo | Default | Descripción |
|-----------|------|---------|-------------|
| **UseSimulatedPlc** | bool | `true` | Usar PLC simulado (true) o real (false) |
| **PlcAmsNetId** | string | `"127.0.0.1.1.1"` | AMS Net ID del PLC TwinCAT |
| **PlcAdsPort** | int | `851` | Puerto ADS del PLC (851 para PLC runtime) |

### 💾 BASE DE DATOS

| Parámetro | Tipo | Default | Descripción |
|-----------|------|---------|-------------|
| **EnableDatabase** | bool | `false` | Habilitar persistencia en base de datos |
| **DatabaseConnectionString** | string | `null` | Connection string de SQL Server |

### 🌐 API / WEB

| Parámetro | Tipo | Default | Descripción |
|-----------|------|---------|-------------|
| **ApiPort** | int | `5000` | Puerto del servidor API |
| **EnableCors** | bool | `true` | Habilitar CORS para frontend |
| **CorsOrigins** | string | `"http://localhost:3000,..."` | Orígenes permitidos (separados por coma) |

### 📂 EXCEL / ARCHIVOS

| Parámetro | Tipo | Default | Descripción |
|-----------|------|---------|-------------|
| **ExcelConfigFileName** | string | `"ProjectConfig.xlsm"` | Nombre del archivo Excel principal |
| **ConfigFolder** | string | `"ExcelConfigs"` | Carpeta de configuraciones |
| **ModelsFolder** | string | `"wwwroot/models"` | Carpeta de modelos 3D (GLB/GLTF) |

### ⚡ CACHE / PERFORMANCE

| Parámetro | Tipo | Default | Descripción |
|-----------|------|---------|-------------|
| **ConfigCacheSeconds** | int | `300` | Tiempo de caché de configuraciones (5 min) |
| **MaxSignalRConnections** | int | `100` | Máximo de conexiones SignalR simultáneas |

## 📝 Ejemplo Completo

```
Parámetro                     | Valor
------------------------------|------------------------------------------
EnablePlcPolling              | true
PlcPollingInterval            | 1000
EnableSignalR                 | true
EnableVerboseLogging          | false
UseSimulatedPlc               | true
PlcAmsNetId                   | 127.0.0.1.1.1
PlcAdsPort                    | 851
EnableDatabase                | false
DatabaseConnectionString      |
ApiPort                       | 5000
EnableCors                    | true
CorsOrigins                   | http://localhost:3000,http://localhost:3001
ExcelConfigFileName           | ProjectConfig.xlsm
ConfigFolder                  | ExcelConfigs
ModelsFolder                  | wwwroot/models
ConfigCacheSeconds            | 300
MaxSignalRConnections         | 100
```

## 🔤 Valores Booleanos Aceptados

El parser es flexible con valores booleanos:

### TRUE
- `true`, `True`, `TRUE`
- `1`
- `yes`, `Yes`, `YES`
- `si`, `Si`, `sí`, `Sí`, `SI`, `SÍ`
- `enabled`, `Enabled`, `ENABLED`

### FALSE
- `false`, `False`, `FALSE`
- `0`
- `no`, `No`, `NO`
- `disabled`, `Disabled`, `DISABLED`

## 🔧 Nombres de Parámetros Flexibles

El sistema acepta variaciones de nombres (case-insensitive):

| Parámetros Equivalentes |
|-------------------------|
| `EnablePlcPolling` ≡ `enable_plc_polling` |
| `PlcPollingInterval` ≡ `plc_polling_interval` |
| `UseSimulatedPlc` ≡ `use_simulated_plc` |
| etc. |

## 🚀 Uso del Endpoint

### GET `/api/config/system`

Obtiene la configuración actual del sistema desde Excel.

```http
GET http://localhost:5000/api/config/system?fileName=ProjectConfig.xlsm
```

**Respuesta (200 OK):**
```json
{
  "enablePlcPolling": true,
  "plcPollingInterval": 1000,
  "enableSignalR": true,
  "enableVerboseLogging": false,
  "useSimulatedPlc": true,
  "plcAmsNetId": "127.0.0.1.1.1",
  "plcAdsPort": 851,
  "enableDatabase": false,
  "databaseConnectionString": null,
  "apiPort": 5000,
  "enableCors": true,
  "corsOrigins": "http://localhost:3000,http://localhost:3001",
  "excelConfigFileName": "ProjectConfig.xlsm",
  "configFolder": "ExcelConfigs",
  "modelsFolder": "wwwroot/models",
  "configCacheSeconds": 300,
  "maxSignalRConnections": 100
}
```

**Errores:**
- `404` - Excel file not found / System Config sheet not found
- `500` - Error reading Excel file

## 🎯 Casos de Uso

### Desarrollo Local
```
UseSimulatedPlc = true
EnableVerboseLogging = true
PlcPollingInterval = 500
```

### Producción con PLC Real
```
UseSimulatedPlc = false
PlcAmsNetId = 192.168.1.100.1.1
PlcAdsPort = 851
EnableVerboseLogging = false
PlcPollingInterval = 1000
```

### Sin SignalR (Solo REST API)
```
EnableSignalR = false
EnablePlcPolling = false
```

### Con Base de Datos
```
EnableDatabase = true
DatabaseConnectionString = Server=localhost;Database=ScadaDB;...
```

## 📊 Integración con Swagger

El endpoint está documentado en Swagger UI:

```
http://localhost:5000/swagger/index.html
```

Busca: **GET /api/config/system**

## ⚠️ Notas Importantes

1. **Reinicio requerido**: Cambios en la hoja Excel requieren reiniciar el backend
2. **Valores vacíos**: Si una celda está vacía, se usa el valor por defecto
3. **Parámetros desconocidos**: Se ignoran sin error (permite futuras expansiones)
4. **Case-insensitive**: Los nombres de parámetros no distinguen mayúsculas/minúsculas
5. **Formato flexible**: Acepta snake_case y camelCase indistintamente

## 🔍 Logs de Carga

Al iniciar el servidor con la configuración del sistema, verás:

```
📊 Loading system configuration from Excel: ProjectConfig.xlsm
   ✅ Found parameter: EnablePlcPolling = true
   ✅ Found parameter: PlcPollingInterval = 1000
   ...
✅ Returning system configuration
```

## 🛠️ Extensión Futura

Para agregar nuevos parámetros:

1. Agregar propiedad en `SystemConfiguration` (Models/ExcelModels.cs)
2. Agregar case en `LoadSystemConfigurationAsync()` (Services/ExcelConfigService.cs)
3. Agregar fila en la hoja Excel "System Config"
4. Documentar aquí

**¡No se requiere reiniciar el servidor para leer los valores actualizados desde el endpoint!** (Solo para aplicar los cambios al comportamiento del sistema)

## 🔐 Parámetros de Seguridad (EU CRA Compliance)

### Git Repositories

| Parámetro | Tipo | Default | Descripción |
|-----------|------|---------|-------------|
| **GitRepoBackend** | string | `""` | Ruta al repositorio Git del Backend |
| **GitRepoFrontend** | string | `""` | Ruta al repositorio Git del Frontend |
| **GitRepoTwinCatPlc** | string | `""` | Ruta al repositorio Git del TwinCAT PLC |

### Modo de Entorno

| Parámetro | Tipo | Default | Descripción |
|-----------|------|---------|-------------|
| **EnvironmentMode** | string | `"development"` | Modo de entorno del sistema |

**Valores posibles:**
- `development` - Todos los repositorios son editables desde Git Panel
- `production` - Solo TwinCAT es editable (Backend/Frontend bloqueados con 🔒)

> ⚠️ **IMPORTANTE**: En instalaciones industriales, configurar `EnvironmentMode = production` para cumplir con EU CRA. Solo el código PLC (TwinCAT) debe ser modificable en campo.
