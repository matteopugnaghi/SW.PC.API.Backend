using Microsoft.AspNetCore.Mvc;
using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Services;

namespace SW.PC.API.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ModelsController : ControllerBase
    {
        private readonly IModelService _modelService;
        private readonly ILogger<ModelsController> _logger;
        
        public ModelsController(IModelService modelService, ILogger<ModelsController> logger)
        {
            _modelService = modelService;
            _logger = logger;
        }
        
        /// <summary>
        /// Get all available 3D models
        /// </summary>
        /// <returns>List of 3D models with metadata</returns>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<Model3D>), 200)]
        public async Task<ActionResult<IEnumerable<Model3D>>> GetModels()
        {
            try
            {
                var models = await _modelService.GetAllModelsAsync();
                return Ok(models);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving models");
                return StatusCode(500, "Internal server error while retrieving models");
            }
        }
        
        /// <summary>
        /// Get a specific 3D model by ID
        /// </summary>
        /// <param name="id">Model ID</param>
        /// <returns>3D model metadata</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(Model3D), 200)]
        [ProducesResponseType(404)]
        public async Task<ActionResult<Model3D>> GetModel(string id)
        {
            try
            {
                var model = await _modelService.GetModelAsync(id);
                
                if (model == null)
                {
                    return NotFound($"Model with ID '{id}' not found");
                }
                
                return Ok(model);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving model with ID: {ModelId}", id);
                return StatusCode(500, "Internal server error while retrieving model");
            }
        }
        
        /// <summary>
        /// Download a 3D model file
        /// </summary>
        /// <param name="id">Model ID</param>
        /// <returns>3D model file</returns>
        [HttpGet("{id}/download")]
        [ProducesResponseType(typeof(FileResult), 200)]
        [ProducesResponseType(404)]
        public async Task<ActionResult> DownloadModel(string id)
        {
            try
            {
                var model = await _modelService.GetModelAsync(id);
                
                if (model == null)
                {
                    return NotFound($"Model with ID '{id}' not found");
                }
                
                var fileBytes = await _modelService.GetModelFileAsync(model.FileName);
                
                if (fileBytes == null)
                {
                    return NotFound($"Model file '{model.FileName}' not found");
                }
                
                var contentType = GetContentType(model.FileType);
                return File(fileBytes, contentType, model.FileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading model with ID: {ModelId}", id);
                return StatusCode(500, "Internal server error while downloading model");
            }
        }
        
        /// <summary>
        /// Get model file directly by filename
        /// </summary>
        /// <param name="fileName">Model file name</param>
        /// <returns>3D model file</returns>
        [HttpGet("file/{fileName}")]
        [ProducesResponseType(typeof(FileResult), 200)]
        [ProducesResponseType(404)]
        public async Task<ActionResult> GetModelFile(string fileName)
        {
            try
            {
                var fileBytes = await _modelService.GetModelFileAsync(fileName);
                
                if (fileBytes == null)
                {
                    return NotFound($"Model file '{fileName}' not found");
                }
                
                var fileExtension = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
                var contentType = GetContentType(fileExtension);
                
                return File(fileBytes, contentType, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving model file: {FileName}", fileName);
                return StatusCode(500, "Internal server error while retrieving model file");
            }
        }
        
        /// <summary>
        /// Get model thumbnail (placeholder endpoint)
        /// </summary>
        /// <param name="id">Model ID</param>
        /// <returns>Thumbnail image</returns>
        [HttpGet("{id}/thumbnail")]
        [ProducesResponseType(404)]
        public async Task<ActionResult> GetModelThumbnail(string id)
        {
            // Placeholder for thumbnail generation
            // Could be implemented to generate thumbnails from 3D models
            return NotFound("Thumbnail generation not implemented");
        }
        
        private static string GetContentType(string fileExtension)
        {
            return fileExtension.ToLowerInvariant() switch
            {
                "glb" => "model/gltf-binary",
                "gltf" => "model/gltf+json",
                "obj" => "model/obj",
                "stl" => "model/stl",
                _ => "application/octet-stream"
            };
        }
    }
}