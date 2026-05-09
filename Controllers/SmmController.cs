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
using SW.PC.API.Backend.Models.Smm.Entities;
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
        private readonly ISmmPlcEdgeWatcher _edgeWatcher;

        public SmmController(
            ILogger<SmmController> logger,
            IExcelConfigService excelConfigService,
            IRequestProjectContext projectContext,
            IOptions<SmmOptions> smmOptions,
            IProjectDbContextFactory dbFactory,
            ISmmCaptureService capture,
            ISmmExcelSyncService excelSync,
            ISmmPlcEdgeWatcher edgeWatcher)
        {
            _logger = logger;
            _excelConfigService = excelConfigService;
            _projectContext = projectContext;
            _smmOptions = smmOptions.Value;
            _dbFactory = dbFactory;
            _capture = capture;
            _excelSync = excelSync;
            _edgeWatcher = edgeWatcher;
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
                    g.LayoutWidth, g.LayoutHeight, g.LayoutPinned, g.LayoutColor,
                    g.ShowCycleStart, g.ShowCycleEnd, g.ShowCycleDuration,
                    g.ContinuousReadIntervalSec, g.ContinuousRetentionDays,
                    g.DonutMode
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
                    v.ResetOnMaintenance, v.ElementId, v.MaxValue
                })
                .ToListAsync();
            return Ok(vars);
        }

        /// <summary>Ciclos de un grupo (filtra borrados, DEC-023). Incluye snapshot de variables capturadas al cierre.</summary>
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

            // Enriquecer con snapshot de readings por ciclo (DEC-018)
            var cycleIds = cycles.Select(c => c.Id).ToList();
            var readings = await (from r in db.SmmReadings
                                  join v in db.SmmVariables on r.VariableId equals v.Id
                                  where r.CycleId != null && cycleIds.Contains(r.CycleId.Value)
                                  select new
                                  {
                                      CycleId = r.CycleId!.Value,
                                      v.Id,
                                      v.VarName,
                                      v.Unit,
                                      v.DataType,
                                      r.Value,
                                      r.StringValue,
                                      r.IsError,
                                      r.ErrorReason
                                  }).ToListAsync();
            var readingsByCycle = readings.GroupBy(r => r.CycleId)
                .ToDictionary(g => g.Key, g => g.Select(x => new
                {
                    variableId = x.Id,
                    varName = x.VarName,
                    unit = x.Unit,
                    dataType = x.DataType,
                    value = x.Value,
                    stringValue = x.StringValue,
                    isError = x.IsError,
                    errorReason = x.ErrorReason
                }).ToList());

            // Lista de alarmas por ciclo (DEC-018) — el frontend traducirá AlarmCode con alarmService
            var cycleAlarms = await db.SmmCycleAlarms
                .Where(a => cycleIds.Contains(a.CycleId))
                .Select(a => new
                {
                    a.CycleId, a.AlarmCode, a.AlarmText, a.Severity,
                    a.RaisedAt, a.ClearedAt, a.DurationInCycle_s
                })
                .ToListAsync();
            var alarmsByCycle = cycleAlarms.GroupBy(a => a.CycleId)
                .ToDictionary(g => g.Key, g => g.Select(a => new
                {
                    alarmCode = a.AlarmCode,
                    alarmText = a.AlarmText,
                    severity = a.Severity,
                    raisedAt = a.RaisedAt,
                    clearedAt = a.ClearedAt,
                    durationInCycle_s = a.DurationInCycle_s
                }).ToList());

            var enriched = cycles.Select(c => new
            {
                c.Id, c.StartedAt, c.CompletedAt, c.Status, c.EndedReason,
                c.AlarmsCount, c.AlarmTime_s, c.HadAlarms,
                Readings = readingsByCycle.TryGetValue(c.Id, out var rs) ? rs : new(),
                Alarms = alarmsByCycle.TryGetValue(c.Id, out var als) ? als : new()
            });
            return Ok(enriched);
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

        /// <summary>
        /// Lecturas recientes batch para un grupo entero (Continuous/OnDemand).
        /// Devuelve los últimos N timestamps con TODAS las variables del grupo en cada uno
        /// (formato similar a "ciclos" pero sin cycleId, agrupado por timestamp del snapshot).
        /// Filtros opcionales: from/to (ISO 8601 UTC).
        /// </summary>
        [HttpGet("groups/{groupId:int}/readings/recent")]
        [AllowAnonymous]
        public async Task<IActionResult> GetGroupRecentReadingsAsync(
            int groupId,
            [FromQuery] int take = 30,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null)
        {
            using var db = _dbFactory.CreateDbContext();
            take = System.Math.Clamp(take, 1, 20000);

            var q = db.SmmReadings.Where(r => r.GroupId == groupId && r.CycleId == null);
            if (from.HasValue) q = q.Where(r => r.Timestamp >= from.Value);
            if (to.HasValue)   q = q.Where(r => r.Timestamp <= to.Value);

            // Tomamos los últimos N timestamps distintos (cada snapshot escribe el mismo Timestamp para todas sus vars)
            var recentTimestamps = await q
                .Select(r => r.Timestamp)
                .Distinct()
                .OrderByDescending(t => t)
                .Take(take)
                .ToListAsync();

            if (recentTimestamps.Count == 0)
                return Ok(new List<object>());

            var minTs = recentTimestamps.Min();
            var maxTs = recentTimestamps.Max();

            var readings = await db.SmmReadings
                .Where(r => r.GroupId == groupId
                            && r.CycleId == null
                            && r.Timestamp >= minTs
                            && r.Timestamp <= maxTs)
                .Select(r => new
                {
                    r.Id, r.Timestamp, r.VariableId, r.Value, r.StringValue,
                    r.Source, r.IsError, r.ErrorReason
                })
                .ToListAsync();

            // Agrupar por timestamp y proyectar como "snapshots"
            // Forzar Kind=Utc para que el JSON serialice con sufijo "Z" y JS lo interprete como UTC.
            var snapshots = readings
                .GroupBy(r => r.Timestamp)
                .OrderByDescending(g => g.Key)
                .Select(g => new
                {
                    timestamp = DateTime.SpecifyKind(g.Key, DateTimeKind.Utc),
                    readings = g.Select(r => new
                    {
                        variableId = r.VariableId,
                        value = r.Value,
                        stringValue = r.StringValue,
                        isError = r.IsError,
                        errorReason = r.ErrorReason,
                        source = r.Source
                    }).ToList()
                })
                .ToList();

            return Ok(snapshots);
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

        /// <summary>Soft-delete masivo de TODOS los ciclos de un grupo (DEC-023). Solo Admin/SuperAdmin.</summary>
        [HttpDelete("groups/{groupId:int}/cycles")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> SoftDeleteAllGroupCyclesAsync(int groupId, [FromBody] SoftDeleteCycleRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Reason) || req.Reason.Length < 10)
                return BadRequest(new { error = "reason mínimo 10 caracteres (DEC-023)" });

            using var db = _dbFactory.CreateDbContext();
            var now = System.DateTime.UtcNow;
            var who = User?.Identity?.Name ?? "admin";
            var cycles = await db.SmmCycles.Where(c => c.GroupId == groupId && !c.IsDeleted).ToListAsync();
            foreach (var c in cycles)
            {
                c.IsDeleted = true;
                c.DeletedAt = now;
                c.DeletedBy = who;
                c.DeleteReason = req.Reason;
            }
            await db.SaveChangesAsync();
            return Ok(new { ok = true, groupId, deleted = cycles.Count, deletedBy = who });
        }

        /// <summary>
        /// HARD-DELETE físico de TODOS los ciclos del grupo y sus dependencias (readings, snapshots,
        /// alarmas). IRREVERSIBLE. Solo SuperAdmin. Diferente del soft-delete (DEC-023): aquí los
        /// datos desaparecen físicamente de la BD, sin posibilidad de recuperación ni auditoría.
        /// </summary>
        [HttpPost("groups/{groupId:int}/cycles/hard-purge")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> HardPurgeGroupCyclesAsync(int groupId, [FromBody] SoftDeleteCycleRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Reason) || req.Reason.Length < 10)
                return BadRequest(new { error = "reason mínimo 10 caracteres" });

            using var db = _dbFactory.CreateDbContext();
            var who = User?.Identity?.Name ?? "superadmin";

            // Sub-query: ids de ciclos del grupo (incluye soft-deleted: el hard purge se los lleva todos).
            // Borrado en orden inverso a las FK para no violar restricciones.
            // Cada DELETE tolera "tabla no existe" (algunas instalaciones no tienen SMM_CycleSnapshots).
            async Task<int> SafeDeleteAsync(string sql)
            {
                try { return await db.Database.ExecuteSqlRawAsync(sql, groupId); }
                catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.Message.Contains("no such table"))
                { return -1; }
            }

            var deletedReadings  = await SafeDeleteAsync(
                "DELETE FROM SMM_Readings        WHERE CycleId IN (SELECT Id FROM SMM_Cycles WHERE GroupId = {0})");
            // También borrar snapshots Continuous (CycleId IS NULL) del grupo
            var deletedContinuous = await SafeDeleteAsync(
                "DELETE FROM SMM_Readings        WHERE GroupId = {0} AND CycleId IS NULL");
            var deletedSnapshots = await SafeDeleteAsync(
                "DELETE FROM SMM_CycleSnapshots  WHERE CycleId IN (SELECT Id FROM SMM_Cycles WHERE GroupId = {0})");
            var deletedAlarms    = await SafeDeleteAsync(
                "DELETE FROM SMM_CycleAlarms     WHERE CycleId IN (SELECT Id FROM SMM_Cycles WHERE GroupId = {0})");
            var deletedCycles    = await SafeDeleteAsync(
                "DELETE FROM SMM_Cycles          WHERE GroupId = {0}");

            _logger.LogWarning("HARD PURGE ciclos grupo {GroupId} por {User}. Razón: {Reason}. Borrados: cycles={C}, readings={R}, continuous={CR}, snapshots={S}, alarms={A}",
                groupId, who, req.Reason, deletedCycles, deletedReadings, deletedContinuous, deletedSnapshots, deletedAlarms);

            return Ok(new {
                ok = true, groupId, deletedBy = who,
                deletedCycles, deletedReadings, deletedContinuous, deletedSnapshots, deletedAlarms
            });
        }

        /// <summary>
        /// Borra TODOS los snapshots Continuous/OnDemand del grupo (CycleId IS NULL).
        /// No afecta a ciclos PerCycle. Hard delete (irreversible). SuperAdmin solo.
        /// </summary>
        [HttpDelete("groups/{groupId:int}/snapshots/all")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> DeleteAllGroupSnapshotsAsync(int groupId, [FromBody] SoftDeleteCycleRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Reason) || req.Reason.Length < 10)
                return BadRequest(new { error = "reason mínimo 10 caracteres" });

            using var db = _dbFactory.CreateDbContext();
            var who = User?.Identity?.Name ?? "superadmin";

            var deleted = await db.Database.ExecuteSqlRawAsync(
                "DELETE FROM SMM_Readings WHERE GroupId = {0} AND CycleId IS NULL", groupId);

            _logger.LogWarning("DELETE ALL SNAPSHOTS Continuous grupo {GroupId} por {User}. Razón: {Reason}. Borrados: {N}",
                groupId, who, req.Reason, deleted);

            return Ok(new { ok = true, groupId, deletedBy = who, deletedSnapshots = deleted });
        }

        /// <summary>
        /// Sincroniza catálogo SMM (Groups/Elements/Variables/Consumables) desde el Excel del proyecto.
        /// UPSERT idempotente. Si purgeMissing=true (solo SuperAdmin), borra entidades ya no presentes
        /// en el Excel (cascada SQLite a ciclos/readings/intervenciones asociados).
        /// </summary>
        [HttpPost("sync-from-excel")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> SyncFromExcelAsync([FromQuery] bool purgeMissing = false)
        {
            var excelPath = _projectContext.ExcelConfigPath;
            if (string.IsNullOrWhiteSpace(excelPath) || !System.IO.File.Exists(excelPath))
                return BadRequest(new { ok = false, error = $"Excel no encontrado: {excelPath}" });

            // Restricción: purgeMissing solo para SuperAdmin (operación destructiva).
            if (purgeMissing && !User.IsInRole("SuperAdmin"))
                return Forbid();

            var result = await _excelSync.SyncFromExcelAsync(excelPath, purgeMissing);
            if (!result.Success)
                return StatusCode(500, new { ok = false, error = result.Error, warnings = result.Warnings });

            // Notificar al edge-watcher que recargue su mapa de CycleRunningVar (DEC-018)
            try { await _edgeWatcher.RefreshAsync(); }
            catch (Exception ex) { _logger.LogWarning(ex, "[SMM] Error refrescando edge-watcher tras sync"); }

            return Ok(new
            {
                ok = true,
                purged = purgeMissing,
                groups      = new { added = result.GroupsAdded,      updated = result.GroupsUpdated,      deleted = result.GroupsDeleted },
                elements    = new { added = result.ElementsAdded,    updated = result.ElementsUpdated,    deleted = result.ElementsDeleted },
                variables   = new { added = result.VariablesAdded,   updated = result.VariablesUpdated,   deleted = result.VariablesDeleted },
                consumables = new { added = result.ConsumablesAdded, updated = result.ConsumablesUpdated, deleted = result.ConsumablesDeleted },
                warnings = result.Warnings
            });
        }

        // ════════════════════════════════════════════════════════════════════
        // FASE 6.2 — Mantenimiento (DEC-014/017/019/023)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Consumibles asociados a un elemento (catálogo Excel).</summary>
        [HttpGet("elements/{elementId:int}/consumables")]
        [AllowAnonymous]
        public async Task<IActionResult> GetElementConsumablesAsync(int elementId)
        {
            using var db = _dbFactory.CreateDbContext();
            var items = await db.SmmConsumables
                .Where(c => c.ElementId == elementId)
                .OrderBy(c => c.TaskName).ThenBy(c => c.PartSku)
                .Select(c => new { c.Id, c.TaskName, c.PartSku, c.PartDescription, c.PartUnit, c.PartDefaultQuantity })
                .ToListAsync();
            return Ok(items);
        }

        /// <summary>Histórico de intervenciones de un elemento.</summary>
        [HttpGet("elements/{elementId:int}/interventions")]
        [AllowAnonymous]
        public async Task<IActionResult> GetElementInterventionsAsync(int elementId, [FromQuery] int take = 100)
        {
            using var db = _dbFactory.CreateDbContext();
            take = System.Math.Clamp(take, 1, 500);
            var items = await db.SmmInterventions
                .Where(i => i.ElementId == elementId)
                .OrderByDescending(i => i.PerformedAt)
                .Take(take)
                .Select(i => new
                {
                    i.Id, i.TaskName, i.InterventionType, i.PerformedAt,
                    i.PerformedByRole, i.PerformedByUser, i.WorkOrderRef,
                    i.AccumulatedValueAtMaintenance, i.Notes, i.CreatedAt
                })
                .ToListAsync();
            return Ok(items);
        }

        /// <summary>Crea una nueva intervención de mantenimiento + uso de consumibles.</summary>
        [HttpPost("interventions")]
        [Authorize]
        public async Task<IActionResult> CreateInterventionAsync([FromBody] CreateInterventionRequest req)
        {
            if (req == null) return BadRequest(new { error = "body requerido" });
            if (req.ElementId <= 0) return BadRequest(new { error = "elementId requerido" });
            if (string.IsNullOrWhiteSpace(req.TaskName)) return BadRequest(new { error = "taskName requerido" });

            using var db = _dbFactory.CreateDbContext();

            var element = await db.SmmElements.FirstOrDefaultAsync(e => e.Id == req.ElementId);
            if (element == null) return NotFound(new { error = $"Element {req.ElementId} no existe" });

            // Resolver/crear lifecycle activo del elemento
            var lifecycle = await db.SmmElementLifecycles
                .Where(l => l.ElementId == req.ElementId && l.EndedAt == null)
                .OrderByDescending(l => l.StartedAt)
                .FirstOrDefaultAsync();

            if (lifecycle == null)
            {
                lifecycle = new SmmElementLifecycle
                {
                    ElementId = req.ElementId,
                    StartedAt = System.DateTime.UtcNow,
                    AccumulatedValueAtStartJson = "{}"
                };
                db.SmmElementLifecycles.Add(lifecycle);
                await db.SaveChangesAsync();
            }

            var user = User?.Identity?.Name ?? "user";
            var intervention = new SmmIntervention
            {
                ElementId = req.ElementId,
                ElementLifecycleId = lifecycle.Id,
                TaskName = req.TaskName.Trim(),
                InterventionType = string.IsNullOrWhiteSpace(req.InterventionType) ? "Maintenance" : req.InterventionType.Trim(),
                PerformedAt = req.PerformedAt ?? System.DateTime.UtcNow,
                PerformedByRole = string.IsNullOrWhiteSpace(req.PerformedByRole) ? "CustomerMaintainer" : req.PerformedByRole.Trim(),
                PerformedByUser = string.IsNullOrWhiteSpace(req.PerformedByUser) ? user : req.PerformedByUser.Trim(),
                WorkOrderRef = string.IsNullOrWhiteSpace(req.WorkOrderRef) ? null : req.WorkOrderRef.Trim(),
                AccumulatedValueAtMaintenance = req.AccumulatedValueAtMaintenance,
                Notes = string.IsNullOrWhiteSpace(req.Notes) ? null : req.Notes.Trim(),
                CreatedBy = user
            };
            db.SmmInterventions.Add(intervention);
            await db.SaveChangesAsync();

            // Consumable usages
            if (req.ConsumableUsages != null && req.ConsumableUsages.Count > 0)
            {
                foreach (var u in req.ConsumableUsages)
                {
                    if (string.IsNullOrWhiteSpace(u.PartSku) || u.Quantity <= 0) continue;
                    db.SmmConsumableUsage.Add(new SmmConsumableUsage
                    {
                        InterventionId = intervention.Id,
                        PartSku = u.PartSku.Trim(),
                        PartDescription = string.IsNullOrWhiteSpace(u.PartDescription) ? null : u.PartDescription.Trim(),
                        PartUnit = string.IsNullOrWhiteSpace(u.PartUnit) ? "ud" : u.PartUnit.Trim(),
                        Quantity = u.Quantity
                    });
                }
                await db.SaveChangesAsync();
            }

            // Si es Replacement: cerrar lifecycle actual y abrir uno nuevo (DEC-019)
            if (string.Equals(intervention.InterventionType, "Replacement", System.StringComparison.OrdinalIgnoreCase))
            {
                lifecycle.EndedAt = intervention.PerformedAt;
                lifecycle.EndingInterventionId = intervention.Id;
                db.SmmElementLifecycles.Add(new SmmElementLifecycle
                {
                    ElementId = req.ElementId,
                    StartedAt = intervention.PerformedAt,
                    AccumulatedValueAtStartJson = "{}"
                });
                await db.SaveChangesAsync();
            }

            return Ok(new { ok = true, interventionId = intervention.Id, lifecycleId = lifecycle.Id });
        }

        /// <summary>Datos para PDF/print de un pedido de consumibles (frontend renderiza HTML).</summary>
        [HttpPost("orders/build")]
        [Authorize]
        public async Task<IActionResult> BuildConsumablesOrderAsync([FromBody] BuildOrderRequest req)
        {
            if (req == null || req.Items == null || req.Items.Count == 0)
                return BadRequest(new { error = "items requerido" });

            using var db = _dbFactory.CreateDbContext();
            var skus = req.Items.Select(i => i.PartSku).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
            var catalog = await db.SmmConsumables
                .Where(c => skus.Contains(c.PartSku))
                .Select(c => new { c.PartSku, c.PartDescription, c.PartUnit, ElementName = db.SmmElements.Where(e => e.Id == c.ElementId).Select(e => e.ElementName).FirstOrDefault() })
                .ToListAsync();

            var lookup = catalog.GroupBy(c => c.PartSku).ToDictionary(g => g.Key, g => g.First());

            var lines = req.Items.Select(i =>
            {
                lookup.TryGetValue(i.PartSku, out var c);
                return new
                {
                    sku = i.PartSku,
                    description = !string.IsNullOrWhiteSpace(i.PartDescription) ? i.PartDescription : c?.PartDescription,
                    unit = !string.IsNullOrWhiteSpace(i.PartUnit) ? i.PartUnit : (c?.PartUnit ?? "ud"),
                    quantity = i.Quantity,
                    elementName = c?.ElementName
                };
            }).ToList();

            return Ok(new
            {
                orderRef = $"ORD-{System.DateTime.UtcNow:yyyyMMdd-HHmmss}",
                generatedAt = System.DateTime.UtcNow,
                generatedBy = User?.Identity?.Name ?? "user",
                projectId = _projectContext.ProjectId,
                customer = req.CustomerName,
                notes = req.Notes,
                lines
            });
        }

        public class CreateInterventionRequest
        {
            public int ElementId { get; set; }
            public string TaskName { get; set; } = string.Empty;
            public string? InterventionType { get; set; } = "Maintenance";
            public System.DateTime? PerformedAt { get; set; }
            public string? PerformedByRole { get; set; }
            public string? PerformedByUser { get; set; }
            public string? WorkOrderRef { get; set; }
            public double? AccumulatedValueAtMaintenance { get; set; }
            public string? Notes { get; set; }
            public List<ConsumableUsageDto>? ConsumableUsages { get; set; }
        }

        public class ConsumableUsageDto
        {
            public string PartSku { get; set; } = string.Empty;
            public string? PartDescription { get; set; }
            public string? PartUnit { get; set; }
            public double Quantity { get; set; } = 1.0;
        }

        public class BuildOrderRequest
        {
            public string? CustomerName { get; set; }
            public string? Notes { get; set; }
            public List<OrderLineDto> Items { get; set; } = new();
        }

        public class OrderLineDto
        {
            public string PartSku { get; set; } = string.Empty;
            public string? PartDescription { get; set; }
            public string? PartUnit { get; set; }
            public double Quantity { get; set; } = 1.0;
        }
    }
}
