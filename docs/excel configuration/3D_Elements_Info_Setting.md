# 3D_Elements_Info_Setting

Hoja Excel para configurar la visualización de información de elementos 3D en la página principal.

---

## 📋 Resumen

| Concepto | Valor |
|----------|-------|
| **Nombre de hoja** | `3D_Elements_Info_Setting` |
| **Total columnas** | 152 (A-EV) |
| **Botones escritura PLC** | 5 máximo |
| **Slots lectura PLC** | 10 máximo |

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

### SECCIÓN 1: Identificación y Configuración Base (A-G)

| Col | Campo | Tipo | Ejemplo | Descripción |
|-----|-------|------|---------|-------------|
| **A** | `ModelName` | string | `GANTRY_1` | Nombre del modelo padre (debe existir en hoja "3D Elements") |
| **B** | `DisplayType` | enum | `linked` | Tipo de visualización (ver tabla DisplayType) |
| **C** | `ScreenPosition` | string | `top-left-1` | Posición en pantalla (ver tabla ScreenPosition) |
| **D** | `ModelPosition` | enum | `top` | Posición relativa al modelo (ver tabla ModelPosition) |
| **E** | `OffsetX` | double | `0` | Ajuste fino posición X |
| **F** | `OffsetY` | double | `30` | Ajuste fino posición Y |
| **G** | `ModelIcon` | string | `motor.png` o `⚙️` | Icono del modelo (vacío = sin icono) |

### SECCIÓN 2: Botones de Escritura PLC (H-V)

5 botones de acción, 3 columnas cada uno. Si `BtnX_PlcVar` está vacío, el botón no aparece.

#### Botón 1 (H-J)
| Col | Campo | Tipo | Ejemplo | Descripción |
|-----|-------|------|---------|-------------|
| **H** | `Btn1_PlcVar` | string | `GVL.Motor[1].CMD_Start` | Variable PLC a escribir (BOOL) |
| **I** | `Btn1_Description` | string | `Arrancar` | Texto del botón |
| **J** | `Btn1_Icon` | string | `▶️` o `play.png` | Icono (vacío = sin icono) |

#### Botón 2 (K-M)
| Col | Campo | Tipo | Ejemplo |
|-----|-------|------|---------|
| **K** | `Btn2_PlcVar` | string | `GVL.Motor[1].CMD_Stop` |
| **L** | `Btn2_Description` | string | `Parar` |
| **M** | `Btn2_Icon` | string | `⏹️` |

#### Botón 3 (N-P)
| Col | Campo | Tipo | Ejemplo |
|-----|-------|------|---------|
| **N** | `Btn3_PlcVar` | string | `GVL.Motor[1].CMD_Reset` |
| **O** | `Btn3_Description` | string | `Reset` |
| **P** | `Btn3_Icon` | string | `🔄` |

#### Botón 4 (Q-S)
| Col | Campo | Tipo | Ejemplo |
|-----|-------|------|---------|
| **Q** | `Btn4_PlcVar` | string | `GVL.Motor[1].CMD_Enable` |
| **R** | `Btn4_Description` | string | `Habilitar` |
| **S** | `Btn4_Icon` | string | `✅` |

#### Botón 5 (T-V)
| Col | Campo | Tipo | Ejemplo |
|-----|-------|------|---------|
| **T** | `Btn5_PlcVar` | string | `GVL.Motor[1].CMD_Disable` |
| **U** | `Btn5_Description` | string | `Deshabilitar` |
| **V** | `Btn5_Icon` | string | `🚫` |

### SECCIÓN 3: Slots de Lectura PLC (W-EV)

10 slots de datos, 13 columnas cada uno. Si `SlotX_Type` está vacío, el slot no aparece.

#### Estructura de cada Slot (13 columnas)

| # | Campo | Tipo | Ejemplo | Descripción |
|---|-------|------|---------|-------------|
| 1 | `SlotX_Type` | enum | `numeric+sparkline` | Tipo de visualización (ver tabla Slot_Type) |
| 2 | `SlotX_PlcVar` | string | `GVL.Gantry[1].Position` | Variable PLC a leer |
| 3 | `SlotX_Description` | string | `Posición` | Etiqueta/descripción del dato |
| 4 | `SlotX_Unit` | string | `mm` | Unidad de medida |
| 5 | `SlotX_Format` | string | `#.0` | Formato numérico |
| 6 | `SlotX_Min` | double | `0` | Valor mínimo (para gauge/progress) |
| 7 | `SlotX_Max` | double | `5000` | Valor máximo (para gauge/progress) |
| 8 | `SlotX_Warning` | double | `4000` | Umbral amarillo (para gauge) |
| 9 | `SlotX_Critical` | double | `4500` | Umbral rojo (para gauge) |
| 10 | `SlotX_History` | int | `30` | Tamaño historial (para sparkline) |
| 11 | `SlotX_TextOn` | string | `Activo` | Texto cuando TRUE (para boolean) |
| 12 | `SlotX_TextOff` | string | `Parado` | Texto cuando FALSE (para boolean) |
| 13 | `SlotX_Icon` | string | `🌡️` o `temp.png` | Icono del slot (vacío = sin icono) |

#### Mapa de Columnas por Slot

| Slot | Columnas | Rango |
|------|----------|-------|
| Slot 1 | W-AI | 13 columnas |
| Slot 2 | AJ-AV | 13 columnas |
| Slot 3 | AW-BI | 13 columnas |
| Slot 4 | BJ-BV | 13 columnas |
| Slot 5 | BW-CI | 13 columnas |
| Slot 6 | CJ-CV | 13 columnas |
| Slot 7 | CW-DI | 13 columnas |
| Slot 8 | DJ-DV | 13 columnas |
| Slot 9 | DW-EI | 13 columnas |
| Slot 10 | EJ-EV | 13 columnas |

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

| A | B | C | D | E | F | G | H | I | J | ... | W | X | Y | Z |
|---|---|---|---|---|---|---|---|---|---|-----|---|---|---|---|
| MOTOR_01 | linked | top-left-1 | top | 0 | 30 | ⚙️ | GVL.Motor[1].CMD_Start | Arrancar | ▶️ | ... | gauge+sparkline | GVL.Motor[1].Amps | Consumo | A |
| TANK_01 | always-visible | | right | 0 | 0 | 💧 | | | | ... | progress | GVL.Tank[1].Level | Nivel | % |
| GANTRY_1 | screen-always | top-left-2 | top | 0 | 30 | 🏗️ | | | | ... | numeric+sparkline | GVL.Gantry[1].Pos | Posición | mm |
| VALVE_01 | always-linked | | left | 0 | 0 | 🔧 | GVL.Valve[1].CMD_Open | Abrir | | ... | boolean | GVL.Valve[1].IsOpen | Estado | |

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
| 2026-01-04 | 1.0 | Diseño inicial de la hoja || 2026-01-04 | 1.1 | Implementación completa backend/frontend |

---

## 🛠️ Implementación Técnica

### Backend (ASP.NET Core)

| Archivo | Descripción |
|---------|-------------|
| [Models/Excel/ElementInfoSettingConfig.cs](../../Models/Excel/ElementInfoSettingConfig.cs) | Modelo C# con todas las propiedades |
| [Services/ExcelConfigService.cs](../../Services/ExcelConfigService.cs) | Método `Load3DElementsInfoSettingAsync` |
| [Controllers/ConfigController.cs](../../Controllers/ConfigController.cs) | Endpoint `GET /api/config/3d-elements-info-setting` |

### Frontend (React + Babylon.js)

| Archivo | Descripción |
|---------|-------------|
| [services/api.js](../../../my-3d-app/src/services/api.js) | Método `get3DElementsInfoSetting()` |
| [utils/InfoDisplayManager.js](../../../my-3d-app/src/utils/InfoDisplayManager.js) | Manager para paneles de info 3D |
| [BabylonScene.js](../../../my-3d-app/src/BabylonScene.js) | Integración con escena 3D |

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
    "modelName": "MOTOR_01",
    "displayType": "Linked",
    "screenPosition": "top-left-1",
    "modelPosition": "top",
    "offsetX": 0,
    "offsetY": 30,
    "modelIcon": "⚙️",
    "buttons": [
      {
        "index": 1,
        "plcVariable": "GVL.Motor[1].CMD_Start",
        "description": "Arrancar",
        "icon": "▶️"
      }
    ],
    "slots": [
      {
        "index": 1,
        "type": "NumericSparkline",
        "plcVariable": "GVL.Motor[1].Amps",
        "description": "Consumo",
        "unit": "A",
        "format": "#.0",
        "min": 0,
        "max": 50,
        "warningThreshold": 35,
        "criticalThreshold": 45,
        "historySize": 30
      }
    ]
  }
]
```

### Enumeraciones

**ElementDisplayType:**
- `AlwaysVisible` - Info pegada al modelo, siempre visible
- `AttachedLabel` - Info pegada al modelo, toggle con checkbox
- `ScreenFixed` - Panel fijo en pantalla, toggle con checkbox
- `Linked` - Panel en pantalla + nombre en modelo
- `ScreenAlways` - Panel siempre visible + checkbox para localizar
- `AlwaysLinked` - Info siempre visible + checkbox para resaltar

**SlotDisplayType:**
- `None` - Slot no configurado
- `Numeric` - Valor numérico simple
- `Boolean` - Estado ON/OFF con LED
- `Text` - Texto literal
- `Progress` - Barra de progreso horizontal
- `Gauge` - Velocímetro/gauge circular
- `Sparkline` - Mini gráfico de tendencia
- `NumericSparkline` - Valor numérico + gráfico
- `NumericGauge` - Valor numérico + gauge
- `ProgressNumeric` - Barra de progreso + valor
- `GaugeSparkline` - Gauge + gráfico