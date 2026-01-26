# 🏢 SERVIDOR EMPRESA - Guía de Configuración

## 📋 Resumen

El servidor de empresa permite a los ingenieros configurar proyectos con el **selector de proyectos habilitado**.

| Parámetro | Valor |
|-----------|-------|
| **IP Servidor** | `192.168.2.199` |
| **Ruta instalación** | `C:\Aquafrisch Supervisor\Backend\` |
| **Modo** | Development (selector funciona) |
| **Puerto HTTP** | 5000 |
| **Puerto HTTPS** | 5001 |

---

## 🏗️ Arquitectura

```
TU PC (Desarrollo)                          SERVIDOR EMPRESA (192.168.2.199)
══════════════════                          ═════════════════════════════════

  Código fuente                              C:\Aquafrisch Supervisor\Backend\
  ├── SW.PC.API.Backend_\                    ├── SW.PC.API.Backend.exe  ← Código
  └── my-3d-app\                             ├── wwwroot\               ← Frontend
                                             │
       │                                     └── Projects\              ← INTOCABLE
       │                                         ├── A70.AMITWP\
       │  Deploy-Servidor-Empresa.ps1            ├── Proyecto-X\
       │  (solo copia código)                    └── (los ingenieros gestionan)
       │
       ▼
  ┌─────────────────────────────────────┐
  │  SOLO SE COPIA:                     │
  │  ✅ Backend (.exe, .dll)            │
  │  ✅ Frontend (html, js, css)        │
  │                                     │
  │  NO SE TOCA:                        │
  │  ❌ Projects/ (Excel, 3D, DB)       │
  └─────────────────────────────────────┘
```

---

## 🚀 Instalación Inicial (Primera vez)

### Paso 1: Ejecutar deploy desde tu PC

```powershell
cd "c:\Users\mpugnaghi\Documents\Work_In_Process\_Web\AI test\SW.PC.API.Backend_"
.\Deploy-Servidor-Empresa.ps1
```

El script:
1. Compila backend y frontend
2. Se conecta al servidor
3. Crea la estructura de carpetas
4. Copia el código
5. Crea acceso directo en el escritorio del servidor

### Paso 2: Los ingenieros crean proyectos

Los ingenieros deben crear manualmente los proyectos en:
```
C:\Aquafrisch Supervisor\Backend\Projects\
```

Estructura de un proyecto:
```
Projects/
└── NombreProyecto/
    ├── config/
    │   └── ProjectConfig.xlsm    ← Excel de configuración
    ├── models/
    │   └── *.glb                 ← Modelos 3D
    ├── data/
    │   └── project.db            ← Base de datos (auto-generada)
    └── backups/
        └── *.zip                 ← Backups
```

---

## 🔄 Actualizar Código (Cuando haces cambios)

### Opción 1: Compilar y desplegar
```powershell
.\Deploy-Servidor-Empresa.ps1
```
- Compila backend + frontend (~1-2 min)
- Copia al servidor
- **NO toca los proyectos**

### Opción 2: Solo copiar (sin compilar)
```powershell
.\Deploy-Servidor-Empresa.ps1 -SkipBuild
```
- Usa compilación anterior (~10 seg)
- Útil si solo quieres re-copiar

---

## ▶️ Arrancar el Servidor

En el servidor (192.168.2.199):

**Opción A:** Doble clic en `Aquafrisch Servidor` (acceso directo en escritorio)

**Opción B:** Ejecutar manualmente:
```cmd
C:\Aquafrisch Supervisor\Backend\Start-ServidorEmpresa.bat
```

**Para parar:** `Ctrl+C` en la ventana o cerrar la ventana

---

## 🌐 Acceso Web

| Desde | URL |
|-------|-----|
| Mismo servidor | `http://localhost:5000` |
| Red interna | `http://192.168.2.199:5000` |
| HTTPS (seguro) | `https://192.168.2.199:5001` |

---

## 👥 Roles

| Rol | Qué hace | Dónde |
|-----|----------|-------|
| **Desarrollador (Tú)** | Actualiza código con `Deploy-Servidor-Empresa.ps1` | Tu PC |
| **Ingenieros** | Crean/editan proyectos (Excel, 3D) | Servidor o carpeta compartida |

---

## 📂 Archivos del Sistema

| Archivo | Ubicación | Propósito |
|---------|-----------|-----------|
| `Deploy-Servidor-Empresa.ps1` | Raíz del backend (tu PC) | Script de deploy |
| `Start-ServidorEmpresa.bat` | `Installers/` → Servidor | Arrancar en modo Development |

---

## ❓ FAQ

**¿Qué pasa si los ingenieros están trabajando y hago deploy?**
→ El servidor se para brevemente (~10 seg) y arranca de nuevo. Los proyectos **NO se tocan**.

**¿Pierden los ingenieros su trabajo?**
→ **NO**. Solo se actualiza el código, los proyectos (Excel, modelos, DB) permanecen intactos.

**¿Cómo crean los ingenieros un proyecto nuevo?**
→ Copian la carpeta `_template` y la renombran con el ID del proyecto.

**¿Puedo acceder a los proyectos desde mi PC?**
→ Sí, via `\\192.168.2.199\c$\Aquafrisch Supervisor\Backend\Projects\`

---

## 📁 Acceso a Carpetas de Proyecto (Ingenieros)

### Opción 1: Share de red (sin credenciales)
```
\\192.168.2.199\AquafrischProjects
```
- Configurado para acceso sin autenticación
- Apunta a `C:\Aquafrisch Supervisor\Backend\Projects`

### Opción 2: Admin share (requiere credenciales)
```
\\192.168.2.199\c$\Aquafrisch Supervisor\Backend\Projects\
```
- Requiere: `Administrator` / `Aqua2023`

### Opción 3: Desde la interfaz web
1. Login en http://192.168.2.199:5000
2. En el selector de proyectos, clic en **"📁 Abrir carpeta del proyecto"**
3. Se copia la ruta al portapapeles
4. Pegar en el Explorador de Windows

---

## 💾 Sistema de Backup/Restore

El sistema incluye gestión completa de backups accesible desde el botón **DATA** en la interfaz.

### Funcionalidades
| Acción | Descripción |
|--------|-------------|
| **Crear backup** | Guarda Excel + modelos 3D + base de datos en ZIP |
| **Restaurar** | Restaura un backup existente en el servidor |
| **Importar** | Sube un ZIP desde otra máquina (ej: cliente) |
| **Exportar** | Descarga el backup como ZIP |
| **Verificar** | Comprueba integridad del backup |

### Ubicación de backups
```
Projects/{proyecto}/backups/
├── backup_20260127_1430.zip
├── backup_20260127_1000.zip
└── ...
```

### Flujo típico
1. Técnico trae ZIP de máquina cliente
2. Lo copia a la carpeta compartida del servidor
3. En la web: DATA → Importar Backup → Seleccionar ZIP
4. Restaurar cuando sea necesario

---

## 🔧 Solución de Problemas

### Error "archivos bloqueados" al desplegar
El servidor está corriendo. El script ahora lo para automáticamente con `taskkill`.
Si falla, para manualmente:
```powershell
taskkill /S 192.168.2.199 /U Administrator /P Aqua2023 /IM "SW.PC.API.Backend.exe" /F
```

### El servidor no arranca
1. Verificar que el .bat existe: `C:\Aquafrisch Supervisor\Backend\Start-ServidorEmpresa.bat`
2. Verificar que el modo es Development (debe mostrar selector de proyectos)
3. Ver logs en la consola

---

*Última actualización: 27 Enero 2026*

