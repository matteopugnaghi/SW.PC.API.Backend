using Microsoft.AspNetCore.Mvc;
using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Services;

namespace SW.PC.API.Backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PumpElementsController : ControllerBase
    {
        private readonly IPumpElementService _pumpElementService;
        private readonly ITwinCATService _twinCATService;
        private readonly ILogger<PumpElementsController> _logger;

        public PumpElementsController(
            IPumpElementService pumpElementService,
            ITwinCATService twinCATService,
            ILogger<PumpElementsController> logger)
        {
            _pumpElementService = pumpElementService;
            _twinCATService = twinCATService;
            _logger = logger;
        }

        /// <summary>
        /// Obtener todos los elementos de bombas desde Excel
        /// </summary>
        /// <returns>Lista de elementos PumpElement3D</returns>
        [HttpGet]
        public async Task<ActionResult<List<PumpElement3D>>> GetAllPumpElements()
        {
            try
            {
                var elements = await _pumpElementService.LoadPumpElementsAsync("ProjectConfig.xlsm");
                
                _logger.LogInformation("Returning {Count} pump elements", elements.Count);
                
                return Ok(elements);
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogWarning(ex, "Excel file not found");
                return NotFound(new { message = "Excel configuration file not found", error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading pump elements");
                return StatusCode(500, new { message = "Error loading pump elements", error = ex.Message });
            }
        }

        /// <summary>
        /// Obtener un elemento de bomba específico por nombre
        /// </summary>
        /// <param name="name">Nombre del elemento</param>
        /// <returns>Elemento PumpElement3D</returns>
        [HttpGet("{name}")]
        public async Task<ActionResult<PumpElement3D>> GetPumpElementByName(string name)
        {
            try
            {
                var elements = await _pumpElementService.LoadPumpElementsAsync("ProjectConfig.xlsm");
                var element = elements.FirstOrDefault(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

                if (element == null)
                {
                    return NotFound(new { message = $"Pump element '{name}' not found" });
                }

                return Ok(element);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading pump element {Name}", name);
                return StatusCode(500, new { message = "Error loading pump element", error = ex.Message });
            }
        }

        /// <summary>
        /// Obtener elementos filtrados por categoría
        /// </summary>
        /// <param name="category">Categoría (pumps, valves, tanks...)</param>
        /// <returns>Lista filtrada</returns>
        [HttpGet("category/{category}")]
        public async Task<ActionResult<List<PumpElement3D>>> GetPumpElementsByCategory(string category)
        {
            try
            {
                var elements = await _pumpElementService.LoadPumpElementsAsync("ProjectConfig.xlsm");
                var filtered = elements.Where(e => e.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();

                return Ok(filtered);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading pump elements by category {Category}", category);
                return StatusCode(500, new { message = "Error loading pump elements", error = ex.Message });
            }
        }

        /// <summary>
        /// Obtener estadísticas de elementos
        /// </summary>
        /// <returns>Resumen estadístico</returns>
        [HttpGet("stats")]
        public async Task<ActionResult<object>> GetPumpElementsStats()
        {
            try
            {
                var elements = await _pumpElementService.LoadPumpElementsAsync("ProjectConfig.xlsm");

                var stats = new
                {
                    totalElements = elements.Count,
                    totalWithChildren = elements.Count(e => e.OffspringsCount > 0),
                    totalChildren = elements.Sum(e => e.OffspringsCount),
                    categories = elements.GroupBy(e => e.Category).Select(g => new { category = g.Key, count = g.Count() }),
                    plcVariables = new
                    {
                        mainPageRefs = elements.Count(e => !string.IsNullOrEmpty(e.PlcMainPageReference)),
                        manualPageRefs = elements.Count(e => !string.IsNullOrEmpty(e.PlcManualPageReference)),
                        configPageRefs = elements.Count(e => !string.IsNullOrEmpty(e.PlcConfigPageReference))
                    },
                    colors = new
                    {
                        uniqueOnColors = elements.Select(e => e.ColorElementOn).Distinct().Count(),
                        uniqueOffColors = elements.Select(e => e.ColorElementOff).Distinct().Count()
                    }
                };

                return Ok(stats);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating pump elements stats");
                return StatusCode(500, new { message = "Error calculating stats", error = ex.Message });
            }
        }

        /// <summary>
        /// Guardar elementos de bombas en Excel
        /// </summary>
        /// <param name="elements">Lista de elementos a guardar</param>
        /// <returns>Resultado de la operación</returns>
        [HttpPost]
        public async Task<ActionResult> SavePumpElements([FromBody] List<PumpElement3D> elements)
        {
            try
            {
                var success = await _pumpElementService.SavePumpElementsAsync(elements, "ProjectConfig_Output.xlsm");

                if (success)
                {
                    return Ok(new { message = "Pump elements saved successfully", count = elements.Count });
                }
                else
                {
                    return StatusCode(500, new { message = "Failed to save pump elements" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving pump elements");
                return StatusCode(500, new { message = "Error saving pump elements", error = ex.Message });
            }
        }

        /// <summary>
        /// Leer estado de una bomba específica desde TwinCAT PLC
        /// </summary>
        /// <param name="pumpIndex">Índice de la bomba (1, 2, 3...)</param>
        /// <returns>Estado de la bomba (0=Disabled, 1=Off, 2=On, 3=Alarm)</returns>
        [HttpGet("plc/pump/{pumpIndex}/state")]
        public async Task<ActionResult<object>> GetPumpStateFromPLC(int pumpIndex)
        {
            try
            {
                if (!_twinCATService.IsConnected)
                {
                    return StatusCode(503, new { message = "PLC not connected" });
                }

                var variableName = $"MAIN.fbMachine.st_MainForm.i_StatePumps[{pumpIndex}]";
                var state = await _twinCATService.ReadVariableAsync(variableName, typeof(int));

                if (state == null)
                {
                    return NotFound(new { message = $"Variable {variableName} not found" });
                }

                var stateInt = Convert.ToInt32(state);
                var stateDescription = stateInt switch
                {
                    0 => "Disabled",
                    1 => "Off",
                    2 => "On",
                    3 => "Alarm",
                    _ => "Unknown"
                };

                _logger.LogInformation("Pump {Index} state: {State} ({Description})", pumpIndex, stateInt, stateDescription);

                // Nota: El broadcast via SignalR ahora lo maneja PlcPollingService automáticamente
                // Este endpoint solo retorna el estado actual para consultas bajo demanda

                return Ok(new
                {
                    pumpIndex,
                    variableName,
                    state = stateInt,
                    stateDescription,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading pump state from PLC");
                return StatusCode(500, new { message = "Error reading PLC variable", error = ex.Message });
            }
        }

        /// <summary>
        /// Obtener estado actual de cualquier variable PLC
        /// </summary>
        /// <param name="variableName">Nombre completo de la variable PLC</param>
        /// <returns>Valor actual de la variable</returns>
        [HttpGet("plc/variable")]
        public async Task<ActionResult<object>> GetPlcVariableState([FromQuery] string variableName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(variableName))
                {
                    return BadRequest(new { message = "Variable name is required" });
                }

                if (!_twinCATService.IsConnected)
                {
                    return StatusCode(503, new { message = "PLC not connected" });
                }

                var value = await _twinCATService.ReadVariableAsync(variableName, typeof(int));

                if (value == null)
                {
                    return NotFound(new { message = $"Variable {variableName} not found or could not be read" });
                }

                _logger.LogDebug("Variable {Name} value: {Value}", variableName, value);

                return Ok(new
                {
                    variableName,
                    value,
                    timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading PLC variable {Variable}", variableName);
                return StatusCode(500, new { message = "Error reading PLC variable", error = ex.Message });
            }
        }

        /// <summary>
        /// Obtener configuración de colores por estado PLC desde Excel
        /// Convierte los colores de los elementos en un formato usable por el frontend
        /// </summary>
        /// <param name="fileName">Nombre del archivo Excel (default: ProjectConfig.xlsm)</param>
        /// <returns>Array de configuraciones de color por estado</returns>
        [HttpGet("state-colors")]
        public async Task<ActionResult<object>> GetStateColors([FromQuery] string fileName = "ProjectConfig.xlsm")
        {
            try
            {
                var elements = await _pumpElementService.LoadPumpElementsAsync(fileName);
                var stateColors = new List<object>();

                foreach (var element in elements)
                {
                    if (string.IsNullOrEmpty(element.PlcMainPageReference))
                        continue;

                    // Crear configuración SOLO para estados con colores definidos en Excel
                    // Si el Excel está vacío, el frontend usará el color original del GLB/GLTF
                    
                    // Estado 0: Disabled
                    if (!string.IsNullOrWhiteSpace(element.ColorElementDisabled))
                    {
                        stateColors.Add(CreateStateColorConfig(element.PlcMainPageReference, 0, "Disabled", element.ColorElementDisabled));
                    }

                    // Estado 1: Off
                    if (!string.IsNullOrWhiteSpace(element.ColorElementOff))
                    {
                        stateColors.Add(CreateStateColorConfig(element.PlcMainPageReference, 1, "Off", element.ColorElementOff));
                    }

                    // Estado 2: On
                    if (!string.IsNullOrWhiteSpace(element.ColorElementOn))
                    {
                        stateColors.Add(CreateStateColorConfig(element.PlcMainPageReference, 2, "On", element.ColorElementOn));
                    }

                    // Estado 3: Alarm
                    if (!string.IsNullOrWhiteSpace(element.ColorElementAlarm))
                    {
                        stateColors.Add(CreateStateColorConfig(element.PlcMainPageReference, 3, "Alarm", element.ColorElementAlarm));
                    }
                }

                _logger.LogInformation("Generated {Count} state color configurations from {FileName}", stateColors.Count, fileName);
                return Ok(stateColors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating state colors from {FileName}", fileName);
                return StatusCode(500, new { message = "Error loading state colors", error = ex.Message });
            }
        }

        /// <summary>
        /// Crear configuración de color para un estado específico
        /// </summary>
        private object CreateStateColorConfig(string variableName, int stateValue, string stateName, string colorHex)
        {
            // Parsear color hex a RGB
            var (r, g, b) = ParseHexColor(colorHex);

            return new
            {
                variablePattern = variableName,
                stateValue,
                stateName,
                colorR = r,
                colorG = g,
                colorB = b,
                colorHex = colorHex.StartsWith("#") ? colorHex : $"#{colorHex}",
                colorNormalized = new
                {
                    r = r / 255.0,
                    g = g / 255.0,
                    b = b / 255.0
                }
            };
        }

        /// <summary>
        /// Parsear color hexadecimal o nombre de color a RGB
        /// </summary>
        private (int r, int g, int b) ParseHexColor(string color)
        {
            if (string.IsNullOrWhiteSpace(color))
                return (128, 128, 128); // Gris por defecto

            color = color.Trim();

            // Si es un color nombrado (ej: "Lime", "Red"), convertir a hex
            var namedColors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Lime", "#00FF00" },
                { "Gray", "#808080" },
                { "Grey", "#808080" },
                { "Violet", "#EE82EE" },
                { "Red", "#FF0000" },
                { "Green", "#008000" },
                { "Blue", "#0000FF" },
                { "Yellow", "#FFFF00" },
                { "Orange", "#FFA500" },
                { "Cyan", "#00FFFF" },
                { "Magenta", "#FF00FF" },
                { "White", "#FFFFFF" },
                { "Black", "#000000" }
            };

            if (namedColors.TryGetValue(color, out var hexColor))
            {
                color = hexColor;
            }

            // Remover # si existe
            color = color.TrimStart('#');

            // Convertir hex a RGB
            try
            {
                if (color.Length == 6)
                {
                    int r = Convert.ToInt32(color.Substring(0, 2), 16);
                    int g = Convert.ToInt32(color.Substring(2, 2), 16);
                    int b = Convert.ToInt32(color.Substring(4, 2), 16);
                    return (r, g, b);
                }
                else if (color.Length == 3)
                {
                    // Formato corto #RGB → #RRGGBB
                    int r = Convert.ToInt32(new string(color[0], 2), 16);
                    int g = Convert.ToInt32(new string(color[1], 2), 16);
                    int b = Convert.ToInt32(new string(color[2], 2), 16);
                    return (r, g, b);
                }
            }
            catch
            {
                _logger.LogWarning("Invalid color format: {Color}, using default gray", color);
            }

            return (128, 128, 128); // Gris por defecto en caso de error
        }
    }
}
