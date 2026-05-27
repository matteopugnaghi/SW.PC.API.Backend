using System.Collections.Concurrent;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace SW.PC.API.Backend.Services
{
    /// <summary>
    /// IFileProvider dinámico para servir archivos GLB/GLTF/OBJ bajo /models.
    /// 
    /// Resuelve la carpeta física en CADA request:
    /// - En Development con header X-Project-Id: usa el proyecto del request (IRequestProjectContext).
    /// - En el resto de casos: usa el proyecto global (IProjectContextService.ModelsPath).
    /// 
    /// Soluciona el bug en el que /models/{file} servía siempre desde la carpeta del
    /// proyecto en el que arrancó el backend, ignorando los cambios posteriores hechos
    /// vía SetActiveProject o vía X-Project-Id (servidor empresa multi-instancia).
    /// </summary>
    public class ProjectModelsFileProvider : IFileProvider
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IProjectContextService _globalContext;
        private readonly ILogger<ProjectModelsFileProvider> _logger;

        // Cache de PhysicalFileProvider por ruta absoluta para evitar
        // reconstruirlos en cada request (cada uno crea un FileSystemWatcher).
        private readonly ConcurrentDictionary<string, PhysicalFileProvider> _providers = new();

        public ProjectModelsFileProvider(
            IHttpContextAccessor httpContextAccessor,
            IProjectContextService globalContext,
            ILogger<ProjectModelsFileProvider> logger)
        {
            _httpContextAccessor = httpContextAccessor;
            _globalContext = globalContext;
            _logger = logger;
        }

        public IFileInfo GetFileInfo(string subpath)
        {
            var provider = ResolveProvider();
            return provider?.GetFileInfo(subpath) ?? new NotFoundFileInfo(subpath);
        }

        public IDirectoryContents GetDirectoryContents(string subpath)
        {
            var provider = ResolveProvider();
            return provider?.GetDirectoryContents(subpath) ?? NotFoundDirectoryContents.Singleton;
        }

        public IChangeToken Watch(string filter)
        {
            var provider = ResolveProvider();
            return provider?.Watch(filter) ?? NullChangeToken.Singleton;
        }

        private PhysicalFileProvider? ResolveProvider()
        {
            var path = ResolveModelsPath();
            if (string.IsNullOrWhiteSpace(path)) return null;

            return _providers.GetOrAdd(path, p =>
            {
                if (!Directory.Exists(p))
                {
                    try
                    {
                        Directory.CreateDirectory(p);
                        _logger.LogInformation("📁 Created models directory: {Path}", p);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "⚠️ Could not create models directory: {Path}", p);
                    }
                }
                _logger.LogInformation("📁 ProjectModelsFileProvider → new PhysicalFileProvider for: {Path}", p);
                return new PhysicalFileProvider(p);
            });
        }

        private string ResolveModelsPath()
        {
            // Intentar resolver el contexto de proyecto del request (scoped).
            // Esto honora el header X-Project-Id en Development (servidor empresa).
            try
            {
                var http = _httpContextAccessor.HttpContext;
                if (http != null)
                {
                    var requestContext = http.RequestServices.GetService<IRequestProjectContext>();
                    if (requestContext != null && !string.IsNullOrWhiteSpace(requestContext.ModelsPath))
                    {
                        return requestContext.ModelsPath;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not resolve IRequestProjectContext, falling back to global");
            }

            // Fallback: ruta del proyecto global activo (refresca ante SetActiveProject).
            return _globalContext.ModelsPath;
        }
    }
}
