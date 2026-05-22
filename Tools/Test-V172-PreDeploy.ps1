<#
.SYNOPSIS
    Pruebas automatizadas pre-deploy v1.7.2 contra backend en https://localhost:5001
.DESCRIPTION
    Cubre:
      1) Rate limit "auth" - sliding window 20/5min (v1.7.5, antes 10/min en v1.7.2)
      2) (OPCIONAL) Lockout por usuario tras 5 fallos
      3) SCG-05 - Backup ImportBackup negativo (.txt renombrado a .zip)
      4) SCG-143 - Modelo .glb con magic bytes corruptos (prepara fichero; requiere reinicio)
      5) SCG-66 - Modelo oversize (prepara fichero; requiere reinicio)

    NO toca ficheros reales del proyecto: usa subcarpeta "_predeploy_tests/" dentro de models/
    para que el ModelAssetValidationService los escanee sin contaminar el set real.

.PARAMETER BackendUrl
    URL del backend. Default: https://localhost:5001
.PARAMETER SkipLockout
    Si se indica, omite el test de lockout (recomendado para no bloquear el admin real).
.PARAMETER LockoutUser
    Usuario dedicado para el test de lockout (debe existir y ser desechable).
.PARAMETER PrepareModelTests
    Prepara los ficheros corrupto + oversize en models/_predeploy_tests/ y pide reiniciar backend.
.PARAMETER CleanupModelTests
    Borra la carpeta models/_predeploy_tests/ (ejecutar tras verificar el audit log).

.EXAMPLE
    # Test rápido sin lockout y sin preparar modelos:
    .\Tools\Test-V172-PreDeploy.ps1

.EXAMPLE
    # Test completo incluyendo preparación de modelos para validación:
    .\Tools\Test-V172-PreDeploy.ps1 -PrepareModelTests

.EXAMPLE
    # Limpiar tras los tests:
    .\Tools\Test-V172-PreDeploy.ps1 -CleanupModelTests
#>
[CmdletBinding()]
param(
    [string]$BackendUrl = "https://localhost:5001",
    [switch]$SkipLockout,
    [string]$LockoutUser = "",
    [switch]$PrepareModelTests,
    [switch]$CleanupModelTests
)

# -- Bypass cert autofirmado (PS 5.1) --
Add-Type @"
    using System.Net;
    using System.Security.Cryptography.X509Certificates;
    public class TrustAllCertsPolicy : ICertificatePolicy {
        public bool CheckValidationResult(ServicePoint sp, X509Certificate cert, WebRequest req, int problem) { return true; }
    }
"@ -ErrorAction SilentlyContinue
[System.Net.ServicePointManager]::CertificatePolicy = New-Object TrustAllCertsPolicy
[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12 -bor [System.Net.SecurityProtocolType]::Tls13

$ErrorActionPreference = "Continue"
$script:pass = 0
$script:fail = 0
$script:skip = 0

function Write-Section($name) {
    Write-Host ""
    Write-Host ("=" * 70) -ForegroundColor Cyan
    Write-Host " $name" -ForegroundColor Cyan
    Write-Host ("=" * 70) -ForegroundColor Cyan
}
function Write-Pass($msg) { Write-Host "  [PASS] $msg" -ForegroundColor Green; $script:pass++ }
function Write-Fail($msg) { Write-Host "  [FAIL] $msg" -ForegroundColor Red;   $script:fail++ }
function Write-Skip($msg) { Write-Host "  [SKIP] $msg" -ForegroundColor Yellow; $script:skip++ }
function Write-Info($msg) { Write-Host "  $msg" -ForegroundColor Gray }

# -- Resolver carpeta del proyecto activo --
$repoRoot = Split-Path -Parent $PSScriptRoot
$activeProjectFile = Join-Path $repoRoot "active-project.json"
if (-not (Test-Path $activeProjectFile)) {
    Write-Host "ERROR: no encuentro $activeProjectFile" -ForegroundColor Red
    exit 1
}
$activeProject = (Get-Content $activeProjectFile -Raw | ConvertFrom-Json).activeProject
$modelsDir = if ($activeProject -eq "default") {
    Join-Path $repoRoot "wwwroot\models"
} else {
    Join-Path $repoRoot "Projects\$activeProject\models"
}
$testModelsDir = Join-Path $modelsDir "_predeploy_tests"
Write-Host "Backend  : $BackendUrl" -ForegroundColor Gray
Write-Host "Proyecto : $activeProject" -ForegroundColor Gray
Write-Host "Models   : $modelsDir" -ForegroundColor Gray

# ============================================================
#  CLEANUP MODE
# ============================================================
if ($CleanupModelTests) {
    Write-Section "Limpieza de ficheros de prueba de modelos"
    if (Test-Path $testModelsDir) {
        Remove-Item -Path $testModelsDir -Recurse -Force
        Write-Pass "Carpeta eliminada: $testModelsDir"
    } else {
        Write-Skip "No exista $testModelsDir"
    }
    Write-Host ""
    Write-Host "Reinicia el backend para que el escaneo no encuentre los ficheros." -ForegroundColor Yellow
    exit 0
}

# ============================================================
#  PREPARE MODEL TESTS (no requiere backend corriendo)
# ============================================================
if ($PrepareModelTests) {
    Write-Section "Preparación de ficheros corruptos/oversize para validación de modelos"
    if (-not (Test-Path $testModelsDir)) {
        New-Item -ItemType Directory -Path $testModelsDir -Force | Out-Null
    }

    # 1) GLB con magic bytes corruptos (debería ser "glTF" 0x67 0x6C 0x54 0x46)
    $corruptGlb = Join-Path $testModelsDir "CORRUPT_MAGIC.glb"
    [byte[]]$badMagic = @(0xDE, 0xAD, 0xBE, 0xEF, 0x02, 0x00, 0x00, 0x00, 0x10, 0x00, 0x00, 0x00)
    [System.IO.File]::WriteAllBytes($corruptGlb, $badMagic)
    Write-Pass "Creado: CORRUPT_MAGIC.glb (12 B, magic DEADBEEF)"

    # 2) GLB oversize (>100 MB por defecto Limits:MaxModel3DSizeMB)
    $oversizeGlb = Join-Path $testModelsDir "OVERSIZE.glb"
    $size = 101MB
    Write-Info "Creando OVERSIZE.glb ($([math]::Round($size/1MB,0)) MB)..."
    $fs = [System.IO.File]::Create($oversizeGlb)
    try {
        # Magic correcto al inicio, resto ceros (solo fallará por tamaño, no por magic)
        $fs.Write([byte[]]@(0x67,0x6C,0x54,0x46), 0, 4)
        $fs.SetLength($size)
    } finally { $fs.Close() }
    Write-Pass "Creado: OVERSIZE.glb ($([math]::Round((Get-Item $oversizeGlb).Length/1MB,0)) MB)"

    Write-Host ""
    Write-Host "SIGUIENTE PASO:" -ForegroundColor Yellow
    Write-Host "  1. Reinicia el backend" -ForegroundColor Yellow
    Write-Host "  2. Espera ~30 s (delay inicial del ModelAssetValidationService)" -ForegroundColor Yellow
    Write-Host "  3. Busca en el log de arranque:" -ForegroundColor Yellow
    Write-Host "     - 'CORRUPT_MAGIC.glb' con badMagic" -ForegroundColor Yellow
    Write-Host "     - 'OVERSIZE.glb' con oversize" -ForegroundColor Yellow
    Write-Host "     - audit Model3DValidation con total/valid/oversize/badMagic" -ForegroundColor Yellow
    Write-Host "  4. Cuando termines: .\Tools\Test-V172-PreDeploy.ps1 -CleanupModelTests" -ForegroundColor Yellow
    exit 0
}

# ============================================================
#  TESTS HTTP (requieren backend corriendo)
# ============================================================

# Sanity check
Write-Section "Sanity check del backend"
try {
    $resp = Invoke-WebRequest -Uri "$BackendUrl/api/auth/login-banner" -Method GET -UseBasicParsing -TimeoutSec 5
    if ($resp.StatusCode -eq 200) {
        Write-Pass "Backend responde HTTP 200 en /api/auth/login-banner"
    } else {
        Write-Fail "Status inesperado: $($resp.StatusCode)"
        exit 1
    }
} catch {
    Write-Fail "Backend no responde en $BackendUrl - $($_.Exception.Message)"
    Write-Host "  Arranca el backend antes de ejecutar el script." -ForegroundColor Yellow
    exit 1
}

# ============================================================
#  TEST 1 - Rate limit "auth" sliding window 20/5min (v1.7.5)
# ============================================================
Write-Section "TEST 1 - Rate limit 'auth' (SlidingWindow 20/5min)"
Write-Info "Disparando 25 logins con usuario inexistente desde la misma IP..."
Write-Info "Esperado: primeros ~20 devuelven 401 Unauthorized, el resto 429 Too Many Requests"

$ghostUser = "ratetest_nobody_$([guid]::NewGuid().ToString('N').Substring(0,8))"
$body = @{ username = $ghostUser; password = "wrong" } | ConvertTo-Json
$results = @{ "200"=0; "401"=0; "429"=0; "other"=0 }

for ($i = 1; $i -le 25; $i++) {
    try {
        $r = Invoke-WebRequest -Uri "$BackendUrl/api/auth/login" -Method POST -Body $body `
            -ContentType "application/json" -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
        $code = $r.StatusCode
    } catch {
        $code = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 0 }
    }
    $key = if ($results.ContainsKey("$code")) { "$code" } else { "other" }
    $results[$key]++
    Write-Host ("    Intento {0,2}: HTTP {1}" -f $i, $code) -ForegroundColor DarkGray
}
Write-Info "Resumen: 401=$($results['401'])  429=$($results['429'])  200=$($results['200'])  other=$($results['other'])"

if ($results['401'] -ge 18 -and $results['401'] -le 22 -and $results['429'] -ge 3) {
    Write-Pass "Rate limit 'auth' activo: ~20 logins consumidos antes de 429"
} else {
    Write-Fail "Distribución inesperada - revisar política 'auth' en Program.cs"
}

# v1.7.5: ventana 5min con 5 segmentos = cada segmento dura 60s. Tras 65s deberia
# haber liberado ~4 permits (1 segmento completo). Esperamos al menos un 401 valido.
Write-Info "Esperando 65 s para verificar regeneración de permits (sliding window 5min/5seg)..."
Start-Sleep -Seconds 65
try {
    $r = Invoke-WebRequest -Uri "$BackendUrl/api/auth/login" -Method POST -Body $body `
        -ContentType "application/json" -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
    $code = $r.StatusCode
} catch {
    $code = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 0 }
}
if ($code -eq 401) {
    Write-Pass "Sliding window liberó permits: tras 65s nuevo intento devuelve 401 (no 429)"
} elseif ($code -eq 429) {
    Write-Skip "Aún 429 tras 65s - sliding lento, considerar esperar más"
} else {
    Write-Fail "Código inesperado tras 65s: $code"
}

# ============================================================
#  TEST 2 - Endpoints fuera de scope 'auth' NO consumen el limiter
# ============================================================
Write-Section "TEST 2 - Endpoints fuera de scope 'auth' (verificación del fix v1.7.2)"
Write-Info "Tras gastar el rate limit en /login, otros endpoints /api/auth/* deben seguir respondiendo"

$ok = 0; $blocked = 0
foreach ($ep in @("/api/auth/login-banner", "/api/auth/password-policy", "/api/auth/available-users")) {
    try {
        $r = Invoke-WebRequest -Uri "$BackendUrl$ep" -Method GET -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
        Write-Host ("    $ep -> HTTP $($r.StatusCode)") -ForegroundColor DarkGray
        if ($r.StatusCode -lt 400) { $ok++ }
    } catch {
        $code = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 0 }
        Write-Host ("    $ep -> HTTP $code") -ForegroundColor DarkGray
        if ($code -eq 429) { $blocked++ }
        elseif ($code -in 401, 403) { $ok++ } # auth required pero no bloqueado por rate
    }
}
if ($blocked -eq 0 -and $ok -ge 2) {
    Write-Pass "Endpoints fuera de scope responden sin 429 - fix v1.7.2 OK"
} else {
    Write-Fail "Algún endpoint devolvió 429 ($blocked) - el atributo a nivel clase podría seguir activo"
}

# ============================================================
#  TEST 3 - Lockout por usuario (OPCIONAL)
# ============================================================
Write-Section "TEST 3 - Lockout por usuario (FailedLoginAttempts)"
if ($SkipLockout -or [string]::IsNullOrWhiteSpace($LockoutUser)) {
    Write-Skip "Omitido (usa -LockoutUser [username] para activarlo; bloquea la cuenta temporalmente)"
} else {
    Write-Info "Esperando 65 s para que el rate limit IP se reponga antes del test de lockout..."
    Start-Sleep -Seconds 65
    $body = @{ username = $LockoutUser; password = "wrong" } | ConvertTo-Json
    for ($i = 1; $i -le 5; $i++) {
        try {
            $r = Invoke-WebRequest -Uri "$BackendUrl/api/auth/login" -Method POST -Body $body `
                -ContentType "application/json" -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
            Write-Host ("    Intento ${i}: HTTP $($r.StatusCode)") -ForegroundColor DarkGray
        } catch {
            $code = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 0 }
            Write-Host ("    Intento ${i}: HTTP $code") -ForegroundColor DarkGray
        }
        Start-Sleep -Milliseconds 500
    }
    Write-Host ""
    Write-Host "  Verifica manualmente:" -ForegroundColor Yellow
    Write-Host "    1. El usuario '$LockoutUser' está bloqueado en BD (IsLockedOut=true)" -ForegroundColor Yellow
    Write-Host "    2. Login con password correcta debe seguir devolviendo 'cuenta bloqueada'" -ForegroundColor Yellow
    Write-Host "    3. Desbloquear: POST /api/auth/users/{id}/unlock (admin)" -ForegroundColor Yellow
    Write-Skip "Lockout disparado - verificar manualmente"
}

# ============================================================
#  TEST 4 - SCG-05 Backup ImportBackup negativo
# ============================================================
Write-Section "TEST 4 - SCG-05 Backup ImportBackup (.txt renombrado a .zip)"
Write-Info "Crea un .txt y lo intenta subir como .zip - debe rechazar HTTP 400 por magic bytes"

# Necesitamos token JWT de admin para llamar al endpoint protegido
Write-Info "Para este test se necesita un token JWT válido de admin."
Write-Info "Salta el test si no proporcionas credenciales (omitido por defecto)."

$adminUser = Read-Host "  Usuario admin (Enter para SALTAR el test)"
if ([string]::IsNullOrWhiteSpace($adminUser)) {
    Write-Skip "Test 4 omitido (sin credenciales admin)"
} else {
    $adminPass = Read-Host "  Password admin" -AsSecureString
    $plainPass = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
        [Runtime.InteropServices.Marshal]::SecureStringToBSTR($adminPass))

    # Login para obtener token
    $loginBody = @{ username = $adminUser; password = $plainPass } | ConvertTo-Json
    try {
        $loginResp = Invoke-RestMethod -Uri "$BackendUrl/api/auth/login" -Method POST `
            -Body $loginBody -ContentType "application/json" -TimeoutSec 10
        $token = $loginResp.token
        if (-not $token) { throw "No vino token en la respuesta" }
        Write-Pass "Login admin OK, token obtenido"
    } catch {
        Write-Fail "Login admin falló: $($_.Exception.Message)"
        $token = $null
    }

    if ($token) {
        # Crear .txt renombrado a .zip
        $fakeZip = Join-Path $env:TEMP "fake_backup_$([guid]::NewGuid().ToString('N').Substring(0,8)).zip"
        "ESTO ES TEXTO PLANO, NO UN ZIP REAL" | Out-File -FilePath $fakeZip -Encoding utf8
        Write-Info "Fake .zip creado: $fakeZip ($((Get-Item $fakeZip).Length) bytes)"

        # Subir como multipart
        $boundary = [guid]::NewGuid().ToString()
        $LF = "`r`n"
        $fileContent = [System.IO.File]::ReadAllBytes($fakeZip)
        $fileBase64 = [System.Text.Encoding]::GetEncoding("iso-8859-1").GetString($fileContent)
        $bodyLines = @(
            "--$boundary",
            "Content-Disposition: form-data; name=`"file`"; filename=`"fake_backup.zip`"",
            "Content-Type: application/zip",
            "",
            $fileBase64,
            "--$boundary--"
        ) -join $LF

        try {
            $r = Invoke-WebRequest -Uri "$BackendUrl/api/projects/restore" -Method POST `
                -Body $bodyLines -ContentType "multipart/form-data; boundary=$boundary" `
                -Headers @{ Authorization = "Bearer $token" } -UseBasicParsing -TimeoutSec 15 -ErrorAction Stop
            Write-Fail "Esperaba 400 pero obtuve $($r.StatusCode) - magic bytes NO validados"
        } catch {
            $code = if ($_.Exception.Response) { [int]$_.Exception.Response.StatusCode } else { 0 }
            if ($code -eq 400) {
                Write-Pass "Rechazado HTTP 400 correctamente (magic bytes ZIP inválidos)"
            } elseif ($code -eq 404) {
                Write-Skip "Endpoint /api/projects/restore no existe (404) - comprueba el nombre real del endpoint de importación"
            } else {
                Write-Fail "Código inesperado: $code (esperaba 400)"
            }
        }
        Remove-Item $fakeZip -Force -ErrorAction SilentlyContinue
    }
}

# ============================================================
#  RESUMEN
# ============================================================
Write-Section "RESUMEN"
Write-Host "  PASS: $script:pass" -ForegroundColor Green
Write-Host "  FAIL: $script:fail" -ForegroundColor $(if ($script:fail -gt 0) { "Red" } else { "Green" })
Write-Host "  SKIP: $script:skip" -ForegroundColor Yellow
Write-Host ""
if ($script:fail -eq 0) {
    Write-Host "OK - Pruebas automatizables superadas. Continua con las manuales." -ForegroundColor Green
    Write-Host ""
    Write-Host "Tests manuales recomendados:" -ForegroundColor Cyan
    Write-Host "  - Login con cada rol visible (Admin/Mantenim./Operador/Visor/Auditor)" -ForegroundColor Gray
    Write-Host "  - Round-trip backup: crear -> descargar -> re-importar (debe pasar)" -ForegroundColor Gray
    Write-Host "  - Cambio de password forzado en primer login" -ForegroundColor Gray
    Write-Host "  - SignalR: animacion 3D en vivo" -ForegroundColor Gray
    Write-Host "  - Si usaste -PrepareModelTests: verifica audit Model3DValidation" -ForegroundColor Gray
    exit 0
} else {
    Write-Host "FALLOS DETECTADOS - revisar antes del deploy" -ForegroundColor Red
    exit 1
}
