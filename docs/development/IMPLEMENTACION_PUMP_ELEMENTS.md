# 🎯 Implementación Completa - Sistema de Configuración Excel para Elementos 3D

## 📋 Resumen

Se ha implementado un sistema completo para leer y procesar la configuración de elementos 3D (bombas) desde el archivo Excel `ProjectConfig.xlsm` con soporte para **30 columnas base + parámetros adicionales web**.

---

## 🏗️ Archivos Creados

### 1. **Models/PumpElement3D.cs** (Nuevo modelo completo)

Modelo C# que mapea las 30 columnas del Excel más parámetros adicionales para web:

#### Grupos de propiedades:

**A-C: Identificación**
- `TotalElements` (int?) - Total de elementos (solo fila 2)
- `Name` (string) - Nombre/descripción
- `FileName` (string) - Ruta archivo 3D

**D-F: Posición 3D**
- `OffsetX`, `OffsetY`, `OffsetZ` (double) - Desplazamiento del modelo

**G-I: Variables PLC TwinCAT**
- `PlcMainPageReference` - Estado en página principal
- `PlcManualPageReference` - Página de manuales
- `PlcConfigPageReference` - Página de configuración

**J-M: Colores según Estado**
- `ColorElementOn` - Color cuando PLC = 2 (encendido)
- `ColorElementOff` - Color cuando PLC = 1 (apagado)
- `ColorElementDisabled` - Color cuando PLC = 0 (deshabilitado)
- `ColorElementAlarm` - Color cuando PLC = 3 (alarma)

**N-U: Label/Etiqueta 3D**
- `ElementNameDescription` - Texto del label
- `LabelFontSize` - Tamaño de fuente
- `LabelOffsetX_Pos1/Y/Z` - Posición 1
- `LabelOffsetX_Pos2/Y/Z` - Posición 2

**V: Jerarquía Padre-Hijo**
- `OffspringsCount` - Número de hijos
- `Children` - Lista de elementos hijos

**W-Z (Col 23-26): Metadatos**
- `IconFileReference` - Imagen .jpg/.png para UI 2D
- `IconLanguageLabelRow` - Línea en MSG.ENG/ITA/ESP
- `BrandAndModel` - Marca (no usado)
- `BindGantryNumber` - Vinculación Gantry (no usado)

**AA (Col 30): Catálogo**
- `AvailableColors` - Lista de colores válidos

#### Parámetros adicionales web:

**Transformaciones 3D**
- `RotationX/Y/Z` - Rotación en grados
- `ScaleX/Y/Z` - Escala (1.0 = normal)

**Interacción**
- `IsClickable` - ¿Clickeable?
- `ShowTooltip` - ¿Mostrar tooltip?
- `NavigateToScreen` - Pantalla destino al click

**Animaciones**
- `AnimationType` - none/rotate/pulse/bounce
- `AnimationSpeed` - Velocidad (0-10)
- `AnimateOnlyWhenOn` - Solo animar si ON

**Visibilidad**
- `InitiallyVisible` - ¿Visible al inicio?
- `VisibilityCondition` - Variable PLC de visibilidad

**Agrupación**
- `Category` - Categoría (pumps/valves/tanks)
- `Layer` - Capa de visualización

**Performance**
- `CastShadows/ReceiveShadows` - Control de sombras
- `LOD` - Level of Detail (high/medium/low)

---

### 2. **Services/PumpElementService.cs** (Servicio de lectura/escritura Excel)

#### Interfaz: `IPumpElementService`
```csharp
Task<List<PumpElement3D>> LoadPumpElementsAsync(string filePath);
Task<bool> SavePumpElementsAsync(List<PumpElement3D> elements, string filePath);
```

#### Funcionalidades clave:

**LoadPumpElementsAsync()**
- Lee hoja "1) Pumps" del Excel
- Obtiene total de elementos desde A2
- Lee filas desde 2 hasta 2+total-1
- Mapea 30 columnas a propiedades C#
- Procesa jerarquía padre-hijo (offsprings)
- Asigna valores por defecto a parámetros web

**SavePumpElementsAsync()**
- Crea hoja "1) Pumps" con headers
- Escribe datos desde fila 2
- Guarda total en A2 solo en primera fila
- Autoajusta columnas

**ProcessOffspringsAsync()**
- Vincula elementos hijos con padres
- Los hijos están en filas consecutivas después del padre

**Métodos helper:**
- `ParseDouble()` - Parse seguro de doubles
- `ParseInt()` - Parse seguro de integers

---

### 3. **Controllers/PumpElementsController.cs** (API REST)

#### Endpoints disponibles:

| Método | Ruta | Descripción |
|--------|------|-------------|
| GET | `/api/pumpelements` | Obtener todos los elementos |
| GET | `/api/pumpelements/{name}` | Obtener elemento por nombre |
| GET | `/api/pumpelements/category/{category}` | Filtrar por categoría |
| GET | `/api/pumpelements/stats` | Estadísticas del sistema |
| POST | `/api/pumpelements` | Guardar elementos en Excel |

#### Ejemplo de estadísticas retornadas:
```json
{
  "totalElements": 2,
  "totalWithChildren": 1,
  "totalChildren": 3,
  "categories": [
    { "category": "pumps", "count": 2 }
  ],
  "plcVariables": {
    "mainPageRefs": 2,
    "manualPageRefs": 2,
    "configPageRefs": 2
  },
  "colors": {
    "uniqueOnColors": 3,
    "uniqueOffColors": 1
  }
}
```

---

## ⚙️ Configuración

### Program.cs - Registro del servicio
```csharp
builder.Services.AddScoped<IPumpElementService, PumpElementService>();
```

✅ Ya añadido en el código

---

## 📊 Estructura del Excel "1) Pumps"

### Formato de filas:
- **Fila 1**: Headers (nombres de columnas)
- **Fila 2**: Primer elemento + total en A2
- **Fila 3+**: Elementos restantes

### Ejemplo:
```
| A | B        | C                     | D | E | F | G                                    | ... |
|---|----------|-----------------------|---|---|---|--------------------------------------|-----|
|   | Name     | File Name             | Offset X | Y | Z | PLC(main page reference)  | ... |
| 2 | PUMP_1   | Pumps/PUMP_01.OBJ     | 0 | 0 | 0 | MAIN.fbMachine.st_Pump[1] | ... |
|   | PUMP_2   | Pumps/PUMP_02.OBJ     | 5 | 0 | 0 | MAIN.fbMachine.st_Pump[2] | ... |
```

### Jerarquía padre-hijo:
```
Fila 2: PUMP_1 (OffspringsCount = 2)
  → Fila 3: PIPE_1 (hijo 1, hereda modelo PUMP_01.OBJ)
  → Fila 4: PIPE_2 (hijo 2, hereda modelo PUMP_01.OBJ)
Fila 5: PUMP_2 (OffspringsCount = 0)
```

---

## 🚀 Uso del Sistema

### 1. Cargar elementos desde Excel

**Backend C#:**
```csharp
var service = serviceProvider.GetRequiredService<IPumpElementService>();
var elements = await service.LoadPumpElementsAsync("ProjectConfig.xlsm");

foreach (var element in elements)
{
    Console.WriteLine($"Bomba: {element.Name}");
    Console.WriteLine($"  Archivo: {element.FileName}");
    Console.WriteLine($"  Posición: ({element.OffsetX}, {element.OffsetY}, {element.OffsetZ})");
    Console.WriteLine($"  PLC: {element.PlcMainPageReference}");
    Console.WriteLine($"  Hijos: {element.OffspringsCount}");
}
```

**API REST:**
```bash
# Obtener todos los elementos
curl http://localhost:5000/api/pumpelements

# Obtener bomba específica
curl http://localhost:5000/api/pumpelements/PUMP_1

# Obtener estadísticas
curl http://localhost:5000/api/pumpelements/stats
```

### 2. Guardar elementos modificados

**Backend C#:**
```csharp
var elements = new List<PumpElement3D>
{
    new PumpElement3D
    {
        Name = "NEW_PUMP",
        FileName = "Pumps/NEW_PUMP.OBJ",
        OffsetX = 10,
        OffsetY = 0,
        OffsetZ = 5,
        ColorElementOn = "Green",
        ColorElementOff = "Gray"
    }
};

await service.SavePumpElementsAsync(elements, "ProjectConfig_Output.xlsm");
```

**API REST:**
```bash
curl -X POST http://localhost:5000/api/pumpelements \
  -H "Content-Type: application/json" \
  -d '[{"name":"NEW_PUMP","fileName":"Pumps/NEW_PUMP.OBJ",...}]'
```

---

## 🎨 Integración con Frontend React/Babylon.js

### Ejemplo de carga en BabylonScene.js:

```javascript
// 1. Cargar configuración desde API
const response = await fetch('http://localhost:5000/api/pumpelements');
const pumpElements = await response.json();

// 2. Cargar cada modelo 3D
for (const element of pumpElements) {
    // Cargar modelo
    const result = await BABYLON.SceneLoader.ImportMeshAsync(
        "",
        "http://localhost:5000/models/",
        element.fileName,
        scene
    );
    
    const mesh = result.meshes[0];
    
    // Aplicar transformaciones desde Excel
    mesh.position.x = element.offsetX;
    mesh.position.y = element.offsetY;
    mesh.position.z = element.offsetZ;
    
    mesh.rotation.x = BABYLON.Tools.ToRadians(element.rotationX);
    mesh.rotation.y = BABYLON.Tools.ToRadians(element.rotationY);
    mesh.rotation.z = BABYLON.Tools.ToRadians(element.rotationZ);
    
    mesh.scaling.x = element.scaleX;
    mesh.scaling.y = element.scaleY;
    mesh.scaling.z = element.scaleZ;
    
    // Aplicar color inicial (off por defecto)
    const material = new BABYLON.StandardMaterial("mat_" + element.name, scene);
    material.diffuseColor = BABYLON.Color3.FromHexString(
        colorNameToHex(element.colorElementOff)
    );
    mesh.material = material;
    
    // Configurar interacción
    if (element.isClickable) {
        mesh.actionManager = new BABYLON.ActionManager(scene);
        mesh.actionManager.registerAction(
            new BABYLON.ExecuteCodeAction(
                BABYLON.ActionManager.OnPickTrigger,
                () => {
                    if (element.navigateToScreen) {
                        navigateToScreen(element.navigateToScreen);
                    }
                }
            )
        );
    }
    
    // Configurar animación
    if (element.animationType !== "none") {
        applyAnimation(mesh, element.animationType, element.animationSpeed);
    }
    
    // Almacenar referencia para updates PLC
    elementMeshMap.set(element.name, mesh);
    plcReferenceMap.set(element.plcMainPageReference, element);
}

// 3. Conectar SignalR para updates en tiempo real
signalRConnection.on("PlcDataUpdate", (data) => {
    const element = plcReferenceMap.get(data.variableName);
    if (element) {
        const mesh = elementMeshMap.get(element.name);
        const material = mesh.material;
        
        // Cambiar color según estado PLC
        switch (data.value) {
            case 0: // Disabled
                material.diffuseColor = BABYLON.Color3.FromHexString(
                    colorNameToHex(element.colorElementDisabled)
                );
                break;
            case 1: // Off
                material.diffuseColor = BABYLON.Color3.FromHexString(
                    colorNameToHex(element.colorElementOff)
                );
                break;
            case 2: // On
                material.diffuseColor = BABYLON.Color3.FromHexString(
                    colorNameToHex(element.colorElementOn)
                );
                // Activar animación si está configurada
                if (element.animateOnlyWhenOn) {
                    startAnimation(mesh);
                }
                break;
            case 3: // Alarm
                material.diffuseColor = BABYLON.Color3.FromHexString(
                    colorNameToHex(element.colorElementAlarm)
                );
                showAlarmLabel(element.elementNameDescription, mesh.position);
                break;
        }
    }
});
```

---

## 📝 Próximos Pasos

### Fase 1 - Testing (Ahora)
1. **Reiniciar backend** para cargar nuevos servicios
2. **Probar endpoint** `GET /api/pumpelements`
3. **Verificar lectura** de ProjectConfig.xlsm
4. **Revisar logs** para errores de parsing

### Fase 2 - Integración Frontend
1. Modificar `BabylonScene.js` para cargar desde `/api/pumpelements`
2. Implementar mapeo de colores (color names → hex)
3. Configurar listeners SignalR para variables PLC
4. Añadir sistema de labels con flechas

### Fase 3 - Funcionalidades Avanzadas
1. Implementar animaciones (rotate/pulse/bounce)
2. Sistema de clicks e interacción
3. Filtros por categoría/layer
4. LOD dinámico según distancia de cámara
5. Soporte para 26 hojas restantes del Excel

---

## 🐛 Troubleshooting

### Error: "Sheet '1) Pumps' not found"
- Verificar que el nombre de la hoja sea exactamente `1) Pumps` (con espacio y paréntesis)
- El archivo debe ser ProjectConfig.xlsm en la carpeta ExcelConfigs/

### Error: Parsing de columnas
- Verificar que las columnas estén en orden correcto (A-Z, AA...)
- Usar `sheet.Cells[row, colNumber]` para columnas > 26

### Error: Offsprings no cargados
- Asegurar que `OffspringsCount` sea correcto
- Los hijos deben estar en filas inmediatamente después del padre

### Performance: Carga lenta
- Implementar caché de elementos cargados
- Usar LOD para modelos complejos
- Cargar modelos en segundo plano

---

## 📚 Referencias

- **EPPlus Documentation**: https://github.com/EPPlusSoftware/EPPlus
- **Babylon.js Loaders**: https://doc.babylonjs.com/features/featuresDeepDive/importers
- **SignalR Real-time**: https://learn.microsoft.com/en-us/aspnet/core/signalr/

---

## ✅ Archivos Modificados

1. ✅ `Models/PumpElement3D.cs` - Creado
2. ✅ `Services/PumpElementService.cs` - Creado
3. ✅ `Controllers/PumpElementsController.cs` - Creado
4. ✅ `Program.cs` - Añadida línea de registro del servicio

---

## 🎯 Estado Final

**Backend:**
- ✅ Modelo completo con 30+ propiedades
- ✅ Servicio de lectura/escritura Excel
- ✅ API REST con 5 endpoints
- ✅ Soporte para jerarquía padre-hijo
- ✅ Parsing robusto con defaults
- ⏳ **Pendiente reiniciar para probar**

**Frontend:**
- ⏳ Pendiente integración con API
- ⏳ Pendiente mapeo de colores
- ⏳ Pendiente sistema de labels

**Excel:**
- ✅ Formato documentado
- ✅ 30 columnas mapeadas
- ✅ Jerarquía soportada
