using System.ComponentModel.DataAnnotations;

namespace SW.PC.API.Backend.Models.Database
{
    // Modelos para Base de Datos (Alarmas, Recetas, Estadísticas)
    
    /// <summary>
    /// Alarma del sistema SCADA
    /// </summary>
    public class Alarm
    {
        public int Id { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string AlarmCode { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;
        
        public AlarmSeverity Severity { get; set; } = AlarmSeverity.Warning;
        
        public AlarmStatus Status { get; set; } = AlarmStatus.Active;
        
        [MaxLength(200)]
        public string? Source { get; set; }  // PLC tag, sensor, etc.
        
        public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? AcknowledgedAt { get; set; }
        
        [MaxLength(100)]
        public string? AcknowledgedBy { get; set; }
        
        public DateTime? ResolvedAt { get; set; }
        
        public string? Notes { get; set; }
        
        public Dictionary<string, object>? AdditionalData { get; set; }
    }
    
    public enum AlarmSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2,
        Critical = 3
    }
    
    public enum AlarmStatus
    {
        Active = 0,
        Acknowledged = 1,
        Resolved = 2,
        Cleared = 3
    }
    
    /// <summary>
    /// Receta de producción
    /// </summary>
    public class Recipe
    {
        public int Id { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
        
        [MaxLength(50)]
        public string Version { get; set; } = "1.0";
        
        public string? Description { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string ProductType { get; set; } = string.Empty;
        
        public Dictionary<string, object> Parameters { get; set; } = new();
        
        public bool IsActive { get; set; } = true;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? UpdatedAt { get; set; }
        
        [MaxLength(100)]
        public string CreatedBy { get; set; } = string.Empty;
        
        [MaxLength(100)]
        public string? UpdatedBy { get; set; }
        
        public List<RecipeStep> Steps { get; set; } = new();
    }
    
    /// <summary>
    /// Paso de receta
    /// </summary>
    public class RecipeStep
    {
        public int Id { get; set; }
        
        public int RecipeId { get; set; }
        
        public Recipe? Recipe { get; set; }
        
        public int StepNumber { get; set; }
        
        [Required]
        [MaxLength(200)]
        public string StepName { get; set; } = string.Empty;
        
        public string? Description { get; set; }
        
        public int DurationSeconds { get; set; }
        
        public Dictionary<string, object> Parameters { get; set; } = new();
        
        public Dictionary<string, object>? Conditions { get; set; }
    }
    
    /// <summary>
    /// Estadística de producción
    /// </summary>
    public class ProductionStatistic
    {
        public int Id { get; set; }
        
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        [Required]
        [MaxLength(100)]
        public string MachineId { get; set; } = string.Empty;
        
        [MaxLength(100)]
        public string? RecipeName { get; set; }
        
        public int UnitsProduced { get; set; }
        
        public int UnitsRejected { get; set; }
        
        public double EfficiencyPercentage { get; set; }
        
        public double CycleTimeSeconds { get; set; }
        
        public double? Temperature { get; set; }
        
        public double? Pressure { get; set; }
        
        public double? Speed { get; set; }
        
        public Dictionary<string, object>? CustomMetrics { get; set; }
        
        public string? Notes { get; set; }
    }
    
    /// <summary>
    /// Log de eventos del sistema
    /// </summary>
    public class SystemLog
    {
        public int Id { get; set; }
        
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        public LogLevel Level { get; set; } = LogLevel.Information;
        
        [Required]
        [MaxLength(100)]
        public string Source { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(500)]
        public string Message { get; set; } = string.Empty;
        
        public string? Details { get; set; }
        
        [MaxLength(100)]
        public string? UserId { get; set; }
        
        public string? StackTrace { get; set; }
    }
    
    public enum LogLevel
    {
        Trace = 0,
        Debug = 1,
        Information = 2,
        Warning = 3,
        Error = 4,
        Critical = 5
    }
    
    /// <summary>
    /// Usuario del sistema SCADA
    /// </summary>
    public class ScadaUser
    {
        public int Id { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string Username { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(255)]
        public string PasswordHash { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(200)]
        public string FullName { get; set; } = string.Empty;
        
        [EmailAddress]
        [MaxLength(200)]
        public string? Email { get; set; }
        
        public UserRole Role { get; set; } = UserRole.Operator;
        
        public bool IsActive { get; set; } = true;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? LastLoginAt { get; set; }
        
        public List<string> Permissions { get; set; } = new();
    }
    
    public enum UserRole
    {
        Operator = 0,
        Supervisor = 1,
        Engineer = 2,
        Administrator = 3
    }
}