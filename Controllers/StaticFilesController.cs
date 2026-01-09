using Microsoft.AspNetCore.Mvc;
using SW.PC.API.Backend.Services;

namespace SW.PC.API.Backend.Controllers
{
    /// <summary>
    /// Controller to serve static 3D model files at /models/{filename}
    /// This mimics static file serving but through a controller endpoint
    /// </summary>
    [ApiController]
    [Route("models")] // Changed from [controller] to explicit "models"
    public class ModelsStaticController : ControllerBase
    {
        private readonly IModelService _modelService;
        private readonly ILogger<ModelsStaticController> _logger;
        private readonly IExcelConfigService _excelConfigService;
        
        public ModelsStaticController(
            IModelService modelService, 
            ILogger<ModelsStaticController> logger,
            IExcelConfigService excelConfigService)
        {
            _modelService = modelService;
            _logger = logger;
            _excelConfigService = excelConfigService;
        }
        
        /// <summary>
        /// Serve 3D model files directly at /models/{fileName}
        /// El modo de caché se determina por EnvironmentMode en Excel (System Config):
        /// - development: sin caché para ver cambios inmediatos
        /// - production: caché de 1 hora para mejor rendimiento
        /// </summary>
        /// <param name="fileName">Model file name (e.g., Box.glb)</param>
        /// <returns>3D model file</returns>
        [HttpGet("{fileName}")]
        public async Task<ActionResult> GetFile(string fileName)
        {
            try
            {
                // Leer EnvironmentMode del Excel (System Config)
                var systemConfig = _excelConfigService.LoadSystemConfigurationAsync("ProjectConfig.xlsm").GetAwaiter().GetResult();
                var environmentMode = systemConfig?.EnvironmentMode?.ToLower() ?? "development";
                var isDevelopment = environmentMode == "development";
                
                // Log la ruta real que está usando el ModelService
                var modelsPath = _modelService.GetModelsPath();
                _logger.LogInformation("📥 Request for static file: {FileName} (EnvironmentMode: {Mode}, ModelsPath: {Path})", 
                    fileName, environmentMode, modelsPath);
                
                var fileBytes = await _modelService.GetModelFileAsync(fileName);
                
                if (fileBytes == null)
                {
                    _logger.LogWarning("❌ File not found: {FileName}", fileName);
                    return NotFound($"Model file '{fileName}' not found");
                }
                
                var fileExtension = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
                var contentType = GetContentType(fileExtension);
                
                // 🔄 CACHE CONDICIONAL basado en EnvironmentMode del Excel:
                // - development: NO-CACHE para ver cambios inmediatos en modelos GLB
                // - production: Cache de 1 hora para mejor rendimiento
                if (isDevelopment)
                {
                    Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
                    Response.Headers["Pragma"] = "no-cache";
                    Response.Headers["Expires"] = "0";
                    _logger.LogDebug("🔄 Development mode (Excel): No cache for {FileName}", fileName);
                }
                else
                {
                    Response.Headers["Cache-Control"] = "public, max-age=3600"; // 1 hora en producción
                }
                
                _logger.LogInformation("✅ Serving file: {FileName} ({Size} bytes, {ContentType})", 
                    fileName, fileBytes.Length, contentType);
                
                return File(fileBytes, contentType, fileName, enableRangeProcessing: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Error serving file: {FileName}", fileName);
                return StatusCode(500, "Internal server error while serving model file");
            }
        }
        
        private static string GetContentType(string fileExtension)
        {
            return fileExtension.ToLowerInvariant() switch
            {
                "glb" => "model/gltf-binary",
                "gltf" => "model/gltf+json",
                "obj" => "model/obj",
                "stl" => "model/stl",
                "mtl" => "text/plain",
                "png" => "image/png",
                "jpg" or "jpeg" => "image/jpeg",
                _ => "application/octet-stream"
            };
        }
    }
}
