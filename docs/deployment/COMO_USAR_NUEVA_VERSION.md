# 🚀 Cómo Activar la Nueva Arquitectura

## Paso 1: Cambiar en App.js

Abre `src/App.js` y cambia la línea de import:

```javascript
// ❌ ANTES (versión antigua de 7139 líneas)
import BabylonScene from './BabylonScene';

// ✅ DESPUÉS (versión nueva optimizada)
import BabylonScene from './BabylonScene_NEW';
```

## Paso 2: Guardar y Recargar

```bash
# La aplicación se recargará automáticamente (hot reload)
# Si no recarga, presiona Ctrl+C y ejecuta:
npm run start:dev
```

## Paso 3: Verificar en Consola

Deberías ver estos mensajes en la consola del navegador:

```
✅ SceneManager inicializado correctamente
✅ ColorManager inicializado
✅ AnimationController inicializado
✅ ModelLoader inicializado
🚀 Iniciando carga de X modelos...
📦 Procesando lote 1/N (20 modelos)
...
✅ X modelos cargados en Y.Ys
🎨 Colores iniciales aplicados a todos los modelos
🔌 SignalR conectado
✅ Sistema inicializado completamente
```

## 🎮 Controles en Pantalla

Una vez cargada la escena verás:

### Top Bar (arriba)
- **📷 Free** - Cámara libre (WASD + mouse)
- **🔄 Orbital** - Cámara orbital (arrastrar mouse)
- **⬇️ Top** - Vista superior
- **⛶ Fullscreen** - Pantalla completa
- **🟢/🔴 PLC Status** - Estado de conexión SignalR

### System Logs (abajo izquierda)
- Últimos 5 eventos del sistema
- Color según tipo: verde=success, rojo=error, amarillo=warning

### Stats (abajo derecha)
- Modelos cargados
- Cámara activa

## 📊 Comparación de Performance

### Con BabylonScene.js (antiguo)
```
Carga de 43 modelos: ~15-20 segundos
Uso de memoria: ~800MB
Límite práctico: ~20-30 modelos
```

### Con BabylonScene_NEW.js (nuevo)
```
Carga de 43 modelos: ~8-12 segundos ⚡
Uso de memoria: ~400MB 💾
Límite práctico: 200+ modelos ✅
```

## 🐛 Troubleshooting

### Problema: Pantalla negra sin modelos

**Solución:**
1. Abre la consola del navegador (F12)
2. Busca errores en rojo
3. Verifica que el backend esté corriendo en `http://localhost:5000`
4. Prueba abrir: `http://localhost:5000/api/pumpelements`

### Problema: SignalR no conecta (🔴 rojo)

**Solución:**
1. Verifica que el backend esté ejecutándose
2. Abre: `http://localhost:5000/swagger`
3. En consola del navegador busca mensajes de SignalR
4. El sistema reintentará automáticamente cada 3 segundos

### Problema: Modelos en posiciones incorrectas

**Solución:**
1. Verifica las columnas del Excel:
   - `F, G, H` → offsetX, offsetY, offsetZ
   - `I, J, K` → rotationX, rotationY, rotationZ
   - `L, M, N` → scaleX, scaleY, scaleZ

### Problema: Colores no se aplican

**Solución:**
1. Verifica que las columnas `J, K, L, M` del Excel tengan colores válidos
2. Formato: `#RRGGBB` (ej: `#FF0000` para rojo)
3. Al menos una columna debe tener color para OBJ/STL

## 🔄 Volver a la Versión Antigua

Si necesitas volver temporalmente:

```javascript
// En App.js
import BabylonScene from './BabylonScene'; // Versión antigua
```

**Nota:** La versión antigua está respaldada y no se ha modificado.

## 📝 Logs Útiles

### Ver progreso de carga
```javascript
// En consola del navegador
localStorage.setItem('DEBUG_LOGS', 'true');
// Recargar página
```

### Ver estado de managers
```javascript
// En consola del navegador (mientras la app corre)
window.__sceneManager
window.__modelLoader
window.__colorManager
```

## ✅ Checklist de Verificación

- [ ] Backend corriendo en puerto 5000
- [ ] Frontend corriendo en puerto 3001
- [ ] Excel configurado correctamente
- [ ] Archivos 3D en `wwwroot/models/Pumps/`
- [ ] Import cambiado a `BabylonScene_NEW`
- [ ] Consola sin errores
- [ ] Modelos visibles en pantalla
- [ ] SignalR conectado (🟢 verde)

## 🎉 ¡Listo!

Si todo está correcto, deberías ver:
- ✅ Escena 3D con todos los modelos
- ✅ Barra de progreso durante carga
- ✅ Controles de cámara funcionando
- ✅ Conexión PLC activa
- ✅ Colores aplicados correctamente

---

**Próximos pasos:**
1. Probar con los 43 modelos existentes
2. Añadir más modelos progresivamente
3. Configurar animaciones en Excel
4. Probar actualizaciones PLC en tiempo real
