# 📡 Ejemplo de API Backend para Modelos 3D

## Configuración en el Frontend

En `BabylonScene.js` (líneas 5-6):

```javascript
const ENABLE_BACKEND = false; // Cambiar a true cuando el backend esté listo
const BACKEND_URL = 'http://localhost:3001/api'; // URL de tu backend
```

### Formatos de modelo soportados

- STL (requiere `@babylonjs/loaders/STL`)
- OBJ (requiere `@babylonjs/loaders/OBJ/objFileLoader`)
- GLTF/GLB (requiere `@babylonjs/loaders/glTF`)

Nota: Para GLTF/GLB se respetan los materiales PBR del archivo por defecto (no se sobreescriben con `StandardMaterial`). Para aplicar color en GLTF/GLB, el backend debe enviar `colorize: true` y, opcionalmente, `applyColorMode: "tint" | "override"`.

## Endpoint Requerido

### `GET /api/models`

Debe devolver un array JSON con la configuración de los modelos 3D.

## Ejemplo de Respuesta del Backend

```json
[
  {
    "name": "STATION_01",
    "file": "CONTROL ROOM A85.stl",
    "position": {
      "x": 0,
      "y": 0,
      "z": 0
    },
    "scale": 0.1,
    "color": {
      "r": 0.7,
      "g": 0.2,
      "b": 0.2
    },
    "isMovable": false,
    "animation": null
  },
  {
    "name": "PART_01",
    "file": "SUELO.glb",
    "position": { "x": 0, "y": 0, "z": 0 },
    "scale": 0.1,
    "color": { "r": 0.2, "g": 0.2, "b": 0.7 },
    "colorize": true,
    "applyColorMode": "tint",
    "isMovable": false,
    "animation": null
  },
  {
    "name": "TRAIN_01",
    "file": "TRAIN_01.stl",
    "position": {
      "x": 0,
      "y": 0,
      "z": 0
    },
    "scale": 0.1,
    "color": {
      "r": 0.2,
      "g": 0.7,
      "b": 0.2
    },
    "isMovable": true,
    "animation": {
      "enabled": true,
      "speed": 0.5,
      "axis": "x",
      "maxDistance": 100,
      "direction": 1
    }
  }
]
```

## Descripción de Campos

### Campos Principales
- **name** (string): Identificador único del modelo
- **file** (string): Nombre del archivo STL (debe estar en `/public/models/`)
- **position** (object): Posición inicial en coordenadas 3D
  - x, y, z (number): Coordenadas
- **scale** (number): Factor de escala (0.1 = 10% del tamaño original)
- **color** (object): Color RGB del modelo (valores entre 0 y 1)
  - r, g, b (number): Componentes de color
- **colorize** (boolean, opcional): Si se debe aplicar color al modelo
  - Para GLTF/GLB: por defecto es false (se preserva el PBR); ponlo en true para tinte/override
- **applyColorMode** (string, opcional): Modo de color cuando `colorize` es true
  - `tint`: tinte multiplicativo (conserva texturas)
  - `override`: color plano (elimina albedoTexture)
- **isMovable** (boolean): Si el modelo puede moverse/animarse
- **animation** (object|null): Configuración de animación (null si no se anima)

### Campos de Animación
- **enabled** (boolean): Activar/desactivar animación
- **speed** (number): Velocidad de movimiento (unidades por frame)
- **axis** (string): Eje de movimiento: "x", "y" o "z"
- **maxDistance** (number): Distancia máxima antes de volver al origen
- **direction** (number): Dirección del movimiento (1 = adelante, -1 = atrás)

## Ejemplo en Node.js/Express

```javascript
// backend/routes/models.js
const express = require('express');
const router = express.Router();

router.get('/models', (req, res) => {
  const models = [
    {
      name: "STATION_01",
      file: "CONTROL ROOM A85.stl",
      position: { x: 0, y: 0, z: 0 },
      scale: 0.1,
      color: { r: 0.7, g: 0.2, b: 0.2 },
      isMovable: false,
      animation: null
    },
    {
      name: "PART_01",
      file: "SUELO.glb",
      position: { x: 0, y: 0, z: 0 },
      scale: 0.1,
      color: { r: 0.2, g: 0.2, b: 0.7 },
      colorize: true,
      applyColorMode: 'tint',
      isMovable: false,
      animation: null
    },
    {
      name: "TRAIN_01",
      file: "TRAIN_01.stl",
      position: { x: 0, y: 0, z: 0 },
      scale: 0.1,
      color: { r: 0.2, g: 0.7, b: 0.2 },
      isMovable: true,
      animation: {
        enabled: true,
        speed: 0.5,
        axis: "x",
        maxDistance: 100,
        direction: 1
      }
    }
  ];

  res.json(models);
});

module.exports = router;
```

## Pruebas

### Modo Simulación (Actual)
```javascript
const ENABLE_BACKEND = false; // Usa datos hardcoded
```

### Modo Backend
```javascript
const ENABLE_BACKEND = true; // Usa datos del servidor
```

## CORS

No olvides configurar CORS en tu backend:

```javascript
// backend/server.js
const cors = require('cors');
app.use(cors({
  origin: 'http://localhost:3000'
}));
```

---

## 🚀 Habilitar GPT-5 globalmente (opción A: control en backend)

Esta app de frontend consumirá el modelo que tu backend determine. La forma más segura de habilitar/deshabilitar GPT-5 para todos los clientes es mediante una variable de entorno en tu servidor.

### 1) Bandera en servidor

```bash
# .env del backend
ENABLE_GPT5=true
DEFAULT_MODEL=gpt-4o-mini
GPT5_MODEL=gpt-5
```

### 2) Selección centralizada del modelo (Node.js/Express)

```javascript
// backend/services/aiModelSelector.js
function getCurrentModel() {
  const enableGpt5 = String(process.env.ENABLE_GPT5).toLowerCase() === 'true';
  const defaultModel = process.env.DEFAULT_MODEL || 'gpt-4o-mini';
  const gpt5Model = process.env.GPT5_MODEL || 'gpt-5';
  return enableGpt5 ? gpt5Model : defaultModel;
}

module.exports = { getCurrentModel };
```

```javascript
// backend/routes/ai.js
const express = require('express');
const router = express.Router();
const { getCurrentModel } = require('../services/aiModelSelector');

// Endpoint opcional para que el frontend conozca el modelo activo
router.get('/ai/model', (req, res) => {
  res.json({ model: getCurrentModel() });
});

// Ejemplo de uso en una ruta de inferencia
router.post('/ai/complete', async (req, res) => {
  try {
    const model = getCurrentModel();
    const { prompt } = req.body;
    // Llama a tu proveedor con `model`
    // const result = await provider.complete({ model, prompt });
    res.json({ model, completion: '...respuesta...' });
  } catch (err) {
    console.error(err);
    res.status(500).json({ error: 'AI request failed' });
  }
});

module.exports = router;
```

### 3) Contrato simple para el frontend

- GET `/api/ai/model` → `{ model: "gpt-5" }` o `{ model: "gpt-4o-mini" }`
- Si el endpoint no existe, el frontend usará su valor por defecto sin romper.

### 4) Despliegue y rollback

- Habilitar: `ENABLE_GPT5=true` → todos los clientes usan GPT-5 sin cambiar el frontend
- Rollback: `ENABLE_GPT5=false` → vuelve a `DEFAULT_MODEL`
- Añade logs a nivel backend para trazabilidad (quién, cuándo, qué modelo)

### 5) Pruebas recomendadas

- Unit test del selector de modelo con `ENABLE_GPT5=true/false`
- E2E de una llamada `/ai/complete` verificando el modelo usado

## Actualización en Tiempo Real (Futuro)

Para actualizar los modelos en tiempo real, podrías:

1. **Polling**: Llamar al endpoint cada X segundos
2. **WebSockets**: Usar Socket.io para actualizaciones en tiempo real
3. **Server-Sent Events (SSE)**: Para actualizaciones unidireccionales

Ejemplo con polling:

```javascript
// En BabylonScene.js
useEffect(() => {
  if (ENABLE_BACKEND) {
    const interval = setInterval(() => {
      loadModelsFromBackend();
    }, 5000); // Actualizar cada 5 segundos

    return () => clearInterval(interval);
  }
}, [ENABLE_BACKEND]);
```
