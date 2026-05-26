// ============================================================================
// FileSystemController.cs — Explorador remoto de carpetas del servidor backend.
// ============================================================================
// Uso: alimenta el FolderPickerModal del frontend para que el usuario seleccione
// una carpeta del PC donde corre el backend en lugar de teclear la ruta a mano.
//
//   • Base: /api/filesystem
//   • Autorización: Administrator (info sensible del PC).
//   • Read-only: no crea, no borra, no escribe.
//   • Soporta UNC validando si el path existe y es directorio.
// ============================================================================

using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SW.PC.API.Backend.Controllers;

[ApiController]
[Route("api/filesystem")]
[Authorize(Roles = "Administrator,SuperAdmin")]
public class FileSystemController : ControllerBase
{
    private readonly ILogger<FileSystemController> _logger;

    public FileSystemController(ILogger<FileSystemController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Lista subcarpetas de la ruta dada en el PC del backend.
    /// Si path está vacío → devuelve unidades disponibles (C:\, D:\, …).
    /// </summary>
    [HttpGet("browse")]
    public IActionResult Browse([FromQuery] string? path)
    {
        // 1) Path vacío → root: listar unidades del sistema.
        if (string.IsNullOrWhiteSpace(path))
        {
            return Ok(BuildRoot());
        }

        var trimmed = path.Trim();

        // 2) Validar y normalizar.
        string fullPath;
        try
        {
            // Permitimos UNC (\\server\share). Path.GetFullPath maneja ambos.
            fullPath = Path.GetFullPath(trimmed);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = $"Ruta inválida: {ex.Message}" });
        }

        if (!Directory.Exists(fullPath))
        {
            return NotFound(new { error = "La carpeta no existe o no es accesible." });
        }

        // 3) Listar subcarpetas (sin recursión).
        var folders = new List<FolderEntry>();
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(fullPath))
            {
                try
                {
                    var info = new DirectoryInfo(dir);
                    // Saltar carpetas ocultas/sistema para reducir ruido.
                    if ((info.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden) continue;
                    if ((info.Attributes & FileAttributes.System) == FileAttributes.System) continue;
                    folders.Add(new FolderEntry
                    {
                        Name = info.Name,
                        Path = info.FullName,
                    });
                }
                catch
                {
                    // Carpeta inaccesible: ignorar silencio.
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            return StatusCode(403, new { error = "Sin permisos para listar esta carpeta." });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al listar {Path}", fullPath);
            return BadRequest(new { error = ex.Message });
        }

        folders.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

        // 4) Calcular padre (null si ya estamos en raíz de unidad o UNC root).
        string? parent = null;
        try
        {
            var parentInfo = Directory.GetParent(fullPath);
            if (parentInfo != null) parent = parentInfo.FullName;
        }
        catch { /* sin padre */ }

        return Ok(new BrowseResult
        {
            Path = fullPath,
            Parent = parent,
            Folders = folders,
            IsRoot = false,
        });
    }

    private BrowseResult BuildRoot()
    {
        var folders = new List<FolderEntry>();
        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (!drive.IsReady) continue;
                    folders.Add(new FolderEntry
                    {
                        Name = $"{drive.Name} ({drive.DriveType})",
                        Path = drive.RootDirectory.FullName,
                    });
                }
                catch { /* ignore */ }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudieron enumerar unidades");
        }

        return new BrowseResult
        {
            Path = "",
            Parent = null,
            Folders = folders,
            IsRoot = true,
            Platform = RuntimeInformation.OSDescription,
        };
    }

    public class BrowseResult
    {
        public string Path { get; set; } = "";
        public string? Parent { get; set; }
        public List<FolderEntry> Folders { get; set; } = new();
        public bool IsRoot { get; set; }
        public string? Platform { get; set; }
    }

    public class FolderEntry
    {
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
    }
}
