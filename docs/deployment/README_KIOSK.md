# 🏭 Configuración de Kiosk - Aquafrisch Supervisor

## Archivos Incluidos

1. **LaunchKiosk.bat** - Versión simple
   - Lanza el navegador en modo kiosk
   - Si se cierra el navegador, lo vuelve a lanzar automáticamente
   - Log en `kiosk_log.txt`

2. **LaunchKioskWithWatchdog.bat** - Versión con Watchdog (RECOMENDADA)
   - Todo lo anterior, más:
   - Verifica cada 60 segundos si el backend responde
   - Si falla 5 veces seguidas (5 minutos), REINICIA el equipo
   - Detecta si el navegador se cerró y lo relanza

## Configuración en Registry (Shell Launcher)

### Paso 1: Abrir Registry Editor
```
regedit
```

### Paso 2: Navegar a la clave de Shell
```
HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon
```

### Paso 3: Modificar el valor "Shell"
- Valor original: `explorer.exe`
- Nuevo valor: `C:\Aquafrisch\Tools\Kiosk\LaunchKioskWithWatchdog.bat`

> ⚠️ **IMPORTANTE**: Copiar los archivos .bat a `C:\Aquafrisch\Tools\Kiosk\` o ajustar la ruta

### Alternativa: Solo para usuario específico
```
HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon
```
Crear valor String "Shell" = ruta al .bat

## Configuración del .bat

Editar las variables al inicio del archivo:

```batch
SET FRONTEND_URL=http://localhost:3001/
SET BACKEND_CHECK_URL=http://localhost:5000/api/models
SET WATCHDOG_INTERVAL=60      (segundos entre verificaciones)
SET MAX_FAILURES=5            (fallos antes de reiniciar)
```

## Cómo Salir del Modo Kiosk (para mantenimiento)

### Opción 1: Desde la aplicación
- Login como SuperAdmin/Administrator
- Ir a Herramientas del Sistema → Cerrar Sesión Windows

### Opción 2: Ctrl+Alt+Del (si está habilitado)
- Puede no funcionar en todos los modos kiosk

### Opción 3: Reinicio físico
- Mantener botón de encendido 10 segundos
- Al reiniciar, presionar F8 o Shift+F8 para modo seguro

### Opción 4: SSH/Remote Desktop (si está configurado)
- Conectarse remotamente y matar el proceso

## Troubleshooting

### El navegador no se ve en pantalla completa
- Verificar que el flag `--kiosk` está presente
- Asegurarse de que no hay otro navegador abierto

### El watchdog reinicia muy seguido
- Aumentar `MAX_FAILURES` a 10
- Aumentar `WATCHDOG_INTERVAL` a 120

### Ver los logs
- El archivo `kiosk_log.txt` se crea junto al .bat
- Revisar para ver errores de conexión

## Requisitos

- Windows 10/11
- Microsoft Edge o Google Chrome instalado
- Backend corriendo en localhost:5000
- Frontend corriendo en localhost:3001

## Seguridad

⚠️ **IMPORTANTE para producción:**
1. Configurar usuario Windows sin privilegios de admin
2. Deshabilitar Ctrl+Alt+Del si es necesario (Group Policy)
3. Configurar auto-login del usuario kiosk
4. Instalar backend y frontend como servicios Windows
