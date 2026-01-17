using Microsoft.AspNetCore.Mvc;
using SW.PC.API.Backend.Services;
using System.Text.Json;

namespace SW.PC.API.Backend.Controllers
{
    /// <summary>
    /// Controller para gestión de traducciones del sistema (i18n).
    /// Sirve las traducciones desde el archivo JSON del proyecto activo.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class TranslationsController : ControllerBase
    {
        private readonly IRequestProjectContext _projectContext;
        private readonly IExcelConfigService _excelConfigService;
        private readonly ILogger<TranslationsController> _logger;
        
        // Cache de traducciones por proyecto
        private static readonly Dictionary<string, (TranslationsData Data, DateTime LoadedAt)> _cache = new();
        private static readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);
        private static readonly object _cacheLock = new();

        public TranslationsController(
            IRequestProjectContext projectContext,
            IExcelConfigService excelConfigService,
            ILogger<TranslationsController> logger)
        {
            _projectContext = projectContext;
            _excelConfigService = excelConfigService;
            _logger = logger;
        }

        /// <summary>
        /// Obtiene todas las traducciones del proyecto activo.
        /// </summary>
        /// <returns>Objeto con metadata, páginas y traducciones</returns>
        [HttpGet]
        [ProducesResponseType(typeof(TranslationsData), 200)]
        public async Task<ActionResult<TranslationsData>> GetAllTranslations()
        {
            try
            {
                var translations = await LoadTranslationsAsync();
                return Ok(translations);
            }
            catch (FileNotFoundException)
            {
                _logger.LogWarning("🌐 Archivo de traducciones no encontrado para proyecto {Project}", _projectContext.ProjectId);
                return NotFound(new { error = "Translations file not found", projectId = _projectContext.ProjectId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🌐 Error cargando traducciones");
                return StatusCode(500, new { error = "Error loading translations", message = ex.Message });
            }
        }

        /// <summary>
        /// Obtiene las traducciones para una página específica.
        /// </summary>
        /// <param name="pageId">ID de la página (LOGIN, MAIN, TRAIN, etc.)</param>
        /// <returns>Traducciones de la página solicitada</returns>
        [HttpGet("page/{pageId}")]
        [ProducesResponseType(typeof(PageTranslationsResponse), 200)]
        public async Task<ActionResult<PageTranslationsResponse>> GetPageTranslations(string pageId)
        {
            try
            {
                var translations = await LoadTranslationsAsync();
                var normalizedPageId = pageId.ToUpperInvariant();

                if (!translations.Pages.ContainsKey(normalizedPageId))
                {
                    return NotFound(new { error = $"Page '{pageId}' not found", availablePages = translations.Pages.Keys });
                }

                var pageInfo = translations.Pages[normalizedPageId];
                var pageTranslations = new Dictionary<string, Dictionary<string, string>>();

                // Obtener las traducciones de los labels de esta página
                foreach (var labelId in pageInfo.Labels)
                {
                    if (translations.Translations.TryGetValue(labelId, out var labelTranslations))
                    {
                        pageTranslations[labelId] = labelTranslations;
                    }
                }

                // Incluir también COMMON si existe
                if (translations.Pages.ContainsKey("COMMON") && normalizedPageId != "COMMON")
                {
                    foreach (var labelId in translations.Pages["COMMON"].Labels)
                    {
                        if (translations.Translations.TryGetValue(labelId, out var labelTranslations))
                        {
                            pageTranslations[labelId] = labelTranslations;
                        }
                    }
                }

                return Ok(new PageTranslationsResponse
                {
                    PageId = normalizedPageId,
                    Description = pageInfo.Description,
                    Labels = pageInfo.Labels,
                    Translations = pageTranslations,
                    AvailableLanguages = translations.Metadata.Languages,
                    DefaultLanguage = translations.Metadata.DefaultLanguage
                });
            }
            catch (FileNotFoundException)
            {
                return NotFound(new { error = "Translations file not found" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🌐 Error cargando traducciones de página {PageId}", pageId);
                return StatusCode(500, new { error = "Error loading page translations", message = ex.Message });
            }
        }

        /// <summary>
        /// Obtiene la configuración de i18n incluyendo si mostrar IDs de labels.
        /// </summary>
        /// <returns>Configuración de internacionalización</returns>
        [HttpGet("config")]
        [ProducesResponseType(typeof(I18nConfigResponse), 200)]
        public async Task<ActionResult<I18nConfigResponse>> GetI18nConfig()
        {
            try
            {
                var translations = await LoadTranslationsAsync();
                var systemConfig = await _excelConfigService.LoadSystemConfigurationAsync(_projectContext.ExcelConfigPath);

                return Ok(new I18nConfigResponse
                {
                    AvailableLanguages = translations.Metadata.Languages,
                    DefaultLanguage = systemConfig?.DefaultLanguage ?? translations.Metadata.DefaultLanguage,
                    ExposeLabelIds = systemConfig?.ExposeLabelIds ?? false,
                    AvailablePages = translations.Pages.Keys.ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🌐 Error obteniendo configuración i18n");
                return StatusCode(500, new { error = "Error loading i18n config", message = ex.Message });
            }
        }

        /// <summary>
        /// Invalida la caché de traducciones del proyecto actual.
        /// Útil después de editar el archivo translations.json.
        /// </summary>
        [HttpPost("invalidate-cache")]
        [ProducesResponseType(200)]
        public ActionResult InvalidateCache()
        {
            lock (_cacheLock)
            {
                var cacheKey = GetCacheKey();
                if (_cache.ContainsKey(cacheKey))
                {
                    _cache.Remove(cacheKey);
                    _logger.LogInformation("🌐 Cache de traducciones invalidado para {Project}", _projectContext.ProjectId);
                }
            }
            return Ok(new { success = true, message = "Cache invalidated" });
        }

        /// <summary>
        /// Obtiene una traducción específica por ID.
        /// </summary>
        /// <param name="labelId">ID del label (ej: login.status.online)</param>
        /// <param name="language">Código de idioma ISO 639-2 (opcional, usa default si no se especifica)</param>
        /// <returns>Texto traducido</returns>
        [HttpGet("label/{labelId}")]
        [ProducesResponseType(typeof(LabelTranslationResponse), 200)]
        public async Task<ActionResult<LabelTranslationResponse>> GetLabelTranslation(string labelId, [FromQuery] string? language = null)
        {
            try
            {
                var translations = await LoadTranslationsAsync();
                
                if (!translations.Translations.TryGetValue(labelId, out var labelTranslations))
                {
                    return NotFound(new { error = $"Label '{labelId}' not found" });
                }

                var lang = language?.ToUpperInvariant() ?? translations.Metadata.DefaultLanguage;
                var text = labelTranslations.GetValueOrDefault(lang) 
                    ?? labelTranslations.GetValueOrDefault(translations.Metadata.DefaultLanguage)
                    ?? labelId; // Fallback al ID

                return Ok(new LabelTranslationResponse
                {
                    LabelId = labelId,
                    Language = lang,
                    Text = text,
                    AllTranslations = labelTranslations
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🌐 Error obteniendo traducción de {LabelId}", labelId);
                return StatusCode(500, new { error = "Error loading label translation", message = ex.Message });
            }
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // MÉTODOS PRIVADOS
        // ═══════════════════════════════════════════════════════════════════════════

        private string GetCacheKey() => $"translations_{_projectContext.ProjectId}";

        private async Task<TranslationsData> LoadTranslationsAsync()
        {
            var cacheKey = GetCacheKey();

            // Verificar caché
            lock (_cacheLock)
            {
                if (_cache.TryGetValue(cacheKey, out var cached) && 
                    DateTime.Now - cached.LoadedAt < _cacheExpiration)
                {
                    return cached.Data;
                }
            }

            // Cargar desde archivo
            var translationsPath = Path.Combine(_projectContext.TranslationsPath, "translations.json");
            
            if (!System.IO.File.Exists(translationsPath))
            {
                _logger.LogWarning("🌐 Archivo no encontrado: {Path}", translationsPath);
                throw new FileNotFoundException("Translations file not found", translationsPath);
            }

            var jsonContent = await System.IO.File.ReadAllTextAsync(translationsPath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };

            var data = JsonSerializer.Deserialize<TranslationsData>(jsonContent, options);
            
            if (data == null)
            {
                throw new InvalidOperationException("Failed to deserialize translations file");
            }

            // Guardar en caché
            lock (_cacheLock)
            {
                _cache[cacheKey] = (data, DateTime.Now);
            }

            _logger.LogInformation("🌐 Traducciones cargadas: {Count} labels, {Languages} idiomas", 
                data.Translations.Count, data.Metadata.Languages.Count);

            return data;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // DTOs
    // ═══════════════════════════════════════════════════════════════════════════

    public class TranslationsData
    {
        public TranslationsMetadata Metadata { get; set; } = new();
        public Dictionary<string, PageInfo> Pages { get; set; } = new();
        public Dictionary<string, Dictionary<string, string>> Translations { get; set; } = new();
    }

    public class TranslationsMetadata
    {
        public string Version { get; set; } = "1.0.0";
        public string LastModified { get; set; } = "";
        public List<string> Languages { get; set; } = new() { "SPA", "ENG" };
        public string DefaultLanguage { get; set; } = "SPA";
        public string? Description { get; set; }
    }

    public class PageInfo
    {
        public string? Description { get; set; }
        public List<string> Labels { get; set; } = new();
    }

    public class PageTranslationsResponse
    {
        public string PageId { get; set; } = "";
        public string? Description { get; set; }
        public List<string> Labels { get; set; } = new();
        public Dictionary<string, Dictionary<string, string>> Translations { get; set; } = new();
        public List<string> AvailableLanguages { get; set; } = new();
        public string DefaultLanguage { get; set; } = "SPA";
    }

    public class I18nConfigResponse
    {
        public List<string> AvailableLanguages { get; set; } = new();
        public string DefaultLanguage { get; set; } = "SPA";
        public bool ExposeLabelIds { get; set; }
        public List<string> AvailablePages { get; set; } = new();
    }

    public class LabelTranslationResponse
    {
        public string LabelId { get; set; } = "";
        public string Language { get; set; } = "";
        public string Text { get; set; } = "";
        public Dictionary<string, string> AllTranslations { get; set; } = new();
    }
}
