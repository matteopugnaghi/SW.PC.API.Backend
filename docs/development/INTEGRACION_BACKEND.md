# 🔗 Guía de Integración Frontend-Backend

## ✅ Configuración Completada

### Backend (ASP.NET Core) - Puerto 5000
- ✅ API REST corriendo en `http://localhost:5000`
- ✅ SignalR Hub en `ws://localhost:5000/hubs/scada`
- ✅ CORS configurado para puertos 3000, 3001, 5173
- ✅ Endpoints disponibles:
  - `GET /api/models` - Lista de modelos 3D
  - `GET /api/models/{id}` - Obtener modelo específico
  - `GET /models/{filename}.glb` - Archivo GLB estático
  - `GET /api/config` - Configuración de la aplicación
  - `POST /api/config` - Actualizar configuración

### Frontend (React + Babylon.js) - Puerto 3000
- ✅ Archivo `.env` creado con:
  ```
  REACT_APP_ENABLE_BACKEND=true
  REACT_APP_BACKEND_URL=http://localhost:5000
  REACT_APP_ENABLE_COLOR_PANEL=true
  PORT=3000
  ```

---

## 🚀 Pasos para Probar la Integración

### 1. Iniciar el Backend (Ya está corriendo)
```powershell
# En la carpeta del backend
cd "C:\Users\mpugnaghi.AQUAFRISCH\Documents\Work_In_Process\_Web\AI test\SW.PC.API.Backend_"
dotnet run
```

Verifica que veas:
```
Now listening on: http://localhost:5000
Now listening on: https://localhost:5001
```

### 2. Iniciar el Frontend
```powershell
# En la carpeta del frontend
cd "C:\Users\mpugnaghi.AQUAFRISCH\Documents\Work_In_Process\_Web\AI test\SW.PC.REACT.Frontend\my-3d-app"
npm start
```

O usa el script con backend habilitado:
```powershell
npm run start:backend
```

El frontend se abrirá en `http://localhost:3000`

---

## 🔧 Próxima Integración Necesaria

El frontend actualmente tiene las variables de entorno configuradas pero **aún no implementa las llamadas al backend**. Necesitas añadir:

### A. Servicio API (Crear: `src/services/api.js`)

```javascript
// src/services/api.js
const API_BASE_URL = process.env.REACT_APP_BACKEND_URL || 'http://localhost:5000';

export const api = {
  // Obtener lista de modelos 3D
  async getModels() {
    try {
      const response = await fetch(`${API_BASE_URL}/api/models`);
      if (!response.ok) throw new Error('Error al obtener modelos');
      return await response.json();
    } catch (error) {
      console.error('Error en getModels:', error);
      return [];
    }
  },

  // Obtener un modelo específico
  async getModel(id) {
    try {
      const response = await fetch(`${API_BASE_URL}/api/models/${id}`);
      if (!response.ok) throw new Error('Error al obtener modelo');
      return await response.json();
    } catch (error) {
      console.error('Error en getModel:', error);
      return null;
    }
  },

  // Obtener URL del archivo GLB
  getModelFileUrl(filename) {
    return `${API_BASE_URL}/models/${filename}`;
  },

  // Obtener configuración
  async getConfig() {
    try {
      const response = await fetch(`${API_BASE_URL}/api/config`);
      if (!response.ok) throw new Error('Error al obtener configuración');
      return await response.json();
    } catch (error) {
      console.error('Error en getConfig:', error);
      return null;
    }
  },

  // Actualizar configuración
  async updateConfig(config) {
    try {
      const response = await fetch(`${API_BASE_URL}/api/config`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify(config),
      });
      if (!response.ok) throw new Error('Error al actualizar configuración');
      return await response.json();
    } catch (error) {
      console.error('Error en updateConfig:', error);
      throw error;
    }
  }
};
```

### B. SignalR Service (Crear: `src/services/signalr.js`)

Primero instala SignalR:
```powershell
npm install @microsoft/signalr
```

Luego crea el servicio:
```javascript
// src/services/signalr.js
import * as signalR from '@microsoft/signalr';

const SIGNALR_URL = process.env.REACT_APP_BACKEND_URL 
  ? `${process.env.REACT_APP_BACKEND_URL}/hubs/scada`
  : 'http://localhost:5000/hubs/scada';

class SignalRService {
  constructor() {
    this.connection = null;
    this.listeners = new Map();
  }

  async connect() {
    if (this.connection) {
      console.warn('SignalR ya está conectado');
      return;
    }

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(SIGNALR_URL)
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Information)
      .build();

    // Configurar eventos
    this.connection.onreconnecting(() => {
      console.log('🔄 SignalR reconectando...');
    });

    this.connection.onreconnected(() => {
      console.log('✅ SignalR reconectado');
    });

    this.connection.onclose(() => {
      console.log('❌ SignalR desconectado');
    });

    try {
      await this.connection.start();
      console.log('✅ SignalR conectado exitosamente');
      this.setupListeners();
    } catch (error) {
      console.error('❌ Error al conectar SignalR:', error);
      throw error;
    }
  }

  setupListeners() {
    // Escuchar actualizaciones de variables del PLC
    this.connection.on('PlcVariableUpdated', (data) => {
      console.log('📡 Variable PLC actualizada:', data);
      this.notifyListeners('PlcVariableUpdated', data);
    });

    // Escuchar actualizaciones de configuración
    this.connection.on('ConfigurationUpdated', (data) => {
      console.log('⚙️ Configuración actualizada:', data);
      this.notifyListeners('ConfigurationUpdated', data);
    });

    // Escuchar alarmas
    this.connection.on('AlarmTriggered', (data) => {
      console.log('🚨 Alarma disparada:', data);
      this.notifyListeners('AlarmTriggered', data);
    });
  }

  // Suscribirse a eventos
  on(eventName, callback) {
    if (!this.listeners.has(eventName)) {
      this.listeners.set(eventName, []);
    }
    this.listeners.get(eventName).push(callback);
  }

  // Desuscribirse de eventos
  off(eventName, callback) {
    if (!this.listeners.has(eventName)) return;
    const callbacks = this.listeners.get(eventName);
    const index = callbacks.indexOf(callback);
    if (index > -1) {
      callbacks.splice(index, 1);
    }
  }

  // Notificar a los listeners
  notifyListeners(eventName, data) {
    if (!this.listeners.has(eventName)) return;
    this.listeners.get(eventName).forEach(callback => {
      try {
        callback(data);
      } catch (error) {
        console.error(`Error en listener ${eventName}:`, error);
      }
    });
  }

  // Invocar métodos del servidor
  async invoke(methodName, ...args) {
    if (!this.connection) {
      throw new Error('SignalR no está conectado');
    }
    return await this.connection.invoke(methodName, ...args);
  }

  async disconnect() {
    if (this.connection) {
      await this.connection.stop();
      this.connection = null;
      console.log('SignalR desconectado');
    }
  }
}

export const signalRService = new SignalRService();
```

### C. Integración en BabylonScene.js

Añade al inicio del componente (después de las importaciones):

```javascript
import { api } from './services/api';
import { signalRService } from './services/signalr';
```

Luego, dentro del componente, añade useEffect para cargar datos del backend:

```javascript
// Conectar al backend si está habilitado
useEffect(() => {
  if (!ENABLE_BACKEND) {
    console.log('ℹ️ Backend deshabilitado');
    return;
  }

  let isSubscribed = true;

  const initBackend = async () => {
    try {
      // 1. Cargar configuración
      const config = await api.getConfig();
      if (isSubscribed && config) {
        console.log('✅ Configuración cargada:', config);
        // Aplicar configuración (colores, etc.)
      }

      // 2. Cargar modelos 3D disponibles
      const modelsList = await api.getModels();
      if (isSubscribed && modelsList) {
        console.log('✅ Modelos disponibles:', modelsList);
        // Puedes cargar los modelos dinámicamente aquí
      }

      // 3. Conectar SignalR para actualizaciones en tiempo real
      await signalRService.connect();

      // 4. Suscribirse a actualizaciones de variables PLC
      signalRService.on('PlcVariableUpdated', (data) => {
        console.log('📡 Actualización PLC:', data);
        // Aquí puedes actualizar el estado de tus modelos 3D
        // Por ejemplo: cambiar colores, posiciones, rotaciones, etc.
      });

      // 5. Suscribirse a alarmas
      signalRService.on('AlarmTriggered', (alarm) => {
        console.log('🚨 Nueva alarma:', alarm);
        setAlarms(prev => [alarm, ...prev]);
      });

    } catch (error) {
      console.error('❌ Error al inicializar backend:', error);
    }
  };

  initBackend();

  // Cleanup
  return () => {
    isSubscribed = false;
    signalRService.disconnect();
  };
}, [ENABLE_BACKEND]);
```

---

## 🧪 Probar la Conexión

### 1. Probar Endpoints del Backend

Abre el navegador en:
- http://localhost:5000 → Debería mostrar Swagger UI
- http://localhost:5000/api/models → Lista de modelos (puede estar vacía)
- http://localhost:5000/api/config → Configuración actual

### 2. Probar desde el Frontend

Abre la consola del navegador (F12) y ejecuta:

```javascript
// Probar API
fetch('http://localhost:5000/api/models')
  .then(r => r.json())
  .then(d => console.log('Modelos:', d));

fetch('http://localhost:5000/api/config')
  .then(r => r.json())
  .then(d => console.log('Config:', d));
```

### 3. Verificar SignalR

En la consola del navegador deberías ver:
```
✅ SignalR conectado exitosamente
```

---

## 📦 Datos de Prueba

### Añadir Modelos 3D de Prueba

1. **Descargar modelos GLB de ejemplo:**
   - https://github.com/KhronosGroup/glTF-Sample-Models/tree/master/2.0
   - Ejemplos: `Box.glb`, `Duck.glb`, `Avocado.glb`

2. **Copiar archivos GLB a:**
   ```
   SW.PC.API.Backend_\wwwroot\models\
   ```

3. **Crear archivo Excel** `ProjectConfig.xlsx` en:
   ```
   SW.PC.API.Backend_\ExcelConfigs\
   ```

   Sigue la plantilla en `PLANTILLA_EXCEL.md`

---

## 🎯 Resumen de Estado Actual

| Componente | Estado | Puerto |
|-----------|--------|--------|
| Backend API | ✅ Corriendo | 5000 |
| SignalR Hub | ✅ Activo | 5000/hubs/scada |
| Frontend React | ⏸️ Por iniciar | 3000 |
| Integración API | ⚠️ Por implementar | - |
| SignalR Client | ⚠️ Por implementar | - |
| Modelos 3D | ⚠️ Por añadir | - |
| Excel Config | ⚠️ Por crear | - |

---

## 📝 Próximos Pasos

1. ✅ Instalar SignalR en el frontend: `npm install @microsoft/signalr`
2. ✅ Crear `src/services/api.js`
3. ✅ Crear `src/services/signalr.js`
4. ✅ Integrar servicios en `BabylonScene.js`
5. ⏳ Añadir modelos GLB de prueba
6. ⏳ Crear archivo Excel de configuración
7. ⏳ Probar carga dinámica de modelos
8. ⏳ Probar actualizaciones en tiempo real vía SignalR

¿Quieres que implemente los servicios de API y SignalR ahora?
