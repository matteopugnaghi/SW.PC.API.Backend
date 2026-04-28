<#
.SYNOPSIS
    Aquafrisch SBOM Scanner - Genera informe de vulnerabilidades CRA con plantilla 06.7-XX-07.

.DESCRIPTION
    Modo interactivo:
      1. Pregunta los metadatos del documento (DOC_CODE, PROYECTO, CLIENTE, FASE, PSO...)
      2. Selecciona o genera SBOM CycloneDX
      3. Consulta cada purl al servidor publico OSV.dev
      4. Aplica logica de decision automatica:
         - Critical/High + Aplica=Si  -> "Parchear" + 30 dias
         - Medium                      -> "Proximo ciclo"
         - Low / Aplica=No             -> "Aceptada / Documentada"
      5. Rellena la plantilla 06.7-{PROY}-07 y genera MD + CSV + JSON crudo

.PARAMETER NonInteractive
    Usa todos los defaults sin preguntar (para CI/CD).

.PARAMETER ParamFile
    Ruta a un .json con los parametros pre-rellenados (modo desatendido).
#>

param(
    [string]$SbomFile = "",
    [switch]$Generate,
    [switch]$Regenerate,
    [string]$BackendUrl = "http://localhost:5000",
    [switch]$FailOnHigh,
    [switch]$NonInteractive,
    [string]$ParamFile = ""
)

$ErrorActionPreference = 'Stop'
$ProgressPreference    = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

try {
    Add-Type @"
        using System.Net;
        using System.Security.Cryptography.X509Certificates;
        public class TrustAllCerts : ICertificatePolicy {
            public bool CheckValidationResult(ServicePoint sp, X509Certificate cert, WebRequest req, int problem) { return true; }
        }
"@ -ErrorAction SilentlyContinue
    [System.Net.ServicePointManager]::CertificatePolicy = New-Object TrustAllCerts
} catch {}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$sbomDir   = Join-Path $scriptDir "sboms"
$reportDir = Join-Path $scriptDir "reports"
$paramsDir = Join-Path $scriptDir "params"
foreach ($d in @($sbomDir, $reportDir, $paramsDir)) {
    if (-not (Test-Path $d)) { New-Item -ItemType Directory -Path $d -Force | Out-Null }
}

$timestamp = Get-Date -Format "yyyy-MM-dd_HHmmss"
$today     = Get-Date -Format "yyyy-MM-dd"

# ============================================================
# FUNCIONES AUXILIARES
# ============================================================

function Read-WithDefault {
    param([string]$Prompt, [string]$Default = "", [switch]$Required)
    $displayDefault = if ($Default) { " [$Default]" } else { "" }
    while ($true) {
        $val = Read-Host "  $Prompt$displayDefault"
        if (-not $val) { $val = $Default }
        if ($Required -and -not $val) {
            Write-Host "  [!] Este campo es obligatorio." -ForegroundColor Yellow
            continue
        }
        return $val
    }
}

function Invoke-GenerateSbom {
    param([string]$Url, [switch]$Force, [string]$DestPath)
    if ($Force) {
        Write-Host "  -> Forzando regeneracion en backend..." -ForegroundColor Cyan
        try { Invoke-RestMethod -Uri "$Url/api/sbom/generate" -Method Post -TimeoutSec 120 | Out-Null }
        catch { Write-Host "  [WARN] No se pudo regenerar: $_" -ForegroundColor Yellow }
    }
    Write-Host "  -> Descargando SBOM desde $Url/api/sbom/download ..." -ForegroundColor Cyan
    Invoke-RestMethod -Uri "$Url/api/sbom/download" -OutFile $DestPath -TimeoutSec 60
}

function Get-CvssNumeric {
    param($vuln)
    if ($vuln.severity) {
        foreach ($s in $vuln.severity) {
            if ($s.score -match '^[\d\.]+$') { return [double]$s.score }
        }
    }
    return $null
}

function Get-NormalizedSeverity {
    param($vuln)
    if ($vuln.database_specific.severity) {
        $s = $vuln.database_specific.severity.ToUpper()
        if ($s -eq "MODERATE") { return "MEDIUM" }
        return $s
    }
    $score = Get-CvssNumeric $vuln
    if ($score -is [double]) {
        if ($score -ge 9.0) { return "CRITICAL" }
        if ($score -ge 7.0) { return "HIGH" }
        if ($score -ge 4.0) { return "MEDIUM" }
        if ($score -gt 0)   { return "LOW" }
    }
    return "UNKNOWN"
}

function Get-AutoDecision {
    param([string]$Severity, [string]$Aplica)
    # Aplica: "Si", "No", "[revisar]"
    if ($Aplica -eq "No") { return @{ Decision = "Aceptada / Documentada"; Plazo = "-" } }
    switch ($Severity) {
        "CRITICAL" { return @{ Decision = "Parchear"; Plazo = "30 dias" } }
        "HIGH"     { return @{ Decision = "Parchear"; Plazo = "30 dias" } }
        "MEDIUM"   { return @{ Decision = "Proximo ciclo"; Plazo = "Proximo release" } }
        "LOW"      { return @{ Decision = "Aceptada / Documentada"; Plazo = "-" } }
        default    { return @{ Decision = "[revisar]"; Plazo = "[revisar]" } }
    }
}

# ============================================================
# FASE 1 - METADATOS DEL DOCUMENTO
# ============================================================

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  AQUAFRISCH SBOM SCANNER - Plantilla 06.7-XX-07" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ""

$params = @{}

if ($ParamFile -and (Test-Path $ParamFile)) {
    Write-Host "Cargando parametros desde: $ParamFile" -ForegroundColor Cyan
    $params = Get-Content $ParamFile -Raw | ConvertFrom-Json | ForEach-Object {
        $h = @{}; $_.PSObject.Properties | ForEach-Object { $h[$_.Name] = $_.Value }; $h
    }
}
elseif (-not $NonInteractive) {
    Write-Host "[METADATOS DEL INFORME] - pulsa ENTER para usar el valor por defecto" -ForegroundColor Yellow
    Write-Host ""

    $params['PROY']       = Read-WithDefault -Prompt "Codigo proyecto corto (ej: A72)" -Required
    $params['DOC_CODE']   = Read-WithDefault -Prompt "Codigo documento" -Default "06.7-$($params['PROY'])-07"
    $params['VERSION']    = Read-WithDefault -Prompt "Version del informe" -Default "1.0"
    $params['FECHA']      = Read-WithDefault -Prompt "Fecha del informe" -Default $today
    $params['PROYECTO']   = Read-WithDefault -Prompt "Nombre proyecto (ej: A72.TOUTWP)" -Required
    $params['CLIENTE']    = Read-WithDefault -Prompt "Cliente" -Required

    Write-Host ""
    Write-Host "  Fases disponibles: FAT / SAT / Postventa-N / Pre-FAT" -ForegroundColor Gray
    $params['FASE']       = Read-WithDefault -Prompt "Fase" -Default "FAT"

    Write-Host ""
    $params['EXECUTOR']   = Read-WithDefault -Prompt "Ejecutor del escaneo" -Default $env:USERNAME
    $params['ROL']        = Read-WithDefault -Prompt "Rol del ejecutor" -Default "PSO Proyecto"
    $params['PSO']        = Read-WithDefault -Prompt "PSO del proyecto" -Default $params['EXECUTOR']
    $params['DB_LIST']    = Read-WithDefault -Prompt "Bases de datos consultadas" -Default "OSV, GHSA, NVD"
    $params['NEXT_SCAN']  = Read-WithDefault -Prompt "Proxima fecha escaneo prevista (YYYY-MM-DD)" -Default ((Get-Date).AddMonths(6).ToString("yyyy-MM-dd"))
    $params['EXCLUSIONS'] = Read-WithDefault -Prompt "Componentes excluidos" -Default "ninguno"

    # Guardar params para reutilizar
    $paramSavePath = Join-Path $paramsDir "params-$($params['PROY']).json"
    $params | ConvertTo-Json | Set-Content $paramSavePath -Encoding UTF8
    Write-Host ""
    Write-Host "  Parametros guardados en: $paramSavePath" -ForegroundColor DarkGray
    Write-Host "  (puedes reutilizarlos con: -ParamFile `"$paramSavePath`")" -ForegroundColor DarkGray
}
else {
    # Defaults si NonInteractive y sin ParamFile
    $params = @{
        PROY = "XXX"; DOC_CODE = "06.7-XXX-07"; VERSION = "1.0"; FECHA = $today
        PROYECTO = "Sin especificar"; CLIENTE = "Sin especificar"; FASE = "FAT"
        EXECUTOR = $env:USERNAME; ROL = "Auto"; PSO = $env:USERNAME
        DB_LIST = "OSV, GHSA, NVD"; NEXT_SCAN = ((Get-Date).AddMonths(6).ToString("yyyy-MM-dd"))
        EXCLUSIONS = "ninguno"
    }
}

# ============================================================
# FASE 2 - SELECCION SBOM
# ============================================================

Write-Host ""
Write-Host "[SELECCION DE SBOM]" -ForegroundColor Yellow

$selectedSbom = $null
if ($SbomFile) {
    if (-not (Test-Path $SbomFile)) { Write-Host "[ERROR] No existe: $SbomFile" -ForegroundColor Red; exit 2 }
    $selectedSbom = (Resolve-Path $SbomFile).Path
}
elseif ($Generate) {
    $selectedSbom = Join-Path $sbomDir "sbom-$timestamp.json"
    try { Invoke-GenerateSbom -Url $BackendUrl -Force:$Regenerate -DestPath $selectedSbom }
    catch { Write-Host "[ERROR] $_" -ForegroundColor Red; exit 2 }
}
else {
    $existing = @(Get-ChildItem -Path $sbomDir -Filter "*.json" -ErrorAction SilentlyContinue | Sort-Object LastWriteTime -Descending)

    Write-Host "  [G] Generar SBOM nuevo desde backend ($BackendUrl)"
    Write-Host "  [R] Regenerar SBOM en backend + descargar"
    if ($existing.Count -gt 0) {
        Write-Host "  --- SBOMs existentes en .\sboms\ ---" -ForegroundColor Gray
        for ($i = 0; $i -lt $existing.Count; $i++) {
            $f = $existing[$i]
            $sizeKb = [math]::Round($f.Length / 1KB, 1)
            Write-Host ("  [{0}] {1}  ({2} KB, {3:yyyy-MM-dd HH:mm})" -f ($i+1), $f.Name, $sizeKb, $f.LastWriteTime)
        }
    } else {
        Write-Host "  (no hay SBOMs en .\sboms\ - copia un .json ahi o usa [G])" -ForegroundColor DarkGray
    }
    Write-Host "  [Q] Salir"
    Write-Host ""
    $choice = Read-Host "  Selecciona opcion"

    switch -Regex ($choice.Trim().ToUpper()) {
        '^Q$' { Write-Host "Cancelado."; exit 0 }
        '^G$' {
            $selectedSbom = Join-Path $sbomDir "sbom-$timestamp.json"
            try { Invoke-GenerateSbom -Url $BackendUrl -DestPath $selectedSbom } catch { Write-Host "[ERROR] $_" -ForegroundColor Red; exit 2 }
        }
        '^R$' {
            $selectedSbom = Join-Path $sbomDir "sbom-$timestamp.json"
            try { Invoke-GenerateSbom -Url $BackendUrl -Force -DestPath $selectedSbom } catch { Write-Host "[ERROR] $_" -ForegroundColor Red; exit 2 }
        }
        '^\d+$' {
            $idx = [int]$choice - 1
            if ($idx -lt 0 -or $idx -ge $existing.Count) { Write-Host "[ERROR] Indice fuera de rango." -ForegroundColor Red; exit 2 }
            $selectedSbom = $existing[$idx].FullName
        }
        default { Write-Host "[ERROR] Opcion no valida: '$choice'" -ForegroundColor Red; exit 2 }
    }
}

Write-Host ""
Write-Host "  -> SBOM: $selectedSbom" -ForegroundColor Green

# ============================================================
# FASE 3 - PARSEAR SBOM
# ============================================================

$sbom = Get-Content $selectedSbom -Raw | ConvertFrom-Json
$totalComps = $sbom.components.Count
$scanable = @($sbom.components | Where-Object { $_.purl -like "pkg:nuget/*" -or $_.purl -like "pkg:npm/*" })
$nugetCount = @($scanable | Where-Object { $_.purl -like "pkg:nuget/*" }).Count
$npmCount   = @($scanable | Where-Object { $_.purl -like "pkg:npm/*" }).Count
$otCount    = $totalComps - $nugetCount - $npmCount

# Auto-rellenar metadatos del SBOM
$params['SUPERVISOR_VERSION'] = $sbom.metadata.component.version
$params['SBOM_FILE']          = Split-Path -Leaf $selectedSbom
$params['TOOL_NAME']          = if ($sbom.metadata.tools) { $sbom.metadata.tools[0].name } else { "Aquafrisch SBOM Scanner" }
$params['TOOL_VERSION']       = if ($sbom.metadata.tools) { $sbom.metadata.tools[0].version } else { "1.1" }

# ============================================================
# FASE 4 - CONSULTAR OSV.dev
# ============================================================

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  ESCANEO OSV.dev (componentes: $($scanable.Count))" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan

$findings  = @()       # Una entrada por VULNERABILIDAD
$rawScan   = @()       # JSON crudo (para anexo)
$cleanList = @()       # Componentes sin vulns
$errorCount = 0
$scanStart = Get-Date

foreach ($c in $scanable) {
    try {
        $body = @{ package = @{ purl = $c.purl } } | ConvertTo-Json -Compress
        $resp = Invoke-RestMethod -Uri "https://api.osv.dev/v1/query" -Method Post -Body $body -ContentType "application/json" -TimeoutSec 20

        $rawScan += [PSCustomObject]@{ purl = $c.purl; name = $c.name; version = $c.version; response = $resp }

        if ($resp.vulns) {
            foreach ($v in $resp.vulns) {
                $sev = Get-NormalizedSeverity $v
                $cvss = Get-CvssNumeric $v
                $cvssDisplay = if ($cvss) { [string]$cvss } else { "N/D" }

                # Fixed version
                $fixedVersion = ""
                if ($v.affected) {
                    foreach ($a in $v.affected) {
                        if ($a.ranges) {
                            foreach ($r in $a.ranges) {
                                if ($r.events) {
                                    $f = $r.events | Where-Object { $_.fixed } | ForEach-Object { $_.fixed } | Select-Object -First 1
                                    if ($f -and -not $fixedVersion) { $fixedVersion = $f }
                                }
                            }
                        }
                    }
                }

                $findings += [PSCustomObject]@{
                    Name         = $c.name
                    Version      = $c.version
                    Purl         = $c.purl
                    Ecosystem    = if ($c.purl -like "pkg:nuget/*") { "NuGet" } else { "npm" }
                    VulnId       = $v.id
                    Aliases      = ($v.aliases -join ", ")
                    Severity     = $sev
                    Cvss         = $cvssDisplay
                    FixedVersion = $fixedVersion
                    Summary      = $v.summary
                    Aplica       = "[revisar]"   # Lo rellena el PSO a mano
                    Decision     = ""            # Se calcula despues
                    Plazo        = ""
                }
            }
            Write-Host ("  [!] {0}@{1} -> {2}" -f $c.name, $c.version, (($resp.vulns | ForEach-Object { $_.id }) -join ", ")) -ForegroundColor Red
        } else {
            $cleanList += [PSCustomObject]@{ Name = $c.name; Version = $c.version; Ecosystem = if ($c.purl -like "pkg:nuget/*") { "NuGet" } else { "npm" } }
            Write-Host ("  [OK] {0}@{1}" -f $c.name, $c.version) -ForegroundColor Green
        }
    } catch {
        $errorCount++
        Write-Host ("  [ERR] {0}: {1}" -f $c.name, $_) -ForegroundColor Yellow
    }
}

$scanEnd = Get-Date
$scanDuration = [math]::Round(($scanEnd - $scanStart).TotalSeconds, 1)

# Aplicar logica de decision
foreach ($f in $findings) {
    $dec = Get-AutoDecision -Severity $f.Severity -Aplica $f.Aplica
    $f.Decision = $dec.Decision
    $f.Plazo = $dec.Plazo
}

# Contadores
$nCritical = @($findings | Where-Object { $_.Severity -eq "CRITICAL" }).Count
$nHigh     = @($findings | Where-Object { $_.Severity -eq "HIGH" }).Count
$nMedium   = @($findings | Where-Object { $_.Severity -eq "MEDIUM" }).Count
$nLow      = @($findings | Where-Object { $_.Severity -eq "LOW" }).Count
$nUnknown  = @($findings | Where-Object { $_.Severity -eq "UNKNOWN" }).Count
$nTotal    = $findings.Count

# Veredicto
if ($nCritical -gt 0) {
    $verdict = "NO APTO"
    $conclusionNote = "Se han detectado $nCritical vulnerabilidad(es) **CRITICA(S)** que deben mitigarse antes de la $($params.FASE). Notificar a ENISA en 24h si esta(n) activamente explotada(s) (CRA Art. 14)."
} elseif ($nHigh -gt 0) {
    $verdict = "APTO CON ACCIONES"
    $conclusionNote = "Se han detectado $nHigh vulnerabilidad(es) **ALTA(S)** que deben planificarse en el siguiente release ($($params.FASE) requiere mitigacion en 30 dias)."
} elseif ($nMedium -gt 0 -or $nLow -gt 0) {
    $verdict = "APTO CON OBSERVACIONES"
    $conclusionNote = "Vulnerabilidades menores detectadas ($nMedium media, $nLow baja). Programar revision en proximo ciclo."
} else {
    $verdict = "APTO"
    $conclusionNote = "Sin vulnerabilidades conocidas a fecha de escaneo. Proximo escaneo previsto: $($params.NEXT_SCAN)."
}

# ============================================================
# FASE 5 - GENERAR INFORME (PLANTILLA OFICIAL)
# ============================================================

$baseFileName = "$($params.DOC_CODE)_$($params.PROYECTO)_$($params.FASE)_$timestamp"
$mdPath   = Join-Path $reportDir "$baseFileName.md"
$csvPath  = Join-Path $reportDir "$baseFileName.csv"
$jsonPath = Join-Path $reportDir "$baseFileName.raw.json"
$txtPath  = Join-Path $reportDir "$baseFileName.txt"

# CSV detallado
$findings | Export-Csv $csvPath -NoTypeInformation -Encoding UTF8

# JSON crudo (anexo)
$rawScan | ConvertTo-Json -Depth 20 | Set-Content $jsonPath -Encoding UTF8

# TXT legible (anexo)
$txtContent = @()
$txtContent += "=== OSV.dev Scan Output ==="
$txtContent += "Date: $($scanEnd.ToString('yyyy-MM-dd HH:mm:ss'))"
$txtContent += "SBOM: $selectedSbom"
$txtContent += "Components scanned: $($scanable.Count)"
$txtContent += "Vulnerabilities: $nTotal (Critical: $nCritical, High: $nHigh, Medium: $nMedium, Low: $nLow)"
$txtContent += ""
foreach ($f in $findings) {
    $txtContent += "[{0}] {1}@{2} - {3} (CVSS {4})" -f $f.Severity, $f.Name, $f.Version, $f.VulnId, $f.Cvss
    $txtContent += "    Summary: $($f.Summary)"
    $txtContent += "    Fixed:   $($f.FixedVersion)"
    $txtContent += ""
}
Set-Content -Path $txtPath -Value $txtContent -Encoding UTF8

# Construir CVE_TABLE_ROWS
$cveRows = if ($findings.Count -eq 0) { "| - | _Sin vulnerabilidades_ | - | - | - | - | - |" } else {
    ($findings | Sort-Object @{Expression={ switch ($_.Severity) { "CRITICAL" {0} "HIGH" {1} "MEDIUM" {2} "LOW" {3} default {4} } }}, Name | ForEach-Object {
        $sevTag = switch ($_.Severity) {
            "CRITICAL" { "**CRITICA**" }
            "HIGH"     { "**ALTA**" }
            "MEDIUM"   { "Media" }
            "LOW"      { "Baja" }
            default    { "?" }
        }
        $fix = if ($_.FixedVersion) { " (fix: ``$($_.FixedVersion)``)" } else { "" }
        "| [$($_.VulnId)](https://osv.dev/vulnerability/$($_.VulnId)) | $($_.Name)$fix | ``$($_.Version)`` | $($_.Cvss) | $($_.Aplica) | $($_.Decision) ($sevTag) | $($_.Plazo) |"
    }) -join "`n"
}

# Pending actions
$pendingActions = if ($nCritical -gt 0 -or $nHigh -gt 0) {
    $items = @()
    foreach ($f in ($findings | Where-Object { $_.Severity -in @("CRITICAL","HIGH") })) {
        $fixHint = if ($f.FixedVersion) { " a version ``$($f.FixedVersion)``" } else { "" }
        $items += "- [ ] [$($f.Severity)] Parchear ``$($f.Name)@$($f.Version)``$fixHint antes de la $($params.FASE) (ref: $($f.VulnId))"
    }
    $items += "- [ ] PSO debe rellenar columna 'Aplica' (Si/No) en la tabla y revalidar decisiones"
    $items -join "`n"
} elseif ($nMedium -gt 0) {
    "- [ ] Programar actualizacion de paquetes con vulnerabilidades MEDIUM en proximo ciclo de release`n- [ ] PSO debe revisar columna 'Aplica' antes de cerrar el informe"
} else {
    "- [ ] Mantener escaneo periodico (proximo: $($params.NEXT_SCAN))"
}

# Plantilla
$md = @"
# $($params.DOC_CODE) Informe de Vulnerabilidades - $($params.PROYECTO) @ $($params.FASE)

| | |
|---|---|
| **Doc** | $($params.DOC_CODE) v$($params.VERSION) ($($params.FECHA)) |
| **Proyecto** | $($params.PROYECTO) ($($params.CLIENTE)) |
| **Fase** | $($params.FASE) |
| **SBOM analizado** | ``$($params.SBOM_FILE)`` (Supervisor v$($params.SUPERVISOR_VERSION)) |
| **Herramienta** | $($params.TOOL_NAME) v$($params.TOOL_VERSION) + Aquafrisch OSV Scanner v1.1 |
| **Bases de datos** | $($params.DB_LIST) |
| **Ejecutado por** | $($params.EXECUTOR) ($($params.ROL)) |

---

## 1. Resumen ejecutivo

| Severidad | N CVEs |
|---|:-:|
| Critica | $nCritical |
| Alta | $nHigh |
| Media | $nMedium |
| Baja | $nLow |
$(if ($nUnknown -gt 0) { "| Sin clasificar | $nUnknown |" })
| **Total** | **$nTotal** |

**Conclusion**: **$verdict** para $($params.FASE).

> $conclusionNote

---

## 2. Alcance

- Componentes analizados: **$totalComps** ($nugetCount NuGet + $npmCount npm + $otCount OT/firmware)
- Componentes escaneados contra OSV.dev: **$($scanable.Count)** (NuGet + npm)
- Componentes excluidos: $($params.EXCLUSIONS)
- Errores de consulta: $errorCount
- Duracion del escaneo: $scanDuration s
- Proximo escaneo previsto: **$($params.NEXT_SCAN)** (minimo 2x/ano, politica ``02.4-05``)

> Los componentes OT/firmware (PLC TwinCAT, FortiGate, Windows IPC, etc.) NO son consultables contra OSV.dev y requieren verificacion manual contra advisories del fabricante (Beckhoff, Fortinet PSIRT, Microsoft MSRC).

---

## 3. Vulnerabilidades detectadas

| CVE | Componente | Version | CVSS | Aplica | Decision | Plazo |
|---|---|---|:-:|:-:|---|---|
$cveRows

> **Politica de decision (``02.4-05``)**:
> - Critica/Alta + Aplica=Si  -> **Parchear** en <=30 dias
> - Media -> **Proximo ciclo** de release
> - Baja o Aplica=No -> **Aceptada / Documentada**
>
> **"Aplica"** = la funcion vulnerable se usa realmente en el producto. Por defecto el script marca **[revisar]** porque es un juicio humano que pide la norma. **El PSO debe completar esta columna manualmente** y reevaluar la "Decision" si Aplica=No.

---

## 4. Componentes sin hallazgos

**$($cleanList.Count)** componentes analizados sin vulnerabilidades conocidas a fecha de escaneo.

<details>
<summary>Ver lista completa</summary>

| Componente | Version | Ecosistema |
|---|---|---|
$(if ($cleanList.Count -eq 0) { "| _ninguno_ | - | - |" } else { ($cleanList | Sort-Object Ecosystem, Name | ForEach-Object { "| $($_.Name) | ``$($_.Version)`` | $($_.Ecosystem) |" }) -join "`n" })

</details>

---

## 5. Acciones pendientes

$pendingActions

---

## 6. Anexos

| Archivo | Descripcion |
|---|---|
| ``$($params.SBOM_FILE)`` | SBOM CycloneDX origen |
| ``$(Split-Path -Leaf $csvPath)`` | Hallazgos en formato CSV (importable Excel) |
| ``$(Split-Path -Leaf $jsonPath)`` | Output bruto del escaner OSV.dev |
| ``$(Split-Path -Leaf $txtPath)`` | Output legible texto plano |

---

## 7. Aprobacion

| Rol | Nombre | Fecha | Firma |
|---|---|---|---|
| PSO $($params.PROY) | $($params.PSO) | | |
| Resp. SGSI | Nuria Martinez | | |

---

_Informe generado automaticamente por ``Tools/SBOM-Scanner/Scan-SBOM-OSV.ps1`` el $($scanEnd.ToString('yyyy-MM-dd HH:mm:ss')) - Cumplimiento EU CRA Art. 13/14_
"@

# Escribir con UTF-8 BOM (compatible con visores Windows)
[System.IO.File]::WriteAllText($mdPath, $md, [System.Text.UTF8Encoding]::new($true))

# ============================================================
# RESUMEN FINAL
# ============================================================

Write-Host ""
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  RESUMEN" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan
Write-Host ("  Veredicto:               {0}" -f $verdict) -ForegroundColor $(if ($nCritical -gt 0) { "Red" } elseif ($nHigh -gt 0) { "Yellow" } else { "Green" })
Write-Host ("  Componentes escaneados:  {0}" -f $scanable.Count)
Write-Host ("  Total vulnerabilidades:  {0}" -f $nTotal)
Write-Host ("    - Critical:  {0}" -f $nCritical) -ForegroundColor $(if ($nCritical -gt 0) { "Red" } else { "Green" })
Write-Host ("    - High:      {0}" -f $nHigh) -ForegroundColor $(if ($nHigh -gt 0) { "Yellow" } else { "Green" })
Write-Host ("    - Medium:    {0}" -f $nMedium)
Write-Host ("    - Low:       {0}" -f $nLow)
Write-Host ""
Write-Host "  Archivos generados en .\reports\:" -ForegroundColor Cyan
Write-Host "    - $(Split-Path -Leaf $mdPath)" -ForegroundColor White
Write-Host "    - $(Split-Path -Leaf $csvPath)" -ForegroundColor DarkGray
Write-Host "    - $(Split-Path -Leaf $jsonPath)" -ForegroundColor DarkGray
Write-Host "    - $(Split-Path -Leaf $txtPath)" -ForegroundColor DarkGray
Write-Host ""
Write-Host "  >>> RECORDATORIO: el PSO debe rellenar la columna 'Aplica' del informe MD <<<" -ForegroundColor Yellow
Write-Host ""

if ($FailOnHigh -and ($nCritical -gt 0 -or $nHigh -gt 0)) {
    Write-Host "[FAIL] -FailOnHigh activo y se detectaron vulnerabilidades High/Critical" -ForegroundColor Red
    exit 1
}
exit 0
