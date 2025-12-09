# 🎨 Integración Frontend - Sistema de Pump Elements

## 📋 Objetivo

Modificar `BabylonScene.js` para cargar modelos 3D desde la API `/api/pumpelements` en lugar de usar datos hardcodeados.

---

## 🔧 Pasos de Integración

### 1. **Añadir función de carga desde API**

Buscar en `BabylonScene.js` donde se inicializan los modelos y añadir:

```javascript
// Función para cargar pump elements desde backend
const loadPumpElementsFromBackend = async () => {
  try {
    console.log('🔄 Cargando elementos desde backend...');
    
    const response = await fetch('http://localhost:5000/api/pumpelements');
    
    if (!response.ok) {
      throw new Error(`HTTP error! status: ${response.status}`);
    }
    
    const pumpElements = await response.json();
    console.log(`✅ Cargados ${pumpElements.length} elementos pump desde Excel`);
    
    return pumpElements;
  } catch (error) {
    console.error('❌ Error cargando pump elements:', error);
    return [];
  }
};
```

### 2. **Convertir pump elements a formato de modelos**

```javascript
// Mapear pump elements a formato de modelos 3D
const mapPumpElementsToModels = (pumpElements) => {
  return pumpElements.map(element => ({
    // Identificación
    name: element.name,
    file: element.fileName, // ej: "Pumps/PUMP_01.OBJ"
    
    // Posición
    position: new BABYLON.Vector3(
      element.offsetX,
      element.offsetY,
      element.offsetZ
    ),
    
    // Rotación (si existe)
    rotation: new BABYLON.Vector3(
      BABYLON.Tools.ToRadians(element.rotationX || 0),
      BABYLON.Tools.ToRadians(element.rotationY || 0),
      BABYLON.Tools.ToRadians(element.rotationZ || 0)
    ),
    
    // Escala
    scaling: new BABYLON.Vector3(
      element.scaleX || 1,
      element.scaleY || 1,
      element.scaleZ || 1
    ),
    
    // Color inicial (estado OFF por defecto)
    color: colorNameToColor3(element.colorElementOff),
    
    // Configuración de comportamiento
    colorize: true,
    applyColorMode: 'override',
    isClickable: element.isClickable,
    showTooltip: element.showTooltip,
    
    // Datos PLC para cambios de color dinámicos
    plcData: {
      mainReference: element.plcMainPageReference,
      manualReference: element.plcManualPageReference,
      configReference: element.plcConfigPageReference,
      colorOn: element.colorElementOn,
      colorOff: element.colorElementOff,
      colorDisabled: element.colorElementDisabled,
      colorAlarm: element.colorElementAlarm
    },
    
    // Datos de label
    label: {
      text: element.elementNameDescription,
      fontSize: element.labelFontSize,
      position1: new BABYLON.Vector3(
        element.labelOffsetX_Pos1,
        element.labelOffsetY_Pos1,
        element.labelOffsetZ_Pos1
      ),
      position2: new BABYLON.Vector3(
        element.labelOffsetX_Pos2,
        element.labelOffsetY_Pos2,
        element.labelOffsetZ_Pos2
      )
    },
    
    // Metadatos
    category: element.category,
    layer: element.layer,
    initiallyVisible: element.initiallyVisible,
    
    // Animación
    animation: {
      type: element.animationType,
      speed: element.animationSpeed,
      onlyWhenOn: element.animateOnlyWhenOn
    },
    
    // Hijos (offsprings)
    children: element.children || []
  }));
};
```

### 3. **Función helper para convertir nombres de colores a Color3**

```javascript
// Mapeo de nombres de colores a BABYLON.Color3
const colorNameToColor3 = (colorName) => {
  const colorMap = {
    // Colores básicos
    'Red': BABYLON.Color3.Red(),
    'Green': BABYLON.Color3.Green(),
    'Blue': BABYLON.Color3.Blue(),
    'Yellow': BABYLON.Color3.Yellow(),
    'White': BABYLON.Color3.White(),
    'Black': BABYLON.Color3.Black(),
    'Gray': BABYLON.Color3.Gray(),
    'Purple': BABYLON.Color3.Purple(),
    'Magenta': BABYLON.Color3.Magenta(),
    'Teal': BABYLON.Color3.Teal(),
    
    // Colores CSS comunes
    'Lime': new BABYLON.Color3(0, 1, 0), // Verde brillante
    'Cyan': new BABYLON.Color3(0, 1, 1),
    'Orange': new BABYLON.Color3(1, 0.5, 0),
    'Violet': new BABYLON.Color3(0.5, 0, 1),
    'Pink': new BABYLON.Color3(1, 0.75, 0.8),
    'Brown': new BABYLON.Color3(0.6, 0.3, 0.1),
    'AliceBlue': new BABYLON.Color3(0.94, 0.97, 1),
    'Gold': new BABYLON.Color3(1, 0.84, 0),
    'Silver': new BABYLON.Color3(0.75, 0.75, 0.75),
    
    // Tonos industriales
    'DarkGray': new BABYLON.Color3(0.3, 0.3, 0.3),
    'LightGray': new BABYLON.Color3(0.8, 0.8, 0.8),
    'Navy': new BABYLON.Color3(0, 0, 0.5),
    'Maroon': new BABYLON.Color3(0.5, 0, 0),
    'Olive': new BABYLON.Color3(0.5, 0.5, 0),
  };
  
  // Buscar color (case insensitive)
  const normalizedName = colorName?.trim();
  const foundColor = Object.keys(colorMap).find(
    key => key.toLowerCase() === normalizedName?.toLowerCase()
  );
  
  if (foundColor) {
    return colorMap[foundColor];
  }
  
  // Si es un hex color (#RRGGBB)
  if (normalizedName?.startsWith('#')) {
    return hexToColor3(normalizedName);
  }
  
  // Default: gris
  console.warn(`⚠️ Color desconocido: "${colorName}", usando Gray`);
  return BABYLON.Color3.Gray();
};

// Convertir hex a Color3
const hexToColor3 = (hex) => {
  const result = /^#?([a-f\d]{2})([a-f\d]{2})([a-f\d]{2})$/i.exec(hex);
  if (!result) return null;
  
  return new BABYLON.Color3(
    parseInt(result[1], 16) / 255,
    parseInt(result[2], 16) / 255,
    parseInt(result[3], 16) / 255
  );
};
```

### 4. **Integrar en useEffect principal**

Buscar el `useEffect` donde se inicializa la escena y modificar:

```javascript
useEffect(() => {
  // ... código existente de inicialización de escena ...
  
  // Cargar modelos desde backend
  const loadModels = async () => {
    // Cargar pump elements desde API
    const pumpElements = await loadPumpElementsFromBackend();
    
    if (pumpElements.length > 0) {
      // Convertir a formato de modelos
      const modelsFromBackend = mapPumpElementsToModels(pumpElements);
      
      // Establecer estado de modelos
      setModels(modelsFromBackend);
      
      console.log(`✅ ${modelsFromBackend.length} modelos cargados desde Excel`);
    } else {
      console.warn('⚠️ No se cargaron modelos desde backend, usando modelos por defecto');
      // Aquí puedes mantener modelos hardcodeados como fallback
    }
  };
  
  loadModels();
  
  // ... resto del código ...
}, []);
```

### 5. **Configurar SignalR para cambios dinámicos de color**

```javascript
// Listener para cambios de PLC
useEffect(() => {
  if (!signalRService.connection) return;
  
  const handlePlcUpdate = (data) => {
    const { variableName, value } = data;
    
    // Buscar modelo que corresponda a esta variable PLC
    const modelToUpdate = models.find(
      m => m.plcData?.mainReference === variableName
    );
    
    if (!modelToUpdate) return;
    
    // Determinar color según estado PLC
    let newColor;
    switch (value) {
      case 0: // Disabled
        newColor = colorNameToColor3(modelToUpdate.plcData.colorDisabled);
        break;
      case 1: // Off
        newColor = colorNameToColor3(modelToUpdate.plcData.colorOff);
        break;
      case 2: // On
        newColor = colorNameToColor3(modelToUpdate.plcData.colorOn);
        // Iniciar animación si está configurada
        if (modelToUpdate.animation?.onlyWhenOn && modelToUpdate.animation.type !== 'none') {
          startModelAnimation(modelToUpdate.name, modelToUpdate.animation);
        }
        break;
      case 3: // Alarm
        newColor = colorNameToColor3(modelToUpdate.plcData.colorAlarm);
        // Mostrar label de alarma
        showAlarmLabel(modelToUpdate);
        break;
      default:
        return;
    }
    
    // Aplicar color al modelo
    updateModelColor(modelToUpdate.name, newColor);
    
    console.log(`🎨 Color actualizado: ${modelToUpdate.name} → Estado ${value}`);
  };
  
  signalRService.connection.on('PlcDataUpdate', handlePlcUpdate);
  
  return () => {
    signalRService.connection.off('PlcDataUpdate', handlePlcUpdate);
  };
}, [models]);
```

---

## 🧪 Testing

### 1. **Verificar carga inicial**

Abrir consola del navegador y buscar:
```
🔄 Cargando elementos desde backend...
✅ Cargados N elementos pump desde Excel
✅ N modelos cargados desde Excel
```

### 2. **Verificar que los modelos aparecen en escena**

Comprobar que los modelos se cargan con:
- Posición correcta (offsetX/Y/Z)
- Color inicial (colorElementOff por defecto)
- Escala y rotación aplicadas

### 3. **Verificar cambios dinámicos de PLC**

Si SignalR está conectado, los cambios en variables PLC deben actualizar los colores automáticamente.

---

## 📝 Checklist de Integración

- [ ] Añadir `loadPumpElementsFromBackend()`
- [ ] Añadir `mapPumpElementsToModels()`
- [ ] Añadir `colorNameToColor3()` y `hexToColor3()`
- [ ] Modificar useEffect principal para cargar desde API
- [ ] Configurar listener SignalR para updates de PLC
- [ ] Probar carga inicial de modelos
- [ ] Verificar posiciones y colores
- [ ] Probar cambios dinámicos de color (si PLC disponible)

---

## 🎯 Resultado Esperado

Cuando añadas modelos al Excel y reinicies el frontend:

1. **Frontend llama** → `GET http://localhost:5000/api/pumpelements`
2. **Backend responde** con JSON de elementos desde Excel
3. **Frontend mapea** pump elements a modelos 3D
4. **Babylon.js carga** los archivos 3D especificados
5. **Aplica** posiciones, rotaciones, escalas, colores
6. **SignalR escucha** cambios de variables PLC
7. **Actualiza colores** dinámicamente según estado

---

## 🔍 Debug

Si algo falla:

```javascript
// Añadir logs detallados
console.log('📦 Pump Elements recibidos:', pumpElements);
console.log('🎨 Modelos mapeados:', modelsFromBackend);
console.log('🔗 Archivo a cargar:', model.file);
console.log('📍 Posición:', model.position);
```

**Errores comunes:**
- ❌ **404 en modelo**: Verificar que el archivo exista en `wwwroot/models/`
- ❌ **Color negro**: Verificar nombre de color en `colorNameToColor3()`
- ❌ **Posición incorrecta**: Verificar offsetX/Y/Z en Excel
- ❌ **CORS error**: Verificar backend está en puerto 5000 con CORS habilitado

---

## 📚 Referencias

- **API Endpoint**: `GET http://localhost:5000/api/pumpelements`
- **Formato respuesta**: Ver `IMPLEMENTACION_PUMP_ELEMENTS.md`
- **Mapeo columnas**: Ver `MAPEO_COLUMNAS_EXCEL.md`
- **Babylon.js Color3**: https://doc.babylonjs.com/typedoc/classes/BABYLON.Color3
