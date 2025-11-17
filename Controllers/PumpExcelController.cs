using Microsoft.AspNetCore.Mvc;
using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Services;

namespace SW.PC.API.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PumpExcelController : ControllerBase
    {
        private readonly IPumpElementService _pumpService;
        private readonly ILogger<PumpExcelController> _logger;

        public PumpExcelController(IPumpElementService pumpService, ILogger<PumpExcelController> logger)
        {
            _pumpService = pumpService;
            _logger = logger;
        }

        /// <summary>
        /// Crear un Excel de ejemplo con elementos pump
        /// </summary>
        [HttpPost("create-template")]
        public async Task<IActionResult> CreatePumpTemplate()
        {
            try
            {
                // Crear elementos de ejemplo
                var sampleElements = new List<PumpElement3D>
                {
                    new PumpElement3D
                    {
                        // A-C: Identificación
                        TotalElements = 2, // Solo en primer elemento
                        Name = "PUMP_1",
                        FileName = "Pumps/PUMP_01.OBJ",

                        // D-F: Posición
                        OffsetX = 0,
                        OffsetY = 0,
                        OffsetZ = 0,

                        // G: Variable PLC (solo main page en nueva estructura)
                        PlcMainPageReference = "MAIN.fbMachine.st_MainForm.i_StatePumps[1]",

                        // H-K: Colores (ORDEN CORRECTO)
                        ColorElementOn = "Lime",
                        ColorElementOff = "Gray",
                        ColorElementDisabled = "Violet",
                        ColorElementAlarm = "Red",

                        // L: Descripción
                        ElementNameDescription = "P 01",
                        RotationX = 0,
                        RotationY = 0,
                        RotationZ = 0,
                        ScaleX = 1.0,
                        ScaleY = 1.0,
                        ScaleZ = 1.0,
                        IsClickable = true,
                        ShowTooltip = true,
                        NavigateToScreen = "manual",
                        AnimationType = "none",
                        AnimationSpeed = 1.0,
                        AnimateOnlyWhenOn = true,
                        InitiallyVisible = true,
                        VisibilityCondition = "",
                        Category = "pumps",
                        Layer = "default",
                        CastShadows = true,
                        ReceiveShadows = true,
                        LOD = "high"
                    },
                    new PumpElement3D
                    {
                        // Segundo elemento
                        Name = "PUMP_2",
                        FileName = "Pumps/PUMP_02.OBJ",
                        OffsetX = 5,
                        OffsetY = 0,
                        OffsetZ = 0,
                        PlcMainPageReference = "MAIN.fbMachine.st_MainForm.i_StatePumps[2]",
                        PlcManualPageReference = "MAIN.fbMachine.st_Pump[2].b_Command_Jog",
                        PlcConfigPageReference = "MAIN.fbMachine.st_GenericConfiguration.b_enablePump[2]",
                        ColorElementOn = "Blue",
                        ColorElementOff = "Gray",
                        ColorElementDisabled = "Violet",
                        ColorElementAlarm = "Red",
                        ElementNameDescription = "P 02",
                        LabelFontSize = 20,
                        LabelOffsetX_Pos1 = 20,
                        LabelOffsetY_Pos1 = 20,
                        LabelOffsetZ_Pos1 = 400,
                        LabelOffsetX_Pos2 = 20,
                        LabelOffsetY_Pos2 = 20,
                        LabelOffsetZ_Pos2 = 100,
                        OffspringsCount = 0,
                        IconFileReference = "Images/PUMP_7_8.png",
                        IconLanguageLabelRow = 110,
                        BrandAndModel = "",
                        BindGantryNumber = -1,
                        AvailableColors = "AliceBlue, Lime, Gray, Violet, Red",
                        RotationX = 0,
                        RotationY = 90, // Rotado 90 grados
                        RotationZ = 0,
                        ScaleX = 1.2, // Más grande
                        ScaleY = 1.2,
                        ScaleZ = 1.2,
                        IsClickable = true,
                        ShowTooltip = true,
                        AnimationType = "rotate",
                        AnimationSpeed = 2.0,
                        AnimateOnlyWhenOn = true,
                        InitiallyVisible = true,
                        Category = "pumps",
                        Layer = "default",
                        CastShadows = true,
                        ReceiveShadows = true,
                        LOD = "high"
                    }
                };

                // Guardar en Excel
                var fileName = "ProjectConfig_Template.xlsm";
                var success = await _pumpService.SavePumpElementsAsync(sampleElements, fileName);

                if (success)
                {
                    return Ok(new
                    {
                        message = "Excel template creado exitosamente",
                        fileName = fileName,
                        path = $"ExcelConfigs/{fileName}",
                        elements = sampleElements.Count,
                        structure = "3D Elements (29 columnas)",
                        columns = new
                        {
                            total = 29,
                            descripcion = "Estructura simplificada sin propiedades deprecadas"
                        }
                    });
                }
                else
                {
                    return StatusCode(500, new { message = "Error al crear Excel template" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creando pump template");
                return StatusCode(500, new { message = "Error creando template", error = ex.Message });
            }
        }

        /// <summary>
        /// Obtener información de las columnas del Excel
        /// </summary>
        [HttpGet("column-info")]
        public IActionResult GetColumnInfo()
        {
            var columnInfo = new
            {
                totalColumns = 29,
                excelVersion = "3D Elements (Simplificado)",
                groups = new[]
                {
                    new { name = "Identificación", columns = "A-C", count = 3, properties = new[] { "TotalElements", "Name", "FileName" } },
                    new { name = "Posición 3D", columns = "D-F", count = 3, properties = new[] { "OffsetX", "OffsetY", "OffsetZ" } },
                    new { name = "Variables PLC", columns = "G", count = 1, properties = new[] { "PlcMainPageReference" } },
                    new { name = "Colores Estado", columns = "H-K", count = 4, properties = new[] { "ColorElementOn", "ColorElementOff", "ColorElementDisabled", "ColorElementAlarm" } },
                    new { name = "Descripción", columns = "L", count = 1, properties = new[] { "ElementNameDescription" } },
                    new { name = "Rotación", columns = "M-O", count = 3, properties = new[] { "RotationX", "RotationY", "RotationZ" } },
                    new { name = "Escala", columns = "P-R", count = 3, properties = new[] { "ScaleX", "ScaleY", "ScaleZ" } },
                    new { name = "Interactividad", columns = "S-T", count = 2, properties = new[] { "IsClickable", "ShowTooltip" } },
                    new { name = "Animación", columns = "U-W", count = 3, properties = new[] { "AnimationType", "AnimationSpeed", "AnimateOnlyWhenOn" } },
                    new { name = "Visualización", columns = "X-Z", count = 3, properties = new[] { "InitiallyVisible", "Category", "Layer" } },
                    new { name = "Renderizado", columns = "AA-AC", count = 3, properties = new[] { "CastShadows", "ReceiveShadows", "LOD" } }
                }
            };

            return Ok(columnInfo);
        }
    }
}
