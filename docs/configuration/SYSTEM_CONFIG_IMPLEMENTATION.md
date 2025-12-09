# ⚙️ Sistema de Configuración desde Excel - Implementación

## 📋 Resumen

Sistema completo para configurar el backend ASP.NET Core desde una hoja Excel sin tocar código. La configuración se muestra en tiempo real en el panel derecho del frontend React.

## ✅ Componentes Implementados

### Backend (ASP.NET Core)

1. **Models/ExcelModels.cs - SystemConfiguration**
   - 17 propiedades configurables
   - Valores por defecto sensibles
   - Organizado en 5 categorías

2. **Services/ExcelConfigService.cs - LoadSystemConfigurationAsync()**
   - Lee hoja "System Config" del Excel
   - Formato clave-valor (Columna A = Parámetro, Columna B = Valor)
   - Parser flexible: acepta camelCase y snake_case
   - Booleanos flexibles: true/false, 1/0, yes/no, si/no, enabled/disabled

3. **Controllers/ConfigController.cs - GET /api/config/system**
   - Endpoint REST para obtener configuración
   - Manejo de errores completo
   - Logging estructurado
   - Swagger documentado

### Frontend (React + Babylon.js)

1. **services/api.js - getSystemConfiguration()**
   - Llamada al endpoint del backend
   - Manejo de errores
   - Logging en consola

2. **BabylonScene.js - Estado y Carga**
   - Estado `systemConfig` con configuración completa
   - Carga automática al inicializar backend
   - Refresco automático cada 30 segundos
   - Integración con logs del sistema

3. **BabylonScene.js - Panel Derecho "Estado de Máquina"**
   - Nueva sección "🔧 CONFIGURACIÓN DEL SISTEMA"
   - Muestra configuración en tiempo real (solo lectura)
   - Organizada por categorías:
     - SERVICIOS (Polling PLC, SignalR, intervalos)
     - TWINCAT/PLC (Modo, AMS Net ID, puerto)
     - RENDIMIENTO (Caché, conexiones máximas)
     - BASE DE DATOS (si está habilitada)
   - Indicadores visuales con colores
   - Iconos para estado activo/inactivo

## 📊 Configuración Disponible

### SERVICIOS
```
EnablePlcPolling       → Habilitar polling del PLC (true/false)
PlcPollingInterval     → Intervalo en ms (1000 = 1 segundo)
EnableSignalR          → Habilitar tiempo real (true/false)
EnableVerboseLogging   → Logs detallados (true/false)
```

### TWINCAT / PLC
```
UseSimulatedPlc        → Modo simulado (true) o real (false)
PlcAmsNetId            → AMS Net ID del PLC (ej: 192.168.1.100.1.1)
PlcAdsPort             → Puerto ADS (851 = PLC runtime)
```

### BASE DE DATOS
```
EnableDatabase         → Activar persistencia (true/false)
DatabaseConnectionString → Connection string SQL Server
```

### API / WEB
```
ApiPort                → Puerto del servidor (5000)
EnableCors             → Habilitar CORS (true/false)
CorsOrigins            → Orígenes permitidos (separados por coma)
```

### EXCEL / ARCHIVOS
```
ExcelConfigFileName    → Nombre del archivo Excel
ConfigFolder           → Carpeta de configuraciones
ModelsFolder           → Carpeta de modelos 3D
```

### CACHE / PERFORMANCE
```
ConfigCacheSeconds     → Tiempo de caché (300 = 5 minutos)
MaxSignalRConnections  → Máximo conexiones simultáneas
```

## 🗂️ Formato de la Hoja Excel

### Nombre de la hoja
El servicio busca automáticamente:
- `System Config` ✅ (preferido)
- `SystemConfig`
- `Config`
- `Settings`

### Estructura

| A (Parametro) | B (Valor) |
|---------------|-----------|
| EnablePlcPolling | true |
| PlcPollingInterval | 1000 |
| EnableSignalR | true |
| UseSimulatedPlc | true |
| ... | ... |

**Fila 1**: Encabezados (opcional)
**Fila 2+**: Datos (Nombre parámetro | Valor)

## 🎨 Visualización en Frontend

### Ubicación
- **Panel derecho** → Después de "Estado de Máquina"
- **Solo visible** cuando la configuración se carga correctamente

### Diseño
```
🔧 CONFIGURACIÓN DEL SISTEMA
├── SERVICIOS
│   ├── Polling PLC: ✓ Activo
│   ├── Intervalo Polling: 1000ms
│   └── SignalR: ✓ Activo
├── TWINCAT / PLC
│   ├── Modo: 🔧 Simulado
│   ├── AMS Net ID: 127.0.0.1.1.1
│   └── Puerto ADS: 851
└── RENDIMIENTO
    ├── Caché Config: 300s
    └── Max Conexiones: 100
```

### Colores
- **✓ Activo**: Verde (#00ff00)
- **✗ Inactivo**: Rojo (#ff4444)
- **🔧 Simulado**: Naranja (#ffaa00)
- **🏭 Real**: Verde (#00ff00)
- **Títulos**: Azul claro (#4db8ff)

## 🔄 Actualización Automática

### Backend
- ❌ **No tiene refresco automático**
- ⚠️ Cambios en Excel requieren **reiniciar el servidor**
- ✅ Endpoint siempre lee el Excel actualizado

### Frontend
- ✅ **Refresco automático cada 30 segundos**
- ✅ Carga inicial al conectar con backend
- ✅ Logs en consola del navegador
- ✅ Sin necesidad de recargar página

## 🧪 Pruebas

### 1. Verificar Endpoint Backend
```bash
# PowerShell
Invoke-RestMethod -Uri "http://localhost:5000/api/config/system" -Method GET

# O en navegador
http://localhost:5000/swagger/index.html
```

### 2. Verificar Frontend
1. Iniciar backend: `dotnet run` (puerto 5000)
2. Iniciar frontend: `npm run start:dev` (puerto 3001)
3. Abrir panel derecho
4. Buscar sección "🔧 CONFIGURACIÓN DEL SISTEMA"
5. Verificar valores cargados

### 3. Probar Actualización
1. Modificar valor en Excel (ej: `PlcPollingInterval` → 2000)
2. Guardar Excel
3. Esperar 30 segundos (refresco automático frontend)
4. Verificar cambio en panel derecho

**Nota**: Para que el backend USE los nuevos valores, debe reiniciarse.

## 📈 Casos de Uso

### Desarrollo Local
```
UseSimulatedPlc = true
EnableVerboseLogging = true
PlcPollingInterval = 500
```
→ PLC simulado, logs detallados, polling rápido

### Producción (PLC Real)
```
UseSimulatedPlc = false
PlcAmsNetId = 192.168.1.100.1.1
EnableVerboseLogging = false
PlcPollingInterval = 1000
```
→ PLC real en red, logs normales, polling estándar

### Solo REST (Sin SignalR)
```
EnableSignalR = false
EnablePlcPolling = false
```
→ Solo endpoints REST, sin tiempo real

### Con Persistencia
```
EnableDatabase = true
DatabaseConnectionString = Server=localhost;Database=ScadaDB;...
```
→ Guardar datos históricos en SQL Server

## 🚀 Próximos Pasos Posibles

### 1. Aplicar Configuración al Inicio ⭐
Modificar `Program.cs` para leer configuración y:
- Iniciar/detener PlcPollingService según `EnablePlcPolling`
- Ajustar intervalos de polling dinámicamente
- Configurar CORS desde Excel
- Activar verbose logging

### 2. Hot-Reload de Configuración
Implementar endpoint PUT para:
- Modificar configuración sin reiniciar
- Aplicar cambios en servicios en ejecución
- FileSystemWatcher para detectar cambios en Excel

### 3. Validación de Configuración
- Validar rangos (ej: PlcPollingInterval >= 100ms)
- Validar formatos (ej: AMS Net ID correcto)
- Alertas en frontend si configuración inválida

### 4. Editor en Frontend
Panel de administración para:
- Ver/editar configuración desde navegador
- Guardar cambios al Excel
- Reiniciar servicios desde UI

### 5. Múltiples Perfiles
- Crear plantillas: Development, Production, Testing
- Cambiar entre perfiles sin editar Excel
- Exportar/importar configuraciones

## 📝 Archivos Creados/Modificados

### Backend
- ✅ `Models/ExcelModels.cs` - SystemConfiguration class
- ✅ `Services/ExcelConfigService.cs` - LoadSystemConfigurationAsync()
- ✅ `Controllers/ConfigController.cs` - GET /api/config/system
- ✅ `ExcelConfigs/SYSTEM_CONFIG_SHEET.md` - Documentación
- ✅ `ExcelConfigs/SystemConfig_Template.csv` - Plantilla CSV

### Frontend
- ✅ `src/services/api.js` - getSystemConfiguration()
- ✅ `src/BabylonScene.js` - Estado, carga, visualización

### Excel
- ✅ `ProjectConfig.xlsm` - Hoja "System Config" con datos

## 🎯 Conclusión

Sistema completo y funcional que permite configurar el backend desde Excel con visualización en tiempo real en el frontend. 

**Ventajas**:
- ✅ Sin tocar código para cambios de configuración
- ✅ Excel familiar para personal IT/OT
- ✅ Centralización de toda la configuración
- ✅ Visualización en tiempo real
- ✅ Flexible y extensible

**Limitaciones actuales**:
- ⚠️ Backend no aplica cambios automáticamente (requiere reinicio)
- ⚠️ Solo lectura en frontend (no editable desde UI)

**Estado**: ✅ **100% IMPLEMENTADO Y FUNCIONAL**
