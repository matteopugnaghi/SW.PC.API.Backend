// ============================================================================
// LocalFileRunner.cs — Persiste el archivo en una carpeta local o UNC
// ============================================================================
// Modelo de seguridad (CRA / OWASP — Path Traversal):
//   - Las carpetas se configuran como ExportFolderProfiles desde la UI.
//     El operador es responsable de su elección (no hay whitelist Excel).
//   - Si SystemConfig.AllowedExportFolders está configurado (legacy), se
//     aplica como restricción adicional opcional. Si está vacío, permite
//     cualquier ruta normalizada por el OS.
//   - El path final (Folder + Filename) DEBE quedar dentro de la carpeta
//     resuelta tras normalización (Path.GetFullPath).
//   - El filename NO puede contener separadores ni caracteres prohibidos.
// ============================================================================

using Microsoft.Extensions.Logging;
using SW.PC.API.Backend.Models.Export;

namespace SW.PC.API.Backend.Services.Export.Runners;

public class LocalFileRunner : IExportRunner
{
    private readonly ILogger<LocalFileRunner>? _logger;

    public LocalFileRunner() { }
    public LocalFileRunner(ILogger<LocalFileRunner> logger) { _logger = logger; }

    public string DestinationType => "local";

    public async Task<ExportResult> ExecuteAsync(ExportRunContext ctx, CancellationToken ct = default)
    {
        var result = new ExportResult { DestinationType = DestinationType };

        var folder = ctx.Config?.Folder?.Trim();
        if (string.IsNullOrWhiteSpace(folder))
        {
            result.Success = false;
            result.ErrorMessage = "Carpeta destino no especificada.";
            return result;
        }

        // Normalización de la carpeta (resuelve '..', symlinks parcialmente).
        string canonicalFolder;
        try
        {
            canonicalFolder = Path.GetFullPath(folder)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Ruta de carpeta inválida: {ex.Message}";
            return result;
        }

        // Whitelist legacy opcional: si Excel define AllowedExportFolders se respeta.
        if (ctx.AllowedFolders.Count > 0 &&
            !IsFolderAuthorized(canonicalFolder, ctx.AllowedFolders))
        {
            result.Success = false;
            result.ErrorMessage = $"Carpeta '{folder}' no está autorizada (AllowedExportFolders).";
            return result;
        }

        if (!IsFilenameSafe(ctx.Filename))
        {
            result.Success = false;
            result.ErrorMessage = $"Nombre de archivo inválido: '{ctx.Filename}'.";
            return result;
        }

        // Path final + verificación que sigue dentro de la carpeta resuelta
        // (defensa contra '..' o symlinks).
        var fullPath = Path.GetFullPath(Path.Combine(canonicalFolder, ctx.Filename));
        if (!fullPath.StartsWith(canonicalFolder, StringComparison.OrdinalIgnoreCase))
        {
            result.Success = false;
            result.ErrorMessage = "Path traversal detectado. Escritura abortada.";
            return result;
        }

        try
        {
            Directory.CreateDirectory(canonicalFolder);

            // Estrategia anti-sobreescritura (incremental / skip)
            var strategy = (ctx.Config?.OnFileExists ?? "overwrite").Trim().ToLowerInvariant();
            if (File.Exists(fullPath))
            {
                if (strategy == "skip")
                {
                    result.Success = true;
                    result.Path = fullPath;
                    result.SizeBytes = new FileInfo(fullPath).Length;
                    result.ErrorMessage = $"Archivo ya existe — escritura omitida (estrategia 'skip').";
                    return result;
                }
                if (strategy == "rename")
                {
                    fullPath = FindNextAvailablePath(fullPath);
                }
                // 'overwrite' o cualquier otro valor → comportamiento por defecto
            }

            await File.WriteAllBytesAsync(fullPath, ctx.File.Bytes, ct);

            result.Success = true;
            result.Path = fullPath;
            result.SizeBytes = ctx.File.Bytes.LongLength;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger?.LogWarning(ex, "[ExportLocal] Permisos insuficientes escribiendo en '{Folder}'", canonicalFolder);
            result.Success = false;
            result.ErrorMessage = $"Permisos insuficientes para escribir en '{canonicalFolder}': {ex.Message}";
        }
        catch (IOException ex)
        {
            _logger?.LogWarning(ex, "[ExportLocal] Error de E/S escribiendo en '{Path}'", fullPath);
            result.Success = false;
            result.ErrorMessage = $"Error de E/S: {ex.Message}";
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "[ExportLocal] Error inesperado escribiendo en '{Path}'", fullPath);
            result.Success = false;
            result.ErrorMessage = $"Error inesperado: {ex.Message}";
        }

        return result;
    }

    private static bool IsFolderAuthorized(string canonicalFolder, IReadOnlyList<string> allowed)
    {
        foreach (var a in allowed)
        {
            if (string.IsNullOrWhiteSpace(a)) continue;
            string allowedFull;
            try { allowedFull = Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
            catch { continue; }

            if (string.Equals(canonicalFolder, allowedFull, StringComparison.OrdinalIgnoreCase) ||
                canonicalFolder.StartsWith(allowedFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static bool IsFilenameSafe(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename)) return false;
        if (filename.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return false;
        // Bloquea separadores y referencias relativas explícitas.
        if (filename.Contains('/') || filename.Contains('\\') || filename.Contains("..")) return false;
        return true;
    }

    /// <summary>
    /// Busca el siguiente nombre libre añadiendo sufijo _001, _002, … antes de la extensión.
    /// Ej.: <c>report.xlsx</c> → <c>report_001.xlsx</c> si ya existe.
    /// Aborta tras 9999 intentos (devuelve la última ruta probada).
    /// </summary>
    private static string FindNextAvailablePath(string fullPath)
    {
        var dir = Path.GetDirectoryName(fullPath) ?? string.Empty;
        var name = Path.GetFileNameWithoutExtension(fullPath);
        var ext = Path.GetExtension(fullPath);
        for (int i = 1; i <= 9999; i++)
        {
            var candidate = Path.Combine(dir, $"{name}_{i:D3}{ext}");
            if (!File.Exists(candidate)) return candidate;
        }
        return Path.Combine(dir, $"{name}_{DateTime.Now:yyyyMMddHHmmssfff}{ext}");
    }
}
