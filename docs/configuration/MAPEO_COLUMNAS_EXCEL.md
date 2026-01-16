# 📋 Mapeo Completo de Columnas Excel → Modelo C#

## Hoja: "1) Pumps"

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         ESTRUCTURA DEL EXCEL                                 │
└─────────────────────────────────────────────────────────────────────────────┘

Fila 1: HEADERS
Fila 2: Primer elemento (A2 = total de elementos)
Fila 3+: Elementos restantes
```

---

## 📊 Tabla de Mapeo

| Col | Letra | Header Excel | Propiedad C# | Tipo | Descripción |
|-----|-------|--------------|--------------|------|-------------|
| 1 | A | Num pump | `TotalElements` | int? | Total elementos (solo fila 2) |
| 2 | B | Name | `Name` | string | Nombre/descripción del elemento |
| 3 | C | File Name | `FileName` | string | Ruta archivo 3D (ej: Pumps/PUMP_01.OBJ) |
| 4 | D | Offset file X | `OffsetX` | double | Desplazamiento X en escena 3D |
| 5 | E | Offset file Y | `OffsetY` | double | Desplazamiento Y en escena 3D |
| 6 | F | Offset file Z | `OffsetZ` | double | Desplazamiento Z en escena 3D |
| 7 | G | PLC(main page reference) | `PlcMainPageReference` | string | Variable TwinCAT página principal |
| 8 | H | PLC(manual page reference) | `PlcManualPageReference` | string | Variable TwinCAT página manuales |
| 9 | I | PLC(config page reference) | `PlcConfigPageReference` | string | Variable TwinCAT página config |
| 10 | J | Color element on | `ColorElementOn` | string | Color cuando PLC = 2 (ON) |
| 11 | K | Color element off | `ColorElementOff` | string | Color cuando PLC = 1 (OFF) |
| 12 | L | Color element disabled | `ColorElementDisabled` | string | Color cuando PLC = 0 (DISABLED) |
| 13 | M | Color element alarm | `ColorElementAlarm` | string | Color cuando PLC = 3 (ALARM) |
| 14 | N | Element name descript. | `ElementNameDescription` | string | Texto del label en 3D |
| 15 | O | Element name descript. FontSize | `LabelFontSize` | int | Tamaño fuente del label |
| 16 | P | Offset position X (Pos 1) | `LabelOffsetX_Pos1` | double | Label offset X posición 1 |
| 17 | Q | Offset position Y (Pos 1) | `LabelOffsetY_Pos1` | double | Label offset Y posición 1 |
| 18 | R | Offset position Z (Pos 1) | `LabelOffsetZ_Pos1` | double | Label offset Z posición 1 |
| 19 | S | Offset position X (Pos 2) | `LabelOffsetX_Pos2` | double | Label offset X posición 2 |
| 20 | T | Offset position Y (Pos 2) | `LabelOffsetY_Pos2` | double | Label offset Y posición 2 |
| 21 | U | Offset position Z (Pos 2) | `LabelOffsetZ_Pos2` | double | Label offset Z posición 2 |
| 22 | V | Offsprings image (Pipe) | `OffspringsCount` | int | Número de elementos hijos |
| 23 | W | Icon file reference | `IconFileReference` | string | Imagen .jpg/.png para UI 2D |
| 24 | X | Icon Language label row | `IconLanguageLabelRow` | int | Línea en MSG.ENG/ITA/ESP |
| 25 | Y | Brand and model | `BrandAndModel` | string | Marca/modelo (no usado) |
| 26 | Z | BIND GANTRY NUMBER | `BindGantryNumber` | int | Vinculación gantry (-1=sin vincular) |
| 30 | AD | Colores (27) | `AvailableColors` | string | Catálogo de colores válidos |

---

## 🎯 Estados PLC y Colores

```
┌─────────────────────────────────────────────────────────────────┐
│  Variable G (PlcMainPageReference) determina el color:          │
├─────────────────────────────────────────────────────────────────┤
│  0 → Columna L (ColorElementDisabled)  → ej: "Violet"          │
│  1 → Columna K (ColorElementOff)       → ej: "Gray"            │
│  2 → Columna J (ColorElementOn)        → ej: "Lime" / "Green"  │
│  3 → Columna M (ColorElementAlarm)     → ej: "Red"             │
└─────────────────────────────────────────────────────────────────┘
```

---

## 👨‍👩‍👧‍👦 Jerarquía Padre-Hijo (Offsprings)

```
┌────────────────────────────────────────────────────────────────────┐
│  Columna V = Número de hijos que heredan el modelo del padre      │
├────────────────────────────────────────────────────────────────────┤
│                                                                    │
│  Fila 2: PUMP_1 (V=2, FileName=Pumps/PUMP_01.OBJ)                │
│    ├─ Fila 3: PIPE_1 (hereda PUMP_01.OBJ, propios offset/color) │
│    └─ Fila 4: PIPE_2 (hereda PUMP_01.OBJ, propios offset/color) │
│                                                                    │
│  Fila 5: PUMP_2 (V=0, FileName=Pumps/PUMP_02.OBJ)                │
│    └─ Sin hijos                                                    │
│                                                                    │
└────────────────────────────────────────────────────────────────────┘

Los hijos aparecen en las filas inmediatamente después del padre.
Cada hijo tiene sus propios:
  - Offset X/Y/Z (columnas D-F)
  - Colores on/off/disabled/alarm (columnas J-M)
  - Referencias PLC (columnas G-I)
```

---

## 🏷️ Sistema de Labels

```
┌──────────────────────────────────────────────────────────────────┐
│  Labels aparecen cuando:                                          │
│    • Usuario pulsa botón de elemento                             │
│    • Ocurre alarma (PLC = 3)                                     │
├──────────────────────────────────────────────────────────────────┤
│                                                                   │
│  Columna N: Texto del label (ej: "P 01")                        │
│  Columna O: Tamaño fuente (ej: 20)                              │
│                                                                   │
│  Posición 1 (P,Q,R): Coordenadas X/Y/Z donde mostrar label      │
│  Posición 2 (S,T,U): Coordenadas alternativas                   │
│                                                                   │
│  Flecha apunta desde label hacia modelo 3D                       │
└──────────────────────────────────────────────────────────────────┘
```

---

## 🌐 Multiidioma

```
┌──────────────────────────────────────────────────────────────────┐
│  Columna X (IconLanguageLabelRow) = Línea en archivos de texto  │
├──────────────────────────────────────────────────────────────────┤
│                                                                   │
│  Archivos de traducción:                                          │
│    • MSG.ENG (línea X = texto en inglés)                         │
│    • MSG.ITA (línea X = texto en italiano)                       │
│    • MSG.ESP (línea X = texto en español)                        │
│                                                                   │
│  Ejemplo: IconLanguageLabelRow = 109                             │
│    → Lee línea 109 del archivo según idioma seleccionado        │
└──────────────────────────────────────────────────────────────────┘
```

---

## ➕ Parámetros Adicionales Web (No en Excel)

Estos parámetros se asignan con valores por defecto al cargar:

### Transformaciones 3D
```
RotationX = 0.0     // Rotación en grados
RotationY = 0.0
RotationZ = 0.0
ScaleX = 1.0        // Escala (1.0 = tamaño original)
ScaleY = 1.0
ScaleZ = 1.0
```

### Pivot para Rotaciones (REF PLC ROTATION)
```
PivotOffset = "X,Y,Z"      // Offset del pivot para rotación (ej: "10,5,0")
                            // Si vacío, usa centro geométrico automático
NavigateToScreen = null     // Pantalla destino al click
```

### Animaciones
```
AnimationType = "none"      // none | rotate | pulse | bounce
AnimationSpeed = 1.0        // Velocidad 0-10
AnimateOnlyWhenOn = true    // Solo animar si PLC=2
```

### Visibilidad
```
InitiallyVisible = true         // ¿Visible al cargar?
VisibilityCondition = null      // Variable PLC de visibilidad
```

### Agrupación
```
Category = "pumps"          // pumps | valves | tanks | ...
Layer = "default"           // Para filtrar grupos
```

### Performance
```
CastShadows = true          // ¿Proyecta sombras?
ReceiveShadows = true       // ¿Recibe sombras?
LOD = "high"                // high | medium | low
```

---

## 📦 Ejemplo de Datos Reales

```excel
┌───┬──────────┬───────────────────────┬───┬───┬───┬─────────────────────────────────────┬───────┬───────┬────────┬─────┐
│ A │    B     │          C            │ D │ E │ F │                  G                  │   J   │   K   │   L    │  M  │
├───┼──────────┼───────────────────────┼───┼───┼───┼─────────────────────────────────────┼───────┼───────┼────────┼─────┤
│   │   Name   │      File Name        │ X │ Y │ Z │      PLC(main page reference)       │  ON   │  OFF  │DISABLE │ALARM│
├───┼──────────┼───────────────────────┼───┼───┼───┼─────────────────────────────────────┼───────┼───────┼────────┼─────┤
│ 2 │ PUMP_1   │ Pumps/PUMP_01.OBJ     │ 0 │ 0 │ 0 │ MAIN.fbMachine.st_Pump[1].i_State  │ Lime  │ Gray  │ Violet │ Red │
│   │ PUMP_2   │ Pumps/PUMP_02.OBJ     │ 5 │ 0 │ 0 │ MAIN.fbMachine.st_Pump[2].i_State  │ Blue  │ Gray  │ Violet │ Red │
└───┴──────────┴───────────────────────┴───┴───┴───┴─────────────────────────────────────┴───────┴───────┴────────┴─────┘
```

Fila 2: A2=2 (total 2 bombas)
Datos en B2:M2 y B3:M3

---

## 🔄 Flujo de Lectura

```
1. Abrir Excel → ProjectConfig.xlsm
2. Seleccionar hoja → "1) Pumps"
3. Leer A2 → TotalElements = 2
4. Loop desde fila 2 hasta fila (2 + TotalElements - 1)
   ├─ Leer columnas A-Z, AD
   ├─ Parsear valores (double, int, string)
   ├─ Crear objeto PumpElement3D
   └─ Añadir a lista
5. Procesar offsprings (padre-hijo)
6. Retornar lista completa
```

---

## 🎨 Ejemplo de Uso en Frontend

```javascript
// 1. Cargar elementos
const response = await fetch('http://localhost:5000/api/pumpelements');
const elements = await response.json();

// 2. Para cada elemento
elements.forEach(element => {
    console.log(`
    Bomba: ${element.name}
    Archivo: ${element.fileName}
    Posición: (${element.offsetX}, ${element.offsetY}, ${element.offsetZ})
    PLC Variable: ${element.plcMainPageReference}
    
    Colores por estado:
      ON (2): ${element.colorElementOn}
      OFF (1): ${element.colorElementOff}
      DISABLED (0): ${element.colorElementDisabled}
      ALARM (3): ${element.colorElementAlarm}
    
    Label: "${element.elementNameDescription}" (size: ${element.labelFontSize})
    Hijos: ${element.offspringsCount}
    `);
    
    // Cargar modelo 3D
    loadModel(element.fileName, element.offsetX, element.offsetY, element.offsetZ);
});

// 3. Listener de cambios PLC
signalR.on('PlcDataUpdate', (data) => {
    const element = findElementByPlcVar(data.variableName);
    if (element) {
        const color = getColorForState(element, data.value);
        updateModelColor(element.name, color);
    }
});
```

---

## ✅ Checklist de Implementación

- [x] Modelo PumpElement3D con 30+ propiedades
- [x] Servicio de lectura LoadPumpElementsAsync()
- [x] Servicio de escritura SavePumpElementsAsync()
- [x] Procesamiento de jerarquía padre-hijo
- [x] API REST con 5 endpoints
- [x] Registro en Program.cs
- [ ] Reiniciar backend y probar
- [ ] Integración con BabylonScene.js
- [ ] Sistema de mapeo de colores
- [ ] Labels con flechas en 3D
- [ ] Integración SignalR con PLC

---

## 📚 Columnas Pendientes de Análisis

Las siguientes columnas no tienen datos en el Excel actual pero están mapeadas:

- Columnas 27-29: Sin nombre/sin datos
- Posible expansión futura

---

## 🎯 Resumen Rápido

| Categoría | Columnas | Cantidad | Propiedades C# |
|-----------|----------|----------|----------------|
| Identificación | A-C | 3 | TotalElements, Name, FileName |
| Posición 3D | D-F | 3 | OffsetX/Y/Z |
| Variables PLC | G-I | 3 | PlcMainPageReference, PlcManualPageReference, PlcConfigPageReference |
| Colores Estado | J-M | 4 | ColorElement On/Off/Disabled/Alarm |
| Labels | N-U | 8 | ElementNameDescription, LabelFontSize, LabelOffset X/Y/Z Pos1/2 |
| Jerarquía | V | 1 | OffspringsCount, Children |
| Metadatos | W-Z (23-26) | 4 | IconFileReference, IconLanguageLabelRow, BrandAndModel, BindGantryNumber |
| Catálogo | AD (30) | 1 | AvailableColors |
| **TOTAL EXCEL** | | **27** | **30 propiedades base** |
| Parámetros Web | - | - | +18 propiedades adicionales |
| **TOTAL MODELO** | | | **48 propiedades** |
