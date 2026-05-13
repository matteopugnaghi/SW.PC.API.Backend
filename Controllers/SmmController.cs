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
using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Models.Smm;
using SW.PC.API.Backend.Models.Smm.Entities;
using SW.PC.API.Backend.Services;
using SW.PC.API.Backend.Services.Smm;
using System.Text.Json;

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
        private readonly IAuditLogService _auditLogService;

        public SmmController(
            ILogger<SmmController> logger,
            IExcelConfigService excelConfigService,
            IRequestProjectContext projectContext,
            IOptions<SmmOptions> smmOptions,
            IProjectDbContextFactory dbFactory,
            ISmmCaptureService capture,
            ISmmExcelSyncService excelSync,
            ISmmPlcEdgeWatcher edgeWatcher,
            IAuditLogService auditLogService)
        {
            _logger = logger;
            _excelConfigService = excelConfigService;
            _projectContext = projectContext;
            _smmOptions = smmOptions.Value;
            _dbFactory = dbFactory;
            _capture = capture;
            _excelSync = excelSync;
            _edgeWatcher = edgeWatcher;
            _auditLogService = auditLogService;
        }

        /// <summary>
        /// Helper: registra evento auditable L1 (firma SHA256 + chain hash, retención configurable).
        /// Wrappea try/catch para que un fallo de auditoría nunca rompa la operación de negocio.
        /// </summary>
        private async Task LogMaintenanceAuditAsync(AuditAction action, AuditResult result, object payload, int affected = 0)
        {
            try
            {
                var userId = User?.FindFirst("sub")?.Value;
                var userName = User?.Identity?.Name ?? "unknown";
                var ip = HttpContext?.Connection?.RemoteIpAddress?.ToString();
                var details = JsonSerializer.Serialize(payload);
                await _auditLogService.LogAsync(
                    AuditCategory.Maintenance, action, result,
                    details, userId, userName, ip, affected);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Audit log failed for {Action}", action);
            }
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
                    g.CycleRunningVar, g.AlarmHistVar, g.RunningBitVar,
                    g.LayoutWidth, g.LayoutHeight, g.LayoutPinned, g.LayoutColor,
                    g.ShowCycleStart, g.ShowCycleEnd, g.ShowCycleDuration,
                    g.ContinuousReadIntervalSec, g.ContinuousRetentionDays,
                    g.DonutMode, g.ShowInMaintenance
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
                .OrderBy(v => v.SortOrder).ThenBy(v => v.VarName)
                .Select(v => new
                {
                    v.Id, v.VarName, v.PlcVariable, v.Unit, v.DataType,
                    v.Formula, v.FormulaScope, v.Warning, v.Critical,
                    v.ResetOnMaintenance, v.ElementId, v.MaxValue, v.SortOrder,
                    v.LowerIsBetter, v.ScaleFactor
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
                                      v.ScaleFactor,
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
                    // Aplicar ScaleFactor (mbar→bar, etc.). Valor crudo permanece en SMM_Readings.
                    value = x.Value.HasValue ? x.Value.Value * (x.ScaleFactor ?? 1.0) : (double?)null,
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
                    // Forzar Kind=Utc → JSON serializa con "Z" → JS interpreta como UTC y convierte a hora local.
                    // Si no, EF/SQLite devuelve Kind=Unspecified, JS lo asume local y muestra desfase de TZ (p.ej. -2h en CEST).
                    raisedAt = DateTime.SpecifyKind(a.RaisedAt, DateTimeKind.Utc),
                    clearedAt = a.ClearedAt.HasValue ? DateTime.SpecifyKind(a.ClearedAt.Value, DateTimeKind.Utc) : (DateTime?)null,
                    durationInCycle_s = a.DurationInCycle_s
                }).ToList());

            // Forzar Kind=Utc en StartedAt/CompletedAt para que el JSON salga con "Z"
            // y el frontend (new Date().toLocaleString) convierta correctamente a hora local.
            var enriched = cycles.Select(c => new
            {
                c.Id,
                StartedAt = DateTime.SpecifyKind(c.StartedAt, DateTimeKind.Utc),
                CompletedAt = c.CompletedAt.HasValue ? DateTime.SpecifyKind(c.CompletedAt.Value, DateTimeKind.Utc) : (DateTime?)null,
                c.Status, c.EndedReason,
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
            // Lookup ScaleFactor (1x si no definido). Aplicado al proyectar para mantener DB cruda.
            var scale = await db.SmmVariables
                .Where(v => v.Id == variableId)
                .Select(v => v.ScaleFactor)
                .FirstOrDefaultAsync() ?? 1.0;
            var readingsRaw = await db.SmmReadings
                .Where(r => r.VariableId == variableId)
                .OrderByDescending(r => r.Timestamp)
                .Take(take)
                .Select(r => new
                {
                    r.Id, r.Timestamp, r.Value, r.Source, r.IsError, r.ErrorReason, r.CycleId
                })
                .ToListAsync();
            // Forzar Kind=Utc → JSON con "Z" → frontend lo convierte a local correctamente.
            var readings = readingsRaw.Select(r => new
            {
                r.Id,
                Timestamp = DateTime.SpecifyKind(r.Timestamp, DateTimeKind.Utc),
                Value = r.Value.HasValue ? r.Value.Value * scale : (double?)null,
                r.Source, r.IsError, r.ErrorReason, r.CycleId
            });
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

            // Mapa variableId → ScaleFactor (1x si null). Aplicado al proyectar (DB cruda).
            var scaleByVar = await db.SmmVariables
                .Where(v => v.GroupId == groupId)
                .Select(v => new { v.Id, v.ScaleFactor })
                .ToDictionaryAsync(v => v.Id, v => v.ScaleFactor ?? 1.0);

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
                        value = r.Value.HasValue
                            ? r.Value.Value * (scaleByVar.TryGetValue(r.VariableId, out var s) ? s : 1.0)
                            : (double?)null,
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
                .Select(e => new { e.Id, e.ElementName, e.SkuAquafrisch, e.Manufacturer, e.Model, e.ComponentLocation3D, e.Notes, e.ImagePath, e.Model3DPath, e.ManualUrl, e.ParentElementId, e.OrderIndex })
                .ToListAsync();
            return Ok(elements);
        }

        /// <summary>
        /// Devuelve la foto del elemento (LifeBar). Resolución por orden:
        ///  1. ImagePath URL absoluta (http/https) → 302 redirect.
        ///  2. ImagePath ruta relativa al wwwroot → sirve archivo si existe.
        ///  3. Fallback convención: wwwroot/element-photos/{ElementName}.{png|jpg|jpeg|webp}
        ///  4. 404 si no encuentra nada (frontend cae al snapshot 3D del nodo).
        /// </summary>
        [HttpGet("elements/{elementId:int}/photo")]
        [AllowAnonymous]
        public async Task<IActionResult> GetElementPhotoAsync(int elementId)
        {
            // No cachear: si el usuario edita ImagePath en el Excel y resincroniza,
            // queremos que el browser pida la imagen nueva (o vea 404 si la borró).
            Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            using var db = _dbFactory.CreateDbContext();
            var elem = await db.SmmElements
                .Where(e => e.Id == elementId)
                .Select(e => new { e.ElementName, e.ImagePath })
                .FirstOrDefaultAsync();
            if (elem == null) return NotFound(new { error = "Elemento no encontrado" });

            // 1. URL absoluta → redirect
            if (!string.IsNullOrWhiteSpace(elem.ImagePath) &&
                (elem.ImagePath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                 elem.ImagePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            {
                return Redirect(elem.ImagePath);
            }

            var env = HttpContext.RequestServices.GetService<IWebHostEnvironment>();
            var wwwroot = env?.WebRootPath ?? System.IO.Path.Combine(AppContext.BaseDirectory, "wwwroot");
            // Bases candidatas para resolver rutas relativas (orden de preferencia):
            //   - wwwroot                                       (legacy/explícito)
            //   - Projects/{id}/config                          (Excel referencia "Images/foo.png" → config/Images/foo.png)
            //   - Projects/{id}                                 (rutas estilo "models/Pumps/x.glb")
            var bases = new System.Collections.Generic.List<string> { wwwroot };
            try {
                if (!string.IsNullOrEmpty(_projectContext.ConfigPath)) bases.Add(_projectContext.ConfigPath);
                if (!string.IsNullOrEmpty(_projectContext.ProjectBasePath)) bases.Add(_projectContext.ProjectBasePath);
            } catch { /* contexto no disponible */ }

            // 2. Ruta relativa explícita: probar todas las bases
            if (!string.IsNullOrWhiteSpace(elem.ImagePath))
            {
                var rel = elem.ImagePath.Replace('\\', '/').TrimStart('/');
                foreach (var baseDir in bases)
                {
                    var full = System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, rel));
                    var baseFull = System.IO.Path.GetFullPath(baseDir);
                    if (full.StartsWith(baseFull, StringComparison.OrdinalIgnoreCase)
                        && System.IO.File.Exists(full))
                    {
                        return PhysicalFile(full, GuessMime(full));
                    }
                }
            }

            // 3. Sin ruta explícita en Excel = sin foto. (Antes había fallback por
            //    convención a wwwroot/element-photos y config/Images, pero producía
            //    matches falsos en Windows por case-insensitive sobre nombres antiguos.)
            return NotFound();
        }

        private static string GuessMime(string path)
        {
            var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            return ext switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".webp" => "image/webp",
                ".gif" => "image/gif",
                ".glb" => "model/gltf-binary",
                ".gltf" => "model/gltf+json",
                _ => "application/octet-stream",
            };
        }

        /// <summary>
        /// Devuelve el modelo 3D (GLB/GLTF) del elemento. Resolución por orden:
        ///  1. Model3DPath URL absoluta (http/https) → 302 redirect.
        ///  2. Model3DPath ruta relativa al wwwroot → sirve archivo si existe.
        ///  3. Fallback convención: wwwroot/element-models/{ElementName}.{glb|gltf}
        ///  4. 404 si no encuentra nada.
        /// </summary>
        [HttpGet("elements/{elementId:int}/model3d")]
        [AllowAnonymous]
        public async Task<IActionResult> GetElementModel3DAsync(int elementId)
        {
            // No cachear (mismo motivo que /photo): cambios del Excel deben ser inmediatos.
            Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            using var db = _dbFactory.CreateDbContext();
            var elem = await db.SmmElements
                .Where(e => e.Id == elementId)
                .Select(e => new { e.ElementName, e.Model3DPath })
                .FirstOrDefaultAsync();
            if (elem == null) return NotFound(new { error = "Elemento no encontrado" });

            // 1. URL absoluta → redirect
            if (!string.IsNullOrWhiteSpace(elem.Model3DPath) &&
                (elem.Model3DPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                 elem.Model3DPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            {
                return Redirect(elem.Model3DPath);
            }

            var env = HttpContext.RequestServices.GetService<IWebHostEnvironment>();
            var wwwroot = env?.WebRootPath ?? System.IO.Path.Combine(AppContext.BaseDirectory, "wwwroot");

            // 2. Ruta relativa explícita
            // 2. Ruta relativa explícita: probar wwwroot, project root, project models
            var bases = new System.Collections.Generic.List<string> { wwwroot };
            try {
                if (!string.IsNullOrEmpty(_projectContext.ProjectBasePath)) bases.Add(_projectContext.ProjectBasePath);
                if (!string.IsNullOrEmpty(_projectContext.ModelsPath)) bases.Add(_projectContext.ModelsPath);
            } catch { }
            if (!string.IsNullOrWhiteSpace(elem.Model3DPath))
            {
                var rel = elem.Model3DPath.Replace('\\', '/').TrimStart('/');
                foreach (var baseDir in bases)
                {
                    var full = System.IO.Path.GetFullPath(System.IO.Path.Combine(baseDir, rel));
                    var baseFull = System.IO.Path.GetFullPath(baseDir);
                    if (full.StartsWith(baseFull, StringComparison.OrdinalIgnoreCase)
                        && System.IO.File.Exists(full))
                    {
                        return PhysicalFile(full, GuessMime(full));
                    }
                }
            }

            // 3. Sin ruta explícita en Excel = sin modelo 3D.
            return NotFound();
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

            await LogMaintenanceAuditAsync(
                AuditAction.SmmCycleSoftDelete, AuditResult.Success,
                new { CycleId = cycleId, GroupId = cycle.GroupId, Reason = req.Reason, DeletedBy = cycle.DeletedBy }, 1);

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

            await LogMaintenanceAuditAsync(
                AuditAction.SmmCycleGroupSoftDelete, AuditResult.Success,
                new { GroupId = groupId, DeletedCount = cycles.Count, Reason = req.Reason, DeletedBy = who }, cycles.Count);

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

            await LogMaintenanceAuditAsync(
                AuditAction.SmmCycleHardPurge, AuditResult.Warning,
                new {
                    GroupId = groupId, Reason = req.Reason, DeletedBy = who,
                    DeletedCycles = deletedCycles, DeletedReadings = deletedReadings,
                    DeletedContinuous = deletedContinuous, DeletedSnapshots = deletedSnapshots,
                    DeletedAlarms = deletedAlarms
                }, System.Math.Max(deletedCycles, 0));

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

            await LogMaintenanceAuditAsync(
                AuditAction.SmmSnapshotsDelete, AuditResult.Warning,
                new { GroupId = groupId, Reason = req.Reason, DeletedBy = who, DeletedSnapshots = deleted },
                System.Math.Max(deleted, 0));

            return Ok(new { ok = true, groupId, deletedBy = who, deletedSnapshots = deleted });
        }

        /// <summary>
        /// "Máquina nueva": resetea TODOS los baselines de mantenimiento del proyecto.
        /// 1) Borra intervenciones, used-parts y lifecycles existentes.
        /// 2) Crea un lifecycle nuevo por elemento + intervenciones sintéticas:
        ///    - Una Replacement por elemento que tenga variable de vida (ResetOnMaintenance=false).
        ///    - Una Maintenance por cada variable recurrente (ResetOnMaintenance=true).
        ///    En ambos casos AccumulatedValueAtMaintenance = última lectura PLC, de modo que
        ///    `consumido = valorPLC − baseline = 0` y la barra arranca en 0%.
        /// Hard delete sobre los datos previos (irreversible). Admin/SuperAdmin.
        /// </summary>
        [HttpDelete("interventions/all")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public async Task<IActionResult> DeleteAllInterventionsAsync([FromBody] SoftDeleteCycleRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Reason) || req.Reason.Length < 10)
                return BadRequest(new { error = "reason mínimo 10 caracteres" });

            using var db = _dbFactory.CreateDbContext();
            var who = User?.Identity?.Name ?? "admin";
            var nowUtc = System.DateTime.UtcNow;

            async Task<int> SafeDeleteAsync(string sql)
            {
                try { return await db.Database.ExecuteSqlRawAsync(sql); }
                catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.Message.Contains("no such table"))
                { return -1; }
            }

            // 1) Wipe en orden inverso a las FK
            var deletedUsedParts     = await SafeDeleteAsync("DELETE FROM SMM_ConsumableUsage");
            var deletedInterventions = await SafeDeleteAsync("DELETE FROM SMM_Interventions");
            var deletedLifecycles    = await SafeDeleteAsync("DELETE FROM SMM_ElementLifecycles");

            // 2) Cargar variables candidatas (con critical>0 y elementId).
            var vars = await db.SmmVariables
                .Where(v => v.ElementId != null && v.Critical != null && v.Critical > 0)
                .Select(v => new { v.Id, ElementId = v.ElementId!.Value, v.VarName, v.ResetOnMaintenance })
                .ToListAsync();

            // 3) Última lectura por variableId (en memoria; SMM_Readings cabe sin problema).
            var rawReadings = await db.SmmReadings
                .Where(r => !r.IsError && r.Value != null)
                .Select(r => new { r.VariableId, r.Timestamp, r.Value })
                .ToListAsync();
            var latestByVar = rawReadings
                .GroupBy(r => r.VariableId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.Timestamp).First().Value!.Value);

            // 4) Nombres de elemento para taskName de Replacement
            var elementIds = vars.Select(v => v.ElementId).Distinct().ToList();
            var elementNames = await db.SmmElements
                .Where(e => elementIds.Contains(e.Id))
                .ToDictionaryAsync(e => e.Id, e => e.ElementName);

            int createdInterventions = 0, createdLifecycles = 0;
            var notes = $"Reset masivo (máquina nueva): {req.Reason}";

            foreach (var grp in vars.GroupBy(v => v.ElementId))
            {
                var elementId = grp.Key;
                var lifecycle = new SmmElementLifecycle
                {
                    ElementId = elementId,
                    StartedAt = nowUtc,
                    AccumulatedValueAtStartJson = "{}"
                };
                db.SmmElementLifecycles.Add(lifecycle);
                await db.SaveChangesAsync(); // necesitamos lifecycle.Id
                createdLifecycles++;

                var elName = elementNames.TryGetValue(elementId, out var n) ? n : $"#{elementId}";
                var lifeVars  = grp.Where(v => !v.ResetOnMaintenance).ToList();
                var maintVars = grp.Where(v =>  v.ResetOnMaintenance).ToList();

                // Replacement (1 por elemento, baseline = lectura de la 1ª life var disponible).
                if (lifeVars.Count > 0)
                {
                    var lv = lifeVars[0];
                    double? val = latestByVar.TryGetValue(lv.Id, out var v) ? v : null;
                    db.SmmInterventions.Add(new SmmIntervention
                    {
                        ElementId = elementId,
                        ElementLifecycleId = lifecycle.Id,
                        TaskName = $"Reemplazo · {elName}",
                        InterventionType = "Replacement",
                        PerformedAt = nowUtc,
                        PerformedByRole = "Admin",
                        PerformedByUser = who,
                        AccumulatedValueAtMaintenance = val,
                        Notes = notes,
                        CreatedBy = who
                    });
                    createdInterventions++;
                }

                // Maintenance (1 por cada mt var; taskName = varName para que el frontend lo encuentre).
                foreach (var mv in maintVars)
                {
                    double? val = latestByVar.TryGetValue(mv.Id, out var v) ? v : null;
                    db.SmmInterventions.Add(new SmmIntervention
                    {
                        ElementId = elementId,
                        ElementLifecycleId = lifecycle.Id,
                        TaskName = mv.VarName,
                        InterventionType = "Maintenance",
                        PerformedAt = nowUtc,
                        PerformedByRole = "Admin",
                        PerformedByUser = who,
                        AccumulatedValueAtMaintenance = val,
                        Notes = notes,
                        CreatedBy = who
                    });
                    createdInterventions++;
                }
            }
            await db.SaveChangesAsync();

            _logger.LogWarning("RESET MASIVO mantenimiento por {User}. Razón: {Reason}. Borrados: interventions={I}, usedParts={U}, lifecycles={L}. Creados: lifecycles={NL}, interventions={NI}",
                who, req.Reason, deletedInterventions, deletedUsedParts, deletedLifecycles, createdLifecycles, createdInterventions);

            await LogMaintenanceAuditAsync(
                AuditAction.SmmMaintenanceReset, AuditResult.Warning,
                new {
                    Reason = req.Reason, DeletedBy = who,
                    DeletedInterventions = deletedInterventions, DeletedUsedParts = deletedUsedParts,
                    DeletedLifecycles = deletedLifecycles,
                    CreatedLifecycles = createdLifecycles, CreatedInterventions = createdInterventions
                }, System.Math.Max(createdInterventions, 0));

            return Ok(new {
                ok = true, deletedBy = who,
                deletedInterventions, deletedUsedParts, deletedLifecycles,
                createdLifecycles, createdInterventions
            });
        }

        /// <summary>
        /// HARD-PURGE de toda la BD de mantenimiento del proyecto: intervenciones, used parts,
        /// lifecycles, predicciones y stats derivadas. IRREVERSIBLE. Solo SuperAdmin.
        /// </summary>
        [HttpPost("maintenance/hard-purge")]
        [Authorize(Roles = "SuperAdmin")]
        public async Task<IActionResult> HardPurgeMaintenanceAsync([FromBody] SoftDeleteCycleRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Reason) || req.Reason.Length < 10)
                return BadRequest(new { error = "reason mínimo 10 caracteres" });

            using var db = _dbFactory.CreateDbContext();
            var who = User?.Identity?.Name ?? "superadmin";

            async Task<int> SafeDeleteAsync(string sql)
            {
                try { return await db.Database.ExecuteSqlRawAsync(sql); }
                catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.Message.Contains("no such table"))
                { return -1; }
            }

            var deletedPredInts      = await SafeDeleteAsync("DELETE FROM SMM_PredictionInterventions");
            var deletedPredictions   = await SafeDeleteAsync("DELETE FROM SMM_Predictions");
            var deletedDerivedStats  = await SafeDeleteAsync("DELETE FROM SMM_DerivedErrorStats");
            var deletedUsedParts     = await SafeDeleteAsync("DELETE FROM SMM_ConsumableUsage");
            var deletedInterventions = await SafeDeleteAsync("DELETE FROM SMM_Interventions");
            var deletedLifecycles    = await SafeDeleteAsync("DELETE FROM SMM_ElementLifecycles");

            _logger.LogWarning("HARD PURGE mantenimiento por {User}. Razón: {Reason}. Borrados: interventions={I}, usedParts={U}, lifecycles={L}, predictions={P}, predInts={PI}, derivedStats={D}",
                who, req.Reason, deletedInterventions, deletedUsedParts, deletedLifecycles, deletedPredictions, deletedPredInts, deletedDerivedStats);

            await LogMaintenanceAuditAsync(
                AuditAction.SmmMaintenanceHardPurge, AuditResult.Warning,
                new {
                    Reason = req.Reason, DeletedBy = who,
                    DeletedInterventions = deletedInterventions, DeletedUsedParts = deletedUsedParts,
                    DeletedLifecycles = deletedLifecycles, DeletedPredictions = deletedPredictions,
                    DeletedPredictionInterventions = deletedPredInts, DeletedDerivedStats = deletedDerivedStats
                }, System.Math.Max(deletedInterventions, 0));

            return Ok(new {
                ok = true, deletedBy = who,
                deletedInterventions, deletedUsedParts, deletedLifecycles,
                deletedPredictions, deletedPredictionInterventions = deletedPredInts,
                deletedDerivedStats
            });
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
                .Select(c => new { c.Id, c.TaskName, c.PartSku, c.PartDescription, c.PartUnit, c.PartDefaultQuantity, c.ManualUrl })
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
                    i.AccumulatedValueAtMaintenance, i.Notes, i.CreatedAt,
                    ConsumableUsages = db.SmmConsumableUsage
                        .Where(u => u.InterventionId == i.Id)
                        .Select(u => new { u.PartSku, u.PartDescription, u.PartUnit, u.Quantity })
                        .ToList()
                })
                .ToListAsync();
            return Ok(items);
        }

        /// <summary>
        /// Batch: histórico de intervenciones para TODOS los elementos del proyecto activo,
        /// devuelto como diccionario { elementId: Intervention[] }. Pensado para la pantalla
        /// de Mantenimiento (evita N llamadas con N=miles).
        /// </summary>
        [HttpGet("interventions/batch")]
        [AllowAnonymous]
        public async Task<IActionResult> GetAllInterventionsBatchAsync([FromQuery] int takePerElement = 100)
        {
            using var db = _dbFactory.CreateDbContext();
            takePerElement = System.Math.Clamp(takePerElement, 1, 500);

            // 1) Obtener todos los elementId existentes en SmmInterventions (evita rows huérfanas).
            // 2) Por cada elemento, tomar las últimas N intervenciones.
            // EF no soporta GroupBy + Take limitado en SQLite, así que cargamos todas las
            // intervenciones ordenadas y agrupamos en memoria. Para datasets grandes se podría
            // optimizar con SQL bruto (ROW_NUMBER), pero a día de hoy interventions ≪ readings.
            var all = await db.SmmInterventions
                .OrderByDescending(i => i.PerformedAt)
                .Select(i => new
                {
                    i.ElementId,
                    i.Id, i.TaskName, i.InterventionType, i.PerformedAt,
                    i.PerformedByRole, i.PerformedByUser, i.WorkOrderRef,
                    i.AccumulatedValueAtMaintenance, i.Notes, i.CreatedAt
                })
                .ToListAsync();

            var result = new System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<object>>();
            foreach (var i in all)
            {
                if (!result.TryGetValue(i.ElementId, out var list))
                {
                    list = new System.Collections.Generic.List<object>();
                    result[i.ElementId] = list;
                }
                if (list.Count >= takePerElement) continue;
                list.Add(new
                {
                    i.Id, i.TaskName, i.InterventionType, i.PerformedAt,
                    i.PerformedByRole, i.PerformedByUser, i.WorkOrderRef,
                    i.AccumulatedValueAtMaintenance, i.Notes, i.CreatedAt
                });
            }
            return Ok(result);
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

        // ─────────────────────────────────────────────────────────────────────
        // GET /api/smm/orders/preview?d={base64url(json)}
        // Render HTML imprimible para móviles que escanean el QR del kiosko.
        // PC industrial sin internet → operario escanea QR con su móvil dentro
        // de la misma WiFi LAN → carga este HTML directamente desde backend.
        // Anonymous: el QR no lleva token. El payload va en la URL (firmado a
        // futuro si fuera necesario; ahora el contenido es solo lectura).
        // ─────────────────────────────────────────────────────────────────────
        [HttpGet("orders/preview")]
        [AllowAnonymous]
        public IActionResult OrderPreview([FromQuery(Name = "d")] string d)
        {
            if (string.IsNullOrWhiteSpace(d)) return Content("<html><body>missing payload</body></html>", "text/html; charset=utf-8");
            try
            {
                // base64url → base64
                var b64 = d.Replace('-', '+').Replace('_', '/');
                switch (b64.Length % 4) { case 2: b64 += "=="; break; case 3: b64 += "="; break; }
                var json = System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String(b64));
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var root = doc.RootElement;
                string ref_ = root.TryGetProperty("ref", out var r) ? r.GetString() ?? "" : "";
                string elem = root.TryGetProperty("el", out var e) ? e.GetString() ?? "" : "";
                string sku = root.TryGetProperty("sk", out var sk) ? sk.GetString() ?? "" : "";
                string date = root.TryGetProperty("dt", out var dt) ? dt.GetString() ?? "" : System.DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm");
                var sb = new System.Text.StringBuilder();
                sb.Append("<!doctype html><html><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'>");
                sb.Append($"<title>Pedido {System.Net.WebUtility.HtmlEncode(ref_)}</title>");
                sb.Append("<style>body{font-family:-apple-system,Segoe UI,Arial,sans-serif;color:#222;padding:18px;max-width:720px;margin:0 auto}");
                sb.Append("h1{color:#14202b;margin:0 0 4px;font-size:22px}.sub{color:#6b7785;font-size:13px;margin-bottom:18px}");
                sb.Append(".meta{display:grid;grid-template-columns:1fr 1fr;gap:8px;margin-bottom:18px;font-size:13px;background:#f6f9fc;padding:12px;border-radius:8px}");
                sb.Append("table{width:100%;border-collapse:collapse;margin-top:8px;font-size:13px}");
                sb.Append("th,td{border:1px solid #d1d9e0;padding:8px 10px;text-align:left}th{background:#eef3f8;font-weight:700}");
                sb.Append(".qty{text-align:right;font-weight:700}.element-row{background:#fff7e0}");
                sb.Append(".btn{display:inline-block;background:#0066cc;color:#fff;padding:12px 22px;border-radius:8px;text-decoration:none;font-weight:700;margin-top:18px}");
                sb.Append("@media print{.btn{display:none}}</style></head><body>");
                sb.Append($"<h1>📋 Pedido de repuestos</h1><div class='sub'>Ref: <b>{System.Net.WebUtility.HtmlEncode(ref_)}</b> · {System.Net.WebUtility.HtmlEncode(date)}</div>");
                sb.Append("<div class='meta'>");
                sb.Append($"<div><b>Elemento:</b><br/>{System.Net.WebUtility.HtmlEncode(elem)}</div>");
                if (!string.IsNullOrEmpty(sku)) sb.Append($"<div><b>SKU:</b><br/><code>{System.Net.WebUtility.HtmlEncode(sku)}</code></div>");
                sb.Append("</div>");
                sb.Append("<table><thead><tr><th>SKU</th><th>Descripción</th><th>Cant.</th><th>Ud</th></tr></thead><tbody>");
                if (root.TryGetProperty("ln", out var lines) && lines.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var line in lines.EnumerateArray())
                    {
                        var ls = line.TryGetProperty("s", out var sx) ? sx.GetString() ?? "" : "";
                        var ld = line.TryGetProperty("d", out var dx) ? dx.GetString() ?? "" : "";
                        var lq = line.TryGetProperty("q", out var qx) ? qx.GetRawText() : "0";
                        var lu = line.TryGetProperty("u", out var ux) ? ux.GetString() ?? "ud" : "ud";
                        var rowCls = ld.StartsWith("[ELEMENTO]") ? " class='element-row'" : "";
                        sb.Append($"<tr{rowCls}><td><code>{System.Net.WebUtility.HtmlEncode(ls)}</code></td><td>{System.Net.WebUtility.HtmlEncode(ld)}</td><td class='qty'>{lq}</td><td>{System.Net.WebUtility.HtmlEncode(lu)}</td></tr>");
                    }
                }
                sb.Append("</tbody></table>");
                sb.Append("<a class='btn' href='javascript:window.print()'>🖨 Imprimir / Guardar PDF</a>");
                sb.Append("</body></html>");
                return Content(sb.ToString(), "text/html; charset=utf-8");
            }
            catch (System.Exception ex)
            {
                return Content($"<html><body>Error: {System.Net.WebUtility.HtmlEncode(ex.Message)}</body></html>", "text/html; charset=utf-8");
            }
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
