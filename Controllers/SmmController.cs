// ============================================================================
// SmmController.cs — SMM (Statistics & Maintenance Module) Public API
// ============================================================================
// Decisiones FROZEN: DEC-019, DEC-022, DEC-024, DEC-026.
// Endpoint /api/smm/info expone tier AquarIA + SystemDeliveryDate + ContinuousReadTime.
// Frontend lo consume al login para renderizar badge AquarIA BASIC/PRO.
// ============================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SW.PC.API.Backend.Data;
using SW.PC.API.Backend.Models.Smm;
using SW.PC.API.Backend.Services;
using SW.PC.API.Backend.Services.Smm;

namespace SW.PC.API.Backend.Controllers
{
    [ApiController]
    [Route("api/smm")]
    public class SmmController : ControllerBase
    {
        private readonly ILogger<SmmController> _logger;
        private readonly IExcelConfigService _excelConfigService;
        private readonly IRequestProjectContext _projectContext;
        private readonly SmmOptions _smmOptions;
        private readonly IProjectDbContextFactory _dbFactory;
        private readonly ISmmCaptureService _capture;
        private readonly ISmmExcelSyncService _excelSync;

        public SmmController(
            ILogger<SmmController> logger,
            IExcelConfigService excelConfigService,
            IRequestProjectContext projectContext,
            IOptions<SmmOptions> smmOptions,
            IProjectDbContextFactory dbFactory,
            ISmmCaptureService capture,
            ISmmExcelSyncService excelSync)
        {
            _logger = logger;
            _excelConfigService = excelConfigService;
            _projectContext = projectContext;
            _smmOptions = smmOptions.Value;
            _dbFactory = dbFactory;
            _capture = capture;
            _excelSync = excelSync;
        }

        /// <summary>
        /// Endpoint público (sin auth) que expone metadata SMM mínima para el frontend.
        /// DEC-022: aquariaTier permite renderizado condicional Gama 1/2.
        /// DEC-024: systemDeliveryDate usado por AquarIA G1 cuando no hay ciclos.
        /// DEC-026: continuousReadTime hora del job nocturno Continuous.
        /// </summary>
        [HttpGet("info")]
        [AllowAnonymous]
        public async Task<IActionResult> GetInfoAsync()
        {
            string tier = string.IsNullOrWhiteSpace(_smmOptions.Tier) ? "Gama1" : _smmOptions.Tier;

            System.DateTime? systemDeliveryDate = null;
            string continuousReadTime = "03:00";
            string? projectId = _projectContext.ProjectId;

            try
            {
                var excelPath = _projectContext.ExcelConfigPath;
                if (!string.IsNullOrWhiteSpace(excelPath) && System.IO.File.Exists(excelPath))
                {
                    var sysCfg = await _excelConfigService.LoadSystemConfigurationAsync(excelPath);
                    if (sysCfg != null)
                    {
                        systemDeliveryDate = sysCfg.SystemDeliveryDate;
                        if (!string.IsNullOrWhiteSpace(sysCfg.ContinuousReadTime))
                            continuousReadTime = sysCfg.ContinuousReadTime;
                    }
                }
                else
                {
                    _logger.LogDebug("SMM info: Excel no disponible para proyecto '{Project}'. Devolviendo defaults.", projectId);
                }
            }
            catch (System.Exception ex)
            {
                _logger.LogWarning(ex, "SMM info: error leyendo Excel del proyecto '{Project}'. Devolviendo defaults.", projectId);
            }

            return Ok(new
            {
                aquariaTier = tier,
                isPro = string.Equals(tier, "Gama2", System.StringComparison.OrdinalIgnoreCase),
                projectId,
                systemDeliveryDate = systemDeliveryDate?.ToString("yyyy-MM-dd"),
                continuousReadTime
            });
        }

        // ════════════════════════════════════════════════════════════════════
        // FASE 5 — Endpoints REST de lectura (DEC-013)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Catálogo de grupos SMM (paneles del dashboard).</summary>
        [HttpGet("groups")]
        [AllowAnonymous]
        public async Task<IActionResult> GetGroupsAsync()
        {
            using var db = _dbFactory.CreateDbContext();
            var groups = await db.SmmGroups
                .OrderBy(g => g.GroupName)
                .Select(g => new
                {
                    g.Id, g.GroupName, g.UiType, g.ReadFrequency,
                    g.CycleRunningVar, g.AlarmHistVar,
                    g.LayoutWidth, g.LayoutHeight, g.LayoutPinned
                })
                .ToListAsync();
            return Ok(groups);
        }

        /// <summary>Variables de un grupo (incluye fórmulas y umbrales).</summary>
        [HttpGet("groups/{groupId:int}/variables")]
        [AllowAnonymous]
        public async Task<IActionResult> GetGroupVariablesAsync(int groupId)
        {
            using var db = _dbFactory.CreateDbContext();
            var vars = await db.SmmVariables
                .Where(v => v.GroupId == groupId)
                .OrderBy(v => v.VarName)
                .Select(v => new
                {
                    v.Id, v.VarName, v.PlcVariable, v.Unit, v.DataType,
                    v.Formula, v.FormulaScope, v.Warning, v.Critical,
                    v.ResetOnMaintenance, v.ElementId
                })
                .ToListAsync();
            return Ok(vars);
        }

        /// <summary>Ciclos de un grupo (filtra borrados, DEC-023).</summary>
        [HttpGet("groups/{groupId:int}/cycles")]
        [AllowAnonymous]
        public async Task<IActionResult> GetGroupCyclesAsync(int groupId, [FromQuery] int take = 100)
        {
            using var db = _dbFactory.CreateDbContext();
            take = System.Math.Clamp(take, 1, 1000);
            var cycles = await db.SmmCycles
                .Where(c => c.GroupId == groupId && !c.IsDeleted)
                .OrderByDescending(c => c.StartedAt)
                .Take(take)
                .Select(c => new
                {
                    c.Id, c.StartedAt, c.CompletedAt, c.Status, c.EndedReason,
                    c.AlarmsCount, c.AlarmTime_s, c.HadAlarms
                })
                .ToListAsync();
            return Ok(cycles);
        }

        /// <summary>Lecturas recientes de una variable.</summary>
        [HttpGet("variables/{variableId:int}/readings")]
        [AllowAnonymous]
        public async Task<IActionResult> GetVariableReadingsAsync(int variableId, [FromQuery] int take = 200)
        {
            using var db = _dbFactory.CreateDbContext();
            take = System.Math.Clamp(take, 1, 5000);
            var readings = await db.SmmReadings
                .Where(r => r.VariableId == variableId)
                .OrderByDescending(r => r.Timestamp)
                .Take(take)
                .Select(r => new
                {
                    r.Id, r.Timestamp, r.Value, r.Source, r.IsError, r.ErrorReason, r.CycleId
                })
                .ToListAsync();
            return Ok(readings);
        }

        /// <summary>Elementos físicos del catálogo.</summary>
        [HttpGet("elements")]
        [AllowAnonymous]
        public async Task<IActionResult> GetElementsAsync()
        {
            using var db = _dbFactory.CreateDbContext();
            var elements = await db.SmmElements
                .OrderBy(e => e.ElementName)
                .Select(e => new { e.Id, e.ElementName, e.SkuAquafrisch, e.Manufacturer, e.Model, e.ComponentLocation3D })
                .ToListAsync();
            return Ok(elements);
        }

        /// <summary>Predicciones (DEC-022 — vacía en BASIC, poblada en PRO).</summary>
        [HttpGet("predictions")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPredictionsAsync([FromQuery] bool includeResolved = false)
        {
            using var db = _dbFactory.CreateDbContext();
            var query = db.SmmPredictions.AsQueryable();
            if (!includeResolved) query = query.Where(p => p.ResolvedAt == null);
            var predictions = await query
                .OrderByDescending(p => p.CreatedAt)
                .Take(200)
                .Select(p => new
                {
                    p.Id, p.PredictionType, p.RelatedElementId, p.RelatedVariableId,
                    p.CreatedAt, p.ResolvedAt, p.Severity, p.Description, p.Confidence
                })
                .ToListAsync();
            return Ok(predictions);
        }

        // ════════════════════════════════════════════════════════════════════
        // Acciones admin
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Snapshot manual Continuous (DEC-026 punto 6).</summary>
        [HttpPost("continuous/snapshot-now")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> SnapshotNowAsync()
        {
            try
            {
                var n = await _capture.OnDemandSnapshotAsync(null);
                return Ok(new { ok = true, readingsPersisted = n, timestamp = System.DateTime.UtcNow });
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "snapshot-now failed");
                return StatusCode(500, new { ok = false, error = ex.Message });
            }
        }

        /// <summary>Soft-delete de un ciclo (DEC-023 punto 6). Status sigue INMUTABLE.</summary>
        [HttpDelete("cycles/{cycleId:int}")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> SoftDeleteCycleAsync(int cycleId, [FromBody] SoftDeleteCycleRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Reason) || req.Reason.Length < 10)
                return BadRequest(new { error = "reason mínimo 10 caracteres (DEC-023)" });

            using var db = _dbFactory.CreateDbContext();
            var cycle = await db.SmmCycles.FirstOrDefaultAsync(c => c.Id == cycleId);
            if (cycle == null) return NotFound();

            cycle.IsDeleted = true;
            cycle.DeletedAt = System.DateTime.UtcNow;
            cycle.DeletedBy = User?.Identity?.Name ?? "admin";
            cycle.DeleteReason = req.Reason;
            await db.SaveChangesAsync();
            return Ok(new { ok = true, cycleId, deletedBy = cycle.DeletedBy });
        }

        public class SoftDeleteCycleRequest
        {
            public string Reason { get; set; } = string.Empty;
        }

        /// <summary>
        /// Sincroniza catálogo SMM (Groups/Elements/Variables/Consumables) desde el Excel del proyecto.
        /// UPSERT idempotente — no borra nada (preserva readings históricos).
        /// </summary>
        [HttpPost("sync-from-excel")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> SyncFromExcelAsync()
        {
            var excelPath = _projectContext.ExcelConfigPath;
            if (string.IsNullOrWhiteSpace(excelPath) || !System.IO.File.Exists(excelPath))
                return BadRequest(new { ok = false, error = $"Excel no encontrado: {excelPath}" });

            var result = await _excelSync.SyncFromExcelAsync(excelPath);
            if (!result.Success)
                return StatusCode(500, new { ok = false, error = result.Error, warnings = result.Warnings });

            return Ok(new
            {
                ok = true,
                groups = new { added = result.GroupsAdded, updated = result.GroupsUpdated },
                elements = new { added = result.ElementsAdded, updated = result.ElementsUpdated },
                variables = new { added = result.VariablesAdded, updated = result.VariablesUpdated },
                consumables = new { added = result.ConsumablesAdded, updated = result.ConsumablesUpdated },
                warnings = result.Warnings
            });
        }
    }
}
