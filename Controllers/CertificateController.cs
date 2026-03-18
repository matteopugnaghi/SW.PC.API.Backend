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

using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography.X509Certificates;

namespace SW.PC.API.Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CertificateController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<CertificateController> _logger;

    public CertificateController(
        IConfiguration configuration,
        IWebHostEnvironment env,
        ILogger<CertificateController> logger)
    {
        _configuration = configuration;
        _env = env;
        _logger = logger;
    }

    /// <summary>
    /// Download the public certificate (CER format) for trust installation.
    /// This contains ONLY the public key — safe to distribute to clients.
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
                    title = "Instalación automática (recomendado)",
                    steps = new[]
                    {
                        $"1. Descargar script: {installScriptUrl}",
                        "2. Ejecutar como Administrador (click derecho → Ejecutar como administrador)",
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
                        "4. Seleccionar 'Equipo local' → Siguiente",
                        "5. Seleccionar 'Colocar todos los certificados en el siguiente almacén'",
                        "6. Click 'Examinar...' → seleccionar 'Entidades de certificación raíz de confianza'",
                        "7. Click Siguiente → Finalizar",
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
                        $"1. Open Firefox → navigate to https://{hostname}:5001",
                        "2. Click 'Advanced' → 'Accept the Risk and Continue'",
                        "3. Or: Settings → Privacy & Security → Certificates → View Certificates",
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
    echo  Click derecho → Ejecutar como administrador
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
