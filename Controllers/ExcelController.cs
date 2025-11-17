using Microsoft.AspNetCore.Mvc;
using SW.PC.API.Backend.Models.Excel;
using SW.PC.API.Backend.Services;

namespace SW.PC.API.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ExcelController : ControllerBase
    {
        private readonly IExcelConfigService _excelService;
        private readonly ILogger<ExcelController> _logger;
        
        public ExcelController(IExcelConfigService excelService, ILogger<ExcelController> logger)
        {
            _excelService = excelService;
            _logger = logger;
        }
        
        /// <summary>
        /// Crear un archivo Excel de ejemplo con datos de prueba
        /// </summary>
        [HttpPost("create-template")]
        public async Task<IActionResult> CreateTemplate()
        {
            try
            {
                var config = new ProjectConfiguration
                {
                    ProjectName = "Proyecto de Prueba",
                    ProjectCode = "TEST_001",
                    Customer = "Cliente Demo",
                    CreatedDate = DateTime.Now,
                    
                    // Pantallas de ejemplo
                    Screens = new List<HMIScreen>
                    {
                        new HMIScreen 
                        { 
                            ScreenId = "SCREEN_MAIN", 
                            ScreenName = "Principal", 
                            Title = "Pantalla Principal",
                            DisplayOrder = 1,
                            IsEnabled = true,
                            IconName = "home"
                        }
                    },
                    
                    // Variables PLC de ejemplo
                    PlcVariables = new List<PlcVariable>
                    {
                        new PlcVariable
                        {
                            VariableName = "MotorSpeed",
                            SymbolPath = "MAIN.nSpeed",
                            DataType = "INT",
                            AccessMode = "ReadWrite",
                            UpdateRateMs = 1000,
                            Description = "Velocidad del motor",
                            Unit = "RPM",
                            LogToDatabase = true
                        }
                    },
                    
                    // Modelos 3D de ejemplo
                    Models3D = new List<Model3DConfig>
                    {
                        new Model3DConfig
                        {
                            ModelId = "MDL001",
                            ModelName = "Box Test",
                            FileName = "Box.glb",
                            FileType = "glb",
                            Description = "Cubo de prueba",
                            Category = "Equipment",
                            AssociatedScreen = "SCREEN_MAIN",
                            IsEnabled = true,
                            DisplayOrder = 1
                        }
                    }
                };
                
                var success = await _excelService.SaveProjectConfigurationAsync(config, "ProjectConfig.xlsx");
                
                if (success)
                {
                    return Ok(new { message = "Excel template created successfully", path = "ExcelConfigs/ProjectConfig.xlsx" });
                }
                
                return StatusCode(500, "Failed to create Excel template");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating Excel template");
                return StatusCode(500, ex.Message);
            }
        }
    }
}
