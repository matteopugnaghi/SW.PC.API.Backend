# 📋 REFERENCIA RÁPIDA: Estructura Excel - Sistema 3D Jerárquico
**Versión**: 15 columnas por hijo (109 columnas totales: A-DE)  
**Fecha**: Noviembre 2025  
**Uso**: Guía de referencia para pruebas y configuración manual

---

## 🎯 RESUMEN RÁPIDO
- **Columnas totales**: 109 (A hasta DE)
- **Padre**: 34 columnas (A-AH)
- **Hijos**: 75 columnas (AI-DE) = 5 hijos × 15 campos cada uno
- **Cada hijo puede**: Tener su propio modelo 3D, colores, animaciones y variable PLC

---

## 📊 COLUMNAS DEL PADRE (A-AH: 34 columnas)

### 🔹 Identificación (A-C)
| Columna | Nombre Campo            | Tipo   | Ejemplo          | Descripción                          |
|---------|-------------------------|--------|------------------|--------------------------------------|
| **A**   | TotalElements           | int    | 5                | Total de elementos (solo fila 1)     |
| **B**   | Name                    | string | Tanque Principal | Nombre/descripción del elemento      |
| **C**   | FileName                | string | Pumps/PUMP_01.OBJ| Ruta del archivo 3D                  |

### 🔹 Posición 3D (D-F)
| Columna | Nombre Campo | Tipo   | Ejemplo | Descripción           |
|---------|--------------|--------|---------|-----------------------|
| **D**   | OffsetX      | double | 0.0     | Offset X en escena    |
| **E**   | OffsetY      | double | 0.0     | Offset Y en escena    |
| **F**   | OffsetZ      | double | 0.0     | Offset Z en escena    |

### 🔹 Variable PLC (G)
| Columna | Nombre Campo          | Tipo   | Ejemplo              | Descripción                     |
|---------|-----------------------|--------|----------------------|---------------------------------|
| **G**   | PlcMainPageReference  | string | GVL.Tank01_State     | Variable PLC para estado        |

### 🔹 Colores según Estado PLC (J-M)
| Columna | Nombre Campo       | Tipo   | Ejemplo | Descripción                |
|---------|--------------------|--------|---------|----------------------------|
| **J**   | ColorElementOn     | string | Lime    | Color cuando PLC = 2 (ON)  |
| **K**   | ColorElementOff    | string | Gray    | Color cuando PLC = 1 (OFF) |
| **L**   | ColorElementDisabled| string | Violet  | Color cuando PLC = 0       |
| **M**   | ColorElementAlarm  | string | Red     | Color cuando PLC = 3 (ALARMA)|

### 🔹 Label/Descripción (N)
| Columna | Nombre Campo           | Tipo   | Ejemplo      | Descripción                |
|---------|------------------------|--------|--------------|----------------------------|
| **N**   | ElementNameDescription | string | Tanque A1    | Texto del label 3D         |

### 🔹 Animación Padre (AB-AH)
| Columna | Nombre Campo         | Tipo   | Ejemplo                | Descripción                              |
|---------|----------------------|--------|------------------------|------------------------------------------|
| **AB**  | AnimationType        | string | REF PLC                | none / REF PLC / rotate / pulse / bounce |
| **AC**  | AnimationSpeed       | double | 1.0                    | Velocidad (0-10)                         |
| **AD**  | AnimateOnlyWhenOn    | bool   | TRUE                   | Solo animar cuando estado = ON           |
| **AE**  | AnimationPlcVariable | string | GVL.Tank01_Level       | Variable PLC para animación (LREAL mm)   |
| **AF**  | AnimationMinValue    | double | 0.0                    | Valor mínimo rango (mm)                  |
| **AG**  | AnimationMaxValue    | double | 1000.0                 | Valor máximo rango (mm)                  |
| **AH**  | AnimationAxis        | string | Y                      | Eje de animación (X/Y/Z)                 |

**⚠️ NOTA**: Las columnas H-I (PlcManualPageReference, PlcConfigPageReference) y O-AA (labels, offsprings, metadata) son campos heredados del formato antiguo pero ya no se usan en el sistema web actual. Solo se muestran las columnas activas relevantes.

---

## 👶 COLUMNAS DE LOS HIJOS (AI-DE: 75 columnas = 5 hijos × 15 campos)

### 🟦 HIJO 1 (AI-AW: 15 columnas)
| Columna | Nombre Campo             | Tipo   | Ejemplo                | Descripción                              |
|---------|--------------------------|--------|------------------------|------------------------------------------|
| **AI**  | Child1_Name              | string | PISTON                 | Nombre único del hijo 1                  |
| **AJ**  | Child1_ParentName        | string | TANQUE_01              | Nombre del padre (Name del padre o de otro hijo)|
| **AK**  | Child1_FileName          | string | PISTON.glb             | **Archivo 3D del hijo 1**                |
| **AL**  | Child1_AnimationType     | string | REF PLC                | none / REF PLC / rotate / pulse / bounce |
| **AM**  | Child1_AnimationSpeed    | double | 1.5                    | Velocidad animación (0-10)               |
| **AN**  | Child1_AnimateOnlyWhenOn | bool   | TRUE                   | Solo animar cuando estado padre = ON     |
| **AO**  | Child1_PlcVariable       | string | GVL.Piston01_Position  | Variable PLC (LREAL en mm)               |
| **AP**  | Child1_Axis              | string | Z                      | Eje animación (X/Y/Z)                    |
| **AQ**  | Child1_MinValue          | double | 0.0                    | Valor mínimo PLC (mm)                    |
| **AR**  | Child1_MaxValue          | double | 500.0                  | Valor máximo PLC (mm)                    |
| **AS**  | Child1_ScaleFactor       | double | 0.1                    | Factor conversión mm→Babylon             |
| **AT**  | Child1_ColorOn           | string | Cyan                   | Color cuando estado = 2 (ON)             |
| **AU**  | Child1_ColorOff          | string | DarkGray               | Color cuando estado = 1 (OFF)            |
| **AV**  | Child1_ColorDisabled     | string | Purple                 | Color cuando estado = 0                  |
| **AW**  | Child1_ColorAlarm        | string | Orange                 | Color cuando estado = 3 (ALARMA)         |

### 🟩 HIJO 2 (AX-BL: 15 columnas)
| Columna | Nombre Campo             | Tipo   | Ejemplo                | Descripción                              |
|---------|--------------------------|--------|------------------------|------------------------------------------|
| **AX**  | Child2_Name              | string | BRAZO                  | Nombre único del hijo 2                  |
| **AY**  | Child2_ParentName        | string | PISTON                 | Puede ser el padre o HIJO 1              |
| **AZ**  | Child2_FileName          | string | BRAZO.glb              | **Archivo 3D del hijo 2**                |
| **BA**  | Child2_AnimationType     | string | REF PLC                | none / REF PLC / rotate / pulse / bounce |
| **BB**  | Child2_AnimationSpeed    | double | 2.0                    | Velocidad animación                      |
| **BC**  | Child2_AnimateOnlyWhenOn | bool   | FALSE                  | Condición de animación                   |
| **BD**  | Child2_PlcVariable       | string | GVL.Arm01_Angle        | Variable PLC                             |
| **BE**  | Child2_Axis              | string | Y                      | Eje animación                            |
| **BF**  | Child2_MinValue          | double | -90.0                  | Valor mínimo PLC                         |
| **BG**  | Child2_MaxValue          | double | 90.0                   | Valor máximo PLC                         |
| **BH**  | Child2_ScaleFactor       | double | 0.1                    | Factor conversión                        |
| **BI**  | Child2_ColorOn           | string | Yellow                 | Color ON                                 |
| **BJ**  | Child2_ColorOff          | string | Brown                  | Color OFF                                |
| **BK**  | Child2_ColorDisabled     | string | Pink                   | Color DISABLED                           |
| **BL**  | Child2_ColorAlarm        | string | Magenta                | Color ALARM                              |

### 🟨 HIJO 3 (BM-CA: 15 columnas)
| Columna | Nombre Campo             | Tipo   | Ejemplo                | Descripción                              |
|---------|--------------------------|--------|------------------------|------------------------------------------|
| **BM**  | Child3_Name              | string | VALVULA                | Nombre único del hijo 3                  |
| **BN**  | Child3_ParentName        | string | BRAZO                  | Puede ser hijo anterior                  |
| **BO**  | Child3_FileName          | string | VALVULA.glb            | **Archivo 3D del hijo 3**                |
| **BP**  | Child3_AnimationType     | string | rotate                 | Animación procedural                     |
| **BQ**  | Child3_AnimationSpeed    | double | 3.0                    | Velocidad                                |
| **BR**  | Child3_AnimateOnlyWhenOn | bool   | TRUE                   | Condición                                |
| **BS**  | Child3_PlcVariable       | string | GVL.Valve01_Open       | Variable PLC                             |
| **BT**  | Child3_Axis              | string | X                      | Eje                                      |
| **BU**  | Child3_MinValue          | double | 0.0                    | Min                                      |
| **BV**  | Child3_MaxValue          | double | 100.0                  | Max                                      |
| **BW**  | Child3_ScaleFactor       | double | 0.1                    | Factor                                   |
| **BX**  | Child3_ColorOn           | string | Green                  | ON                                       |
| **BY**  | Child3_ColorOff          | string | Red                    | OFF                                      |
| **BZ**  | Child3_ColorDisabled     | string | Gray                   | DISABLED                                 |
| **CA**  | Child3_ColorAlarm        | string | Red                    | ALARM                                    |

### 🟧 HIJO 4 (CB-CP: 15 columnas)
| Columna | Nombre Campo             | Tipo   | Ejemplo                | Descripción                              |
|---------|--------------------------|--------|------------------------|------------------------------------------|
| **CB**  | Child4_Name              | string | LED                    | Nombre único del hijo 4                  |
| **CC**  | Child4_ParentName        | string | VALVULA                | Padre                                    |
| **CD**  | Child4_FileName          | string | LED.glb                | **Archivo 3D del hijo 4**                |
| **CE**  | Child4_AnimationType     | string | pulse                  | Animación                                |
| **CF**  | Child4_AnimationSpeed    | double | 5.0                    | Velocidad                                |
| **CG**  | Child4_AnimateOnlyWhenOn | bool   | TRUE                   | Condición                                |
| **CH**  | Child4_PlcVariable       | string | GVL.LED01_Status       | Variable PLC                             |
| **CI**  | Child4_Axis              | string | Y                      | Eje                                      |
| **CJ**  | Child4_MinValue          | double | 0.0                    | Min                                      |
| **CK**  | Child4_MaxValue          | double | 1.0                    | Max                                      |
| **CL**  | Child4_ScaleFactor       | double | 0.1                    | Factor                                   |
| **CM**  | Child4_ColorOn           | string | White                  | ON                                       |
| **CN**  | Child4_ColorOff          | string | Black                  | OFF                                      |
| **CO**  | Child4_ColorDisabled     | string | Gray                   | DISABLED                                 |
| **CP**  | Child4_ColorAlarm        | string | Red                    | ALARM                                    |

### 🟥 HIJO 5 (CQ-DE: 15 columnas)
| Columna | Nombre Campo             | Tipo   | Ejemplo                | Descripción                              |
|---------|--------------------------|--------|------------------------|------------------------------------------|
| **CQ**  | Child5_Name              | string | SENSOR                 | Nombre único del hijo 5                  |
| **CR**  | Child5_ParentName        | string | LED                    | Padre                                    |
| **CS**  | Child5_FileName          | string | SENSOR.glb             | **Archivo 3D del hijo 5**                |
| **CT**  | Child5_AnimationType     | string | bounce                 | Animación                                |
| **CU**  | Child5_AnimationSpeed    | double | 1.0                    | Velocidad                                |
| **CV**  | Child5_AnimateOnlyWhenOn | bool   | FALSE                  | Condición                                |
| **CW**  | Child5_PlcVariable       | string | GVL.Sensor01_Detect    | Variable PLC                             |
| **CX**  | Child5_Axis              | string | Z                      | Eje                                      |
| **CY**  | Child5_MinValue          | double | 0.0                    | Min                                      |
| **CZ**  | Child5_MaxValue          | double | 10.0                   | Max                                      |
| **DA**  | Child5_ScaleFactor       | double | 0.1                    | Factor                                   |
| **DB**  | Child5_ColorOn           | string | Blue                   | ON                                       |
| **DC**  | Child5_ColorOff          | string | Navy                   | OFF                                      |
| **DD**  | Child5_ColorDisabled     | string | LightGray              | DISABLED                                 |
| **DE**  | Child5_ColorAlarm        | string | Red                    | ALARM                                    |

---

## 🎨 VALORES DE COLOR VÁLIDOS
```
Lime, Gray, Violet, Red, Orange, Yellow, Green, Blue, Cyan, Magenta,
Pink, Purple, Brown, Navy, White, Black, DarkGray, LightGray
```

---

## 🎬 TIPOS DE ANIMACIÓN VÁLIDOS

| Tipo                    | Descripción                                      | Requiere PlcVariable |
|-------------------------|--------------------------------------------------|----------------------|
| **none**                | Sin animación                                    | No                   |
| **REF PLC**             | Control directo desde PLC (LREAL en mm)          | Sí                   |
| **rotate**              | Rotación continua procedural                     | No                   |
| **pulse**               | Pulsación (escala) procedural                    | No                   |
| **bounce**              | Rebote (posición) procedural                     | No                   |

---

## 📐 EJEMPLO COMPLETO DE JERARQUÍA

```
TANQUE_01 (Pumps/PUMP_01.OBJ) - columna B
    └─ PISTON (PISTON.glb) - columna AI (Child1_Name)
         └─ BRAZO (BRAZO.glb) - columna AX (Child2_Name, parentName=PISTON)
              └─ VALVULA (VALVULA.glb) - columna BM (Child3_Name, parentName=BRAZO)
                   └─ LED (LED.glb) - columna CB (Child4_Name, parentName=VALVULA)
                        └─ SENSOR (SENSOR.glb) - columna CQ (Child5_Name, parentName=LED)
```

**Fila Excel ejemplo**:

| A | B         | C               | G                  | J    | AI     | AJ        | AK         | AX    | AY     | AZ        |
|---|-----------|-----------------|--------------------|----- |--------|-----------|------------|-------|--------|-----------|
| 5 | TANQUE_01 | Pumps/PUMP_01.OBJ| GVL.Tank01_State  | Lime | PISTON | TANQUE_01 | PISTON.glb | BRAZO | PISTON | BRAZO.glb |

---

## ⚡ CONSEJOS RÁPIDOS PARA PRUEBAS

### ✅ Verificación Rápida:
1. **Columna B** (Name): Debe tener valor único
2. **Columna C** (FileName): Archivo debe existir en backend/wwwroot/models/
3. **Columnas AK, AZ, BO, CD, CS** (FileName hijos): Archivos .glb opcionales
4. **ParentName hijos (AJ, AY, BN, CC, CR)**: Debe coincidir con columna B del padre o con Name de otro hijo

### 🚫 Errores Comunes:
- ❌ FileName vacío en columna C → Modelo padre no se cargará
- ❌ ParentName incorrecto → Hijo no se vinculará en jerarquía
- ❌ AnimationType mal escrito → Se usará "none" por defecto
- ❌ Color inválido → Se usará color por defecto del código
- ⚠️ FileName hijo vacío → OK, solo será TransformNode virtual (invisible pero funcional)
- ❌ PlcVariable con AnimationType="REF PLC" vacío → Animación no funcionará

### 🔍 Depuración:
- **Console navegador**: `📦 Modelo 3D cargado para hijo: PISTON`
- **Error carga**: `⚠️ No se pudo cargar modelo para hijo PISTON`
- **Logs backend**: Muestra lectura de 109 columnas (A-DE)

---

## 📋 PLANTILLA MÍNIMA PARA PRUEBA RÁPIDA

### Ejemplo sin hijos:
| A | B       | C               | D   | E   | F   | G                | J    |
|---|---------|-----------------|-----|-----|-----|------------------|------|
| 1 | PUMP_01 | Pumps/PUMP_01.OBJ| 0.0 | 0.0 | 0.0 | GVL.Pump01_State | Lime |

### Ejemplo con 1 hijo:
| A | B       | C               | G                | AI     | AJ      | AK         | AO                  |
|---|---------|-----------------|------------------|--------|---------|------------|---------------------|
| 1 | PUMP_01 | Pumps/PUMP_01.OBJ| GVL.Pump01_State | PISTON | PUMP_01 | PISTON.glb | GVL.Piston01_Pos    |

---

**📌 VERSIÓN**: 15 columnas por hijo (34 padre + 75 hijos = 109 totales A-DE)  
**📅 ÚLTIMA ACTUALIZACIÓN**: Noviembre 2025  
**🎯 USO**: Imprimir y tener a mano durante configuración manual del Excel  
**⚙️ BASADO EN**: PumpElement3D.cs (modelo backend real)
