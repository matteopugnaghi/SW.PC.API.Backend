// ============================================================================
// ManualModeController.cs - API para Modo Manual/Mantenimiento
// ============================================================================
// Endpoints para control manual de elementos (bombas, motores, cepillos, etc.)
// - GET  /api/manual-mode/config  : Obtener configuración desde Excel hoja "Manual"
// - GET  /api/manual-mode/states  : Leer estados actuales desde PLC
// - POST /api/manual-mode/toggle  : Activar/desactivar un elemento
// 
// ⚠️ IMPORTANTE: Las variables usadas deben estar habilitadas en Variable_Views
//    para la vista "MANUAL", de lo contrario no se podrán leer/escribir.
// ============================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Models.Excel;
using SW.PC.API.Backend.Services;
using System.Text.RegularExpressions;

namespace SW.PC.API.Backend.Controllers
{
    [ApiController]
    [Route("api/manual-mode")]
    public class ManualModeController : ControllerBase
    {
        private readonly IExcelConfigService _excelConfigService;
        private readonly ITwinCATService _twinCATService;
        private readonly IRequestProjectContext _projectContext;
        private readonly IOperationLogService _operationLog;
        private readonly ILogger<ManualModeController> _logger;
        
        // Cache de mappings de Variable_Views
        private List<VariableViewMapping>? _viewMappingsCache;

        public ManualModeController(
            IExcelConfigService excelConfigService,
            ITwinCATService twinCATService,
            IRequestProjectContext projectContext,
            IOperationLogService operationLog,
            ILogger<ManualModeController> logger)
        {
            _excelConfigService = excelConfigService;
            _twinCATService = twinCATService;
            _projectContext = projectContext;
            _operationLog = operationLog;
            _logger = logger;
        }

        /// <summary>
        /// Sanitiza un string para usarlo como ID (elimina caracteres especiales)
        /// </summary>
        private static string SanitizeId(string input)
        {
            if (string.IsNullOrEmpty(input)) return "unknown";
            // Reemplazar espacios y caracteres especiales con guiones bajos
            return Regex.Replace(input.ToLowerInvariant(), @"[^a-z0-9]+", "_").Trim('_');
        }
        
        /// <summary>
        /// Verifica si una variable está habilitada para la vista MANUAL en Variable_Views
        /// </summary>
        private async Task<bool> IsVariableAllowedForManualViewAsync(string variableName)
        {
            try
            {
                // Cargar mappings si no están en cache
                if (_viewMappingsCache == null)
                {
                    var excelPath = _excelConfigService.GetExcelConfigPath();
                    _viewMappingsCache = await _excelConfigService.LoadVariableViewsAsync(excelPath);
                }
                
                // Obtener las vistas permitidas para esta variable
                var allowedViews = _excelConfigService.GetViewsForVariable(variableName, _viewMappingsCache);
                
                // Verificar si MANUAL o GLOBAL está en la lista
                var isAllowed = allowedViews.Contains(PlcViewIds.MANUAL) || allowedViews.Contains(PlcViewIds.GLOBAL);
                
                if (!isAllowed)
                {
                    _logger.LogWarning("🔧 Variable '{Var}' NO está habilitada para vista MANUAL. Vistas permitidas: [{Views}]", 
                        variableName, string.Join(", ", allowedViews));
                }
                
                return isAllowed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verificando Variable_Views para '{Var}'", variableName);
                // En caso de error, permitir por defecto (comportamiento anterior)
                return true;
            }
        }

        /// <summary>
        /// Obtener la configuración del modo manual desde Excel (hoja "Manual")
        /// </summary>
        /// <returns>Configuración con elementos controlables</returns>
        [HttpGet("config")]
        [ProducesResponseType(typeof(ManualModeConfiguration), 200)]
        public async Task<ActionResult<ManualModeConfiguration>> GetManualModeConfiguration()
        {
            try
            {
                var excelPath = _excelConfigService.GetExcelConfigPath();
                _logger.LogInformation("🔧 Loading manual mode configuration from: {Path}", excelPath);

                var excelConfig = await _excelConfigService.LoadManualPageAsync(excelPath);

                // Convertir configuración de Excel a modelo de respuesta
                var config = new ManualModeConfiguration
                {
                    ViewTitle = excelConfig.ViewTitle,
                    Elements = excelConfig.Elements.Select((e, i) => new ManualModeElement
                    {
                        Id = $"manual_{SanitizeId(e.Description)}_{i}",
                        Description = e.Description,
                        ImagePath = e.ImagePath,
                        PlcVariable = e.PlcVariable,
                        DisplayOrder = i,
                        RowIndex = e.RowIndex,
                        IsActive = false // Se leerá del PLC con /states
                    }).ToList()
                };

                _logger.LogInformation("🔧 Manual mode config loaded: {Title} with {Count} elements",
                    config.ViewTitle, config.Elements.Count);

                return Ok(config);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading manual mode configuration");
                return StatusCode(500, new { error = "Error loading manual mode configuration", details = ex.Message });
            }
        }

        /// <summary>
        /// Leer estados actuales de todos los elementos desde el PLC
        /// Solo lee variables habilitadas para la vista MANUAL en Variable_Views
        /// </summary>
        /// <returns>Diccionario de estados: ElementId -> bool</returns>
        [HttpGet("states")]
        [ProducesResponseType(typeof(ManualModeStatesResponse), 200)]
        public async Task<ActionResult<ManualModeStatesResponse>> ReadStatesFromPlc()
        {
            try
            {
                var excelPath = _excelConfigService.GetExcelConfigPath();
                var excelConfig = await _excelConfigService.LoadManualPageAsync(excelPath);

                var response = new ManualModeStatesResponse
                {
                    Timestamp = DateTime.UtcNow
                };

                int index = 0;
                foreach (var element in excelConfig.Elements)
                {
                    var elementId = $"manual_{SanitizeId(element.Description)}_{index}";
                    
                    try
                    {
                        if (!string.IsNullOrEmpty(element.PlcVariable))
                        {
                            // ✅ Verificar si la variable está habilitada para vista MANUAL
                            var isAllowed = await IsVariableAllowedForManualViewAsync(element.PlcVariable);
                            
                            if (!isAllowed)
                            {
                                _logger.LogWarning("🔧 Variable '{Var}' bloqueada - No habilitada en Variable_Views para MANUAL", element.PlcVariable);
                                response.States[elementId] = false;
                                index++;
                                continue;
                            }
                            
                            var result = await _twinCATService.ReadVariableAsync(element.PlcVariable, typeof(bool));
                            var value = result is bool b ? b : false;
                            response.States[elementId] = value;
                            _logger.LogDebug("🔧 Read state for {Id}: {Value} (var: {Var})", elementId, value, element.PlcVariable);
                        }
                        else
                        {
                            response.States[elementId] = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "🔧 Could not read PLC variable {Var} for element {Id}", element.PlcVariable, elementId);
                        response.States[elementId] = false;
                    }
                    
                    index++;
                }

                _logger.LogInformation("🔧 Read {Count} manual mode states from PLC", response.States.Count);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading manual mode states from PLC");
                return StatusCode(500, new { error = "Error reading states from PLC", details = ex.Message });
            }
        }

        /// <summary>
        /// Activar o desactivar un elemento manual
        /// </summary>
        /// <param name="request">Datos del toggle: ElementId, PlcVariable, Value</param>
        /// <returns>Resultado de la operación</returns>
        [HttpPost("toggle")]
        [Authorize] // Requiere autenticación
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(500)]
        public async Task<ActionResult> ToggleElement([FromBody] ManualModeToggleRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.PlcVariable))
                {
                    return BadRequest(new { error = "PLC variable is required" });
                }

                _logger.LogInformation("🔧 Toggle manual element - Request received: ElementId={Id}, PlcVariable={Var}, Value={Value}", 
                    request.ElementId, request.PlcVariable, request.Value);
                
                // ✅ Verificar si la variable está habilitada para vista MANUAL
                var isAllowed = await IsVariableAllowedForManualViewAsync(request.PlcVariable);
                if (!isAllowed)
                {
                    _logger.LogWarning("🔧 BLOCKED: Variable '{Var}' no está habilitada en Variable_Views para MANUAL", request.PlcVariable);
                    return BadRequest(new { 
                        error = "Variable no permitida para vista MANUAL", 
                        variable = request.PlcVariable,
                        suggestion = "Agregue esta variable a la hoja Variable_Views con la vista MANUAL habilitada"
                    });
                }
                
                // Verificar estado de conexión PLC
                var plcConnected = _twinCATService.IsConnected;
                _logger.LogInformation("🔧 PLC connection status: IsConnected={Connected}", plcConnected);

                // Escribir al PLC
                _logger.LogInformation("🔧 Calling WriteVariableAsync for {Var} with value {Value} (type: bool)", request.PlcVariable, request.Value);
                var success = await _twinCATService.WriteVariableAsync(request.PlcVariable, request.Value, typeof(bool));
                _logger.LogInformation("🔧 WriteVariableAsync result: {Success}", success);

                if (success)
                {
                    // Registrar en log de operaciones (guardar solo la clave de traducción)
                    var userName = User.Identity?.Name ?? "Unknown";
                    var descriptionKey = !string.IsNullOrEmpty(request.Description) ? request.Description : request.ElementId;
                    await _operationLog.LogAsync(
                        category: OperationCategory.Process,
                        action: OperationAction.ManualModeToggle,
                        description: descriptionKey, // Solo la clave, sin ON/OFF
                        user: userName,
                        details: new Dictionary<string, object> { ["PlcVariable"] = request.PlcVariable, ["ElementId"] = request.ElementId, ["Value"] = request.Value }
                    );

                    _logger.LogInformation("🔧 Manual element {Id} toggled to {Value} by {User}", 
                        request.ElementId, request.Value, userName);

                    return Ok(new { 
                        success = true, 
                        message = $"Element {request.ElementId} set to {(request.Value ? "ON" : "OFF")}",
                        elementId = request.ElementId,
                        value = request.Value
                    });
                }
                else
                {
                    _logger.LogWarning("🔧 Failed to toggle manual element {Id}", request.ElementId);
                    return StatusCode(500, new { error = "Failed to write to PLC", elementId = request.ElementId });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling manual element {Id}", request.ElementId);
                return StatusCode(500, new { error = "Error toggling element", details = ex.Message });
            }
        }

        /// <summary>
        /// Obtener la URL de una imagen de elemento manual
        /// </summary>
        /// <param name="imagePath">Nombre del archivo de imagen</param>
        /// <returns>URL de la imagen</returns>
        [HttpGet("image/{*imagePath}")]
        [ProducesResponseType(typeof(FileResult), 200)]
        [ProducesResponseType(404)]
        public IActionResult GetElementImage(string imagePath)
        {
            try
            {
                if (string.IsNullOrEmpty(imagePath))
                {
                    return NotFound(new { error = "Image path is required" });
                }

                // Decodificar el path si viene codificado
                imagePath = Uri.UnescapeDataString(imagePath);
                
                // Normalizar separadores de path
                imagePath = imagePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
                
                // Obtener solo el nombre del archivo
                var fileName = Path.GetFileName(imagePath);
                
                // Si el path empieza con "images\" o "Images\", quitarlo para buscar en config/Images/
                var cleanPath = imagePath;
                if (cleanPath.StartsWith("images" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                    cleanPath.StartsWith("Images" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    cleanPath = cleanPath.Substring(7); // Quitar "images\" o "Images\"
                }

                _logger.LogInformation("🔧 Image request - Original: '{Original}', FileName: '{FileName}', CleanPath: '{CleanPath}'", imagePath, fileName, cleanPath);

                // Buscar imagen en múltiples ubicaciones posibles
                var projectBasePath = _projectContext.ProjectBasePath;
                var searchPaths = new List<string>
                {
                    // 1. Projects/{proyecto}/config/Images/{archivo} (más común)
                    Path.Combine(projectBasePath, "config", "Images", fileName),
                    // 2. Projects/{proyecto}/config/Images/{cleanPath}
                    Path.Combine(projectBasePath, "config", "Images", cleanPath),
                    // 3. Projects/{proyecto}/images/{archivo}
                    Path.Combine(projectBasePath, "images", fileName),
                    // 4. wwwroot/images/{archivo}
                    Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", fileName),
                    // 5. Con path original completo: Projects/{proyecto}/{path}
                    Path.Combine(projectBasePath, imagePath),
                    // 6. wwwroot/{path}
                    Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", imagePath)
                };

                _logger.LogInformation("🔧 Searching image in {Count} locations:", searchPaths.Count);
                foreach (var p in searchPaths)
                {
                    _logger.LogInformation("   - {Path} (exists: {Exists})", p, System.IO.File.Exists(p));
                }

                string? filePath = searchPaths.FirstOrDefault(p => System.IO.File.Exists(p));

                if (filePath == null)
                {
                    _logger.LogWarning("🔧 Image not found: {Path} (tried {Count} locations)", imagePath, searchPaths.Count);
                    return NotFound(new { error = "Image not found", path = imagePath, searchedLocations = searchPaths });
                }
                
                _logger.LogInformation("🔧 Image found at: {FilePath}", filePath);

                var contentType = GetContentType(filePath);
                var fileBytes = System.IO.File.ReadAllBytes(filePath);
                return File(fileBytes, contentType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error serving image {Path}", imagePath);
                return StatusCode(500, new { error = "Error serving image" });
            }
        }

        private static string GetContentType(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            return extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".svg" => "image/svg+xml",
                _ => "application/octet-stream"
            };
        }
    }
}
