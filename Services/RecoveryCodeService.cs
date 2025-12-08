using System.Security.Cryptography;
using System.Text;

namespace SW.PC.API.Backend.Services;

/// <summary>
/// 🔐 EU CRA - Servicio de Códigos de Recuperación Offline
/// 
/// Genera códigos de recuperación determinísticos que funcionan SIN INTERNET.
/// El mismo algoritmo se usa en:
/// - Backend (para validar)
/// - Herramienta interna Aquafrisch (para generar por teléfono)
/// 
/// FLUJO:
/// 1. Usuario olvida contraseña → llama a Aquafrisch
/// 2. Aquafrisch genera código con su herramienta
/// 3. Usuario introduce código en pantalla de recuperación
/// 4. Backend valida con el mismo algoritmo
/// 5. Contraseña reseteada
/// </summary>
public interface IRecoveryCodeService
{
    /// <summary>
    /// Genera código de recuperación para un usuario
    /// </summary>
    string GenerateRecoveryCode(string installationId, string username, DateTime date);
    
    /// <summary>
    /// Valida código de recuperación (acepta hoy y ayer por cambio de día)
    /// </summary>
    bool ValidateRecoveryCode(string installationId, string username, string code);
    
    /// <summary>
    /// Obtiene el Installation ID del sistema actual
    /// </summary>
    string GetInstallationId();
}

public class RecoveryCodeService : IRecoveryCodeService
{
    private readonly ILogger<RecoveryCodeService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IExcelConfigService _excelConfigService;
    private string? _cachedInstallationId;
    
    // 🔐 SECRETO CONOCIDO SOLO POR AQUAFRISCH
    // Este valor DEBE ser el mismo en:
    // - Este servicio (backend)
    // - Herramienta generadora de Aquafrisch
    // NUNCA compartir con el cliente
    private const string AQUAFRISCH_SECRET = "AQF-2024-S3CR3T-K3Y-N0T-SH4R3";
    
    public RecoveryCodeService(
        ILogger<RecoveryCodeService> logger,
        IConfiguration configuration,
        IExcelConfigService excelConfigService)
    {
        _logger = logger;
        _configuration = configuration;
        _excelConfigService = excelConfigService;
    }
    
    /// <summary>
    /// Genera código de recuperación determinístico
    /// 
    /// Fórmula: HMAC-SHA256(InstallationId + Username + Date + Secret)
    /// Resultado: Formato AQFR-XXXX-XXXX-XXXX (16 chars)
    /// </summary>
    public string GenerateRecoveryCode(string installationId, string username, DateTime date)
    {
        // Normalizar inputs
        var normalizedInstallation = installationId.ToUpperInvariant().Trim();
        var normalizedUsername = username.ToLowerInvariant().Trim();
        var dateString = date.ToString("yyyy-MM-dd"); // Solo fecha, sin hora
        
        // Crear string a hashear
        var dataToHash = $"{normalizedInstallation}|{normalizedUsername}|{dateString}|{AQUAFRISCH_SECRET}";
        
        // Generar HMAC-SHA256
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(AQUAFRISCH_SECRET));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(dataToHash));
        
        // Convertir a código legible (Base32-like para evitar confusiones 0/O, 1/I/L)
        var code = ConvertToReadableCode(hash);
        
        _logger.LogDebug("Código de recuperación generado para {Username} en instalación {InstallationId}", 
            normalizedUsername, normalizedInstallation);
        
        return code;
    }
    
    /// <summary>
    /// Valida código de recuperación
    /// Acepta código de HOY o AYER (por si es cerca de medianoche)
    /// </summary>
    public bool ValidateRecoveryCode(string installationId, string username, string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return false;
        
        var normalizedCode = code.ToUpperInvariant().Replace("-", "").Replace(" ", "");
        
        // Generar código esperado para HOY
        var todayCode = GenerateRecoveryCode(installationId, username, DateTime.Today);
        var normalizedTodayCode = todayCode.Replace("-", "");
        
        // Generar código esperado para AYER (tolerancia por cambio de día)
        var yesterdayCode = GenerateRecoveryCode(installationId, username, DateTime.Today.AddDays(-1));
        var normalizedYesterdayCode = yesterdayCode.Replace("-", "");
        
        var isValid = normalizedCode == normalizedTodayCode || normalizedCode == normalizedYesterdayCode;
        
        if (isValid)
        {
            _logger.LogInformation("✅ Código de recuperación VÁLIDO para {Username} en instalación {InstallationId}", 
                username, installationId);
        }
        else
        {
            _logger.LogWarning("❌ Código de recuperación INVÁLIDO para {Username} en instalación {InstallationId}", 
                username, installationId);
        }
        
        return isValid;
    }
    
    /// <summary>
    /// Obtiene el Installation ID configurado para este sistema
    /// Lee desde: 1) Excel SystemConfig, 2) appsettings, 3) variable entorno, 4) fallback
    /// </summary>
    public string GetInstallationId()
    {
        // Usar cache si ya lo tenemos
        if (!string.IsNullOrEmpty(_cachedInstallationId))
            return _cachedInstallationId;
        
        // 1. Buscar en Excel (fuente principal) - solo pasar nombre de archivo
        try
        {
            var excelFileName = _configuration["PlcPolling:ExcelFileName"] ?? "ProjectConfig.xlsm";
            
            // ExcelConfigService maneja el path internamente
            var systemConfig = _excelConfigService.LoadSystemConfigurationAsync(excelFileName).GetAwaiter().GetResult();
            if (systemConfig != null && !string.IsNullOrEmpty(systemConfig.InstallationId) && systemConfig.InstallationId != "AQF-DEFAULT-001")
            {
                _cachedInstallationId = systemConfig.InstallationId;
                _logger.LogInformation("InstallationId cargado desde Excel: {InstallationId}", _cachedInstallationId);
                return _cachedInstallationId;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo cargar InstallationId desde Excel, usando fallback");
        }
        
        // 2. Buscar en configuración explícita (appsettings.json)
        var configuredId = _configuration["Installation:Id"];
        if (!string.IsNullOrEmpty(configuredId))
        {
            _cachedInstallationId = configuredId;
            return _cachedInstallationId;
        }
        
        // 3. Buscar en variable de entorno
        var envId = Environment.GetEnvironmentVariable("AQUAFRISCH_INSTALLATION_ID");
        if (!string.IsNullOrEmpty(envId))
        {
            _cachedInstallationId = envId;
            return _cachedInstallationId;
        }
        
        // 4. Fallback: generar basado en machine name
        var machineName = Environment.MachineName;
        var hash = machineName.GetHashCode().ToString("X8");
        _cachedInstallationId = $"AQF-{DateTime.Now.Year}-{hash}";
        _logger.LogWarning("Usando InstallationId generado automáticamente: {InstallationId}. Configure uno en el Excel.", _cachedInstallationId);
        return _cachedInstallationId;
    }
    
    /// <summary>
    /// Convierte hash a código legible formato AQFR-XXXX-XXXX-XXXX
    /// Usa caracteres que no se confunden: 2-9, A-H, J-N, P-Z (sin 0,1,I,O)
    /// </summary>
    private string ConvertToReadableCode(byte[] hash)
    {
        // Caracteres sin ambigüedad visual
        const string chars = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";
        
        var sb = new StringBuilder();
        sb.Append("AQFR-"); // Prefijo Aquafrisch
        
        // Tomar 12 caracteres del hash (3 grupos de 4)
        for (int i = 0; i < 12; i++)
        {
            if (i > 0 && i % 4 == 0)
                sb.Append('-');
            
            var index = hash[i] % chars.Length;
            sb.Append(chars[index]);
        }
        
        return sb.ToString();
    }
}

/// <summary>
/// 📋 Modelos para recuperación de contraseña
/// </summary>
public class RecoveryCodeRequest
{
    /// <summary>
    /// Nombre de usuario que necesita recuperar contraseña
    /// </summary>
    public string Username { get; set; } = string.Empty;
    
    /// <summary>
    /// Código de recuperación proporcionado por Aquafrisch
    /// </summary>
    public string RecoveryCode { get; set; } = string.Empty;
    
    /// <summary>
    /// Nueva contraseña (debe cumplir política de seguridad)
    /// </summary>
    public string NewPassword { get; set; } = string.Empty;
}

public class RecoveryCodeResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool MustChangePassword { get; set; }
}
