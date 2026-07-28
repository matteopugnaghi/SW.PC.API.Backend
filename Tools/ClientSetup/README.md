# ClientSetup — Instalación de Certificado SSL en Clientes

Herramientas para distribuir e instalar el certificado raíz de **Aquafrisch
Supervisor** en PCs cliente (operario, mantenimiento, auditor) que se conectan
al backend por HTTPS.

## Archivos

| Archivo | Uso |
|---------|-----|
| `Install-AquafrischCert.bat` | Script offline: pide IP/puerto, descarga el `.cer` del servidor y lo instala en el almacén raíz de Windows. |

## Cuándo usar este `.bat`

Es la **alternativa offline** al endpoint dinámico
`GET /api/certificate/install-script` del backend.

- **Endpoint dinámico**: el `.bat` se genera al vuelo en el servidor y se
  descarga vía navegador. Útil para usuarios que ya pueden alcanzar el server.
- **`.bat` offline (este)**: se distribuye por pendrive / correo / GPO. El
  usuario no necesita abrir el navegador antes de instalar el cert (evita el
  aviso de "conexión no segura" desde el primer acceso).

Ambos hacen exactamente lo mismo:

```text
curl -k -s -o %TEMP%\aquafrisch-supervisor.cer https://<IP>:5001/api/certificate/public
certutil -addstore "Root" %TEMP%\aquafrisch-supervisor.cer
```

## Uso

1. Copiar `Install-AquafrischCert.bat` al PC cliente (pendrive, recurso de red…).
2. **Click derecho → Ejecutar como administrador**.
3. Introducir la IP del servidor cuando se solicite (por defecto `192.168.2.161`).
4. Esperar a que termine ("INSTALACIÓN COMPLETADA").
5. Cerrar y reabrir el navegador.

## Requisitos del PC cliente

- Windows 10 1803 o superior (necesita `curl.exe` integrado).
- Permisos de administrador local.
- Conectividad TCP al servidor en el puerto HTTPS (por defecto 5001).

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

