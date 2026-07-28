// ============================================================================
// CertificateController.cs - HTTPS Certificate Management API
// ============================================================================
// Provides endpoints for certificate distribution to client machines.
// In production with self-signed certificates, clients need to install the
// public certificate (CER) in their Trusted Root CA to avoid browser warnings.
//
// Security: Only the PUBLIC certificate is served (no private key).
// The PFX file (with private key) is NEVER exposed via API.
// ============================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using SW.PC.API.Backend.Data;
using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Services;

namespace SW.PC.API.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CertificateController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<CertificateController> _logger;
    // 📁 BD POR PROYECTO: los registros de equipos mTLS viven en Projects/{id}/data/project.db
    private readonly IProjectDbContextFactory _dbFactory;
    private readonly IAuditLogService _auditLog;

    public CertificateController(
        IConfiguration configuration,
        IWebHostEnvironment env,
        ILogger<CertificateController> logger,
        IProjectDbContextFactory dbFactory,
        IAuditLogService auditLog)
    {
        _configuration = configuration;
        _env = env;
        _logger = logger;
        _dbFactory = dbFactory;
        _auditLog = auditLog;
    }

    /// <summary>
    /// Download the public certificate (CER format) for trust installation.
    /// This contains ONLY the public key â€” safe to distribute to clients.
    /// Clients install this in their OS/browser Trusted Root CA store.
    /// </summary>
    [HttpGet("public")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public IActionResult DownloadPublicCertificate()
    {
        try
        {
            var cert = LoadServerCertificate();
            if (cert == null)
                return NotFound(new { error = "No HTTPS certificate configured on this server." });

            // Export ONLY the public key (DER-encoded X.509)
            var publicCertBytes = cert.Export(X509ContentType.Cert);
            var hostname = Environment.MachineName ?? "aquafrisch";
            var fileName = $"aquafrisch-supervisor-{hostname}.cer";

            return File(publicCertBytes, "application/x-x509-ca-cert", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting public certificate");
            return StatusCode(503, new { error = "Could not export certificate." });
        }
    }

    /// <summary>
    /// Get certificate information (thumbprint, validity, SANs, algorithm, key size).
    /// Useful for IT departments to verify certificate compliance.
    /// </summary>
    [HttpGet("info")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetCertificateInfo()
    {
        try
        {
            var cert = LoadServerCertificate();
            if (cert == null)
                return NotFound(new { error = "No HTTPS certificate configured on this server." });

            var sanExtension = cert.Extensions["2.5.29.17"]; // Subject Alternative Name OID
            var sans = new List<string>();
            if (sanExtension != null)
            {
                // Parse SANs from the formatted string
                var sanString = sanExtension.Format(true);
                foreach (var line in sanString.Split('\n', '\r'))
                {
                    var trimmed = line.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                        sans.Add(trimmed);
                }
            }

            var keySize = cert.PublicKey.GetRSAPublicKey()?.KeySize
                       ?? cert.PublicKey.GetECDsaPublicKey()?.KeySize
                       ?? 0;

            return Ok(new
            {
                subject = cert.Subject,
                issuer = cert.Issuer,
                thumbprint = cert.Thumbprint,
                serialNumber = cert.SerialNumber,
                notBefore = cert.NotBefore.ToString("yyyy-MM-dd HH:mm:ss UTC"),
                notAfter = cert.NotAfter.ToString("yyyy-MM-dd HH:mm:ss UTC"),
                daysUntilExpiry = (cert.NotAfter - DateTime.UtcNow).Days,
                signatureAlgorithm = cert.SignatureAlgorithm.FriendlyName,
                keyAlgorithm = cert.PublicKey.Oid.FriendlyName,
                keySize = keySize,
                version = cert.Version,
                subjectAlternativeNames = sans,
                isSelfSigned = cert.Subject == cert.Issuer,
                isValid = DateTime.UtcNow >= cert.NotBefore && DateTime.UtcNow <= cert.NotAfter
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading certificate info");
            return StatusCode(500, new { error = "Could not read certificate information." });
        }
    }

    /// <summary>
    /// Returns installation instructions for trusting the self-signed certificate
    /// on different operating systems and browsers.
    /// </summary>
    [HttpGet("instructions")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult GetInstallInstructions()
    {
        var cert = LoadServerCertificate();
        var isSelfSigned = cert != null && cert.Subject == cert.Issuer;
        var hostname = Request.Host.Host;
        var port = Request.Host.Port ?? 5001;
        var downloadUrl = $"{Request.Scheme}://{Request.Host}/api/certificate/public";
        var installScriptUrl = $"{Request.Scheme}://{Request.Host}/api/certificate/install-script";

        return Ok(new
        {
            isSelfSigned,
            needsInstallation = isSelfSigned,
            downloadUrl,
            installScriptUrl,
            instructions = new
            {
                automatic = new
                {
                    title = "InstalaciÃ³n automÃ¡tica (recomendado)",
                    steps = new[]
                    {
                        $"1. Descargar script: {installScriptUrl}",
                        "2. Ejecutar como Administrador (click derecho â†’ Ejecutar como administrador)",
                        "3. Reiniciar el navegador"
                    }
                },
                windows = new
                {
                    title = "Windows (Chrome, Edge, Opera) - manual",
                    steps = new[]
                    {
                        $"1. Descargar: {downloadUrl}",
                        "2. Doble click en el archivo .cer",
                        "3. Click 'Instalar certificado...'",
                        "4. Seleccionar 'Equipo local' â†’ Siguiente",
                        "5. Seleccionar 'Colocar todos los certificados en el siguiente almacÃ©n'",
                        "6. Click 'Examinar...' â†’ seleccionar 'Entidades de certificaciÃ³n raÃ­z de confianza'",
                        "7. Click Siguiente â†’ Finalizar",
                        "8. Reiniciar el navegador"
                    }
                },
                windowsPowerShell = new
                {
                    title = "Windows (PowerShell - automated)",
                    steps = new[]
                    {
                        $"1. Download: Invoke-WebRequest -Uri '{downloadUrl}' -OutFile 'aquafrisch.cer'",
                        "2. Install: Import-Certificate -FilePath 'aquafrisch.cer' -CertStoreLocation 'Cert:\\LocalMachine\\Root'",
                        "3. Restart the browser"
                    }
                },
                firefox = new
                {
                    title = "Firefox (uses its own certificate store)",
                    steps = new[]
                    {
                        $"1. Open Firefox â†’ navigate to https://{hostname}:5001",
                        "2. Click 'Advanced' â†’ 'Accept the Risk and Continue'",
                        "3. Or: Settings â†’ Privacy & Security â†’ Certificates â†’ View Certificates",
                        "4. Import the .cer file into 'Authorities' tab"
                    }
                }
            }
        });
    }

    /// <summary>
    /// Returns a BAT script that automatically downloads and installs the certificate.
    /// Users just download this file and run it as Administrator.
    /// </summary>
    [HttpGet("install-script")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetInstallScript()
    {
        var serverHost = Request.Host.Host;
        var serverPort = Request.Host.Port ?? 5001;
        var serverUrl = $"https://{serverHost}:{serverPort}";

        var script = $@"@echo off
chcp 65001 >nul
title Aquafrisch Supervisor - Instalar Certificado SSL

echo ============================================================
echo  AQUAFRISCH SUPERVISOR - Instalacion de Certificado SSL
echo ============================================================
echo.
echo  Servidor: {serverUrl}
echo.

:: Verificar permisos de administrador
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo  [ERROR] Este script requiere permisos de Administrador.
    echo  Click derecho â†’ Ejecutar como administrador
    echo.
    pause
    exit /b 1
)

echo  [1/3] Descargando certificado del servidor...
curl.exe -k -s -o ""%TEMP%\aquafrisch-supervisor.cer"" ""{serverUrl}/api/certificate/public""
if %errorlevel% neq 0 (
    echo  [ERROR] No se pudo descargar el certificado.
    echo  Verifica que el servidor esta accesible: {serverUrl}
    pause
    exit /b 1
)
echo  [OK] Certificado descargado

echo  [2/3] Instalando en Entidades de certificacion raiz de confianza...
certutil -addstore ""Root"" ""%TEMP%\aquafrisch-supervisor.cer"" >nul 2>&1
if %errorlevel% neq 0 (
    echo  [ERROR] No se pudo instalar el certificado.
    pause
    exit /b 1
)
echo  [OK] Certificado instalado correctamente

echo  [3/3] Limpiando archivos temporales...
del ""%TEMP%\aquafrisch-supervisor.cer"" >nul 2>&1
echo  [OK] Limpieza completada

echo.
echo ============================================================
echo  INSTALACION COMPLETADA
echo ============================================================
echo.
echo  El certificado SSL de Aquafrisch Supervisor se ha instalado.
echo  Reinicia el navegador para que los cambios surtan efecto.
echo.
echo  Acceso seguro: {serverUrl}
echo.
pause
";

        return File(
            System.Text.Encoding.UTF8.GetBytes(script),
            "application/x-bat",
            $"instalar-certificado-aquafrisch.bat");
    }

    // ========================================================================
    // ðŸ” mTLS â€” Identidad de mÃ¡quina por certificado cliente
    // ========================================================================

    /// <summary>
    /// Estado mTLS del servidor + identidad de la conexiÃ³n actual.
    /// AnÃ³nimo: lo usa el ClientSetup (para saber si debe hacer enrollment)
    /// y el frontend (banner "equipo no registrado").
    /// </summary>
    [HttpGet("mtls-info")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetMtlsInfo()
    {
        var clientCert = HttpContext.Connection.ClientCertificate;
        var machineName = clientCert?.GetNameInfo(X509NameType.SimpleName, forIssuer: false);
        var remoteIp = OriginPermissionEvaluator.NormalizeIp(
            HttpContext.Connection.RemoteIpAddress?.ToString());

        return Ok(new
        {
            mtlsEnabled = MtlsState.Enabled,
            requireRegisteredMachine = MtlsState.RequireRegisteredMachine,
            hasClientCertificate = clientCert != null,
            machineName,
            remoteIp
        });
    }

    /// <summary>
    /// Descarga el certificado pÃºblico de la Machine CA (DER).
    /// El ClientSetup lo instala en el equipo para que la cadena del certificado
    /// de mÃ¡quina emitido se construya correctamente (Schannel).
    /// </summary>
    [HttpGet("machine-ca")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult DownloadMachineCa()
    {
        try
        {
            var ca = MachineCaService.LoadOrCreateCa(_env.ContentRootPath);
            return File(ca.Export(X509ContentType.Cert), "application/x-x509-ca-cert", "aquafrisch-machine-ca.cer");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exportando Machine CA");
            return StatusCode(503, new { error = "Could not export Machine CA." });
        }
    }

    /// <summary>
    /// Enrollment de equipo: valida un cÃ³digo de registro de un solo uso y firma
    /// el CSR (CN=nombre del equipo) con la Machine CA. Devuelve el certificado
    /// emitido (DER) para `certreq -accept`. La clave privada NUNCA viaja.
    /// </summary>
    [HttpPost("enroll")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> EnrollMachine([FromBody] MachineEnrollRequest request)
    {
        var remoteIp = OriginPermissionEvaluator.NormalizeIp(
            HttpContext.Connection.RemoteIpAddress?.ToString());

        try
        {
            if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Csr))
                return BadRequest(new { error = "CÃ³digo y CSR son obligatorios." });

            var codeHash = HashRegistrationCode(request.Code);

            await using var db = _dbFactory.CreateDbContext();
            var regCode = await db.MachineRegistrationCodes
                .FirstOrDefaultAsync(c => c.CodeHash == codeHash);

            if (regCode == null || regCode.UsedAt != null || regCode.ExpiresAt < DateTime.Now)
            {
                await _auditLog.LogAsync(AuditCategory.Security, AuditAction.PermissionDenied, AuditResult.Warning,
                    details: $"Enrollment mTLS RECHAZADO: cÃ³digo invÃ¡lido/usado/caducado. IP={remoteIp}",
                    ipAddress: remoteIp);
                return BadRequest(new { error = "CÃ³digo de registro invÃ¡lido, usado o caducado." });
            }

            var ca = MachineCaService.LoadOrCreateCa(_env.ContentRootPath);
            var (certDer, machineName, notAfter) = MachineCaService.SignCsr(request.Csr, ca);

            // Quemar el cÃ³digo (un solo uso)
            regCode.UsedAt = DateTime.Now;
            regCode.MachineName = machineName;
            await db.SaveChangesAsync();

            await _auditLog.LogAsync(AuditCategory.Security, AuditAction.CertificateGenerate, AuditResult.Success,
                details: $"Equipo '{machineName}' registrado (mTLS). Cert vÃ¡lido hasta {notAfter:yyyy-MM-dd}. " +
                         $"CÃ³digo generado por {regCode.CreatedBy}. IP={remoteIp}",
                userName: regCode.CreatedBy, ipAddress: remoteIp);

            _logger.LogInformation("ðŸ” mTLS: equipo '{Machine}' registrado desde {Ip}", machineName, remoteIp);
            return File(certDer, "application/x-x509-user-cert", $"{machineName}.cer");
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en enrollment mTLS");
            return StatusCode(500, new { error = "Error firmando el certificado de mÃ¡quina." });
        }
    }

    /// <summary>
    /// Genera un cÃ³digo de registro de equipo (un solo uso, caduca en 24h).
    /// El cÃ³digo en claro solo se devuelve UNA vez; en BD queda su hash.
    /// </summary>
    [HttpPost("registration-codes")]
    [Authorize(Roles = "SuperAdmin,Administrator")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateRegistrationCode()
    {
        var createdBy = User.Identity?.Name ?? "unknown";
        var code = GenerateRegistrationCode();
        var expiresAt = DateTime.Now.AddHours(24);

        await using var db = _dbFactory.CreateDbContext();
        db.MachineRegistrationCodes.Add(new MachineRegistrationCode
        {
            CodeHash = HashRegistrationCode(code),
            CreatedBy = createdBy,
            CreatedAt = DateTime.Now,
            ExpiresAt = expiresAt
        });
        await db.SaveChangesAsync();

        await _auditLog.LogAsync(AuditCategory.Security, AuditAction.CertificateGenerate, AuditResult.Success,
            details: $"CÃ³digo de registro de equipo generado (mTLS), caduca {expiresAt:yyyy-MM-dd HH:mm}",
            userName: createdBy,
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        return Ok(new { code, expiresAt });
    }

    /// <summary>
    /// Lista los cÃ³digos de registro (nunca el cÃ³digo en claro) + equipos registrados.
    /// </summary>
    [HttpGet("registration-codes")]
    [Authorize(Roles = "SuperAdmin,Administrator")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListRegistrationCodes()
    {
        await using var db = _dbFactory.CreateDbContext();
        var now = DateTime.Now;
        var codes = await db.MachineRegistrationCodes
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new
            {
                c.Id,
                c.CreatedBy,
                c.CreatedAt,
                c.ExpiresAt,
                c.UsedAt,
                c.MachineName,
                status = c.UsedAt != null ? "used" : (c.ExpiresAt < now ? "expired" : "pending")
            })
            .ToListAsync();

        return Ok(new { mtlsEnabled = MtlsState.Enabled, codes });
    }

    /// <summary>
    /// Elimina un cÃ³digo de registro PENDIENTE (no usado). Los usados se conservan
    /// como registro del equipo enrolado.
    /// </summary>
    [HttpDelete("registration-codes/{id:int}")]
    [Authorize(Roles = "SuperAdmin,Administrator")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteRegistrationCode(int id)
    {
        await using var db = _dbFactory.CreateDbContext();
        var code = await db.MachineRegistrationCodes.FindAsync(id);
        if (code == null) return NotFound();
        if (code.UsedAt != null)
            return BadRequest(new { error = "No se puede eliminar un cÃ³digo ya usado (registro de equipo)." });

        db.MachineRegistrationCodes.Remove(code);
        await db.SaveChangesAsync();

        await _auditLog.LogAsync(AuditCategory.Security, AuditAction.CertificateGenerate, AuditResult.Warning,
            details: $"CÃ³digo de registro de equipo #{id} eliminado (sin usar)",
            userName: User.Identity?.Name,
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        return Ok(new { deleted = true });
    }

    /// <summary>
    /// Revoca el registro de una mÃ¡quina (elimina el MachineRegistrationCode usado).
    /// </summary>
    [HttpPost("registration-codes/{id:int}/revoke")]
    [Authorize(Roles = "SuperAdmin,Administrator")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RevokeRegistration(int id)
    {
        await using var db = _dbFactory.CreateDbContext();
        var code = await db.MachineRegistrationCodes.FindAsync(id);
        if (code == null) return NotFound();

        var machineName = code.MachineName ?? $"ID#{id}";
        db.MachineRegistrationCodes.Remove(code);
        await db.SaveChangesAsync();

        await _auditLog.LogAsync(AuditCategory.Security, AuditAction.CertificateGenerate, AuditResult.Warning,
            details: $"Registro de equipo '{machineName}' (#{id}) REVOCADO por administrador",
            userName: User.Identity?.Name,
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());

        return Ok(new { revoked = true, machineName });
    }

    /// <summary>Alfabeto sin caracteres ambiguos (sin 0/O/1/I/L).</summary>
    private const string CodeAlphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";

    private static string GenerateRegistrationCode()
    {
        var chars = new char[12];
        for (int i = 0; i < chars.Length; i++)
            chars[i] = CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
        return $"{new string(chars, 0, 4)}-{new string(chars, 4, 4)}-{new string(chars, 8, 4)}";
    }

    /// <summary>Normaliza (mayÃºsculas, sin guiones/espacios) y hashea SHA256 hex.</summary>
    private static string HashRegistrationCode(string code)
    {
        var normalized = new string(code.ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
        var hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Loads the server's HTTPS certificate from the configured PFX path.
    /// Returns null if no certificate is configured or found.
    /// </summary>
    private X509Certificate2? LoadServerCertificate()
    {
        // Try loading from Kestrel HTTPS configuration
        var certPath = _configuration["Kestrel:Endpoints:Https:Certificate:Path"];
        var certPassword = _configuration["Kestrel:Endpoints:Https:Certificate:Password"];

        if (string.IsNullOrEmpty(certPath))
            return null;

        // Resolve relative path from content root
        if (!Path.IsPathRooted(certPath))
            certPath = Path.Combine(_env.ContentRootPath, certPath);

        if (!System.IO.File.Exists(certPath))
        {
            _logger.LogWarning("Certificate file not found: {Path}", certPath);
            return null;
        }

        return new X509Certificate2(certPath, certPassword, X509KeyStorageFlags.EphemeralKeySet);
    }
}
