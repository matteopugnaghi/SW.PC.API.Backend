# SBOM Scanner — OSV.dev

Herramienta para escanear SBOMs CycloneDX contra el servidor público **OSV.dev** (Google).

## Estructura

```
SBOM-Scanner/
├── Scan-SBOM-OSV.ps1   ← Script principal
├── Run-Scan.bat        ← Doble-click para lanzar (modo interactivo)
├── sboms/              ← Coloca aquí los SBOM .json (o se descargan automáticamente)
└── reports/            ← CSV + MD generados (uno por cada scan)
```

## Uso

### 1. Modo interactivo (recomendado)

Doble-click en **`Run-Scan.bat`**. Te aparece un menú:

```
[G] Generar SBOM nuevo desde backend
[R] Regenerar SBOM en backend + descargar
[1] sbom-2026-04-28_153012.json  (4.2 KB, 2026-04-28 15:30)
[2] sbom-2026-04-15_092100.json  (4.1 KB, 2026-04-15 09:21)
[Q] Salir
```

- **G/R** → contacta al backend en `http://localhost:5000` y descarga
- **1, 2, ...** → usa un SBOM ya presente en `.\sboms\`
- **Q** → cancela

### 2. Modo automático (CLI / CI)

```powershell
# Usar SBOM concreto (sin menú)
.\Scan-SBOM-OSV.ps1 -SbomFile "C:\Downloads\sbom.json"

# Generar desde backend remoto y escanear
.\Scan-SBOM-OSV.ps1 -Generate -BackendUrl "https://192.168.2.161:5001"

# Regenerar SBOM (POST /api/sbom/generate) + descargar + escanear
.\Scan-SBOM-OSV.ps1 -Generate -Regenerate

# Modo CI: falla si hay High/Critical
.\Scan-SBOM-OSV.ps1 -SbomFile ".\sboms\latest.json" -FailOnHigh
```

## Cómo añadir un SBOM manualmente

1. Copia el archivo `.json` (CycloneDX 1.4/1.5) a la carpeta **`sboms\`**
2. Ejecuta `Run-Scan.bat`
3. Seleccionalo en el menú por su número

## Salida

Cada scan genera dos archivos en **`reports\`**:

- `osv-scan-<timestamp>.csv` — todos los componentes con su estado (OK / VulnId / Severity)
- `osv-scan-<timestamp>.md`  — resumen ejecutivo con links a osv.dev

## Requisitos

- Windows con PowerShell 5.1+
- Conexión a internet para `https://api.osv.dev/v1/query`
- Para `-Generate`: backend Aquafrisch corriendo (endpoints `/api/sbom/download` y `/api/sbom/generate`)

## Modo offline (planta sin internet)

1. En el IPC de planta: descarga el SBOM desde el frontend (botón DOWNLOAD)
2. Llévalo por USB/email a un PC con internet
3. Cópialo a `SBOM-Scanner\sboms\`
4. Ejecuta `Run-Scan.bat` → seleccionalo
