using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Services;

namespace SW.PC.API.Backend.Controllers;

/// <summary>
/// 🔐 EU CRA - Controlador de Recuperación de Contraseñas Offline
/// 
/// Permite recuperar contraseñas SIN INTERNET usando códigos
/// generados por Aquafrisch por teléfono.
/// 
/// FLUJO:
/// 1. Usuario llama a Aquafrisch
/// 2. Aquafrisch genera código con herramienta interna
/// 3. Usuario usa este endpoint para resetear contraseña
/// </summary>
[ApiController]
[Route("api/[controller]")]
[AllowAnonymous] // Debe ser accesible sin autenticación (el usuario olvidó su contraseña)
public class RecoveryController : ControllerBase
{
    private readonly IRecoveryCodeService _recoveryCodeService;
    private readonly IAuthenticationService _authService;
    private readonly IAuditLogService _auditLog;
    private readonly ILogger<RecoveryController> _logger;

    public RecoveryController(
        IRecoveryCodeService recoveryCodeService,
        IAuthenticationService authService,
        IAuditLogService auditLog,
        ILogger<RecoveryController> logger)
    {
        _recoveryCodeService = recoveryCodeService;
        _authService = authService;
        _auditLog = auditLog;
        _logger = logger;
    }

    /// <summary>
    /// 🔑 Recuperar contraseña usando código de Aquafrisch
    /// 
    /// El código es generado por Aquafrisch por teléfono y es válido por 24-48 horas.
    /// Funciona SIN INTERNET ya que usa algoritmo determinístico.
    /// </summary>
    /// <param name="request">Username, código de recuperación y nueva contraseña</param>
    /// <returns>Resultado de la operación</returns>
    [HttpPost("reset-with-code")]
    [ProducesResponseType(typeof(RecoveryCodeResponse), 200)]
    [ProducesResponseType(typeof(RecoveryCodeResponse), 400)]
    [ProducesResponseType(typeof(RecoveryCodeResponse), 429)]
    public async Task<ActionResult<RecoveryCodeResponse>> ResetPasswordWithCode([FromBody] RecoveryCodeRequest request)
    {
        try
        {
            // Validar request
            if (string.IsNullOrWhiteSpace(request.Username))
            {
                return BadRequest(new RecoveryCodeResponse
                {
                    Success = false,
                    Message = "El nombre de usuario es requerido"
                });
            }

            if (string.IsNullOrWhiteSpace(request.RecoveryCode))
            {
                return BadRequest(new RecoveryCodeResponse
                {
                    Success = false,
                    Message = "El código de recuperación es requerido"
                });
            }

            if (string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return BadRequest(new RecoveryCodeResponse
                {
                    Success = false,
                    Message = "La nueva contraseña es requerida"
                });
            }

            // Obtener Installation ID del sistema
            var installationId = _recoveryCodeService.GetInstallationId();

            // Validar el código de recuperación
            var isValidCode = _recoveryCodeService.ValidateRecoveryCode(
                installationId, 
                request.Username, 
                request.RecoveryCode);

            if (!isValidCode)
            {
                await _auditLog.LogAsync(
                    AuditCategory.Authentication,
                    AuditAction.PasswordReset,
                    AuditResult.Failure,
                    $"Intento de recuperación con código inválido para usuario {request.Username}",
                    request.Username,
                    "RECOVERY_SYSTEM",
                    GetClientIp());

                _logger.LogWarning("❌ Código de recuperación inválido para {Username}", request.Username);

                return BadRequest(new RecoveryCodeResponse
                {
                    Success = false,
                    Message = "Código de recuperación inválido o expirado. Contacte a Aquafrisch para obtener un nuevo código."
                });
            }

            // Código válido - Resetear contraseña
            var resetResult = await _authService.ResetPasswordWithRecoveryCodeAsync(
                request.Username, 
                request.NewPassword);

            if (!resetResult.Success)
            {
                return BadRequest(new RecoveryCodeResponse
                {
                    Success = false,
                    Message = resetResult.Message ?? "Error al resetear la contraseña"
                });
            }

            await _auditLog.LogAsync(
                AuditCategory.Authentication,
                AuditAction.PasswordReset,
                AuditResult.Success,
                $"Contraseña reseteada con código de recuperación Aquafrisch para usuario {request.Username}",
                request.Username,
                "RECOVERY_SYSTEM",
                GetClientIp());

            _logger.LogInformation("✅ Contraseña reseteada exitosamente para {Username} usando código Aquafrisch", 
                request.Username);

            return Ok(new RecoveryCodeResponse
            {
                Success = true,
                Message = "Contraseña actualizada correctamente. Ya puede iniciar sesión.",
                MustChangePassword = false // Ya la acaba de cambiar
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en recuperación de contraseña para {Username}", request.Username);
            
            return StatusCode(500, new RecoveryCodeResponse
            {
                Success = false,
                Message = "Error interno del servidor. Contacte a soporte técnico."
            });
        }
    }

    /// <summary>
    /// 📋 Obtener información del sistema para recuperación
    /// 
    /// Devuelve el Installation ID que el usuario debe proporcionar
    /// a Aquafrisch por teléfono para generar el código.
    /// </summary>
    [HttpGet("info")]
    [ProducesResponseType(typeof(RecoveryInfoResponse), 200)]
    public ActionResult<RecoveryInfoResponse> GetRecoveryInfo()
    {
        var installationId = _recoveryCodeService.GetInstallationId();

        return Ok(new RecoveryInfoResponse
        {
            InstallationId = installationId,
            SupportPhone = "+34 XXX XXX XXX", // Configurar en appsettings
            SupportEmail = "soporte@aquafrisch.com",
            Instructions = new[]
            {
                "1. Llame al teléfono de soporte de Aquafrisch",
                "2. Proporcione el ID de instalación mostrado arriba",
                "3. Proporcione su nombre de usuario",
                "4. Recibirá un código de recuperación válido por 24 horas",
                "5. Introduzca el código y su nueva contraseña"
            }
        });
    }

    /// <summary>
    /// 🔍 Verificar si un código es válido (sin resetear contraseña)
    /// 
    /// Útil para validar el código antes de pedir la nueva contraseña
    /// </summary>
    [HttpPost("validate-code")]
    [ProducesResponseType(typeof(CodeValidationResponse), 200)]
    public ActionResult<CodeValidationResponse> ValidateCode([FromBody] ValidateCodeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.RecoveryCode))
        {
            return Ok(new CodeValidationResponse
            {
                IsValid = false,
                Message = "Usuario y código son requeridos"
            });
        }

        var installationId = _recoveryCodeService.GetInstallationId();
        var isValid = _recoveryCodeService.ValidateRecoveryCode(
            installationId, 
            request.Username, 
            request.RecoveryCode);

        return Ok(new CodeValidationResponse
        {
            IsValid = isValid,
            Message = isValid 
                ? "Código válido. Puede proceder a cambiar su contraseña." 
                : "Código inválido o expirado."
        });
    }

    private string GetClientIp()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}

// DTOs adicionales
public class RecoveryInfoResponse
{
    public string InstallationId { get; set; } = string.Empty;
    public string SupportPhone { get; set; } = string.Empty;
    public string SupportEmail { get; set; } = string.Empty;
    public string[] Instructions { get; set; } = Array.Empty<string>();
}

public class ValidateCodeRequest
{
    public string Username { get; set; } = string.Empty;
    public string RecoveryCode { get; set; } = string.Empty;
}

public class CodeValidationResponse
{
    public bool IsValid { get; set; }
    public string Message { get; set; } = string.Empty;
}
