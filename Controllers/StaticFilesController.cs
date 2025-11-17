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
        
        public ModelsStaticController(IModelService modelService, ILogger<ModelsStaticController> logger)
        {
            _modelService = modelService;
            _logger = logger;
        }
        
        /// <summary>
        /// Serve 3D model files directly at /models/{fileName}
        /// </summary>
        /// <param name="fileName">Model file name (e.g., Box.glb)</param>
        /// <returns>3D model file</returns>
        [HttpGet("{fileName}")]
        [ResponseCache(Duration = 3600)] // Cache for 1 hour
        public async Task<ActionResult> GetFile(string fileName)
        {
            try
            {
                _logger.LogInformation("📥 Request for static file: {FileName}", fileName);
                
                var fileBytes = await _modelService.GetModelFileAsync(fileName);
                
                if (fileBytes == null)
                {
                    _logger.LogWarning("❌ File not found: {FileName}", fileName);
                    return NotFound($"Model file '{fileName}' not found");
                }
                
                var fileExtension = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
                var contentType = GetContentType(fileExtension);
                
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
