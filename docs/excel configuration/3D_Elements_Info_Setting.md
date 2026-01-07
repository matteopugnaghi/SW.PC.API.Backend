# 3D_Elements_Info_Setting

Hoja Excel para configurar la visualización de información de elementos 3D en la página principal.

> **Estado**: ✅ Implementado (WIP - 5% probado)  
> **Última actualización**: 2026-01-05

---

## 📋 Resumen

| Concepto | Valor |
|----------|-------|
| **Nombre de hoja** | `3D_Elements_Info_Setting` |
| **Total columnas** | 158 (A-FB) |
| **Columnas base** | A-L (12 columnas) |
| **Botones escritura PLC** | 5 máximo (columnas M-AA, 3 cols/botón) |
| **Slots lectura PLC** | 10 máximo (columnas AB-FB, 13 cols/slot) |
| **Endpoint API** | `GET /api/config/3d-elements-info-setting` |

---

## ✅ Funcionalidades Probadas

| Funcionalidad | Estado | Notas |
|---------------|--------|-------|
| Slots tipo `Numeric` | ✅ Funciona | Probado con `lr_LevelTank[1]`, `lr_LevelTank[2]` |
| Iconos en slots (imagen) | ✅ Funciona | Via base64 data URL (CORS solucionado) |
| SignalR valor inicial | ✅ Funciona | Envía valor al suscribirse |
| Layout vertical slots | ✅ Funciona | Altura dinámica del panel |

## ⏳ Pendiente de Probar

| Funcionalidad | Estado |
|---------------|--------|
| Slots tipo `Boolean` | ⏳ Pendiente |
| Slots tipo `Progress` | ⏳ Pendiente |
| Slots tipo `Gauge` | ⏳ Pendiente |
| Slots tipo `Sparkline` | ⏳ Pendiente |
| Botones escritura PLC | ⏳ Pendiente |
| DisplayTypes (linked, screen-fixed, etc.) | ⏳ Pendiente |
| Umbrales warning/critical | ⏳ Pendiente |

---

## 🎯 DisplayType - Tipos de Visualización

| Valor | Panel Pantalla | Label Modelo | Checkbox | Descripción |
|-------|----------------|--------------|----------|-------------|
| `always-visible` | ❌ | SIEMPRE (info) | ❌ | Info pegada al modelo, sin control |
| `attached-label` | ❌ | Toggle (info) | ✅ | Info pegada al modelo, toggle con checkbox |
| `screen-fixed` | Toggle | ❌ | ✅ | Panel en pantalla, toggle con checkbox |
| `linked` | Toggle | Toggle (nombre) | ✅ | Panel pantalla + nombre en modelo, ambos toggle |
| `screen-always` | SIEMPRE | Toggle (nombre) | ✅ | Panel siempre visible + checkbox para localizar en 3D |
| `always-linked` | ❌ | SIEMPRE (info) | ✅ | Info siempre visible + checkbox para resaltar modelo |

---

## 🖥️ ScreenPosition - Posiciones en Pantalla

Para `screen-fixed`, `linked` y `screen-always`.

```
┌────────────────────────────────────────────────────────────────┐
│ top-left-1  top-left-2  │  top-center-1  │  top-right-1  top-right-2
│ top-left-3  top-left-4  │  top-center-2  │  top-right-3  top-right-4
│─────────────────────────┼────────────────┼─────────────────────────│
│                         │                │                         │
│ center-left-1           │    [ESCENA     │           center-right-1│
│ center-left-2           │       3D]      │           center-right-2│
│                         │                │                         │
│─────────────────────────┼────────────────┼─────────────────────────│
│ bottom-left-1           │ bottom-center-1│         bottom-right-1  │
│ bottom-left-2           │ bottom-center-2│         bottom-right-2  │
└────────────────────────────────────────────────────────────────┘
```

| Zona | Slots disponibles |
|------|-------------------|
| `top-left` | `top-left-1`, `top-left-2`, `top-left-3`, `top-left-4` |
| `top-center` | `top-center-1`, `top-center-2` |
| `top-right` | `top-right-1`, `top-right-2`, `top-right-3`, `top-right-4` |
| `center-left` | `center-left-1`, `center-left-2` |
| `center-right` | `center-right-1`, `center-right-2` |
| `bottom-left` | `bottom-left-1`, `bottom-left-2` |
| `bottom-center` | `bottom-center-1`, `bottom-center-2` |
| `bottom-right` | `bottom-right-1`, `bottom-right-2` |

---

## 📍 ModelPosition - Posiciones relativas al Modelo 3D

Para `attached-label`, `always-visible`, `linked`, `screen-always` y `always-linked`.

```
              [top]
                │
                ▼
        ┌───────────────┐
[left]→ │   MODELO 3D   │ ←[right]
        └───────────────┘
                ▲
                │
            [bottom]
```

| Valor | Posición |
|-------|----------|
| `top` | Encima del modelo |
| `right` | A la derecha del modelo |
| `bottom` | Debajo del modelo |
| `left` | A la izquierda del modelo |

---

## 📊 Slot_Type - Tipos de Datos para Slots de Lectura

| Valor | Visual | Descripción |
|-------|--------|-------------|
| `numeric` | `45.2 mm` | Valor numérico con unidad |
| `boolean` | `● Activo` / `○ Parado` | Estado ON/OFF con texto |
| `text` | `AUTO_MODE` | Texto literal del PLC |
| `progress` | `████████░░ 80%` | Barra de progreso horizontal |
| `gauge` | 🎯 Velocímetro circular | Indicador tipo reloj |
| `sparkline` | 📈 Mini gráfico | Tendencia últimos N valores |
| `numeric+sparkline` | `45.2 A` + 📈 | Valor numérico + tendencia |
| `numeric+gauge` | `45.2 A` + 🎯 | Valor numérico + velocímetro |
| `progress+numeric` | `████░░` + `75%` | Barra + valor numérico |
| `gauge+sparkline` | 🎯 + 📈 | Velocímetro + tendencia |

---

## 📋 Estructura de Columnas

### SECCIÓN 1: Identificación y Configuración Base (A-L)

| Col | # | Campo | Tipo | Ejemplo | Descripción |
|-----|---|-------|------|---------|-------------|
| **A** | 1 | `ModelName` | string | `GANTRY_1` | Nombre del modelo padre (debe existir en hoja "3D Elements") |
| **B** | 2 | `DisplayType` | enum | `linked` | Tipo de visualización (ver tabla DisplayType) |
| **C** | 3 | `ScreenPosition` | string | `top-left-1` | Posición en pantalla (ver tabla ScreenPosition) |
| **D** | 4 | `ModelPosition` | enum | `top` | Posición relativa al modelo (ver tabla ModelPosition) |
| **E** | 5 | `OffsetX` | double | `0` | Ajuste fino posición X |
| **F** | 6 | `OffsetY` | double | `30` | Ajuste fino posición Y (arriba/abajo) |
| **G** | 7 | `OffsetZ` | double | `0` | Ajuste fino posición Z (adelante/atrás) |
| **H** | 8 | `ModelIcon` | string | `motor.png` o `⚙️` | Icono del modelo (vacío = sin icono) |
| **I** | 9 | `LabelWidth` | double | `0.6` | Ancho de la etiqueta 3D (default=0.6) |
| **J** | 10 | `LabelHeight` | double | `0.2` | Alto de la etiqueta 3D (default=0.2) |
| **K** | 11 | `LabelScale` | double | `1.0` | Escala general de la etiqueta (0.1-5.0) |
| **L** | 12 | `ShortName` | string | `T1` | Nombre corto para mostrar (vacío = no mostrar) |

### SECCIÓN 2: Botones de Escritura PLC (M-AA)

5 botones de acción, **3 columnas cada uno**. Si `BtnX_PlcVar` está vacío, el botón no aparece.

| Botón | Columnas | # Base | Campos |
|-------|----------|--------|--------|
| Botón 1 | M-O | 13 | PlcVar, Description, Icon |
| Botón 2 | P-R | 16 | PlcVar, Description, Icon |
| Botón 3 | S-U | 19 | PlcVar, Description, Icon |
| Botón 4 | V-X | 22 | PlcVar, Description, Icon |
| Botón 5 | Y-AA | 25 | PlcVar, Description, Icon |

#### Detalle de Columnas por Botón

| Offset | Campo | Tipo | Ejemplo | Descripción |
|--------|-------|------|---------|-------------|
| +0 | `BtnX_PlcVar` | string | `MAIN.fbMachine.CMD_Start` | Variable PLC a escribir (BOOL) |
| +1 | `BtnX_Description` | string | `Arrancar` | Texto del botón |
| +2 | `BtnX_Icon` | string | `▶️` o `play.png` | Icono (vacío = sin icono) |

### SECCIÓN 3: Slots de Lectura PLC (AB-FB)

10 slots de datos, **13 columnas cada uno**. Si `SlotX_Type` está vacío, el slot no aparece.

| Slot | Columnas | # Base | Campos (13) |
|------|----------|--------|-------------|
| Slot 1 | AB-AN | 28 | Type, PlcVar, Desc, Unit, Format, Min, Max, Warning, Critical, History, TextOn, TextOff, Icon |
| Slot 2 | AO-BA | 41 | (mismo patrón) |
| Slot 3 | BB-BN | 54 | (mismo patrón) |
| Slot 4 | BO-CA | 67 | (mismo patrón) |
| Slot 5 | CB-CN | 80 | (mismo patrón) |
| Slot 6 | CO-DA | 93 | (mismo patrón) |
| Slot 7 | DB-DN | 106 | (mismo patrón) |
| Slot 8 | DO-EA | 119 | (mismo patrón) |
| Slot 9 | EB-EN | 132 | (mismo patrón) |
| Slot 10 | EO-FA | 145 | (mismo patrón) |

#### Detalle de Columnas por Slot (13 columnas)

| Offset | Campo | Tipo | Ejemplo | Descripción |
|--------|-------|------|---------|-------------|
| +0 | `SlotX_Type` | enum | `numeric` | Tipo de visualización (ver tabla Slot_Type) |
| +1 | `SlotX_PlcVar` | string | `MAIN.fbMachine.lr_LevelTank[1]` | Variable PLC a leer |
| +2 | `SlotX_Description` | string | `Nivel Tanque 1` | Etiqueta/descripción del dato |
| +3 | `SlotX_Unit` | string | `mm` | Unidad de medida |
| +4 | `SlotX_Format` | string | `#.0` | Formato numérico |
| +5 | `SlotX_Min` | double | `0` | Valor mínimo (para gauge/progress) |
| +6 | `SlotX_Max` | double | `5000` | Valor máximo (para gauge/progress) |
| +7 | `SlotX_Warning` | double | `4000` | Umbral amarillo (para gauge) |
| +8 | `SlotX_Critical` | double | `4500` | Umbral rojo (para gauge) |
| +9 | `SlotX_History` | int | `30` | Tamaño historial (para sparkline) |
| +10 | `SlotX_TextOn` | string | `Activo` | Texto cuando TRUE (para boolean) |
| +11 | `SlotX_TextOff` | string | `Parado` | Texto cuando FALSE (para boolean) |
| +12 | `SlotX_Icon` | string | `🌡️` o `temp.png` | Icono del slot (vacío = sin icono) |

---

## 🎨 Ejemplo Visual de Panel Completo

```
┌─────────────────────────────────────────┐
│ ⚙️ MOTOR_01                             │  ← ModelIcon + ModelName
├─────────────────────────────────────────┤
│                                         │
│  [▶️ Arrancar] [⏹️ Parar] [🔄 Reset]    │  ← Botones de escritura
│  [✅ Habilitar] [🚫 Deshabilitar]       │
│                                         │
├─────────────────────────────────────────┤
│  🌡️ Temperatura:  45.2 °C              │  ← Slot numeric
│  ⚡ Consumo:       12.5 A   ╱╲ ╱╲      │  ← Slot numeric+sparkline
│  ● Estado:        Activo               │  ← Slot boolean
│  📊 Carga:        ████████░░ 75%       │  ← Slot progress
│                                         │
│     ╭─────────╮                         │
│    ╱ 0  50 100 ╲   RPM: 1,450          │  ← Slot numeric+gauge
│   │    ╲│╱      │                       │
│    ╲    ●      ╱                        │
│     ╰─────────╯                         │
│                                         │
└─────────────────────────────────────────┘
```

---

## 📝 Ejemplo de Configuración Excel

| A | B | C | D | E | F | G | H | I | J | K | L | M | ... | AB | AC | AD | AE | ... |
|---|---|---|---|---|---|---|---|---|---|---|---|---|-----|----|----|----|----|-----|
| TANQUE_1 | attached-label | | top | 0 | 0.5 | 0 | tank.png | 0.6 | 0.4 | 1.0 | T1 | | ... | numeric | MAIN.fbMachine.lr_LevelTank[1] | Nivel T1 | mm | ... |
| TANQUE_2 | attached-label | | top | 0 | 0.5 | 0 | tank.png | 0.6 | 0.4 | 1.0 | T2 | | ... | numeric | MAIN.fbMachine.lr_LevelTank[2] | Nivel T2 | mm | ... |

> **Nota**: Las columnas M-AA son para botones, AB en adelante para slots.

---

## 📁 Iconos

Los iconos pueden ser:

| Formato | Ejemplo | Ubicación |
|---------|---------|-----------|
| **Emoji** | `⚙️`, `🌡️`, `💧`, `⚡` | Directo en celda Excel |
| **Archivo** | `motor.png`, `pump.svg` | Carpeta `wwwroot/icons/` o `Projects/{id}/icons/` |

Si el campo de icono está **vacío**, no se muestra ningún icono.

---

## 🔒 Comportamiento de Botones de Escritura

| Característica | Descripción |
|----------------|-------------|
| **Acción** | Escribe `TRUE` (1) a la variable PLC |
| **Tipo** | Pulso momentáneo (TRUE → espera → FALSE) |
| **Vacío** | Si `BtnX_PlcVar` está vacío, el botón no aparece |
| **Permisos** | Respeta permisos de usuario para escritura PLC |

---

## 📅 Historial de Cambios

| Fecha | Versión | Descripción |
|-------|---------|-------------|
| 2026-01-04 | 1.0 | Diseño inicial de la hoja |
| 2026-01-04 | 1.1 | Implementación completa backend/frontend |
| 2026-01-05 | 1.2 | Corrección estructura columnas (A-L base, M-AA botones, AB-FB slots) |
| 2026-01-05 | 1.3 | Añadidos campos LabelWidth/LabelHeight/LabelScale/ShortName/OffsetZ |
| 2026-01-05 | 1.4 | Fix SignalR: envía valor al suscribirse (no solo en cambios) |
| 2026-01-05 | 1.5 | CORS fix: iconos como base64 data URL |

---

## 🛠️ Implementación Técnica

### Backend (ASP.NET Core)

| Archivo | Descripción |
|---------|-------------|
| [Models/Excel/ElementInfoSettingConfig.cs](../../Models/Excel/ElementInfoSettingConfig.cs) | Modelo C# con todas las propiedades |
| [Services/ExcelConfigService.cs](../../Services/ExcelConfigService.cs) | Método `Load3DElementsInfoSettingAsync()` (líneas ~930-1163) |
| [Controllers/ConfigController.cs](../../Controllers/ConfigController.cs) | Endpoint `GET /api/config/3d-elements-info-setting` |
| [Hubs/ScadaHub.cs](../../Hubs/ScadaHub.cs) | `SubscribeToVariable()` - envía valor inicial |
| [Services/PlcPollingService.cs](../../Services/PlcPollingService.cs) | `GetVariableCurrentValue()` - obtener valor actual |

### Frontend (React + Babylon.js)

| Archivo | Descripción |
|---------|-------------|
| [services/api.js](../../../my-3d-app/src/services/api.js) | Método `get3DElementsInfoSetting()` |
| [babylon/ui/InfoDisplayManager.js](../../../my-3d-app/src/babylon/ui/InfoDisplayManager.js) | Manager para paneles de info 3D |
| [BabylonScene.js](../../../my-3d-app/src/BabylonScene.js) | Integración con escena 3D |

### Flujo de Datos

```
Excel → ExcelConfigService → ConfigController (base64 icons) → Frontend API
                                                                    ↓
Frontend SignalR ← ScadaHub ← PlcPollingService ← TwinCAT/Simulado
      ↓
InfoDisplayManager → DynamicTexture (Babylon.js) → 3D Scene
```

### API Endpoint

```http
GET /api/config/3d-elements-info-setting
Headers:
  X-Project-Id: {projectId}  (solo en Development)
```

**Respuesta:**
```json
[
  {
    "modelName": "TANQUE_1",
    "displayType": "AttachedLabel",
    "screenPosition": null,
    "modelPosition": "top",
    "offsetX": 0,
    "offsetY": 0.5,
    "offsetZ": 0,
    "modelIcon": "tank.png",
    "modelIconBase64": "data:image/png;base64,iVBORw0KGgo...",
    "labelWidth": 0.6,
    "labelHeight": 0.4,
    "labelScale": 1.0,
    "shortName": "T1",
    "buttons": [],
    "slots": [
      {
        "index": 1,
        "type": "Numeric",
        "plcVariable": "MAIN.fbMachine.lr_LevelTank[1]",
        "description": "Nivel T1",
        "unit": "mm",
        "format": "#.0",
        "icon": "level.png",
        "iconBase64": "data:image/png;base64,..."
      }
    ],
    "excelRowIndex": 2
  }
]
```

### SignalR - Suscripción a Variables

```javascript
// Frontend: InfoDisplayManager.js
signalRService.invoke('SubscribeToVariable', slot.plcVariable);

// Backend: ScadaHub.cs - Envía valor actual inmediatamente
await Groups.AddToGroupAsync(Context.ConnectionId, $"var_{variableName}");
var currentValue = _plcPollingService.GetVariableCurrentValue(variableName);
if (currentValue != null) {
    await Clients.Caller.SendAsync("PlcVariableUpdated", new {
        variableName,
        value = currentValue,
        timestamp = DateTime.UtcNow,
        isInitialValue = true
    });
}
```

---

## 📚 Enumeraciones

### ElementDisplayType

| Valor | Descripción |
|-------|-------------|
| `AlwaysVisible` | Info pegada al modelo, siempre visible, sin checkbox |
| `AttachedLabel` | Info pegada al modelo, toggle con checkbox (default) |
| `ScreenFixed` | Panel fijo en pantalla, toggle con checkbox |
| `Linked` | Panel en pantalla + nombre en modelo, ambos toggle |
| `ScreenAlways` | Panel siempre visible + checkbox para localizar modelo |
| `AlwaysLinked` | Info siempre visible + checkbox para resaltar/localizar |

**Parser de valores Excel:**
```
"always-visible", "always" → AlwaysVisible
"attached-label", "attached", "label" → AttachedLabel
"screen-fixed", "screen", "fixed" → ScreenFixed
"linked" → Linked
"screen-always" → ScreenAlways
"always-linked" → AlwaysLinked
```

### SlotDisplayType

| Valor | Descripción | Campos usados |
|-------|-------------|---------------|
| `None` | Slot no configurado | - |
| `Numeric` | Valor numérico simple (`45.2 mm`) | PlcVar, Desc, Unit, Format |
| `Boolean` | Estado ON/OFF con LED | PlcVar, Desc, TextOn, TextOff |
| `Text` | Texto literal del PLC | PlcVar, Desc |
| `Progress` | Barra de progreso horizontal | PlcVar, Desc, Min, Max |
| `Gauge` | Velocímetro/gauge circular | PlcVar, Desc, Min, Max, Warning, Critical |
| `Sparkline` | Mini gráfico de tendencia | PlcVar, Desc, History |
| `NumericSparkline` | Valor numérico + gráfico | PlcVar, Desc, Unit, Format, History |
| `NumericGauge` | Valor numérico + gauge | PlcVar, Desc, Unit, Format, Min, Max |
| `ProgressNumeric` | Barra de progreso + valor | PlcVar, Desc, Unit, Min, Max |
| `GaugeSparkline` | Gauge + gráfico de tendencia | PlcVar, Desc, Min, Max, History |

**Parser de valores Excel:**
```
"numeric" → Numeric
"boolean", "bool" → Boolean
"text", "string" → Text
"progress", "progressbar" → Progress
"gauge" → Gauge
"sparkline" → Sparkline
"numeric+sparkline", "numericsparkline" → NumericSparkline
"numeric+gauge", "numericgauge" → NumericGauge
"progress+numeric", "progressnumeric" → ProgressNumeric
"gauge+sparkline", "gaugesparkline" → GaugeSparkline
```

---

## 🔧 Variables PLC Automáticas

Las variables PLC de los slots se registran automáticamente en `GetMonitoredVariableNamesAsync()`:

```csharp
// ExcelConfigService.cs - líneas ~1730-1780
var infoSettingConfigs = await LoadElementsInfoSettingFromSheetAsync(package);
foreach (var config in infoSettingConfigs)
{
    // Variables de slots
    foreach (var slot in config.Slots)
    {
        if (slot.PlcVariable.StartsWith("MAIN.fbMachine", OrdinalIgnoreCase))
            variableNames.Add(slot.PlcVariable);
    }
    // Variables de botones
    foreach (var btn in config.Buttons)
    {
        if (btn.PlcVariable.StartsWith("MAIN.fbMachine", OrdinalIgnoreCase))
            variableNames.Add(btn.PlcVariable);
    }
}
```

Esto asegura que las variables usadas en `3D_Elements_Info_Setting` se incluyan automáticamente en el polling del PLC.