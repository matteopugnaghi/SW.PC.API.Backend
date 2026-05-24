using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using SW.PC.API.Backend.Services;

namespace SW.PC.API.Backend.Controllers
{
    /// <summary>
    /// Sirve los ficheros del manual MkDocs HTML del proyecto activo.
    /// Ruta física: Projects/{id}/docs/AQSmanual_HTML/{lang}/...
    /// Ruta web:    /api/manual/{lang}/{*path}
    ///
    /// Va por /api/* para evitar:
    ///  - El service worker de la PWA React (que devuelve index.html en 404).
    ///  - El fallback SPA de ASP.NET Core (MapFallbackToFile).
    /// </summary>
    [ApiController]
    [Route("api/manual")]
    public class ManualController : ControllerBase
    {
        private readonly IRequestProjectContext _requestContext;
        private readonly ILogger<ManualController> _logger;
        private static readonly FileExtensionContentTypeProvider _mimeProvider = new();

        public ManualController(IRequestProjectContext requestContext, ILogger<ManualController> logger)
        {
            _requestContext = requestContext;
            _logger = logger;
        }

        /// <summary>
        /// Devuelve true si existe al menos un index.html en el idioma indicado.
        /// El idioma por defecto (es) está en la raíz del manual; el resto en subcarpetas.
        /// </summary>
        [HttpGet("exists/{lang}")]
        public IActionResult Exists(string lang)
        {
            var basePath = GetManualRoot();
            var langDir = ResolveLangDir(basePath, lang);
            var indexPath = Path.Combine(langDir, "index.html");
            var exists = System.IO.File.Exists(indexPath);
            return Ok(new { available = exists, lang });
        }

        /// <summary>
        /// Sirve cualquier fichero del manual. Si no existe, devuelve 404 real.
        /// El idioma por defecto (es) se sirve desde la raíz; el resto desde {lang}/.
        /// </summary>
        [HttpGet("{lang}/{**path}")]
        public IActionResult GetFile(string lang, string? path)
        {
            // Sanitizar path: prohibir traversal
            if (string.IsNullOrWhiteSpace(path))
            {
                path = "index.html";
            }

            if (path.Contains("..") || Path.IsPathRooted(path))
            {
                return BadRequest("Invalid path");
            }

            var basePath = GetManualRoot();
            var langDir = ResolveLangDir(basePath, lang);
            var fullPath = Path.GetFullPath(Path.Combine(langDir, path));

            // Verificar que sigue dentro de la carpeta del manual (defensa anti-traversal)
            var safeRoot = Path.GetFullPath(basePath);
            if (!fullPath.StartsWith(safeRoot, StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Invalid path");
            }

            // Si la ruta es un directorio (URL bonita de MkDocs tipo "fr/conceptos/"),
            // probar con index.html dentro de ese directorio
            if (Directory.Exists(fullPath))
            {
                var indexCandidate = Path.Combine(fullPath, "index.html");
                if (System.IO.File.Exists(indexCandidate))
                {
                    fullPath = indexCandidate;
                }
            }
            else if (!System.IO.File.Exists(fullPath))
            {
                // Si la URL no termina en barra pero apunta a un directorio, también probar
                if (Directory.Exists(fullPath.TrimEnd('/', '\\')))
                {
                    var indexCandidate = Path.Combine(fullPath.TrimEnd('/', '\\'), "index.html");
                    if (System.IO.File.Exists(indexCandidate))
                    {
                        fullPath = indexCandidate;
                    }
                }
            }

            // Fallback: si el fichero no existe en la carpeta del idioma,
            // probar en la raíz (para assets compartidos: assets/, css/, js/, img/, search/)
            if (!System.IO.File.Exists(fullPath))
            {
                var rootFallback = Path.GetFullPath(Path.Combine(basePath, path));
                if (rootFallback.StartsWith(safeRoot, StringComparison.OrdinalIgnoreCase))
                {
                    if (System.IO.File.Exists(rootFallback))
                    {
                        fullPath = rootFallback;
                    }
                    else if (Directory.Exists(rootFallback))
                    {
                        var indexCandidate = Path.Combine(rootFallback, "index.html");
                        if (System.IO.File.Exists(indexCandidate))
                        {
                            fullPath = indexCandidate;
                        }
                    }
                }

                if (!System.IO.File.Exists(fullPath))
                {
                    _logger.LogDebug("Manual file not found: {Path}", fullPath);
                    return NotFound(new { error = "Manual file not found", requested = $"{lang}/{path}" });
                }
            }

            if (!_mimeProvider.TryGetContentType(fullPath, out var contentType))
            {
                contentType = "application/octet-stream";
            }

            // Cache corto para que los cambios se vean al actualizar el manual
            Response.Headers["Cache-Control"] = "no-cache, must-revalidate";

            // El manual se embebe en un <iframe> desde la app React, que en
            // desarrollo corre en otro puerto (localhost:3000/3001) y por tanto
            // es cross-origin respecto a este backend (localhost:5000/5001).
            // El middleware global aplica X-Frame-Options: SAMEORIGIN que
            // bloquearía el iframe → sobrescribimos con CSP frame-ancestors
            // permisivo solo para esta ruta (el manual es contenido estático
            // del proyecto, no expone datos sensibles).
            Response.Headers.Remove("X-Frame-Options");
            Response.Headers["Content-Security-Policy"] = "frame-ancestors *;";

            // Inyectar CSS para ocultar el selector de idioma interno de MkDocs
            // (el cambio de idioma se hace desde la app, no desde el manual).
            // Además eliminar los <link rel="alternate" hreflang="..."> porque el JS
            // de MkDocs Material los usa para auto-redirigir al "idioma preferido"
            // guardado en localStorage, lo que provoca redirección a "/" (app React)
            // y pantalla en blanco dentro del iframe.
            if (contentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase))
            {
                var html = System.IO.File.ReadAllText(fullPath);
                const string hideLangCss = "<style>.md-header__option,[data-md-component=\"select\"]{display:none !important;}</style>";

                // Neutralizar el script personalizado de persistencia de idioma
                // (extra.js → aq_lang en localStorage) que provoca redirecciones
                // a URLs absolutas /es/, /en/, /fr/, /it/ → fuera del iframe → pantalla en blanco.
                // Limpiamos también la clave por si quedó guardada de visitas anteriores.
                const string neutralizeLang = "<script>try{localStorage.removeItem('aq_lang');}catch(e){}</script>";

                // Strip <link rel="alternate" ... hreflang="..."> tags
                html = System.Text.RegularExpressions.Regex.Replace(
                    html,
                    @"<link\s+[^>]*rel\s*=\s*[""']alternate[""'][^>]*hreflang\s*=\s*[""'][^""']*[""'][^>]*>",
                    string.Empty,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase,
                    TimeSpan.FromSeconds(1));

                // Strip <script src=".../extra.js"></script> (sin atributos extra delicados)
                html = System.Text.RegularExpressions.Regex.Replace(
                    html,
                    @"<script[^>]*src\s*=\s*[""'][^""']*extra\.js[""'][^>]*>\s*</script>",
                    string.Empty,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase,
                    TimeSpan.FromSeconds(1));

                if (html.Contains("</head>", StringComparison.OrdinalIgnoreCase))
                {
                    html = System.Text.RegularExpressions.Regex.Replace(
                        html, "</head>", neutralizeLang + hideLangCss + "</head>",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
                }
                return Content(html, "text/html; charset=utf-8");
            }

            var stream = System.IO.File.OpenRead(fullPath);
            return File(stream, contentType);
        }

        private string GetManualRoot()
        {
            return Path.Combine(_requestContext.DocsPath, "AQSmanual_HTML");
        }

        /// <summary>
        /// Resuelve la carpeta física del idioma. El default (es) vive en la raíz
        /// del sitio MkDocs i18n; en/fr/it viven en subcarpetas.
        /// </summary>
        private static string ResolveLangDir(string basePath, string lang)
        {
            // Si existe la subcarpeta {lang}/ úsala; si no, usa la raíz (caso del idioma default)
            var sub = Path.Combine(basePath, lang);
            return Directory.Exists(sub) ? sub : basePath;
        }
    }
}
