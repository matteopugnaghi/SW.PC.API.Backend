# WSUS-Configure - Actualizaciones de Windows

Script para gestionar las actualizaciones de Windows desde el servidor WSUS corporativo.
Se ejecuta con doble click o desde PowerShell. Pide permisos de administrador automaticamente.

## Como usarlo

Ejecutar `WSUS-Configure.ps1` y seguir el menu:

```
  1) status   - Ver como esta configurado ahora
  2) setup    - Configurar por primera vez
  3) enable   - Activar actualizaciones
  4) disable  - Desactivar actualizaciones
  5) check    - Buscar si hay actualizaciones nuevas
  6) install  - Instalar actualizaciones (pide confirmacion)
  7) reset    - Quitar todo y volver al estado original
  0) Salir
```

## Primera vez

1. Ejecutar el script
2. Opcion **2** (setup) - configura el servidor WSUS
3. Opcion **3** (enable) - activa las actualizaciones
4. Opcion **5** (check) - comprueba que funciona

## Para instalar actualizaciones

1. Opcion **5** (check) - mira que hay disponible
2. Opcion **6** (install) - te lista las actualizaciones y te pide S/N antes de instalar
3. Si pide reiniciar, te pregunta antes de hacerlo

## Protecciones

- El PC **nunca se reinicia solo** si hay alguien trabajando
- **Nunca instala nada sin tu permiso** - solo descarga y avisa
- Siempre pide confirmacion antes de instalar o reiniciar

## Deshacer

Opcion **7** (reset) borra toda la configuracion y vuelve al estado original de Windows.

## Servidor WSUS

Por defecto usa `http://10.8.82.1:8530`. Para usar otro servidor:

```powershell
.\WSUS-Configure.ps1 -Action setup -Server "http://otro:8530"
```
