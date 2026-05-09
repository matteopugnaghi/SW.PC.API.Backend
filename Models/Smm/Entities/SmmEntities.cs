// ============================================================================
// SmmEntities.cs - Entidades SMM (Statistics & Maintenance Module)
// ============================================================================
// Cubre Fase 3 del roadmap (DEC-013). Decisiones aplicadas:
//  - DEC-014/017/019: SMM_Elements + Variables + Consumables + Lifecycles + Interventions
//  - DEC-016/021: Variables calculadas con Formula + IsError/ErrorReason
//  - DEC-018/020: Cycles con Status/EndedReason + CycleAlarms
//  - DEC-022: Predictions y PredictionInterventions reservadas (vacías en BASIC)
//  - DEC-023: RowVersion (concurrency), soft-delete en Cycles, layouts personales
// ============================================================================

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SW.PC.API.Backend.Models.Smm.Entities;

// ─────────────────────────────────────────────────────────────────────────────
// Catálogo (espejo de Excel)
// ─────────────────────────────────────────────────────────────────────────────

[Table("SMM_Groups")]
public class SmmGroup
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string GroupName { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    // Table | Kpi | Stat | LineChart | BarChart | GaugeChart | DonutChart | ScatterChart | HeatmapChart
    // Legacy: "Chart" se normaliza a "LineChart" durante el sync (retro-compat).
    // Nota: "Stat" mantiene la combinación Table + LineChart. Otras combinaciones (StatBar, StatGauge…) se añadirán bajo demanda.
    public string UiType { get; set; } = "Table";

    [Required, MaxLength(20)]
    public string ReadFrequency { get; set; } = "Continuous"; // Continuous | PerCycle | OnDemand | OnEvent

    [MaxLength(200)]
    public string? CycleRunningVar { get; set; } // DEC-018 (obligatoria si PerCycle)

    public bool ShowCycleStart { get; set; } = true;
    public bool ShowCycleEnd { get; set; } = true;
    public bool ShowCycleDuration { get; set; } = false;

    [MaxLength(200)]
    public string? AlarmHistVar { get; set; } // DEC-020

    /// <summary>
    /// Bit PLC opcional a nivel de GRUPO: si está definido y vale FALSE,
    /// se omite TODO el snapshot del grupo (no se inserta ninguna fila).
    /// Se evalúa ANTES del RunningBitVar por-variable. Reduce ruido en BD
    /// cuando la máquina/módulo está parado.
    /// </summary>
    [MaxLength(200)]
    public string? RunningBitVar { get; set; }

    public int? LayoutWidth { get; set; }
    public int? LayoutHeight { get; set; }
    public bool LayoutPinned { get; set; } = false;

    [MaxLength(20)]
    public string? LayoutColor { get; set; } // Color hex/nombre, opcional. Override del color por readFrequency.

    /// <summary>
    /// Intervalo en segundos entre snapshots Continuous automáticos del grupo.
    /// - null/0/&gt;=86400 → modo DIARIO (1 snapshot/día a las 23:59 UTC).
    /// - 1..86399 → modo CÍCLICO: snapshot cada N segundos.
    /// </summary>
    public int? ContinuousReadIntervalSec { get; set; }

    /// <summary>
    /// Días de retención de snapshots Continuous (CycleId IS NULL) del grupo.
    /// 0/null = sin retención (acumular indefinidamente). Default 30.
    /// </summary>
    public int? ContinuousRetentionDays { get; set; }

    /// <summary>
    /// Modo de agregación por defecto para UiType=DonutChart.
    ///   - "LAST"         (default): muestra el último snapshot de cada variable (valor instantáneo).
    ///   - "DELTA_24H"   : (last - first) en ventana móvil 24h, con wrap-around (MaxValue) si aplica.
    ///   - "DELTA_TODAY" : (last - first) desde hoy 00:00 hora local PC, con wrap-around si aplica.
    ///   - "AVG_24H"     : promedio de valores en ventana móvil 24h.
    ///   - "AVG_TODAY"   : promedio de valores desde hoy 00:00 hora local PC.
    /// El modal del donut ofrece selector con los 5 modos para override puntual.
    /// </summary>
    [MaxLength(20)]
    public string DonutMode { get; set; } = "LAST";

    /// <summary>
    /// Si TRUE, el grupo NO se muestra en la vista Statistics y queda reservado
    /// exclusivamente para la vista Mantenimiento (típicamente UiType=LifeBar).
    /// FALSE / null → comportamiento normal (visible en Statistics).
    /// </summary>
    public bool ShowInMaintenance { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

[Table("SMM_Elements")]
public class SmmElement
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(150)]
    public string ElementName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? ComponentLocation3D { get; set; }

    [MaxLength(100)]
    public string? SkuAquafrisch { get; set; }

    [MaxLength(100)]
    public string? Manufacturer { get; set; }

    [MaxLength(100)]
    public string? Model { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    /// <summary>
    /// Ruta opcional a la foto del elemento. Puede ser:
    ///  - URL absoluta (http/https) → se sirve directamente.
    ///  - Ruta relativa al wwwroot (p.ej. "element-photos/bomba-p101.jpg").
    ///  - Vacía → fallback automático a wwwroot/element-photos/{ElementName}.{png|jpg|webp}
    ///    y si tampoco existe, el frontend cae al snapshot del nodo 3D (ComponentLocation3D).
    /// </summary>
    [MaxLength(300)]
    public string? ImagePath { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

[Table("SMM_Variables")]
public class SmmVariable
{
    [Key]
    public int Id { get; set; }

    public int GroupId { get; set; }

    public int? ElementId { get; set; } // Opcional (FK a SMM_Elements)

    [Required, MaxLength(150)]
    public string VarName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? PlcVariable { get; set; } // mutuamente excluyente con Formula (DEC-016)

    [MaxLength(20)]
    public string? Unit { get; set; }

    [Required, MaxLength(20)]
    public string DataType { get; set; } = "REAL"; // REAL | INT | BOOL | STRING

    [MaxLength(500)]
    public string? Formula { get; set; } // NCalc DEC-016

    [MaxLength(20)]
    public string? FormulaScope { get; set; } // PerCycle | Snapshot | OnEvent

    public double? Warning { get; set; }
    public double? Critical { get; set; }

    public bool ResetOnMaintenance { get; set; } = false; // DEC-017/019

    [MaxLength(200)]
    public string? RunningBitVar { get; set; }

    /// <summary>
    /// Modo de captura por ciclo (DEC-028):
    ///   - "Snapshot" (default): se captura el valor al final del ciclo.
    ///   - "Delta": se captura al inicio Y al final, y se guarda la diferencia (end - start).
    ///     Útil para contadores acumulativos del PLC (m³/L de agua, kWh, ciclos totales, …).
    /// </summary>
    [MaxLength(20)]
    public string CaptureMode { get; set; } = "Snapshot";

    /// <summary>
    /// Valor máximo del contador físico antes de hacer wrap-around (vuelta a 0).
    /// Es propiedad del HARDWARE, no del tipo PLC: aunque el PLC convierta el valor
    /// a LREAL, si el caudalímetro/encoder físico es UDINT, el wrap ocurre igualmente.
    ///
    /// Valores típicos:
    ///   - UINT  16b: 65535
    ///   - UDINT 32b: 4294967295
    ///   - DINT  32b: 2147483647
    ///   - Counter decimal 6 dígitos: 999999
    ///   - Counter decimal 8 dígitos: 99999999
    ///   - null (vacío en Excel): "no wrap esperado". Si el delta resulta negativo,
    ///     el ciclo se marca como error con DeltaNegativeNoMaxValue (no se inventa wrap).
    ///
    /// Solo se usa cuando CaptureMode="Delta".
    /// </summary>
    public double? MaxValue { get; set; }
}

[Table("SMM_Consumables")]
public class SmmConsumable
{
    [Key]
    public int Id { get; set; }

    public int ElementId { get; set; } // FK SMM_Elements

    [Required, MaxLength(150)]
    public string TaskName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string PartSku { get; set; } = string.Empty;

    [Required, MaxLength(300)]
    public string PartDescription { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string PartUnit { get; set; } = "ud";

    public double PartDefaultQuantity { get; set; } = 1.0;
}

// ─────────────────────────────────────────────────────────────────────────────
// Captura (datos en runtime)
// ─────────────────────────────────────────────────────────────────────────────

[Table("SMM_Cycles")]
public class SmmCycle
{
    [Key]
    public int Id { get; set; }

    public int GroupId { get; set; }

    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    [Required, MaxLength(20)]
    public string Status { get; set; } = "Running"; // Running | Completed | Aborted | Error  (INMUTABLE — DEC-023)

    [MaxLength(30)]
    public string? EndedReason { get; set; } // Normal | BackendRestart | Manual | Timeout

    public int AlarmsCount { get; set; } = 0;
    public double AlarmTime_s { get; set; } = 0;
    public bool HadAlarms { get; set; } = false;

    // Soft-delete (DEC-023)
    public bool IsDeleted { get; set; } = false;
    [MaxLength(100)] public string? DeletedBy { get; set; }
    public DateTime? DeletedAt { get; set; }
    [MaxLength(500)] public string? DeleteReason { get; set; }
}

[Table("SMM_CycleAlarms")]
public class SmmCycleAlarm
{
    [Key]
    public int Id { get; set; }

    public int CycleId { get; set; } // FK CASCADE

    [Required, MaxLength(50)]
    public string AlarmCode { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? AlarmText { get; set; }

    public int Severity { get; set; } = 0;

    public DateTime RaisedAt { get; set; }
    public DateTime? ClearedAt { get; set; }
    public double DurationInCycle_s { get; set; } = 0;
}

[Table("SMM_Readings")]
public class SmmReading
{
    [Key]
    public long Id { get; set; }

    public int GroupId { get; set; }
    public int VariableId { get; set; }
    public int? CycleId { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public double? Value { get; set; }

    /// <summary>Valor textual cuando la variable PLC es STRING (TipoTren, TipoLavado, etc.).</summary>
    [MaxLength(500)]
    public string? StringValue { get; set; }

    [MaxLength(20)]
    public string Source { get; set; } = "Plc"; // Plc | Computed (DEC-016)

    public bool IsError { get; set; } = false;

    [MaxLength(300)]
    public string? ErrorReason { get; set; }

    [MaxLength(200)]
    public string? PlcVariable { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Mantenimiento (DEC-014/017/019/023)
// ─────────────────────────────────────────────────────────────────────────────

[Table("SMM_ElementLifecycles")]
public class SmmElementLifecycle
{
    [Key]
    public int Id { get; set; }

    public int ElementId { get; set; }

    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }

    /// <summary>JSON dict VarName→valor acumulado al iniciar este lifecycle</summary>
    public string AccumulatedValueAtStartJson { get; set; } = "{}";

    public int? EndingInterventionId { get; set; }
}

[Table("SMM_Interventions")]
public class SmmIntervention
{
    [Key]
    public int Id { get; set; }

    public int ElementId { get; set; }
    public int ElementLifecycleId { get; set; }

    [Required, MaxLength(150)]
    public string TaskName { get; set; } = string.Empty;

    [Required, MaxLength(30)]
    public string InterventionType { get; set; } = "Maintenance"; // Maintenance | Replacement | Inspection

    public DateTime PerformedAt { get; set; }

    [Required, MaxLength(50)]
    public string PerformedByRole { get; set; } = "CustomerMaintainer";

    [MaxLength(100)]
    public string? PerformedByUser { get; set; }

    [MaxLength(100)]
    public string? WorkOrderRef { get; set; }

    public double? AccumulatedValueAtMaintenance { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public int? TriggeredByPredictionId { get; set; }

    [MaxLength(100)]
    public string CreatedBy { get; set; } = "system";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string? LastModifiedBy { get; set; }
    public DateTime? LastModifiedAt { get; set; }

    /// <summary>Optimistic concurrency token (DEC-023)</summary>
    [Timestamp]
    public byte[]? RowVersion { get; set; }
}

[Table("SMM_ConsumableUsage")]
public class SmmConsumableUsage
{
    [Key]
    public int Id { get; set; }

    public int InterventionId { get; set; } // FK CASCADE

    [Required, MaxLength(100)]
    public string PartSku { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? PartDescription { get; set; }

    [MaxLength(20)]
    public string PartUnit { get; set; } = "ud";

    public double Quantity { get; set; } = 1.0;
}

// ─────────────────────────────────────────────────────────────────────────────
// IA (DEC-021/022)
// ─────────────────────────────────────────────────────────────────────────────

[Table("SMM_DerivedErrorStats")]
public class SmmDerivedErrorStats
{
    [Key]
    public int Id { get; set; }

    public int GroupId { get; set; }

    [Required, MaxLength(150)]
    public string VarName { get; set; } = string.Empty;

    public int TotalEvaluations { get; set; } = 0;
    public int ErrorCount { get; set; } = 0;
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
}

[Table("SMM_Predictions")]
public class SmmPrediction
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(40)]
    public string PredictionType { get; set; } = "Anomaly"; // Anomaly | Forecast | Recommendation | Summary | Correlation

    public int? RelatedElementId { get; set; }
    public int? RelatedVariableId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
    public int? ResolvedByInterventionId { get; set; }

    public int Severity { get; set; } = 0;

    [MaxLength(2000)]
    public string? Description { get; set; }

    public double? Confidence { get; set; }
}

[Table("SMM_PredictionInterventions")]
public class SmmPredictionIntervention
{
    [Key]
    public int Id { get; set; }

    public int PredictionId { get; set; }
    public int InterventionId { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// UI / Audit
// ─────────────────────────────────────────────────────────────────────────────

[Table("SMM_UserDashboardLayouts")]
public class SmmUserDashboardLayout
{
    [Key]
    public int Id { get; set; }

    public int UserId { get; set; }

    /// <summary>JSON serialized react-grid-layout config</summary>
    public string LayoutJson { get; set; } = "{}";

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

[Table("SMM_ExportLog")]
public class SmmExportLog
{
    [Key]
    public int Id { get; set; }

    [MaxLength(100)]
    public string? ExportedBy { get; set; }
    public DateTime ExportedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(20)]
    public string Format { get; set; } = "XLSX"; // XLSX | CSV | PDF

    public int RowCount { get; set; } = 0;

    [MaxLength(2000)]
    public string? FilterJson { get; set; }
}
