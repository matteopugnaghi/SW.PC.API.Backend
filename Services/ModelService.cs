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
        string GetModelsPath();
    }
    
    public class ModelService : IModelService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<ModelService> _logger;
        private readonly IRequestProjectContext _projectContext;
        
        public ModelService(
            IWebHostEnvironment environment, 
            ILogger<ModelService> logger,
            IRequestProjectContext projectContext)
        {
            _environment = environment;
            _logger = logger;
            _projectContext = projectContext;
        }
        
        /// <summary>
        /// Obtiene la ruta actual de la carpeta de modelos (del proyecto actual del request).
        /// </summary>
        public string GetModelsPath()
        {
            var modelsPath = _projectContext.ModelsPath;
            
            // Asegurar que existe la carpeta
            if (!Directory.Exists(modelsPath))
            {
                Directory.CreateDirectory(modelsPath);
                _logger.LogInformation("📁 ModelService: Created models folder at {Path}", modelsPath);
            }
            
            return modelsPath;
        }
        
        public async Task<IEnumerable<Model3D>> GetAllModelsAsync()
        {
            try
            {
                var modelsPath = GetModelsPath();
                _logger.LogDebug("📁 ModelService: Loading models from {Path} (proyecto: {Project})", 
                    modelsPath, _projectContext.ProjectId);
                
                var models = new List<Model3D>();
                var supportedExtensions = new[] { ".glb", ".gltf", ".obj", ".stl" };
                
                var modelFiles = Directory.GetFiles(modelsPath)
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
                _logger.LogError(ex, "Error retrieving models from directory: {ModelsPath}", GetModelsPath());
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
                var modelsPath = GetModelsPath();
                var filePath = Path.Combine(modelsPath, fileName);
                
                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("Model file not found: {FileName} in {Path}", fileName, modelsPath);
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
                var modelsPath = GetModelsPath();
                var filePath = Path.Combine(modelsPath, fileName);
                
                if (!File.Exists(filePath))
                {
                    _logger.LogWarning("Model file not found: {FileName} in {Path}", fileName, modelsPath);
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