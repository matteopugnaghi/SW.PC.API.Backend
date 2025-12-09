# 🔍 Diagnóstico: Animación PLC No Funciona

## Síntoma
El modelo está configurado correctamente pero no se mueve y no se reciben datos de la variable PLC.

## ✅ Checklist de Verificación

### 1. Backend - Verificar Servicio TwinCAT

**Ubicación**: `Services/TwinCATService.cs`

#### Opción A: Modo Simulación (Desarrollo)
Si estás en desarrollo, el backend tiene un modo de simulación que genera valores automáticos.

**Verificar si está activo**:
```csharp
// En TwinCATService.cs, buscar:
private readonly bool _simulationMode = true; // ¿Está en true?
```

Si `_simulationMode = true`, el sistema debería generar valores aleatorios automáticamente.

#### Opción B: Conexión Real a TwinCAT
Si quieres conectarte al PLC real:

1. **Verificar configuración** en `appsettings.json`:
```json
{
  "TwinCAT": {
    "AmsNetId": "127.0.0.1.1.1",
    "Port": 851,
    "SimulationMode": false  // ¿Está en false para modo real?
  }
}
```

2. **Verificar que TwinCAT esté ejecutándose**:
   - Abrir TwinCAT XAE
   - Sistema debe estar en modo "Run" (no "Config")
   - Variable debe existir en el árbol de variables

### 2. Backend - Verificar Variable en Excel

**Archivo**: `ExcelConfigs/PumpElements.xlsx` (o tu archivo Excel)

**Columnas críticas**:
- **Columna U**: `REF PLC` (exactamente este texto, sin espacios extra)
- **Columna AD**: `MAIN.fbMachine.st_MainForm.i_Train Position` (nombre EXACTO de variable TwinCAT)
- **Columna AE**: `0` (mínimo)
- **Columna AF**: `1000` (máximo)
- **Columna AG**: `X` (eje)

**¿Cómo verificar?**
1. Abrir Excel
2. Hoja "1) Pumps" (o como se llame tu hoja)
3. Fila del modelo "tank"
4. Verificar cada columna

### 3. Backend - Logs del Servicio

**Al iniciar el backend**, deberías ver en la consola:

```
✅ PlcNotificationService iniciado (simulación)
🔄 Variables registradas para notificación:
   - MAIN.fbMachine.st_MainForm.i_Train Position (LREAL)
```

Si ves `❌ Error al registrar variable`, hay un problema.

### 4. Frontend - Verificar Conexión SignalR

**En la consola del navegador** (F12 → Console), buscar:

```
✅ SignalR conectado exitosamente
✅ Listeners de SignalR configurados
```

Si ves `❌ Error al conectar SignalR`, el backend no está accesible.

### 5. Frontend - Verificar Modelo Vinculado

**En la consola del navegador**, buscar al cargar:

```
🎬 Modelo tank vinculado a animación PLC: MAIN.fbMachine.st_MainForm.i_Train Position (0-1000mm en eje X)
```

Si NO ves este mensaje:
- ❌ La columna U NO dice "REF PLC"
- ❌ El backend no envió la configuración correctamente

### 6. Frontend - Verificar Recepción de Datos

**En la consola del navegador**, deberías ver periódicamente:

```
📡 Variable PLC actualizada: {
  variable: "MAIN.fbMachine.st_MainForm.i_Train Position",
  value: 123.45,
  timestamp: "10:30:15",
  esAnimacion: true
}

🎬 [DEBUG] Llamando a updateModelAnimationFromPlcData...
🎬 [ANIMATION DEBUG] updateModelAnimationFromPlcData llamado: {
  variableName: "MAIN.fbMachine.st_MainForm.i_Train Position",
  rawValue: 123.45,
  valueInMm: 123.45
}

🎬 Moviendo tank_transform en eje X: 123.5mm (rango: 0-1000mm)
  ✅ Posición X = 123.5mm
```

## 🔧 Soluciones Según el Problema

### Problema 1: NO aparece "📡 Variable PLC actualizada"
**Causa**: El backend NO está enviando datos.

**Solución**:
1. **Verificar backend ejecutándose**: ¿Ves "Now listening on: http://localhost:5000"?
2. **Verificar PlcNotificationService**:
   - Buscar en logs del backend: "PlcNotificationService iniciado"
   - Si dice "Error al iniciar", revisar TwinCAT

3. **Forzar modo simulación** (temporal para probar):
   ```csharp
   // En TwinCATService.cs, cambiar:
   private readonly bool _simulationMode = true;
   ```

### Problema 2: Aparece "📡 Variable PLC actualizada" pero NO "🎬 [ANIMATION DEBUG]"
**Causa**: El nombre de variable no coincide.

**Solución**:
1. Copiar el nombre EXACTO de la consola del log "📡 Variable PLC actualizada"
2. Pegar en columna AD del Excel
3. Guardar Excel
4. Reiniciar backend

### Problema 3: Aparece "🎬 [ANIMATION DEBUG]" pero NO "🎬 Moviendo tank_transform"
**Causa**: El modelo no está vinculado correctamente.

**Solución**:
1. Verificar que columna U = "REF PLC" (con mayúsculas)
2. Verificar que al cargar aparece: "🎬 Modelo tank vinculado a animación PLC"
3. Si NO aparece, recargar página (F5)

### Problema 4: Aparece "🎬 Moviendo tank_transform" pero el modelo NO se mueve visualmente
**Causa**: Problema con la jerarquía del modelo 3D.

**Solución temporal - Probar con otro eje**:
1. Cambiar columna AG de `X` a `Y`
2. Guardar Excel
3. Reiniciar backend
4. Recargar frontend (F5)

## 🧪 Prueba Rápida: Simular Valor Manualmente

Si quieres probar sin esperar al PLC, puedes simular un valor en la consola del navegador:

```javascript
// En la consola del navegador (F12):
const testData = {
  variableName: "MAIN.fbMachine.st_MainForm.i_Train Position",
  name: "MAIN.fbMachine.st_MainForm.i_Train Position",
  value: 500
};

// Buscar la función en el scope global (si está disponible)
// O copiar el valor y cambiar la variable en TwinCAT
console.log("Datos de prueba:", testData);
```

## 📊 Estado Esperado del Sistema

### Backend en Modo Simulación
```
✅ PlcNotificationService iniciado (simulación)
🔄 Simulando cambio de variable: MAIN.fbMachine.st_MainForm.i_Train Position = 123.45
📤 Enviando actualización SignalR...
```

### Backend en Modo Real
```
✅ PlcNotificationService iniciado (TwinCAT)
🔌 Conectado a TwinCAT: 127.0.0.1.1.1:851
📥 Variable actualizada desde PLC: MAIN.fbMachine.st_MainForm.i_Train Position = 123.45
📤 Enviando actualización SignalR...
```

### Frontend
```
✅ SignalR conectado exitosamente
🎬 Modelo tank vinculado a animación PLC: ... (0-1000mm en eje X)
📡 Variable PLC actualizada: { variable: "...", value: 123.45 }
🎬 Moviendo tank_transform en eje X: 123.5mm (rango: 0-1000mm)
```

## 🆘 Si Nada Funciona

1. **Verificar URL del backend**:
   - Frontend: `http://localhost:3001`
   - Backend: `http://localhost:5000`
   - SignalR: `http://localhost:5000/hubs/scada`

2. **Reiniciar todo**:
   ```powershell
   # Terminal 1 (Backend)
   Ctrl+C
   dotnet run
   
   # Terminal 2 (Frontend)
   Ctrl+C
   npm run start:dev
   ```

3. **Verificar firewall**: ¿Bloquea puerto 5000?

4. **Verificar CORS**: En logs del backend, buscar "CORS policy"

## 📝 Información para Reportar Problema

Si sigues con problemas, copia esto:

**Logs del Backend** (últimas 20 líneas):
```
[copiar aquí]
```

**Logs del Frontend** (consola, filtrar por 🎬 o 📡):
```
[copiar aquí]
```

**Configuración Excel**:
- Columna U (Animation Type): [valor]
- Columna AD (PLC Variable): [valor]
- Columna AE (Min): [valor]
- Columna AF (Max): [valor]
- Columna AG (Axis): [valor]
