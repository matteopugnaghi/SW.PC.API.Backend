# 🏭 Arquitectura de Despliegue - Un Backend por Proyecto

## 🎯 Concepto Principal

**Cada instalación del backend en un PC industrial gestiona UN SOLO PROYECTO**

```
┌─────────────────────────────────────────────────────────────┐
│  CLIENTE A - Fábrica Madrid                                 │
│  ┌──────────────────────────────────────────────────────┐   │
│  │  PC Industrial (192.168.1.100)                       │   │
│  │  ├── SW.PC.API.Backend                               │   │
│  │  │   ├── ExcelConfigs/                               │   │
│  │  │   │   └── ProjectConfig.xlsx                      │   │
│  │  │   │       → "Línea Envasado Madrid"               │   │
│  │  │   ├── wwwroot/models/                             │   │
│  │  │   │   ├── envasadora.glb                          │   │
│  │  │   │   ├── conveyor.glb                            │   │
│  │  │   │   └── tank_buffer.glb                         │   │
│  │  │   └── TwinCAT PLC (Local)                         │   │
│  │  └── Frontend HMI (React) → Conecta a localhost:5000 │   │
│  └──────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│  CLIENTE B - Fábrica Barcelona                              │
│  ┌──────────────────────────────────────────────────────┐   │
│  │  PC Industrial (192.168.1.100) - Red diferente      │   │
│  │  ├── SW.PC.API.Backend                               │   │
│  │  │   ├── ExcelConfigs/                               │   │
│  │  │   │   └── ProjectConfig.xlsx                      │   │
│  │  │   │       → "Paletizado Barcelona"                │   │
│  │  │   ├── wwwroot/models/                             │   │
│  │  │   │   ├── robot_paletizador.glb                   │   │
│  │  │   │   └── cinta_salida.glb                        │   │
│  │  │   └── TwinCAT PLC (Local)                         │   │
│  │  └── Frontend HMI (React) → Conecta a localhost:5000 │   │
│  └──────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────┐
│  CLIENTE C - Fábrica Valencia                               │
│  ┌──────────────────────────────────────────────────────┐   │
│  │  PC Industrial (192.168.1.100) - Red diferente      │   │
│  │  ├── SW.PC.API.Backend                               │   │
│  │  │   ├── ExcelConfigs/                               │   │
│  │  │   │   └── ProjectConfig.xlsx                      │   │
│  │  │   │       → "Control Tanques Valencia"            │   │
│  │  │   ├── wwwroot/models/                             │   │
│  │  │   │   ├── tanque_principal.glb                    │   │
│  │  │   │   └── valvulas.glb                            │   │
│  │  │   └── TwinCAT PLC (Local)                         │   │
│  │  └── Frontend HMI (React) → Conecta a localhost:5000 │   │
│  └──────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
```

## ✅ Ventajas de esta Arquitectura

### 1. **Aislamiento Total**
- Cada sitio es completamente independiente
- No hay dependencias entre proyectos
- Fallos en un sitio no afectan a otros

### 2. **Simplicidad**
- Un archivo Excel por instalación: `ProjectConfig.xlsx`
- Modelos en raíz: `wwwroot/models/*.glb`
- No necesitas gestionar múltiples proyectos

### 3. **Seguridad**
- Backend y PLC en red industrial local (aislada)
- No hay comunicación entre sitios
- Datos sensibles permanecen locales

### 4. **Rendimiento**
- Backend y HMI en misma red → Mínima latencia
- PLC en red local → Tiempo real garantizado
- No depende de conexión a internet

### 5. **Mantenimiento**
- Configuración específica por sitio
- Actualizaciones independientes
- Personalización por cliente sin afectar otros

## 📁 Estructura de Archivos

```
SW.PC.API.Backend_/
├── ExcelConfigs/
│   └── ProjectConfig.xlsx          ← UN SOLO archivo por instalación
│
├── wwwroot/
│   └── models/                     ← Modelos del proyecto actual
│       ├── machine_main.glb
│       ├── conveyor.glb
│       ├── robot_arm.glb
│       └── tank_storage.glb
│
├── Models/
│   ├── ExcelModels.cs              ← Model3DConfig, ViewConfiguration, etc.
│   ├── DatabaseModels.cs           ← Alarmas, Recetas, Estadísticas
│   └── TwinCATModels.cs            ← Variables PLC
│
├── Services/
│   ├── ExcelConfigService.cs       ← Lee ProjectConfig.xlsx
│   ├── TwinCATService.cs           ← Comunicación PLC local
│   └── ...
│
└── Controllers/
    ├── ModelsController.cs         ← API para modelos 3D
    └── ...
```

## 🔄 Flujo de Datos

```
1. CONFIGURACIÓN (Una vez)
   Excel (ProjectConfig.xlsx) → Backend lee configuración al iniciar
   
2. TIEMPO REAL (Continuo)
   PLC ←→ Backend (TwinCAT) ←→ SignalR ←→ Frontend HMI
   
3. VISUALIZACIÓN 3D
   Frontend solicita lista de modelos → Backend responde con URLs
   Frontend carga GLB desde Backend → Renderiza en Three.js
   
4. ANIMACIÓN (Opcional)
   PLC actualiza variable → SignalR notifica Frontend
   Frontend anima parte del modelo 3D según valor PLC
```

## 🌐 Red Industrial Típica

```
┌─────────────────────────────────────────────────────┐
│  Red Industrial Local (192.168.1.x)                 │
│                                                      │
│  ┌──────────────┐    ┌──────────────┐              │
│  │ PLC TwinCAT  │◄──►│  PC Backend  │              │
│  │ 192.168.1.10 │    │ 192.168.1.100│              │
│  └──────────────┘    └──────┬───────┘              │
│                              │                       │
│                              ▼                       │
│                      ┌──────────────┐               │
│                      │ HMI Frontend │               │
│                      │ (localhost)  │               │
│                      └──────────────┘               │
│                                                      │
│  Opcional: Panel Táctil / PC Operador              │
│  ┌──────────────┐                                   │
│  │ HMI Cliente  │──►Backend (192.168.1.100:5000)   │
│  │ 192.168.1.50 │                                   │
│  └──────────────┘                                   │
└─────────────────────────────────────────────────────┘

❌ NO HAY conexión a internet
❌ NO HAY comunicación entre sitios diferentes
✅ TODO es local y en tiempo real
```

## 📦 Despliegue en Cliente

### Paso 1: Preparación
```bash
# Compilar backend
dotnet publish -c Release -o ./publish

# Copiar a PC industrial
# ./publish/ → C:\SCADA\Backend\
```

### Paso 2: Configuración
```
1. Crear ProjectConfig.xlsx en ExcelConfigs/
   - Configurar proyecto único
   - Definir variables PLC
   - Definir modelos 3D

2. Copiar archivos GLB a wwwroot/models/
   - envasadora.glb
   - conveyor.glb
   - etc.

3. Configurar appsettings.json
   - IP del PLC TwinCAT
   - Puerto ADS (normalmente 851)
   - Cadena conexión SQL Server (base de datos local)
```

### Paso 3: Instalación como Servicio Windows
```powershell
# Crear servicio Windows para que arranque automáticamente
sc.exe create "SCADA_Backend" binPath="C:\SCADA\Backend\SW.PC.API.Backend.exe" start=auto
sc.exe start "SCADA_Backend"
```

### Paso 4: Frontend
```
1. Compilar React app
   npm run build

2. Copiar build/ a servidor web (IIS, nginx, o servir desde Backend)

3. Configurar conexión a backend local:
   API_URL=http://localhost:5000
   SIGNALR_URL=http://localhost:5000/hubs/scada
```

## 🔧 Mantenimiento

### Actualizar Configuración
```
1. Editar ProjectConfig.xlsx
2. Reiniciar servicio backend
   sc.exe stop "SCADA_Backend"
   sc.exe start "SCADA_Backend"
```

### Agregar Nuevo Modelo 3D
```
1. Copiar archivo.glb a wwwroot/models/
2. Agregar entrada en Excel hoja 3D_Models
3. Reiniciar servicio
```

### Actualizar Backend
```
1. Compilar nueva versión
2. Detener servicio
3. Reemplazar archivos en C:\SCADA\Backend\
4. Iniciar servicio
```

## 📊 Comparación con Arquitectura Multi-Proyecto

| Aspecto | Un Backend por Proyecto ✅ | Multi-Proyecto ❌ |
|---------|---------------------------|-------------------|
| Complejidad | Baja - Un Excel, modelos en raíz | Alta - Múltiples Excels, subcarpetas |
| Aislamiento | Total - Sitios independientes | Parcial - Riesgo de conflictos |
| Rendimiento | Óptimo - Todo local | Variable - Depende de red |
| Mantenimiento | Simple - Un cliente a la vez | Complejo - Cambios afectan varios |
| Seguridad | Alta - Red industrial cerrada | Media - Requiere gestión central |
| Escalabilidad | Horizontal - Más sitios = Más backends | Vertical - Un backend grande |
| Coste | Bajo - Solo PC industrial | Alto - Infraestructura centralizada |

## ✅ Checklist de Despliegue

- [ ] Backend compilado y copiado a PC industrial
- [ ] ProjectConfig.xlsx creado con configuración del sitio
- [ ] Archivos GLB copiados a wwwroot/models/
- [ ] appsettings.json configurado (IP PLC, base de datos)
- [ ] SQL Server instalado localmente
- [ ] TwinCAT runtime instalado y configurado
- [ ] Servicio Windows creado para backend
- [ ] Frontend compilado y desplegado
- [ ] Pruebas de conectividad PLC ↔ Backend
- [ ] Pruebas de visualización HMI ↔ Backend
- [ ] Pruebas de modelos 3D (carga y animación)
- [ ] Documentación entregada al cliente

## 📊 Estados de Servicios Externos (BACKEND EXTERNAL SERVICES)

El panel InfoPanel muestra el estado de cada servicio externo con el siguiente modelo:

| Icono | Estado | Significado |
|-------|--------|-------------|
| 🟢 | **CONNECTED** | Servicio configurado en Excel, habilitado (`*_Enabled=true`), y funcionando correctamente |
| 🔴 | **ERROR** | Servicio configurado y habilitado, pero hay fallo de conexión o comunicación |
| ⚫ | **DISABLED** | Servicio configurado en Excel pero deshabilitado por el usuario (`*_Enabled=false`) |
| 🟡 | **SIMULATED** | Modo simulación activo (solo desarrollo) |
| ⚪ | **N/A** | Servicio no configurado en Excel |

### Servicios Monitorizados

| Servicio | Parámetro Excel | Descripción |
|----------|-----------------|-------------|
| TwinCAT Runtime | `PlcPollingEnabled` | Comunicación con PLC Beckhoff |
| Database | `DatabaseEnabled` | Base de datos SQLite local |
| CVE Scanner | `VulnScan_Enabled` | Escaneo de vulnerabilidades (OSV API) |
| Vuln Reporter | `VulnReport_Enabled` | Reporte a SOC/SIEM/ENISA |
| Remote Repos | Detección automática | Estado de red para verificación Git |

### Lógica de Estados

```
SI servicio NO configurado en Excel → ⚪ N/A
SI servicio configurado PERO Enabled=false → ⚫ DISABLED  
SI servicio Enabled=true Y conecta OK → 🟢 CONNECTED
SI servicio Enabled=true Y falla conexión → 🔴 ERROR
```

Esta lógica permite al operador distinguir claramente entre:
- Un servicio que **no está activo porque el administrador lo deshabilitó** (⚫)
- Un servicio que **debería estar activo pero tiene problemas** (🔴)

---

**🎯 Resultado**: Sistema SCADA completamente funcional, aislado, en tiempo real, personalizado para cada sitio industrial.
