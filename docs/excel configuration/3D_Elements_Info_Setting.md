# 3D_Elements_Info_Setting

Hoja Excel para configurar la visualización de información de elementos 3D en la página principal.

> **Estado**: ✅ Implementado (WIP - 10% probado)  
> **Última actualización**: 2026-01-28

---

## 📋 Resumen

| Concepto | Valor |
|----------|-------|
| **Nombre de hoja** | `3D_Elements_Info_Setting` |
| **Total columnas** | 417 (A-PA) |
| **Columnas base** | A-L (12 columnas) |
| **Botones escritura PLC** | 5 máximo (columnas M-AA, 3 cols/botón) |
| **Slots lectura PLC** | 30 máximo (columnas AB-PA, 13 cols/slot) |
| **Endpoint API** | `GET /api/config/3d-elements-info-setting` |

---

## ✅ Funcionalidades Probadas

| Funcionalidad | Estado | Notas |
|---------------|--------|-------|
| Slots tipo `Numeric` | ✅ Funciona | Probado con `lr_LevelTank[1]`, `lr_LevelTank[2]` |
| Slots tipo `String` (TEXT/WSTRING) | ✅ Funciona | Configurar con `SlotX_Type = "text"` o `"string"` |
| Iconos en slots (imagen) | ✅ Funciona | Via base64 data URL (CORS solucionado) |
| SignalR valor inicial | ✅ Funciona | Envía valor al suscribirse |
| Layout vertical slots | ✅ Funciona | Altura dinámica del panel |
| Ocultar nombre variable sin descripción | ✅ Funciona | Si `SlotX_Description` vacío, no muestra nada |

## ⏳ Pendiente de Probar

| Funcionalidad | Estado |
|---------------|--------|
| Slots tipo `Boolean` | ⚙️ Implementado (pendiente validar en planta) |
| Slots tipo `Progress` | ⚙️ Implementado (pendiente validar en planta) |
| Slots tipo `Gauge` | ⚙️ Implementado (pendiente validar en planta) |
| Slots tipo `Sparkline` | ⚙️ Implementado (pendiente validar en planta) |
| Botones escritura PLC (Pulse/Set/Toggle/Input) | ⚙️ Implementado (pendiente validar en planta) |
| DisplayTypes (linked, screen-fixed, dual-toggle, etc.) | ⚙️ Implementado (pendiente validar en planta) |
| Umbrales warning/critical | ⚙️ Implementado (pendiente validar en planta) |

---

## 🎯 DisplayType - Tipos de Visualización

La lógica real se basa en `ElementDisplayType` (backend) y `_initializeVisibility`/`_hasHighlight` en `InfoDisplayManager` (frontend).

| Valor (`DisplayType`) | `modelLabel` (label 3D info) | `screenPanel` (panel pantalla) | `highlight` (📍 localización) | Checkbox en UI | Descripción funcional |
|------------------------|-------------------------------|-------------------------------|-------------------------------|----------------|------------------------|
| `always-visible` | ✅ Siempre visible | ❌ | ❌ | ❌ | Info pegada al modelo SIEMPRE, sin checkbox ni panel en pantalla |
| `always-linked` | ✅ Siempre visible | ❌ | Toggle | ✅ (highlight) | Info pegada al modelo SIEMPRE + checkbox para resaltar/localizar modelo (highlight) |
| `attached-label` | ❌ | ❌ | Toggle | ✅ (highlight) | Solo highlight tipo etiqueta pequeña pegada al modelo, controlado por checkbox |
| `screen-fixed` | ❌ | ✅ Siempre visible | ❌ | ❌ | Panel fijo en pantalla, sin checkbox (siempre visible) |
| `linked` | ❌ | ✅ Siempre visible | Toggle | ✅ (highlight) | Panel en pantalla SIEMPRE + checkbox para mostrar highlight/localización sobre el modelo |
| `screen-always` | ❌ | Toggle | ❌ | ✅ (panel) | Panel en pantalla con checkbox para mostrar/ocultar (si se marca, se muestra siempre hasta ocultar) |
| `dual-toggle` | ❌ | Toggle | Toggle | ✅✅ (2 checkboxes) | Un checkbox controla panel en pantalla, el otro el highlight/localización sobre el modelo |

---

## 🖥️ ScreenPosition - Posiciones en Pantalla

Para `screen-fixed`, `linked` y `screen-always`.

El software actual usa un **grid de 4 columnas x 3 filas**. Cada panel de pantalla ocupa una de estas 12 posiciones fijas.

```
┌────────────────────────────────────────────────────────────────┐
│ top-left        top-center-left   top-center-right    top-right│
│────────────────────────────────────────────────────────────────│
│ middle-left     middle-center-left middle-center-right middle-right
│ (aliases:       (alias:            (alias:             (alias:
│  center-left)    center-center-left) center-center-right) center-right)
│────────────────────────────────────────────────────────────────│
│ bottom-left     bottom-center-left bottom-center-right bottom-right│
└────────────────────────────────────────────────────────────────┘
```

### Valores válidos de `ScreenPosition`

| Valor | Fila | Columna | Notas |
|-------|------|---------|-------|
| `top-left` | superior | izquierda | Panel arriba a la izquierda |
| `top-center-left` | superior | centro-izquierda | Panel arriba centrado (1) |
| `top-center-right` | superior | centro-derecha | Panel arriba centrado (2) |
| `top-right` | superior | derecha | Panel arriba a la derecha (desplazado para no tapar InfoPanel) |
| `middle-left` | media | izquierda | Panel centrado vertical, a la izquierda |
| `middle-center-left` | media | centro-izquierda | Panel centrado vertical, centro-izquierda |
| `middle-center-right` | media | centro-derecha | Panel centrado vertical, centro-derecha |
| `middle-right` | media | derecha | Panel centrado vertical, derecha |
| `bottom-left` | inferior | izquierda | Panel abajo a la izquierda |
| `bottom-center-left` | inferior | centro-izquierda | Panel abajo centrado (1) |
| `bottom-center-right` | inferior | centro-derecha | Panel abajo centrado (2) |
| `bottom-right` | inferior | derecha | Panel abajo a la derecha |

### Alias aceptados (compatibilidad Excel)

En Excel también se aceptan estos valores, que internamente se mapean a las posiciones `middle-*`:

| Alias | Equivale a |
|-------|------------|
| `center-left` | `middle-left` |
| `center-center-left` | `middle-center-left` |
| `center-center-right` | `middle-center-right` |
| `center-right` | `middle-right` |

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

| Valor | Visual | Descripción | Tipo PLC |
|-------|--------|-------------|----------|
| `numeric` | `45.2 mm` | Valor numérico con unidad | `INT`, `DINT`, `REAL`, `LREAL` |
| `boolean` | `● Activo` / `○ Parado` | Estado ON/OFF con texto | `BOOL` |
| `text` / `string` | `AUTO_MODE` | Texto literal del PLC | `STRING`, `WSTRING` |
| `progress` | `████████░░ 80%` | Barra de progreso horizontal | Numérico |
| `gauge` | 🎯 Velocímetro circular | Indicador tipo reloj | Numérico |
| `sparkline` | 📈 Mini gráfico | Tendencia últimos N valores | Numérico |
| `numeric+sparkline` | `45.2 A` + 📈 | Valor numérico + tendencia | Numérico |
| `numeric+gauge` | `45.2 A` + 🎯 | Valor numérico + velocímetro | Numérico |
| `progress+numeric` | `████░░` + `75%` | Barra + valor numérico | Numérico |
| `gauge+sparkline` | 🎯 + 📈 | Velocímetro + tendencia | Numérico |

### ⚙️ Configuración de Slots tipo STRING

Para mostrar variables de texto del PLC (`STRING` o `WSTRING`):

| Campo Excel | Valor | Obligatorio | Ejemplo |
|-------------|-------|-------------|---------|
| `SlotX_Type` | `"text"` o `"string"` | ✅ Sí | `text` |
| `SlotX_PlcVar` | Nombre variable PLC | ✅ Sí | `MAIN.fbMachine.sMode` |
| `SlotX_Description` | Etiqueta mostrada | ⚠️ Opcional* | `Modo actual` |
| `SlotX_Icon` | Emoji o ruta imagen | ❌ No | `📝` |

> **⚠️ IMPORTANTE**: Si `SlotX_Description` está **vacío**, el slot **NO mostrará ningún texto** (ni descripción ni nombre de variable). Solo se mostrará el valor del PLC. Si quieres ver una etiqueta, debes poner algo en `SlotX_Description`.

**Ejemplo de configuración:**

| Offset | Campo | Valor | Notas |
|--------|-------|-------|-------|
| +0 | `Slot1_Type` | `text` | Tipo STRING |
| +1 | `Slot1_PlcVar` | `MAIN.fbMachine.sStatusMessage` | Variable WSTRING del PLC |
| +2 | `Slot1_Description` | `Estado máquina` | Etiqueta visible (OBLIGATORIO para ver algo) |
| +3 | `Slot1_Unit` | *(vacío)* | No se usa para strings |
| +12 | `Slot1_Icon` | `📝` | Opcional |

**Resultado visual:**
```
┌─────────────────────────────────┐
│ 📝 Estado máquina               │  ← Descripción + icono
│    LAVADO EN CURSO              │  ← Valor del PLC
└─────────────────────────────────┘
```

---

## 📋 Estructura de Columnas

### SECCIÓN 1: Identificación y Configuración Base (A-L)

| Col | # | Campo | Tipo | Ejemplo | Descripción |
|-----|---|-------|------|---------|-------------|
| **A** | 1 | `ModelName` | string | `GANTRY_1` | Nombre del modelo padre (debe existir en hoja "3D Elements") |
| **B** | 2 | `DisplayType` | enum | `linked` | Tipo de visualización (ver tabla DisplayType) |
| **C** | 3 | `ScreenPosition` | string | `top-left` | Posición en pantalla (ver tabla ScreenPosition) |
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

> ⚠️ Formato NUEVO: la segunda columna es una cadena de configuración compuesta `icon|behaviorType|dataType|enableVar`.

| Botón | Columnas | # Base | Campos |
|-------|----------|--------|--------|
| Botón 1 | M-O | 13 | PlcVar, Config, Description |
| Botón 2 | P-R | 16 | PlcVar, Config, Description |
| Botón 3 | S-U | 19 | PlcVar, Config, Description |
| Botón 4 | V-X | 22 | PlcVar, Config, Description |
| Botón 5 | Y-AA | 25 | PlcVar, Config, Description |

#### Detalle de Columnas por Botón

| Offset | Campo (nombre sugerido) | Tipo | Ejemplo | Descripción |
|--------|-------------------------|------|---------|-------------|
| +0 | `BtnX_PlcVar` | string | `MAIN.fbMachine.CMD_Start` | Variable PLC a escribir (BOOL/INT/LREAL/STRING) |
| +1 | `BtnX_Config` | string | `start.png|pulse|bool|GVL.EnableStart` | Config compuesta: `icon|behaviorType|dataType|enableVar` (todas las partes opcionales) |
| +2 | `BtnX_Description` | string | `Arrancar` | Texto visible en el botón |

### SECCIÓN 3: Slots de Lectura PLC (AB-PA)

Hasta **30 slots de datos**, **13 columnas cada uno**. Si `SlotX_Type` está vacío, el slot no aparece.

Resumen de rangos de columnas:

| Slots | Columnas | Índice de columna inicial | Comentario |
|-------|----------|---------------------------|------------|
| 1-10 | AB-FA | 28 | Configuración original (compatibilidad) |
| 11-20 | FB-KA | 158 | Segundo bloque de 10 slots |
| 21-30 | KB-PA | 288 | Tercer bloque de 10 slots |

La fórmula general es: **columna inicial = 28 + (slotIndex - 1) × 13**.

Ejemplos concretos:

| Slot | Columnas |
|------|----------|
| 1 | AB-AN |
| 2 | AO-BA |
| 3 | BB-BN |
| 4 | BO-CA |
| 5 | CB-CN |
| 6 | CO-DA |
| 7 | DB-DN |
| 8 | DO-EA |
| 9 | EB-EN |
| 10 | EO-FA |
| 11 | FB-FN |
| 12 | FO-GA |
| 20 | IO-KA |
| 21 | KB-KN |
| 30 | MO-PA |

#### Detalle de Columnas por Slot (13 columnas)

| Offset | Campo | Tipo | Ejemplo | Descripción |
|--------|-------|------|---------|-------------|
| +0 | `SlotX_Type` | enum | `numeric` | Tipo de visualización (ver tabla Slot_Type) |
| +1 | `SlotX_PlcVar` | string | `MAIN.fbMachine.lr_LevelTank[1]` | Variable PLC a leer |
| +2 | `SlotX_Description` | string | `Nivel Tanque 1` | Etiqueta opcional. Si está vacío, el slot se muestra **solo con el valor/visual** (sin cabecera de texto) |
| +3 | `SlotX_Unit` | string | `mm` | Unidad de medida (opcional, no usar con STRING) |
| +4 | `SlotX_Format` | string | `#.0` | Formato numérico (`#` = entero, `#.0` = 1 decimal, `#.00` = 2 decimales) |
| +5 | `SlotX_Min` | double | `0` | Valor mínimo (para gauge/progress) |
| +6 | `SlotX_Max` | double | `5000` | Valor máximo (para gauge/progress) |
| +7 | `SlotX_Warning` | double | `4000` | Umbral amarillo (para gauge) |
| +8 | `SlotX_Critical` | double | `4500` | Umbral rojo (para gauge) |
| +9 | `SlotX_History` | int | `30` | Tamaño historial (para sparkline) |
| +10 | `SlotX_TextOn` | string | `Activo` | Texto cuando TRUE (para boolean) |
| +11 | `SlotX_TextOff` | string | `Parado` | Texto cuando FALSE (para boolean) |
| +12 | `SlotX_Icon` | string | `🌡️` o `temp.png` | Icono del slot (vacío = sin icono) |

> **⚠️ CAMBIO IMPORTANTE (2026-01-28)**: Anteriormente, si `SlotX_Description` estaba vacío, se mostraba el nombre de la variable PLC. **Ahora, si no hay descripción, el slot NO muestra ningún encabezado** (más limpio). Si quieres ver una etiqueta, debes rellenar `SlotX_Description`.

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
| **Acción por defecto (Pulse)** | Escribe `TRUE` (1) al PLC, espera y vuelve a `FALSE` automáticamente |
| **Tipo `Set`** | Escribe `TRUE` y mantiene el estado (latch) hasta que otro control lo cambie |
| **Tipo `Toggle`** | Alterna entre `TRUE` y `FALSE` leyendo el valor actual de la variable PLC |
| **Tipo `Input`** | Abre un teclado virtual para escribir un valor (INT/LREAL/STRING) y lo envía al PLC |
| **DataType** | Definido en `BtnX_Config` (`bool`, `int`, `lreal`, `string`) y mapeado a `ButtonDataType` en backend |
| **EnableVariable** | Tercer/cuarto campo de `BtnX_Config`: 0=oculto, 1=visible y habilitado, 2=visible pero deshabilitado |
| **Visibilidad** | Si `BtnX_PlcVar` está vacío, el botón no se crea |
| **Permisos** | Respeta permisos de usuario y lógica de escritura PLC en backend |

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
| 2026-01-28 | 1.6 | **CAMBIO**: Si `SlotX_Description` vacío, no muestra header (ni variable PLC) |
| 2026-01-28 | 1.7 | Documentación completa para slots tipo STRING/TEXT (variables WSTRING del PLC) |
| 2026-01-28 | 1.8 | Soporte hasta 30 slots (AB-PA) y actualización de `ScreenPosition`/botones según implementación real |

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
| [src/utils/InfoDisplayManager.js](../../../my-3d-app/src/utils/InfoDisplayManager.js) | Manager para paneles de info 3D (labels 3D + Screen Panels) |
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