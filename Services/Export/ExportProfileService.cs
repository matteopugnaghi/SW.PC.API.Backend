// ============================================================================
// IExportProfileService.cs / ExportProfileService.cs
// ============================================================================
// CRUD de perfiles de carpeta (ExportFolderProfile) y SMTP (ExportEmailProfile).
// La password del perfil SMTP se cifra con IDataProtector (DPAPI en Windows).
// Nunca se devuelve plaintext al frontend — sólo `HasPassword: bool`.
// ============================================================================

using System.Net;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using SW.PC.API.Backend.Data;
using SW.PC.API.Backend.Models.Export;

namespace SW.PC.API.Backend.Services.Export;

public interface IExportProfileService
{
    // Folder profiles
    Task<List<ExportFolderProfileResponse>> ListFolderProfilesAsync(CancellationToken ct = default);
    Task<ExportFolderProfileResponse?> GetFolderProfileAsync(string id, CancellationToken ct = default);
    Task<ExportFolderProfileResponse> CreateFolderProfileAsync(ExportFolderProfileRequest req, string createdBy, CancellationToken ct = default);
    Task<ExportFolderProfileResponse?> UpdateFolderProfileAsync(string id, ExportFolderProfileRequest req, CancellationToken ct = default);
    Task<bool> DeleteFolderProfileAsync(string id, CancellationToken ct = default);

    // Email profiles
    Task<List<ExportEmailProfileResponse>> ListEmailProfilesAsync(CancellationToken ct = default);
    Task<ExportEmailProfileResponse?> GetEmailProfileAsync(string id, CancellationToken ct = default);
    Task<ExportEmailProfileResponse> CreateEmailProfileAsync(ExportEmailProfileRequest req, string createdBy, CancellationToken ct = default);
    Task<ExportEmailProfileResponse?> UpdateEmailProfileAsync(string id, ExportEmailProfileRequest req, CancellationToken ct = default);
    Task<bool> DeleteEmailProfileAsync(string id, CancellationToken ct = default);
    Task<ExportEmailTestResponse> TestEmailProfileAsync(string id, ExportEmailTestRequest req, CancellationToken ct = default);

    // Resolución interna (usada por ExportService al ejecutar tareas)
    Task<ExportFolderProfile?> GetFolderProfileEntityAsync(string id, CancellationToken ct = default);
    Task<ExportSmtpSettings?> ResolveSmtpAsync(string emailProfileId, CancellationToken ct = default);
}

public class ExportProfileService : IExportProfileService
{
    private readonly IProjectDbContextFactory _dbFactory;
    private readonly ISecretProtector _secrets;
    private readonly ILogger<ExportProfileService> _logger;

    public ExportProfileService(
        IProjectDbContextFactory dbFactory,
        ISecretProtector secrets,
        ILogger<ExportProfileService> logger)
    {
        _dbFactory = dbFactory;
        _secrets = secrets;
        _logger = logger;
    }

    // ───────────────── FOLDER PROFILES ─────────────────

    public async Task<List<ExportFolderProfileResponse>> ListFolderProfilesAsync(CancellationToken ct = default)
    {
        using var db = _dbFactory.CreateDbContext();
        var items = await db.ExportFolderProfiles.OrderBy(p => p.Name).ToListAsync(ct);
        return items.Select(ToFolderResponse).ToList();
    }

    public async Task<ExportFolderProfileResponse?> GetFolderProfileAsync(string id, CancellationToken ct = default)
    {
        using var db = _dbFactory.CreateDbContext();
        var e = await db.ExportFolderProfiles.FirstOrDefaultAsync(p => p.Id == id, ct);
        return e is null ? null : ToFolderResponse(e);
    }

    public async Task<ExportFolderProfileResponse> CreateFolderProfileAsync(ExportFolderProfileRequest req, string createdBy, CancellationToken ct = default)
    {
        ValidateFolder(req);
        using var db = _dbFactory.CreateDbContext();
        var entity = new ExportFolderProfile
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = req.Name.Trim(),
            Path = req.Path.Trim(),
            Subfolder = string.IsNullOrWhiteSpace(req.Subfolder) ? null : req.Subfolder.Trim(),
            Description = req.Description,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = createdBy ?? string.Empty
        };
        db.ExportFolderProfiles.Add(entity);
        await db.SaveChangesAsync(ct);
        return ToFolderResponse(entity);
    }

    public async Task<ExportFolderProfileResponse?> UpdateFolderProfileAsync(string id, ExportFolderProfileRequest req, CancellationToken ct = default)
    {
        ValidateFolder(req);
        using var db = _dbFactory.CreateDbContext();
        var e = await db.ExportFolderProfiles.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (e is null) return null;
        e.Name = req.Name.Trim();
        e.Path = req.Path.Trim();
        e.Subfolder = string.IsNullOrWhiteSpace(req.Subfolder) ? null : req.Subfolder.Trim();
        e.Description = req.Description;
        e.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToFolderResponse(e);
    }

    public async Task<bool> DeleteFolderProfileAsync(string id, CancellationToken ct = default)
    {
        using var db = _dbFactory.CreateDbContext();
        var e = await db.ExportFolderProfiles.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (e is null) return false;
        db.ExportFolderProfiles.Remove(e);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ExportFolderProfile?> GetFolderProfileEntityAsync(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        using var db = _dbFactory.CreateDbContext();
        return await db.ExportFolderProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    // ───────────────── EMAIL PROFILES ─────────────────

    public async Task<List<ExportEmailProfileResponse>> ListEmailProfilesAsync(CancellationToken ct = default)
    {
        using var db = _dbFactory.CreateDbContext();
        var items = await db.ExportEmailProfiles.OrderBy(p => p.Name).ToListAsync(ct);
        return items.Select(ToEmailResponse).ToList();
    }

    public async Task<ExportEmailProfileResponse?> GetEmailProfileAsync(string id, CancellationToken ct = default)
    {
        using var db = _dbFactory.CreateDbContext();
        var e = await db.ExportEmailProfiles.FirstOrDefaultAsync(p => p.Id == id, ct);
        return e is null ? null : ToEmailResponse(e);
    }

    public async Task<ExportEmailProfileResponse> CreateEmailProfileAsync(ExportEmailProfileRequest req, string createdBy, CancellationToken ct = default)
    {
        ValidateEmail(req);
        using var db = _dbFactory.CreateDbContext();
        var entity = new ExportEmailProfile
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = req.Name.Trim(),
            Host = req.Host.Trim(),
            Port = req.Port,
            Username = string.IsNullOrWhiteSpace(req.Username) ? null : req.Username.Trim(),
            PasswordEncrypted = string.IsNullOrEmpty(req.Password) ? null : _secrets.Protect(req.Password),
            UseSsl = req.UseSsl,
            FromAddress = req.FromAddress.Trim(),
            FromName = string.IsNullOrWhiteSpace(req.FromName) ? null : req.FromName.Trim(),
            DefaultRecipients = req.DefaultRecipients,
            Description = req.Description,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = createdBy ?? string.Empty
        };
        db.ExportEmailProfiles.Add(entity);
        await db.SaveChangesAsync(ct);
        return ToEmailResponse(entity);
    }

    public async Task<ExportEmailProfileResponse?> UpdateEmailProfileAsync(string id, ExportEmailProfileRequest req, CancellationToken ct = default)
    {
        ValidateEmail(req);
        using var db = _dbFactory.CreateDbContext();
        var e = await db.ExportEmailProfiles.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (e is null) return null;
        e.Name = req.Name.Trim();
        e.Host = req.Host.Trim();
        e.Port = req.Port;
        e.Username = string.IsNullOrWhiteSpace(req.Username) ? null : req.Username.Trim();
        // Password: si viene null/empty se conserva la existente.
        if (!string.IsNullOrEmpty(req.Password))
            e.PasswordEncrypted = _secrets.Protect(req.Password);
        e.UseSsl = req.UseSsl;
        e.FromAddress = req.FromAddress.Trim();
        e.FromName = string.IsNullOrWhiteSpace(req.FromName) ? null : req.FromName.Trim();
        e.DefaultRecipients = req.DefaultRecipients;
        e.Description = req.Description;
        e.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToEmailResponse(e);
    }

    public async Task<bool> DeleteEmailProfileAsync(string id, CancellationToken ct = default)
    {
        using var db = _dbFactory.CreateDbContext();
        var e = await db.ExportEmailProfiles.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (e is null) return false;
        db.ExportEmailProfiles.Remove(e);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ExportEmailTestResponse> TestEmailProfileAsync(string id, ExportEmailTestRequest req, CancellationToken ct = default)
    {
        var smtp = await ResolveSmtpAsync(id, ct);
        if (smtp is null) return new ExportEmailTestResponse { Success = false, Message = "Perfil SMTP no encontrado." };
        if (!smtp.IsConfigured) return new ExportEmailTestResponse { Success = false, Message = "Perfil SMTP incompleto." };

        try
        {
            using var msg = new MailMessage();
            msg.From = new MailAddress(smtp.From);
            msg.To.Add(req.To);
            msg.Subject = "Aquafrisch Export — prueba SMTP";
            msg.Body = "Mensaje de prueba enviado desde el Gestor de Exportaciones.\r\n" +
                       $"Host: {smtp.Host}:{smtp.Port} (SSL={smtp.EnableSsl})\r\n" +
                       $"Fecha: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
            using var client = new SmtpClient(smtp.Host, smtp.Port)
            {
                EnableSsl = smtp.EnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false
            };
            if (!string.IsNullOrWhiteSpace(smtp.Username))
                client.Credentials = new NetworkCredential(smtp.Username, smtp.Password ?? string.Empty);

            await client.SendMailAsync(msg, ct);
            return new ExportEmailTestResponse { Success = true, Message = $"Email enviado a {req.To}." };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Export] TestEmailProfile {Id} falló", id);
            return new ExportEmailTestResponse { Success = false, Message = $"Error: {ex.Message}" };
        }
    }

    public async Task<ExportSmtpSettings?> ResolveSmtpAsync(string emailProfileId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(emailProfileId)) return null;
        using var db = _dbFactory.CreateDbContext();
        var e = await db.ExportEmailProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == emailProfileId, ct);
        if (e is null) return null;
        return new ExportSmtpSettings
        {
            Host = e.Host,
            Port = e.Port,
            Username = e.Username,
            Password = _secrets.Unprotect(e.PasswordEncrypted),
            From = string.IsNullOrWhiteSpace(e.FromName) ? e.FromAddress : $"{e.FromName} <{e.FromAddress}>",
            EnableSsl = e.UseSsl
        };
    }

    // ───────────────── Helpers ─────────────────

    private static void ValidateFolder(ExportFolderProfileRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) throw new ArgumentException("Nombre del perfil obligatorio.");
        if (string.IsNullOrWhiteSpace(r.Path)) throw new ArgumentException("Ruta obligatoria.");
    }

    private static void ValidateEmail(ExportEmailProfileRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) throw new ArgumentException("Nombre del perfil obligatorio.");
        if (string.IsNullOrWhiteSpace(r.Host)) throw new ArgumentException("Host SMTP obligatorio.");
        if (string.IsNullOrWhiteSpace(r.FromAddress)) throw new ArgumentException("FromAddress obligatorio.");
        if (r.Port < 1 || r.Port > 65535) throw new ArgumentException("Puerto SMTP inválido.");
    }

    private static ExportFolderProfileResponse ToFolderResponse(ExportFolderProfile e) => new()
    {
        Id = e.Id,
        Name = e.Name,
        Path = e.Path,
        Subfolder = e.Subfolder,
        Description = e.Description,
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt,
        CreatedBy = e.CreatedBy
    };

    private static ExportEmailProfileResponse ToEmailResponse(ExportEmailProfile e) => new()
    {
        Id = e.Id,
        Name = e.Name,
        Host = e.Host,
        Port = e.Port,
        Username = e.Username,
        HasPassword = !string.IsNullOrEmpty(e.PasswordEncrypted),
        UseSsl = e.UseSsl,
        FromAddress = e.FromAddress,
        FromName = e.FromName,
        DefaultRecipients = e.DefaultRecipients,
        Description = e.Description,
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt,
        CreatedBy = e.CreatedBy
    };
}
