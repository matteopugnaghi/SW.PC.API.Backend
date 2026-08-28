# ClientSetup — Instalación de Certificado SSL en Clientes

Herramientas para distribuir e instalar el certificado raíz de **Aquafrisch
Supervisor** en PCs cliente (operario, mantenimiento, auditor) que se conectan
al backend por HTTPS.

## Archivos

| Archivo | Uso |
|---------|-----|
| `Install-AquafrischCert.bat` | Script offline: pide IP/puerto, descarga el `.cer` del servidor y lo instala en el almacén raíz de Windows. Soporta usuarios estándar y administradores. |
| `AquafrischCert-DistributeToUser.bat` | Copia el certificado mTLS ya enrollado a otro usuario local del mismo PC (requiere Admin). |

## ⚠️ REGLA CRÍTICA — Cómo ejecutar el script

> **El script `Install-AquafrischCert.bat` debe ejecutarse SIEMPRE con el usuario
> que va a usar el Supervisor, NO como Administrador.**

| Situación | Cómo ejecutar |
|-----------|---------------|
| Usuario `aqf` quiere acceder al Supervisor | Iniciar sesión como `aqf` → doble-click normal en el `.bat` |
| Usuario `aqf-admin` quiere acceder al Supervisor | Iniciar sesión como `aqf-admin` → doble-click normal en el `.bat` |
| ❌ INCORRECTO | Click derecho → "Ejecutar como Administrador" |

**¿Por qué?** El certificado de identidad del equipo (mTLS) se instala en el
perfil del usuario que ejecuta el script. Si se ejecuta como Administrador,
el certificado queda en el perfil del Administrador y el usuario normal
(`aqf`) no puede usarlo → el Supervisor dice "equipo no registrado".

El script detecta automáticamente si tiene permisos de Admin o no:
- **Con Admin**: instala la CA raíz a nivel de máquina (todos los usuarios)
- **Sin Admin**: instala la CA raíz a nivel de usuario (solo ese usuario)

En ambos casos, el certificado de identidad del equipo queda en el perfil
del usuario que lo ejecuta.

## Flujo de instalación en un PC con varios usuarios

```
1. aqf-admin ejecuta Install-AquafrischCert.bat (doble-click normal)
   → CA raíz instalada a nivel de máquina (todos los usuarios)
   → Certificado mTLS instalado en perfil de aqf-admin ✓

2. aqf ejecuta Install-AquafrischCert.bat (doble-click normal)
   → CA raíz ya está (no la reinstala)
   → Certificado mTLS instalado en perfil de aqf ✓
   → Necesita un código de registro nuevo (generado en el Supervisor)
```

## Cuándo usar este `.bat`

Es la **alternativa offline** al endpoint dinámico
`GET /api/certificate/install-script` del backend.

- **Endpoint dinámico**: el `.bat` se genera al vuelo en el servidor y se
  descarga vía navegador. Útil para usuarios que ya pueden alcanzar el server.
- **`.bat` offline (este)**: se distribuye por pendrive / correo / GPO. El
  usuario no necesita abrir el navegador antes de instalar el cert (evita el
  aviso de "conexión no segura" desde el primer acceso).

## Uso

1. Copiar `Install-AquafrischCert.bat` al PC cliente (pendrive, recurso de red…).
2. Iniciar sesión con el usuario que va a usar el Supervisor.
3. **Doble-click normal** en el `.bat` (NO "Ejecutar como administrador").
4. Introducir la IP del servidor cuando se solicite (por defecto `192.168.2.161`).
5. Introducir el código de registro cuando se solicite (generado en el panel del Supervisor → Usuarios → Equipos).
6. Esperar a que termine ("INSTALACIÓN COMPLETADA").
7. Cerrar y reabrir el navegador.

## Requisitos del PC cliente

- Windows 10 1803 o superior (necesita `curl.exe` integrado).
- Conectividad TCP al servidor en el puerto HTTPS (por defecto 5001).
- **No requiere permisos de administrador** para el enrollment mTLS.

## Notas para Firefox

Firefox usa su propio almacén de certificados — este `.bat` **no** lo cubre.
Para Firefox, importar manualmente el `.cer` desde:

`Ajustes → Privacidad y Seguridad → Certificados → Ver certificados → Autoridades → Importar`

## Versionado / EU CRA

Este `.bat` queda trazado en git como artefacto de distribución de PKI del
producto. Cualquier cambio en la lógica de instalación debe reflejarse aquí.



Flush socket

Limpia el cache de socket

chrome://net-internals/#sockets

