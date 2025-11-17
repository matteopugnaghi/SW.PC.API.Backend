using SW.PC.API.Backend.Models;
using System.Text.Json;

namespace SW.PC.API.Backend.Services
{
    public interface IModelService
    {
        Task<IEnumerable<Model3D>> GetAllModelsAsync();
        Task<Model3D?> GetModelAsync(string id);
        Task<byte[]?> GetModelFileAsync(string fileName);
        Task<string?> GetModelFilePathAsync(string fileName);
    }
    
    public class ModelService : IModelService
    {
        private readonly string _modelsPath;
        private readonly ILogger<ModelService> _logger;
        
        public ModelService(IWebHostEnvironment environment, ILogger<ModelService> logger)
        {
            _modelsPath = Path.Combine(environment.WebRootPath, "models");
            _logger = logger;
            
            // Ensure models directory exists
            if (!Directory.Exists(_modelsPath))
            {
                Directory.CreateDirectory(_modelsPath);
            }
        }
        
        public async Task<IEnumerable<Model3D>> GetAllModelsAsync()
        {
            try
            {
                var models = new List<Model3D>();
                var supportedExtensions = new[] { ".glb", ".gltf", ".obj", ".stl" };
                
                var modelFiles = Directory.GetFiles(_modelsPath)
                    .Where(file => supportedExtensions.Contains(Path.GetExtension(file).ToLowerInvariant()));
                
                foreach (var filePath in modelFiles)
                {
                    var fileInfo = new FileInfo(filePath);
                    var fileName = fileInfo.Name;
                    var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
                    
                    var model = new Model3D
                    {
                        Id = GenerateModelId(fileName),
                        Name = fileNameWithoutExtension.Replace("_", " ").Replace("-", " "),
                        FileName = fileName,
                        FileType = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant(),
                        FileSizeBytes = fileInfo.Length,
                        CreatedAt = fileInfo.CreationTimeUtc,
                        UpdatedAt = fileInfo.LastWriteTimeUtc,
                        ThumbnailUrl = $"/api/models/{GenerateModelId(fileName)}/thumbnail"
                    };
                    
                    models.Add(model);
                }
                
                return models.OrderBy(m => m.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving models from directory: {ModelsPath}", _modelsPath);
                return new List<Model3D>();
            }
        }
        
        public async Task<Model3D?> GetModelAsync(string id)
        {
            var models = await GetAllModelsAsync();
            return models.FirstOrDefault(m => m.Id == id);
        }
        
        public async Task<byte[]?> GetModelFileAsync(string fileName)
        {
            try
            {
                var filePath = Path.Combine(_modelsPath, fileName);
                
                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("Model file not found: {FileName}", fileName);
                    return null;
                }
                
                return await File.ReadAllBytesAsync(filePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading model file: {FileName}", fileName);
                return null;
            }
        }
        
        public async Task<string?> GetModelFilePathAsync(string fileName)
        {
            try
            {
                var filePath = Path.Combine(_modelsPath, fileName);
                
                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("Model file not found: {FileName}", fileName);
                    return null;
                }
                
                return filePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model file path: {FileName}", fileName);
                return null;
            }
        }
        
        private static string GenerateModelId(string fileName)
        {
            return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(fileName))
                .Replace("+", "-")
                .Replace("/", "_")
                .TrimEnd('=');
        }
    }
}