# 📦 Gestión de Modelos 3D y Archivos

## 🗂️ Estructura de Carpetas

**⚠️ IMPORTANTE**: Cada backend se instala en un PC industrial para **UN SOLO PROYECTO**.

```
SW.PC.API.Backend_/
├── wwwroot/
│   └── models/                 # Modelos 3D del proyecto actual
│       ├── machine_main.glb
│       ├── conveyor.glb
│       ├── robot_arm.glb
│       ├── tank.glb
│       └── valves.glb
│
└── ExcelConfigs/               # Configuración del proyecto
    └── ProjectConfig.xlsx      # UN SOLO archivo Excel por instalación
```

**Despliegue**:
- PC Industrial 1 → Backend Proyecto A → ProjectConfig.xlsx → Modelos del Proyecto A
- PC Industrial 2 → Backend Proyecto B → ProjectConfig.xlsx → Modelos del Proyecto B
- PC Industrial 3 → Backend Proyecto C → ProjectConfig.xlsx → Modelos del Proyecto C

## 📋 Configuración en Excel

### Hoja: `3D_Models`

Estructura de columnas para configurar modelos 3D:

| Columna | Nombre | Descripción | Ejemplo |
|---------|--------|-------------|---------|
| A | ModelId | ID único del modelo | `MODEL_001` |
| B | ModelName | Nombre descriptivo | `Máquina Principal` |
| C | FileName | Nombre del archivo 3D | `machine_main.glb` |
| D | FileType | Tipo de archivo | `glb`, `gltf`, `obj`, `stl`, `fbx` |
| E | Description | Descripción del modelo | `Modelo 3D de la máquina principal` |
| F | Category | Categoría del modelo | `Machine`, `Equipment`, `Part`, `Assembly` |
| G | AssociatedScreen | ID de pantalla HMI relacionada | `SCREEN_MAIN` |
| H | IsEnabled | Habilitado (TRUE/FALSE) | `TRUE` |
| I | DisplayOrder | Orden de visualización | `1`, `2`, `3...` |

### Ejemplo de Configuración:

```
| ModelId    | ModelName          | FileName         | FileType | Description                  | Category  | AssociatedScreen | IsEnabled | DisplayOrder |
|------------|--------------------|------------------|----------|------------------------------|-----------|------------------|-----------|--------------|
| MODEL_001  | Máquina Principal  | machine_main.glb | glb      | Modelo 3D máquina principal  | Machine   | SCREEN_MAIN      | TRUE      | 1            |
| MODEL_002  | Cinta Transportadora| conveyor.glb    | glb      | Cinta de transporte          | Equipment | SCREEN_CONV      | TRUE      | 2            |
| MODEL_003  | Brazo Robótico     | robot_arm.glb    | glb      | Brazo industrial 6 ejes      | Equipment | SCREEN_ROBOT     | TRUE      | 3            |
```

## 🎨 Tipos de Archivos 3D Soportados

| Formato | Extensión | Recomendado | Descripción |
|---------|-----------|-------------|-------------|
| **GLB** | `.glb` | ✅ **SÍ** | Binario, compacto, mejor para web |
| **GLTF** | `.gltf` | ✅ Sí | JSON, fácil de editar |
| OBJ | `.obj` | ⚠️ Limitado | Geometría simple sin materiales PBR |
| FBX | `.fbx` | ⚠️ Requiere conversión | Formato Autodesk |
| STL | `.stl` | ⚠️ Solo geometría | Sin colores ni materiales |

> **Recomendación**: Usar **GLB** para mejor rendimiento en el frontend React.

## 📍 Ubicación de Archivos

### Estructura Simple - Un Proyecto por Backend:
```
wwwroot/models/
  ├── machine_main.glb
  ├── conveyor.glb
  ├── robot_arm.glb
  ├── tank_storage.glb
  └── valve_assembly.glb
```

**No hay subcarpetas de proyectos** - Todos los modelos 3D van directamente en `wwwroot/models/`

## 🔗 URLs de Acceso

Los modelos 3D se sirven a través de endpoints HTTP:

```
GET /api/models                          # Listar todos los modelos del proyecto
GET /api/models/{id}                     # Obtener modelo específico
GET /api/models/{id}/file                # Descargar archivo 3D
GET /models/machine_main.glb             # Acceso directo (wwwroot)
GET /models/tank_storage.glb             # Acceso directo (wwwroot)
```

**Ejemplo**: Si el backend está en `http://192.168.1.100:5000`
```
http://192.168.1.100:5000/models/machine_main.glb
http://192.168.1.100:5000/models/conveyor.glb
```

## 📝 Modelo de Datos

### C# - Model3DConfig (Excel)

```csharp
public class Model3DConfig
{
    public string ModelId { get; set; }                      // "MODEL_001"
    public string ModelName { get; set; }                    // "Máquina Principal"
    public string FileName { get; set; }                     // "machine_main.glb"
    public string FileType { get; set; }                     // "glb"
    public string? Description { get; set; }
    public string? Category { get; set; }                    // "Machine"
    public string? AssociatedScreen { get; set; }            // "SCREEN_MAIN"
    public bool IsEnabled { get; set; }
    public int DisplayOrder { get; set; }
    public ViewConfiguration? InitialView { get; set; }      // Configuración de cámara
    public List<ModelVariableBinding> VariableBindings { get; set; }  // Vinculación con PLC
}
```

### Vinculación con Variables PLC

Los modelos 3D pueden vincularse con variables del PLC para animaciones en tiempo real:

```csharp
public class ModelVariableBinding
{
    public string VariableName { get; set; }      // "MAIN.nSpeed"
    public string ModelPart { get; set; }         // "Motor_Mesh"
    public string BindingType { get; set; }       // "Rotation", "Position", "Scale", "Color", "Visibility"
    public string? Axis { get; set; }             // "X", "Y", "Z"
    public double? MinValue { get; set; }         // 0
    public double? MaxValue { get; set; }         // 100
    public double? MinRange { get; set; }         // 0.0
    public double? MaxRange { get; set; }         // 360.0 (grados)
}
```

### Ejemplo de Vinculación PLC → Modelo 3D:

```
Variable PLC: MAIN.nMotorSpeed (0-1500 RPM)
↓
ModelPart: "Motor_Shaft"
BindingType: "Rotation"
Axis: "Y"
MinValue: 0, MaxValue: 1500
MinRange: 0°, MaxRange: 360°
↓
Resultado: El eje del motor gira según la velocidad del PLC
```

## 🚀 Uso en el Backend

### Cargar Configuración desde Excel:

```csharp
// Cargar el archivo de configuración del proyecto (UN SOLO archivo)
var config = await _excelConfigService.LoadProjectConfigurationAsync("ProjectConfig.xlsx");

Console.WriteLine($"Proyecto: {config.ProjectName}");
Console.WriteLine($"Cliente: {config.Customer}");

foreach (var model3D in config.Models3D)
{
    Console.WriteLine($"Modelo: {model3D.ModelName}");
    Console.WriteLine($"Archivo: {model3D.FileName}");
    Console.WriteLine($"URL: http://localhost:5000/models/{model3D.FileName}");
}
```

### API Controller Example:

```csharp
[HttpGet("models")]
public async Task<ActionResult<List<Model3DConfig>>> GetProjectModels()
{
    // Este backend maneja UN SOLO proyecto
    var config = await _excelConfigService.LoadProjectConfigurationAsync("ProjectConfig.xlsx");
    
    // Agregar URLs completas
    foreach (var model in config.Models3D)
    {
        model.Properties["Url"] = $"{Request.Scheme}://{Request.Host}/models/{model.FileName}";
    }
    
    return Ok(config.Models3D);
}
```

## 📤 Flujo de Trabajo

### Instalación en PC Industrial:

1. **Instalar Backend** en PC Industrial del cliente
2. **Diseño 3D**: Crear modelos en Blender, 3ds Max, etc.
3. **Exportar**: Exportar como `.glb` (recomendado)
4. **Ubicar**: Copiar archivos a `wwwroot/models/` del PC industrial
5. **Configurar**: Editar `ExcelConfigs/ProjectConfig.xlsx` - Hoja `3D_Models`
6. **Vincular (opcional)**: Configurar `ModelVariableBinding` para animaciones PLC
7. **Frontend**: HMI carga modelos desde el backend local

### Despliegue Multi-Sitio:

```
Cliente A - Fábrica Madrid
├── PC Industrial (192.168.1.100)
│   ├── Backend + Excel → Proyecto "Línea Envasado Madrid"
│   └── Modelos: envasadora.glb, transportador.glb
│
Cliente B - Fábrica Barcelona  
├── PC Industrial (192.168.1.100)
│   ├── Backend + Excel → Proyecto "Línea Embalaje Barcelona"
│   └── Modelos: robot_paletizador.glb, cinta_salida.glb
│
Cliente C - Fábrica Valencia
├── PC Industrial (192.168.1.100)
│   ├── Backend + Excel → Proyecto "Control Tanques Valencia"
│   └── Modelos: tanque_principal.glb, válvulas.glb
```

**Cada instalación es independiente** - No hay comunicación entre backends de diferentes sitios.

## 🔄 Sincronización con Frontend

El frontend React (HMI) se conecta al backend local y recibe:

```json
{
  "modelId": "MODEL_001",
  "modelName": "Máquina Principal",
  "fileName": "machine_main.glb",
  "fileType": "glb",
  "url": "http://192.168.1.100:5000/models/machine_main.glb",
  "variableBindings": [
    {
      "variableName": "MAIN.nMotorSpeed",
      "modelPart": "Motor_Shaft",
      "bindingType": "Rotation",
      "axis": "Y"
    }
  ]
}
```

**Red local**: El frontend HMI y el backend están en la misma red industrial (ej: 192.168.1.x)

## ⚙️ Configuración Avanzada

### ViewConfiguration (Vista Inicial de Cámara):

```csharp
model.InitialView = new ViewConfiguration
{
    CameraPosition = new Vector3 { X = 5.0, Y = 3.0, Z = 5.0 },
    CameraTarget = new Vector3 { X = 0.0, Y = 0.0, Z = 0.0 },
    CameraZoom = 1.0,
    AutoRotate = false
};
```

## 📊 Estado Actual de Implementación

| Componente | Estado | Notas |
|------------|--------|-------|
| ✅ Modelo de datos `Model3DConfig` | Completado | En `Models/ExcelModels.cs` |
| ✅ Estructura de carpetas | Creada | `wwwroot/models/projects/`, `ExcelConfigs/` |
| ⏳ Lectura desde Excel | Pendiente | Código preparado, comentado temporalmente |
| ⏳ API Endpoints | Pendiente | Ampliar `ModelsController.cs` |
| ⏳ Ejemplo Excel | Pendiente | Crear plantilla `.xlsx` |

## 📝 TODO: Próximos Pasos

1. **Descomentar código en `ExcelConfigService.cs`**:
   - Línea ~71: `config.Models3D = await LoadModels3DFromSheetAsync(package);`
   - Agregar método privado `LoadModels3DFromSheetAsync`

2. **Crear plantilla Excel** con hoja `3D_Models`

3. **Ampliar `ModelsController.cs`**:
   - Endpoint para obtener modelos por proyecto
   - Endpoint para servir archivos GLB
   - Endpoint para obtener configuración de vinculaciones PLC

4. **Documentar en frontend** cómo cargar modelos con Three.js

## 🎯 Ejemplo Completo

### Instalación en PC Industrial:

**Excel: `ExcelConfigs/ProjectConfig.xlsx`**

**Hoja: General**
```
Project Name:   Línea de Envasado - Planta Madrid
Project Code:   MADRID_ENV_001
Customer:       Bebidas Iberia S.A.
Created Date:   2025-11-08
```

**Hoja: 3D_Models**
```
| ModelId | ModelName     | FileName       | FileType | Description      | Category | AssociatedScreen | IsEnabled | DisplayOrder |
|---------|---------------|----------------|----------|------------------|----------|------------------|-----------|--------------|
| MDL001  | Envasadora    | envasadora.glb | glb      | Máquina envasado | Machine  | SCR_MAIN         | TRUE      | 1            |
| MDL002  | Transportador | conveyor.glb   | glb      | Cinta transporte | Equipment| SCR_CONV         | TRUE      | 2            |
| MDL003  | Tanque Buffer | tank_buffer.glb| glb      | Tanque intermedio| Equipment| SCR_TANK         | TRUE      | 3            |
```

**Archivos en servidor**:
```
wwwroot/models/
  ├── envasadora.glb
  ├── conveyor.glb
  └── tank_buffer.glb
```

**URL accesible desde HMI local**:
```
http://192.168.1.100:5000/models/envasadora.glb
http://192.168.1.100:5000/models/conveyor.glb
http://192.168.1.100:5000/models/tank_buffer.glb
```

### Otro cliente - Instalación independiente:

**PC Industrial diferente (Planta Barcelona)**
```
Excel: ProjectConfig.xlsx → "Línea Paletizado - Planta Barcelona"
Modelos: robot_paletizador.glb, cinta_salida.glb
URL: http://192.168.1.100:5000/models/robot_paletizador.glb
```

**Cada instalación es completamente independiente.**

---

**✅ Sistema preparado para gestión completa de modelos 3D desde Excel con vinculación a variables PLC en tiempo real.**
