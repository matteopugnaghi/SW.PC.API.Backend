// ============================================================================
// AquafrischDbContext.cs - Contexto de Base de Datos SQLite
// ============================================================================
// Entity Framework Core DbContext para la base de datos principal del sistema
// Incluye: Usuarios, Roles, Sesiones, Intentos de Login
// ============================================================================

using Microsoft.EntityFrameworkCore;
using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Models.Database;

namespace SW.PC.API.Backend.Data;

/// <summary>
/// DbContext principal para SQLite - Sistema Aquafrisch Supervisor
/// </summary>
public class AquafrischDbContext : DbContext
{
    public AquafrischDbContext(DbContextOptions<AquafrischDbContext> options) 
        : base(options)
    {
    }

    #region DbSets

    /// <summary>Usuarios del sistema</summary>
    public DbSet<User> Users { get; set; } = null!;
    
    /// <summary>Roles del sistema</summary>
    public DbSet<Role> Roles { get; set; } = null!;
    
    /// <summary>Relación Usuario-Rol</summary>
    public DbSet<SW.PC.API.Backend.Models.UserRole> UserRoles { get; set; } = null!;
    
    /// <summary>Sesiones activas</summary>
    public DbSet<UserSession> UserSessions { get; set; } = null!;
    
    /// <summary>Historial de intentos de login</summary>
    public DbSet<LoginAttempt> LoginAttempts { get; set; } = null!;
    
    /// <summary>Registro de operaciones (historial alarmas PLC, acciones usuario, etc.)</summary>
    public DbSet<OperationLog> OperationLogs { get; set; } = null!;
    
    /// <summary>Valores de configuración de máquina (memoria persistente)</summary>
    public DbSet<MachineSettingValue> MachineSettings { get; set; } = null!;

    /// <summary>Tipos de lavado (recetas de lavado)</summary>
    public DbSet<WashType> WashTypes { get; set; } = null!;
    
    /// <summary>Parámetros de tipos de lavado</summary>
    public DbSet<WashTypeParameter> WashTypeParameters { get; set; } = null!;
    
    /// <summary>Tipo de lavado activo (selección actual del operador)</summary>
    public DbSet<ActiveWashType> ActiveWashTypes { get; set; } = null!;

    /// <summary>Tipos de tren (recetas de tren)</summary>
    public DbSet<TrainType> TrainTypes { get; set; } = null!;
    
    /// <summary>Parámetros de tipos de tren</summary>
    public DbSet<TrainTypeParameter> TrainTypeParameters { get; set; } = null!;
    
    /// <summary>Datos de interpolación de Gantry para tipos de tren</summary>
    public DbSet<TrainTypeGantryData> TrainTypeGantryData { get; set; } = null!;

    /// <summary>Tipo de tren activo (selección actual del operador)</summary>
    public DbSet<ActiveTrainType> ActiveTrainTypes { get; set; } = null!;

    /// <summary>Configuración de topología EtherCAT guardada</summary>
    public DbSet<Models.EtherCAT.EtherCATSavedConfiguration> EtherCATSavedConfigurations { get; set; } = null!;

    /// <summary>Documentos del DMS (Document Management System)</summary>
    public DbSet<Document> Documents { get; set; } = null!;

    /// <summary>Historial de cambios de documentos</summary>
    public DbSet<DocumentHistory> DocumentHistories { get; set; } = null!;

    /// <summary>Categorías de documentos (configuración dinámica)</summary>
    public DbSet<DocumentCategoryConfig> DocumentCategories { get; set; } = null!;

    /// <summary>Niveles de clasificación de información (ISO 27001 A.8.2)</summary>
    public DbSet<DocumentClassificationLevel> DocumentClassificationLevels { get; set; } = null!;

    /// <summary>Matriz de acceso: roles × categorías (ISO 27001 A.9.1)</summary>
    public DbSet<DocumentCategoryAccess> DocumentCategoryAccess { get; set; } = null!;

    // ─── SMM (Statistics & Maintenance Module) — DEC-013 Fase 3 ───
    public DbSet<Models.Smm.Entities.SmmGroup> SmmGroups { get; set; } = null!;
    public DbSet<Models.Smm.Entities.SmmElement> SmmElements { get; set; } = null!;
    public DbSet<Models.Smm.Entities.SmmVariable> SmmVariables { get; set; } = null!;
    public DbSet<Models.Smm.Entities.SmmConsumable> SmmConsumables { get; set; } = null!;
    public DbSet<Models.Smm.Entities.SmmCycle> SmmCycles { get; set; } = null!;
    public DbSet<Models.Smm.Entities.SmmCycleAlarm> SmmCycleAlarms { get; set; } = null!;
    public DbSet<Models.Smm.Entities.SmmReading> SmmReadings { get; set; } = null!;
    public DbSet<Models.Smm.Entities.SmmElementLifecycle> SmmElementLifecycles { get; set; } = null!;
    public DbSet<Models.Smm.Entities.SmmIntervention> SmmInterventions { get; set; } = null!;
    public DbSet<Models.Smm.Entities.SmmConsumableUsage> SmmConsumableUsage { get; set; } = null!;
    public DbSet<Models.Smm.Entities.SmmDerivedErrorStats> SmmDerivedErrorStats { get; set; } = null!;
    public DbSet<Models.Smm.Entities.SmmPrediction> SmmPredictions { get; set; } = null!;
    public DbSet<Models.Smm.Entities.SmmPredictionIntervention> SmmPredictionInterventions { get; set; } = null!;
    public DbSet<Models.Smm.Entities.SmmUserDashboardLayout> SmmUserDashboardLayouts { get; set; } = null!;
    public DbSet<Models.Smm.Entities.SmmExportLog> SmmExportLog { get; set; } = null!;

    #endregion

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ============================================
        // Configuración de User
        // ============================================
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.Email);
            entity.HasIndex(e => e.Status);
            
            entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
            entity.Property(e => e.PasswordHash).HasMaxLength(255);
            entity.Property(e => e.FullName).HasMaxLength(200);
            entity.Property(e => e.Email).HasMaxLength(200);
            entity.Property(e => e.ActiveDirectoryDN).HasMaxLength(500);
            entity.Property(e => e.LastLoginIp).HasMaxLength(50);
            entity.Property(e => e.CreatedBy).HasMaxLength(100);
            entity.Property(e => e.ModifiedBy).HasMaxLength(100);
            entity.Property(e => e.Notes).HasMaxLength(1000);
        });

        // ============================================
        // Configuración de Role
        // ============================================
        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name).IsUnique();
            entity.HasIndex(e => e.SystemRole);
            
            entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.PermissionsJson).HasMaxLength(4000);
        });

        // ============================================
        // Configuración de UserRole
        // ============================================
        modelBuilder.Entity<SW.PC.API.Backend.Models.UserRole>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.RoleId }).IsUnique();
            
            entity.HasOne(e => e.User)
                  .WithMany(u => u.UserRoles)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.Role)
                  .WithMany(r => r.UserRoles)
                  .HasForeignKey(e => e.RoleId)
                  .OnDelete(DeleteBehavior.Cascade);
            
            entity.Property(e => e.AssignedBy).HasMaxLength(100);
        });

        // ============================================
        // Configuración de UserSession
        // ============================================
        modelBuilder.Entity<UserSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Token);
            entity.HasIndex(e => e.RefreshToken);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.ExpiresAt);
            
            entity.HasOne(e => e.User)
                  .WithMany(u => u.Sessions)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
            
            entity.Property(e => e.Token).IsRequired().HasMaxLength(500);
            entity.Property(e => e.RefreshToken).HasMaxLength(500);
            entity.Property(e => e.IpAddress).HasMaxLength(50);
            entity.Property(e => e.UserAgent).HasMaxLength(500);
            entity.Property(e => e.RevokedReason).HasMaxLength(200);
        });

        // ============================================
        // Configuración de LoginAttempt
        // ============================================
        modelBuilder.Entity<LoginAttempt>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Username);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.IpAddress);
            entity.HasIndex(e => e.Success);
            
            entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
            entity.Property(e => e.IpAddress).HasMaxLength(50);
            entity.Property(e => e.UserAgent).HasMaxLength(500);
            entity.Property(e => e.FailureReason).HasMaxLength(500);
            entity.Property(e => e.AuthMethod).HasMaxLength(50);
        });

        // ============================================
        // Configuración de OperationLog
        // ============================================
        modelBuilder.Entity<OperationLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            // Índices para consultas frecuentes
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.Action);
            entity.HasIndex(e => e.Severity);
            entity.HasIndex(e => e.User);
            entity.HasIndex(e => e.PlcVariable);
            entity.HasIndex(e => e.AlarmIndex);
            entity.HasIndex(e => e.IsAcknowledged);
            
            // Índice compuesto para filtros comunes
            entity.HasIndex(e => new { e.Category, e.Timestamp });
            entity.HasIndex(e => new { e.Category, e.Action, e.Timestamp });
            
            entity.Property(e => e.User).HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.PlcVariable).HasMaxLength(200);
            entity.Property(e => e.AlarmCode).HasMaxLength(50);
            entity.Property(e => e.AlarmType).HasMaxLength(20);
            entity.Property(e => e.ActionKey).HasMaxLength(100);
            entity.Property(e => e.OldValue).HasMaxLength(100);
            entity.Property(e => e.NewValue).HasMaxLength(100);
            entity.Property(e => e.IpAddress).HasMaxLength(50);
            entity.Property(e => e.SessionId).HasMaxLength(100);
            entity.Property(e => e.AcknowledgedBy).HasMaxLength(100);
        });

        // ============================================
        // Configuración de MachineSettingValue
        // ============================================
        modelBuilder.Entity<MachineSettingValue>(entity =>
        {
            entity.ToTable("MachineSettings"); // Nombre explícito de tabla
            entity.HasKey(e => e.Id);
            
            // Índice único para ParameterId (solo un valor por parámetro)
            entity.HasIndex(e => e.ParameterId).IsUnique();
            entity.HasIndex(e => e.DataType);
            entity.HasIndex(e => e.UpdatedAt);
            
            entity.Property(e => e.ParameterId).IsRequired().HasMaxLength(200);
            entity.Property(e => e.PlcVariable).HasMaxLength(500);
            entity.Property(e => e.DataType).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Value).IsRequired();
            entity.Property(e => e.UpdatedBy).HasMaxLength(100);
            entity.Property(e => e.Notes).HasMaxLength(500);
        });

        // ============================================
        // Configuración de WashType (Tipos de Lavado)
        // ============================================
        modelBuilder.Entity<WashType>(entity =>
        {
            entity.ToTable("WashTypes");
            entity.HasKey(e => e.Id);
            
            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.DisplayOrder);
            
            entity.Property(e => e.Code).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Icon).HasMaxLength(100);
            entity.Property(e => e.Color).HasMaxLength(10);
            entity.Property(e => e.CreatedBy).HasMaxLength(100);
            entity.Property(e => e.UpdatedBy).HasMaxLength(100);
            
            entity.HasMany(e => e.Parameters)
                  .WithOne(p => p.WashType)
                  .HasForeignKey(p => p.WashTypeId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ============================================
        // Configuración de WashTypeParameter
        // ============================================
        modelBuilder.Entity<WashTypeParameter>(entity =>
        {
            entity.ToTable("WashTypeParameters");
            entity.HasKey(e => e.Id);
            
            entity.HasIndex(e => new { e.WashTypeId, e.ParameterCode }).IsUnique();
            entity.HasIndex(e => e.DisplayOrder);
            
            entity.Property(e => e.ParameterCode).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.DataType).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Value).HasMaxLength(200);
            entity.Property(e => e.Unit).HasMaxLength(20);
            entity.Property(e => e.PlcVariable).HasMaxLength(200);
        });

        // ============================================
        // Configuración de ActiveWashType
        // ============================================
        modelBuilder.Entity<ActiveWashType>(entity =>
        {
            entity.ToTable("ActiveWashType");
            entity.HasKey(e => e.Id);
            
            entity.HasIndex(e => e.WashTypeId);
            
            entity.Property(e => e.SelectedBy).HasMaxLength(100);
            
            entity.HasOne(e => e.WashType)
                  .WithMany()
                  .HasForeignKey(e => e.WashTypeId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ============================================
        // Configuración de TrainType (Tipos de Tren)
        // ============================================
        modelBuilder.Entity<TrainType>(entity =>
        {
            entity.ToTable("TrainTypes");
            entity.HasKey(e => e.Id);
            
            entity.HasIndex(e => e.Code).IsUnique();
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.DisplayOrder);
            
            entity.Property(e => e.Code).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Icon).HasMaxLength(100);
            entity.Property(e => e.Color).HasMaxLength(10);
            entity.Property(e => e.CreatedBy).HasMaxLength(100);
            entity.Property(e => e.UpdatedBy).HasMaxLength(100);
            
            entity.HasMany(e => e.Parameters)
                  .WithOne(p => p.TrainType)
                  .HasForeignKey(p => p.TrainTypeId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ============================================
        // Configuración de TrainTypeParameter
        // ============================================
        modelBuilder.Entity<TrainTypeParameter>(entity =>
        {
            entity.ToTable("TrainTypeParameters");
            entity.HasKey(e => e.Id);
            
            entity.HasIndex(e => new { e.TrainTypeId, e.ParameterCode }).IsUnique();
            entity.HasIndex(e => e.DisplayOrder);
            
            entity.Property(e => e.ParameterCode).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.DataType).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Value).HasMaxLength(200);
            entity.Property(e => e.Unit).HasMaxLength(20);
            entity.Property(e => e.PlcVariable).HasMaxLength(200);
            entity.Property(e => e.GroupName).HasMaxLength(50);
        });

        // ============================================
        // Configuración de TrainTypeGantryData (Interpolación Gantry)
        // ============================================
        modelBuilder.Entity<TrainTypeGantryData>(entity =>
        {
            entity.ToTable("TrainTypeGantryData");
            entity.HasKey(e => e.Id);
            
            // Índice único: TrainTypeId + TableId + RowNumber
            entity.HasIndex(e => new { e.TrainTypeId, e.TableId, e.RowNumber }).IsUnique();
            entity.HasIndex(e => e.TableId);
            
            // Relación con TrainType
            entity.HasOne(e => e.TrainType)
                  .WithMany(t => t.GantryData)
                  .HasForeignKey(e => e.TrainTypeId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ============================================
        // Configuración de EtherCATSavedConfiguration
        // ============================================
        modelBuilder.Entity<Models.EtherCAT.EtherCATSavedConfiguration>(entity =>
        {
            entity.ToTable("EtherCATSavedConfigurations");
            entity.HasKey(e => e.Id);
            
            // Índice único por proyecto (solo una configuración guardada por proyecto)
            entity.HasIndex(e => e.ProjectId).IsUnique();
            entity.HasIndex(e => e.SavedAt);
            
            entity.Property(e => e.ProjectId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.TopologyJson).IsRequired();
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.ConfigurationHash).HasMaxLength(64);
        });

        // ============================================
        // Configuración de Document + Classification (sin FK automática)
        // ============================================
        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasKey(e => e.Id);
            // ClassificationId se gestiona manualmente, NO es FK
        });

        modelBuilder.Entity<DocumentClassificationLevel>(entity =>
        {
            entity.HasKey(e => e.Id);
        });

        // DocumentCategoryConfig — defaults SQL para que los INSERT OR IGNORE del seed funcionen
        modelBuilder.Entity<DocumentCategoryConfig>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DefaultClassificationId).HasDefaultValue(0);
            entity.Property(e => e.DefaultMinimumRole).HasDefaultValue("Visualizador");
            entity.Property(e => e.Icon).HasDefaultValue("📄");
            entity.Property(e => e.Color).HasDefaultValue("#6b7280");
            entity.Property(e => e.FolderName).HasDefaultValue("");
            entity.Property(e => e.IsSystem).HasDefaultValue(false);
            entity.Property(e => e.SortOrder).HasDefaultValue(100);
        });

        // ============================================
        // Seed Data - Roles del Sistema
        // ============================================
        SeedRoles(modelBuilder);
    }

    /// <summary>
    /// Seed inicial de roles del sistema según EU CRA
    /// Jerarquía: SuperAdmin (Fabricante) > Administrator (Cliente) > Operator > Maintenance > Viewer/Auditor
    /// </summary>
    private static void SeedRoles(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>().HasData(
            // ============================================
            // SUPERADMIN - Solo Fabricante (Aquafrisch)
            // Acceso TOTAL: PLC, TwinCAT, firmware, código, todos los usuarios
            // ============================================
            new Role
            {
                Id = 6, // Nuevo ID para no conflictar con existentes
                Name = "SuperAdmin",
                Description = "Super Administrador (Solo Fabricante Aquafrisch) - Acceso TOTAL incluyendo PLC, TwinCAT, firmware y código fuente. NO se entrega al cliente.",
                SystemRole = SystemRole.SuperAdmin,
                IsSystemRole = true,
                PermissionsJson = """
                {
                    "users": ["create", "read", "update", "delete", "manage_all"],
                    "roles": ["create", "read", "update", "delete", "assign_superadmin"],
                    "audit": ["read", "export", "purge"],
                    "config": ["read", "update", "system"],
                    "plc": ["read", "write", "config", "program", "twincat"],
                    "alarms": ["read", "acknowledge", "config", "system"],
                    "recipes": ["create", "read", "update", "delete", "execute", "import", "export"],
                    "reports": ["read", "create", "export", "system"],
                    "security": ["read", "update", "system"],
                    "backup": ["create", "restore", "system"],
                    "firmware": ["read", "update"],
                    "system": ["read", "update", "restart", "maintenance"],
                    "license": ["read", "update"]
                }
                """
            },
            // ============================================
            // ADMINISTRATOR - Cliente (Responsable Seguridad)
            // Gestión de usuarios de SU instalación, SIN acceso a PLC/código
            // ============================================
            new Role
            {
                Id = 1,
                Name = "Administrator",
                Description = "Administrador del Cliente - Gestión de usuarios de su instalación, configuración operativa. SIN acceso a PLC, TwinCAT, firmware o código.",
                SystemRole = SystemRole.Administrator,
                IsSystemRole = true,
                PermissionsJson = """
                {
                    "users": ["create", "read", "update", "delete"],
                    "roles": ["read", "assign"],
                    "audit": ["read", "export"],
                    "config": ["read", "update"],
                    "plc": ["read"],
                    "alarms": ["read", "acknowledge", "config"],
                    "recipes": ["create", "read", "update", "delete", "execute"],
                    "reports": ["read", "create", "export"],
                    "security": ["read"],
                    "backup": ["create"]
                }
                """
            },
            new Role
            {
                Id = 2,
                Name = "Operator",
                Description = "Operador de proceso - Control de operaciones, reconocimiento de alarmas, sin acceso a configuración",
                SystemRole = SystemRole.Operator,
                IsSystemRole = true,
                PermissionsJson = """
                {
                    "plc": ["read", "write"],
                    "alarms": ["read", "acknowledge"],
                    "recipes": ["read", "execute"],
                    "reports": ["read"]
                }
                """
            },
            new Role
            {
                Id = 3,
                Name = "Maintenance",
                Description = "Personal de mantenimiento - Configuración técnica, diagnósticos, sin acceso a seguridad",
                SystemRole = SystemRole.Maintenance,
                IsSystemRole = true,
                PermissionsJson = """
                {
                    "plc": ["read", "write", "config"],
                    "alarms": ["read", "acknowledge", "config"],
                    "recipes": ["create", "read", "update", "execute"],
                    "reports": ["read", "create"],
                    "config": ["read", "update"]
                }
                """
            },
            new Role
            {
                Id = 4,
                Name = "Viewer",
                Description = "Solo visualización - Acceso de solo lectura a datos de proceso y reportes",
                SystemRole = SystemRole.Viewer,
                IsSystemRole = true,
                PermissionsJson = """
                {
                    "plc": ["read"],
                    "alarms": ["read"],
                    "recipes": ["read"],
                    "reports": ["read"]
                }
                """
            },
            new Role
            {
                Id = 5,
                Name = "Auditor",
                Description = "Auditor de seguridad - Acceso a logs de auditoría, reportes de seguridad y compliance",
                SystemRole = SystemRole.Auditor,
                IsSystemRole = true,
                PermissionsJson = """
                {
                    "audit": ["read", "export"],
                    "reports": ["read", "export"],
                    "security": ["read"],
                    "users": ["read"]
                }
                """
            }
        );
    }
}

/// <summary>
/// Factory para crear el DbContext con configuración de SQLite
/// </summary>
public static class AquafrischDbContextFactory
{
    /// <summary>
    /// Crea y configura el DbContext con la ruta especificada
    /// </summary>
    public static AquafrischDbContext Create(string databasePath)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AquafrischDbContext>();
        optionsBuilder.UseSqlite($"Data Source={databasePath}");
        
        return new AquafrischDbContext(optionsBuilder.Options);
    }
    
    /// <summary>
    /// Asegura que la base de datos existe y aplica migraciones pendientes
    /// </summary>
    public static async Task EnsureDatabaseCreatedAsync(AquafrischDbContext context)
    {
        // Crear directorio si no existe
        var dbPath = context.Database.GetConnectionString();
        if (!string.IsNullOrEmpty(dbPath))
        {
            var match = System.Text.RegularExpressions.Regex.Match(dbPath, @"Data Source=(.+?)(?:;|$)");
            if (match.Success)
            {
                var filePath = match.Groups[1].Value;
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }
        }
        
        // Crear base de datos si no existe
        await context.Database.EnsureCreatedAsync();
        
        // Asegurar que TODAS las tablas auxiliares existen y tienen seed data
        // (OperationLogs, MachineSettings, WashTypes, TrainTypes, EtherCAT, Documents, Categories, etc.)
        await EnsureOperationLogsTableAsync(context);
        await EnsureMachineSettingsTableAsync(context);
        await EnsureWashTypesTablesAsync(context);
        await EnsureTrainTypesTablesAsync(context);
        await EnsureEtherCATSavedConfigurationsTableAsync(context);
        await EnsureDocumentsTablesAsync(context);
        await EnsureSmmTablesAsync(context);
    }
    
    /// <summary>
    /// Crear tabla OperationLogs si no existe (para bases de datos existentes)
    /// </summary>
    private static async Task EnsureOperationLogsTableAsync(AquafrischDbContext context)
    {
        try
        {
            // Crear tabla si no existe (con nueva estructura sin MessageSPA/MessageENG, con ActionKey)
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS OperationLogs (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Timestamp TEXT NOT NULL,
                    Category INTEGER NOT NULL,
                    Action INTEGER NOT NULL,
                    Severity INTEGER NOT NULL DEFAULT 0,
                    User TEXT NOT NULL DEFAULT 'System',
                    Description TEXT NOT NULL DEFAULT '',
                    PlcVariable TEXT,
                    AlarmIndex INTEGER,
                    AlarmCode TEXT,
                    AlarmType TEXT,
                    ActionKey TEXT,
                    OldValue TEXT,
                    NewValue TEXT,
                    IpAddress TEXT,
                    SessionId TEXT,
                    DetailsJson TEXT,
                    IsAcknowledged INTEGER NOT NULL DEFAULT 0,
                    AcknowledgedBy TEXT,
                    AcknowledgedAt TEXT
                )");
            
            // 🔧 Migración: Añadir columna ActionKey si no existe (para tablas antiguas)
            try
            {
                await context.Database.ExecuteSqlRawAsync(@"ALTER TABLE OperationLogs ADD COLUMN ActionKey TEXT");
            }
            catch { /* Columna ya existe */ }
            
            // Crear índices
            await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS IX_OperationLogs_Timestamp ON OperationLogs(Timestamp)");
            await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS IX_OperationLogs_Category ON OperationLogs(Category)");
            await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS IX_OperationLogs_Action ON OperationLogs(Action)");
            await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS IX_OperationLogs_Severity ON OperationLogs(Severity)");
            await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS IX_OperationLogs_User ON OperationLogs(User)");
            await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS IX_OperationLogs_PlcVariable ON OperationLogs(PlcVariable)");
            await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS IX_OperationLogs_IsAcknowledged ON OperationLogs(IsAcknowledged)");
            await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS IX_OperationLogs_Category_Timestamp ON OperationLogs(Category, Timestamp)");
        }
        catch (Exception)
        {
            // Tabla ya existe o error menor - ignorar
        }
        
        // Crear tabla MachineSettings para configuraciones de máquina
        await EnsureMachineSettingsTableAsync(context);
        
        // Crear tablas WashTypes para tipos de lavado
        await EnsureWashTypesTablesAsync(context);
        
        // Crear tablas TrainTypes para tipos de tren
        await EnsureTrainTypesTablesAsync(context);
        
        // Crear tabla EtherCATSavedConfigurations para configuración guardada
        await EnsureEtherCATSavedConfigurationsTableAsync(context);
        
        // Crear tablas Documents y DocumentHistory para el DMS
        await EnsureDocumentsTablesAsync(context);
    }
    
    /// <summary>
    /// Crear tabla MachineSettings si no existe (para bases de datos existentes)
    /// </summary>
    private static async Task EnsureMachineSettingsTableAsync(AquafrischDbContext context)
    {
        try
        {
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS MachineSettings (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ParameterId TEXT NOT NULL UNIQUE,
                    PlcVariable TEXT,
                    DataType TEXT NOT NULL,
                    Value TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL,
                    UpdatedBy TEXT,
                    Notes TEXT
                )");
            
            // Crear índices
            await context.Database.ExecuteSqlRawAsync(@"CREATE UNIQUE INDEX IF NOT EXISTS IX_MachineSettings_ParameterId ON MachineSettings(ParameterId)");
            await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS IX_MachineSettings_DataType ON MachineSettings(DataType)");
            await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS IX_MachineSettings_UpdatedAt ON MachineSettings(UpdatedAt)");
        }
        catch (Exception)
        {
            // Tabla ya existe o error menor - ignorar
        }
    }
    
    /// <summary>
    /// Crear tablas WashTypes, WashTypeParameters y ActiveWashType si no existen
    /// </summary>
    private static async Task EnsureWashTypesTablesAsync(AquafrischDbContext context)
    {
        try
        {
            // Tabla WashTypes (Tipos de Lavado)
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS WashTypes (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Code TEXT NOT NULL UNIQUE,
                    Name TEXT NOT NULL,
                    Description TEXT,
                    Icon TEXT,
                    Color TEXT,
                    IsActive INTEGER NOT NULL DEFAULT 1,
                    IsDefault INTEGER NOT NULL DEFAULT 0,
                    DisplayOrder INTEGER NOT NULL DEFAULT 0,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT,
                    CreatedBy TEXT,
                    UpdatedBy TEXT
                )");
            
            // Índices para WashTypes
            await context.Database.ExecuteSqlRawAsync(@"CREATE UNIQUE INDEX IF NOT EXISTS IX_WashTypes_Code ON WashTypes(Code)");
            await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS IX_WashTypes_IsActive ON WashTypes(IsActive)");
            await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS IX_WashTypes_DisplayOrder ON WashTypes(DisplayOrder)");
            
            // Tabla WashTypeParameters (Parámetros de tipos de lavado)
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS WashTypeParameters (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    WashTypeId INTEGER NOT NULL,
                    ParameterCode TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    DataType TEXT NOT NULL,
                    Value TEXT,
                    MinValue REAL,
                    MaxValue REAL,
                    Unit TEXT,
                    PlcVariable TEXT,
                    DisplayOrder INTEGER NOT NULL DEFAULT 0,
                    IsEditable INTEGER NOT NULL DEFAULT 1,
                    FOREIGN KEY (WashTypeId) REFERENCES WashTypes(Id) ON DELETE CASCADE,
                    UNIQUE (WashTypeId, ParameterCode)
                )");
            
            // Índices para WashTypeParameters
            await context.Database.ExecuteSqlRawAsync(@"CREATE UNIQUE INDEX IF NOT EXISTS IX_WashTypeParameters_WashTypeId_ParameterCode ON WashTypeParameters(WashTypeId, ParameterCode)");
            await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS IX_WashTypeParameters_DisplayOrder ON WashTypeParameters(DisplayOrder)");
            
            // Migración: añadir columna IsEditable si no existe (para bases de datos existentes)
            try
            {
                await context.Database.ExecuteSqlRawAsync(@"ALTER TABLE WashTypeParameters ADD COLUMN IsEditable INTEGER NOT NULL DEFAULT 1");
            }
            catch { /* Columna ya existe */ }
            
            // Tabla ActiveWashType (Tipo de lavado activo)
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS ActiveWashType (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    WashTypeId INTEGER NOT NULL,
                    SelectedAt TEXT NOT NULL,
                    SelectedBy TEXT,
                    WrittenToPlc INTEGER NOT NULL DEFAULT 0,
                    WrittenToPlcAt TEXT,
                    FOREIGN KEY (WashTypeId) REFERENCES WashTypes(Id) ON DELETE RESTRICT
                )");
            
            // Migración: añadir columna WrittenToPlc si no existe
            try
            {
                await context.Database.ExecuteSqlRawAsync(@"ALTER TABLE ActiveWashType ADD COLUMN WrittenToPlc INTEGER NOT NULL DEFAULT 0");
            }
            catch { /* Columna ya existe */ };
            
            // Índice para ActiveWashType
            await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS IX_ActiveWashType_WashTypeId ON ActiveWashType(WashTypeId)");
        }
        catch (Exception)
        {
            // Tablas ya existen o error menor - ignorar
        }
    }
    
    /// <summary>
    /// Crear tablas TrainTypes, TrainTypeParameters y ActiveTrainType si no existen
    /// </summary>
    private static async Task EnsureTrainTypesTablesAsync(AquafrischDbContext context)
    {
        try
        {
            // Tabla TrainTypes (Tipos de Tren)
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS TrainTypes (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Code TEXT NOT NULL UNIQUE,
                    Name TEXT NOT NULL,
                    Description TEXT,
                    Icon TEXT,
                    Color TEXT,
                    IsActive INTEGER NOT NULL DEFAULT 1,
                    IsDefault INTEGER NOT NULL DEFAULT 0,
                    DisplayOrder INTEGER NOT NULL DEFAULT 0,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT,
                    CreatedBy TEXT,
                    UpdatedBy TEXT
                )");
            
            // Índices para TrainTypes
            await context.Database.ExecuteSqlRawAsync(@"CREATE UNIQUE INDEX IF NOT EXISTS IX_TrainTypes_Code ON TrainTypes(Code)");
            await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS IX_TrainTypes_IsActive ON TrainTypes(IsActive)");
            await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS IX_TrainTypes_DisplayOrder ON TrainTypes(DisplayOrder)");
            
            // Tabla TrainTypeParameters (Parámetros de tipos de tren)
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS TrainTypeParameters (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    TrainTypeId INTEGER NOT NULL,
                    ParameterCode TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    DataType TEXT NOT NULL,
                    Value TEXT,
                    MinValue REAL,
                    MaxValue REAL,
                    Unit TEXT,
                    PlcVariable TEXT,
                    DisplayOrder INTEGER NOT NULL DEFAULT 0,
                    GroupName TEXT,
                    FOREIGN KEY (TrainTypeId) REFERENCES TrainTypes(Id) ON DELETE CASCADE,
                    UNIQUE (TrainTypeId, ParameterCode)
                )");
            
            // Índices para TrainTypeParameters
            await context.Database.ExecuteSqlRawAsync(@"CREATE UNIQUE INDEX IF NOT EXISTS IX_TrainTypeParameters_TrainTypeId_ParameterCode ON TrainTypeParameters(TrainTypeId, ParameterCode)");
            await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS IX_TrainTypeParameters_DisplayOrder ON TrainTypeParameters(DisplayOrder)");
            
            // Tabla ActiveTrainType (Tipo de tren activo)
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS ActiveTrainType (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    TrainTypeId INTEGER NOT NULL,
                    SelectedAt TEXT NOT NULL,
                    SelectedBy TEXT,
                    WrittenToPlc INTEGER NOT NULL DEFAULT 0,
                    WrittenToPlcAt TEXT,
                    FOREIGN KEY (TrainTypeId) REFERENCES TrainTypes(Id) ON DELETE RESTRICT
                )");
            
            // Índice para ActiveTrainType
            await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS IX_ActiveTrainType_TrainTypeId ON ActiveTrainType(TrainTypeId)");
            
            // Tabla TrainTypeGantryData (Datos de interpolación de Gantry)
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS TrainTypeGantryData (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    TrainTypeId INTEGER NOT NULL,
                    TableId TEXT NOT NULL,
                    RowNumber INTEGER NOT NULL,
                    EnableLine INTEGER NOT NULL DEFAULT 0,
                    Syncron TEXT DEFAULT 'Syncron',
                    Master1xStart REAL NOT NULL DEFAULT 0,
                    Slave1yStart REAL NOT NULL DEFAULT 0,
                    SpeedSlaveY1Start REAL NOT NULL DEFAULT 0,
                    Master1xEnd REAL NOT NULL DEFAULT 0,
                    Slave1yEnd REAL NOT NULL DEFAULT 0,
                    SpeedSlaveY1End REAL NOT NULL DEFAULT 0,
                    FOREIGN KEY (TrainTypeId) REFERENCES TrainTypes(Id) ON DELETE CASCADE,
                    UNIQUE (TrainTypeId, TableId, RowNumber)
                )");
            
            // Índices para TrainTypeGantryData
            await context.Database.ExecuteSqlRawAsync(@"CREATE UNIQUE INDEX IF NOT EXISTS IX_TrainTypeGantryData_TrainTypeId_TableId_RowNumber ON TrainTypeGantryData(TrainTypeId, TableId, RowNumber)");
            await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS IX_TrainTypeGantryData_TableId ON TrainTypeGantryData(TableId)");
        }
        catch (Exception)
        {
            // Tablas ya existen o error menor - ignorar
        }
    }
    
    /// <summary>
    /// Crear tabla EtherCATSavedConfigurations si no existe
    /// </summary>
    private static async Task EnsureEtherCATSavedConfigurationsTableAsync(AquafrischDbContext context)
    {
        try
        {
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS EtherCATSavedConfigurations (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ProjectId TEXT NOT NULL,
                    SavedAt TEXT NOT NULL,
                    TopologyJson TEXT NOT NULL,
                    TotalSlaves INTEGER NOT NULL DEFAULT 0,
                    Notes TEXT,
                    ConfigurationHash TEXT
                )");
            
            // Índice por ProjectId
            await context.Database.ExecuteSqlRawAsync(@"CREATE UNIQUE INDEX IF NOT EXISTS IX_EtherCATSavedConfigurations_ProjectId ON EtherCATSavedConfigurations(ProjectId)");
        }
        catch (Exception)
        {
            // Tabla ya existe o error menor - ignorar
        }
    }
    
    /// <summary>
    /// Crear tablas Documents y DocumentHistory para el DMS (Document Management System)
    /// EU CRA: Trazabilidad documental completa
    /// </summary>
    private static async Task EnsureDocumentsTablesAsync(AquafrischDbContext context)
    {
        try
        {
            // Tabla DocumentCategories (Categorías dinámicas de documentos)
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS DocumentCategories (
                    Id INTEGER PRIMARY KEY,
                    Name TEXT NOT NULL,
                    Icon TEXT NOT NULL DEFAULT '📄',
                    Color TEXT NOT NULL DEFAULT '#6b7280',
                    FolderName TEXT NOT NULL DEFAULT '',
                    SortOrder INTEGER NOT NULL DEFAULT 100,
                    IsSystem INTEGER NOT NULL DEFAULT 0,
                    ParentId INTEGER,
                    Description TEXT,
                    CreatedBy TEXT NOT NULL DEFAULT 'System',
                    CreatedAt TEXT NOT NULL DEFAULT (datetime('now'))
                )");

            // Migrar tablas existentes: añadir ParentId si no existe
            try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE DocumentCategories ADD COLUMN ParentId INTEGER"); }
            catch { /* columna ya existe */ }

            // Seed categorías del sistema con INSERT OR IGNORE (idempotente)
            // IMPORTANTE: Incluir DefaultClassificationId y DefaultMinimumRole porque EnsureCreatedAsync()
            // crea las columnas NOT NULL sin DEFAULT en SQL, y INSERT OR IGNORE falla silenciosamente
            var seedCols = "Id, Name, Icon, Color, FolderName, SortOrder, IsSystem, Description, CreatedBy, CreatedAt, DefaultClassificationId, DefaultMinimumRole";
            await context.Database.ExecuteSqlRawAsync($@"
                INSERT OR IGNORE INTO DocumentCategories ({seedCols})
                VALUES (0, 'Compliance CRA', '📋', '#ef4444', 'compliance', 0, 1, 'Documentación de cumplimiento normativo CRA', 'System', datetime('now'), 0, 'Visualizador')");
            await context.Database.ExecuteSqlRawAsync($@"
                INSERT OR IGNORE INTO DocumentCategories ({seedCols})
                VALUES (1, 'CRA Genérico (SW)', '🇪🇺', '#3b82f6', 'cra-generic', 1, 1, 'Documentación CRA genérica de software', 'System', datetime('now'), 0, 'Visualizador')");
            await context.Database.ExecuteSqlRawAsync($@"
                INSERT OR IGNORE INTO DocumentCategories ({seedCols})
                VALUES (2, 'Manuales de Usuario', '📖', '#10b981', 'user-guides', 2, 1, 'Manuales y guías de usuario', 'System', datetime('now'), 0, 'Visualizador')");
            await context.Database.ExecuteSqlRawAsync($@"
                INSERT OR IGNORE INTO DocumentCategories ({seedCols})
                VALUES (3, 'Documentación Técnica', '🔧', '#f59e0b', 'technical', 3, 1, 'Documentación técnica del sistema', 'System', datetime('now'), 0, 'Visualizador')");
            await context.Database.ExecuteSqlRawAsync($@"
                INSERT OR IGNORE INTO DocumentCategories ({seedCols})
                VALUES (4, 'Esquemas Eléctricos', '⚡', '#8b5cf6', 'electrical', 4, 1, 'Esquemas y documentación eléctrica', 'System', datetime('now'), 0, 'Visualizador')");
            await context.Database.ExecuteSqlRawAsync($@"
                INSERT OR IGNORE INTO DocumentCategories ({seedCols})
                VALUES (5, 'Mantenimiento', '🔩', '#06b6d4', 'maintenance', 5, 1, 'Documentación de mantenimiento', 'System', datetime('now'), 0, 'Visualizador')");
            // ID 6 "Interno" eliminado — generaba confusión con Clasificación ISO "Interno"
            await context.Database.ExecuteSqlRawAsync($@"
                INSERT OR IGNORE INTO DocumentCategories ({seedCols})
                VALUES (7, 'Otros', '📄', '#9ca3af', '', 7, 1, 'Documentación general no categorizada', 'System', datetime('now'), 0, 'Visualizador')");

            // Migrar DocumentCategories: añadir campos de clasificación por defecto
            try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE DocumentCategories ADD COLUMN DefaultClassificationId INTEGER NOT NULL DEFAULT 0"); }
            catch { /* columna ya existe */ }
            try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE DocumentCategories ADD COLUMN DefaultMinimumRole TEXT NOT NULL DEFAULT 'Visualizador'"); }
            catch { /* columna ya existe */ }

            // ═══ Tabla DocumentClassificationLevels (ISO 27001 A.8.2) ═══
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS DocumentClassificationLevels (
                    Id INTEGER PRIMARY KEY,
                    Name TEXT NOT NULL,
                    Code TEXT NOT NULL UNIQUE,
                    Level INTEGER NOT NULL DEFAULT 0,
                    Icon TEXT NOT NULL DEFAULT '🏷️',
                    Color TEXT NOT NULL DEFAULT '#6b7280',
                    Description TEXT,
                    IsSystem INTEGER NOT NULL DEFAULT 0,
                    SortOrder INTEGER NOT NULL DEFAULT 0,
                    CreatedBy TEXT NOT NULL DEFAULT 'System',
                    CreatedAt TEXT NOT NULL DEFAULT (datetime('now'))
                )");

            // Seed niveles de clasificación ISO 27001 (los nombres se pueden cambiar luego)
            await context.Database.ExecuteSqlRawAsync(@"
                INSERT OR IGNORE INTO DocumentClassificationLevels (Id, Name, Code, Level, Icon, Color, Description, IsSystem, SortOrder, CreatedBy, CreatedAt)
                VALUES (0, 'Público', 'public', 0, '🟢', '#22c55e', 'Información de acceso libre. Manuales de usuario, fichas comerciales.', 1, 0, 'System', datetime('now'))");
            await context.Database.ExecuteSqlRawAsync(@"
                INSERT OR IGNORE INTO DocumentClassificationLevels (Id, Name, Code, Level, Icon, Color, Description, IsSystem, SortOrder, CreatedBy, CreatedAt)
                VALUES (1, 'Interno', 'internal', 1, '🔵', '#3b82f6', 'Solo para personal autorizado. Procedimientos, configuraciones.', 1, 1, 'System', datetime('now'))");
            await context.Database.ExecuteSqlRawAsync(@"
                INSERT OR IGNORE INTO DocumentClassificationLevels (Id, Name, Code, Level, Icon, Color, Description, IsSystem, SortOrder, CreatedBy, CreatedAt)
                VALUES (2, 'Confidencial', 'confidential', 2, '🟠', '#f59e0b', 'Información sensible. Esquemas eléctricos, recetas, credenciales PLC.', 1, 2, 'System', datetime('now'))");
            await context.Database.ExecuteSqlRawAsync(@"
                INSERT OR IGNORE INTO DocumentClassificationLevels (Id, Name, Code, Level, Icon, Color, Description, IsSystem, SortOrder, CreatedBy, CreatedAt)
                VALUES (3, 'Restringido', 'restricted', 3, '🔴', '#ef4444', 'Máxima sensibilidad. Vulnerabilidades, pen-test, informes de seguridad.', 1, 3, 'System', datetime('now'))");

            // ═══ Tabla DocumentCategoryAccess (Matriz roles × categorías — ISO 27001 A.9.1) ═══
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS DocumentCategoryAccess (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CategoryId INTEGER NOT NULL,
                    RoleName TEXT NOT NULL,
                    CanRead INTEGER NOT NULL DEFAULT 1,
                    UpdatedBy TEXT,
                    UpdatedAt TEXT NOT NULL DEFAULT (datetime('now')),
                    UNIQUE(CategoryId, RoleName),
                    FOREIGN KEY (CategoryId) REFERENCES DocumentCategories(Id) ON DELETE CASCADE
                )");
            
            await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS IX_DocumentCategoryAccess_CategoryId ON DocumentCategoryAccess(CategoryId)");
            await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS IX_DocumentCategoryAccess_RoleName ON DocumentCategoryAccess(RoleName)");

            // Seed acceso por defecto (ISO 27001 A.9.1 — Principio de menor privilegio)
            // SuperAdmin tiene acceso implícito (no necesita entradas)
            // Administrador: acceso total. Otros roles: acceso limitado según función.
            var defaultAccess = new Dictionary<string, HashSet<int>>
            {
                // Administrador: TODAS las categorías
                { "Administrador", new() { 0, 1, 2, 3, 4, 5, 7 } },
                // Mantenimiento: Manuales(2), Técnica(3), Eléctricos(4), Mantenimiento(5), Otros(7)
                { "Mantenimiento", new() { 2, 3, 4, 5, 7 } },
                // Auditor: Compliance(0), CRA(1), Manuales(2), Técnica(3), Otros(7)
                { "Auditor", new() { 0, 1, 2, 3, 7 } },
                // Operador: Manuales(2), Otros(7)
                { "Operador", new() { 2, 7 } },
                // Visualizador: Manuales(2), Otros(7)
                { "Visualizador", new() { 2, 7 } },
            };
            for (int catId = 0; catId <= 7; catId++)
            {
                foreach (var (role, allowedCats) in defaultAccess)
                {
                    bool canRead = allowedCats.Contains(catId);
                    await context.Database.ExecuteSqlRawAsync($@"
                        INSERT OR IGNORE INTO DocumentCategoryAccess (CategoryId, RoleName, CanRead, UpdatedBy, UpdatedAt)
                        VALUES ({catId}, '{role}', {(canRead ? 1 : 0)}, 'System', datetime('now'))");
                }
            }

            // Migrar Documents: añadir ClassificationId si no existe
            try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE Documents ADD COLUMN ClassificationId INTEGER NOT NULL DEFAULT 0"); }
            catch { /* columna ya existe */ }
            // Migrar Documents: añadir campos cumplimiento normativo ISO 27001 + IEC 62443
            try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE Documents ADD COLUMN Iso27001Relevant INTEGER NOT NULL DEFAULT 0"); }
            catch { /* columna ya existe */ }
            try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE Documents ADD COLUMN Iso27001Article TEXT"); }
            catch { /* columna ya existe */ }
            try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE Documents ADD COLUMN Iec62443Relevant INTEGER NOT NULL DEFAULT 0"); }
            catch { /* columna ya existe */ }
            try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE Documents ADD COLUMN Iec62443Article TEXT"); }
            catch { /* columna ya existe */ }
            // Migrar Documents: añadir campos DMS Enterprise (Source, DocumentCode, DmsSubcategory*, DmsAuthor, DmsPublishedAt)
            try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE Documents ADD COLUMN Source TEXT NOT NULL DEFAULT 'local'"); }
            catch { /* columna ya existe */ }
            try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE Documents ADD COLUMN DocumentCode TEXT"); }
            catch { /* columna ya existe */ }
            try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE Documents ADD COLUMN DmsSubcategoryCode TEXT"); }
            catch { /* columna ya existe */ }
            try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE Documents ADD COLUMN DmsSubcategoryName TEXT"); }
            catch { /* columna ya existe */ }
            try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE Documents ADD COLUMN DmsAuthor TEXT"); }
            catch { /* columna ya existe */ }
            try { await context.Database.ExecuteSqlRawAsync("ALTER TABLE Documents ADD COLUMN DmsPublishedAt TEXT"); }
            catch { /* columna ya existe */ }
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS Documents (
                    Id TEXT PRIMARY KEY,
                    Slug TEXT NOT NULL UNIQUE,
                    Title TEXT NOT NULL,
                    Description TEXT,
                    FilePath TEXT NOT NULL,
                    FileType INTEGER NOT NULL DEFAULT 0,
                    ContentHash TEXT,
                    FileSize INTEGER NOT NULL DEFAULT 0,
                    Scope INTEGER NOT NULL DEFAULT 1,
                    Category INTEGER NOT NULL DEFAULT 7,
                    SubCategory TEXT,
                    Tags TEXT,
                    AccessLevel INTEGER NOT NULL DEFAULT 0,
                    MinimumRole TEXT NOT NULL DEFAULT 'Viewer',
                    Version TEXT NOT NULL DEFAULT '1.0',
                    Status INTEGER NOT NULL DEFAULT 0,
                    CraRelevant INTEGER NOT NULL DEFAULT 0,
                    CraArticle TEXT,
                    CraDeadline TEXT,
                    Iso27001Relevant INTEGER NOT NULL DEFAULT 0,
                    Iso27001Article TEXT,
                    Iec62443Relevant INTEGER NOT NULL DEFAULT 0,
                    Iec62443Article TEXT,
                    ApprovedBy TEXT,
                    ApprovedAt TEXT,
                    CreatedBy TEXT NOT NULL DEFAULT 'System',
                    CreatedAt TEXT NOT NULL,
                    UpdatedBy TEXT,
                    UpdatedAt TEXT,
                    ParentDocId TEXT,
                    RelatedDocIds TEXT,
                    SearchContent TEXT,
                    Source TEXT NOT NULL DEFAULT 'local',
                    DocumentCode TEXT,
                    DmsSubcategoryCode TEXT,
                    DmsSubcategoryName TEXT,
                    DmsAuthor TEXT,
                    DmsPublishedAt TEXT
                )");
            
            // Índices para Documents
            await context.Database.ExecuteSqlRawAsync(@"CREATE UNIQUE INDEX IF NOT EXISTS IX_Documents_Slug ON Documents(Slug)");
            await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS IX_Documents_Category ON Documents(Category)");
            await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS IX_Documents_Scope ON Documents(Scope)");
            await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS IX_Documents_Status ON Documents(Status)");
            await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS IX_Documents_AccessLevel ON Documents(AccessLevel, MinimumRole)");
            await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS IX_Documents_CraRelevant ON Documents(CraRelevant)");
            await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS IX_Documents_FilePath ON Documents(FilePath)");
            await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS IX_Documents_Source ON Documents(Source)");
            await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS IX_Documents_DocumentCode ON Documents(DocumentCode)");
            await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS IX_Documents_CreatedAt ON Documents(CreatedAt)");
            
            // Tabla DocumentHistory (Historial de cambios)
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS DocumentHistories (
                    Id TEXT PRIMARY KEY,
                    DocumentId TEXT NOT NULL,
                    Version TEXT NOT NULL,
                    Action TEXT NOT NULL,
                    ChangedBy TEXT NOT NULL,
                    ChangedAt TEXT NOT NULL,
                    CommitHash TEXT,
                    ContentHash TEXT,
                    ChangeNote TEXT,
                    FOREIGN KEY (DocumentId) REFERENCES Documents(Id) ON DELETE CASCADE
                )");
            
            // Índices para DocumentHistory
            await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS IX_DocumentHistories_DocumentId ON DocumentHistories(DocumentId)");
            await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS IX_DocumentHistories_ChangedAt ON DocumentHistories(ChangedAt)");
            await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS IX_DocumentHistories_Action ON DocumentHistories(Action)");
        }
        catch (Exception)
        {
            // Tablas ya existen o error menor - ignorar
        }
    }

    /// <summary>
    /// Crear tablas SMM_* (Statistics & Maintenance Module) — DEC-013 Fase 3
    /// Compatible con DBs existentes y nuevas. Reglas DEC-014/016/017/018/019/020/021/022/023.
    /// </summary>
    private static async Task EnsureSmmTablesAsync(AquafrischDbContext context)
    {
        try
        {
            // ── Catálogo (espejo Excel) ───────────────────────────────────
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS SMM_Groups (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    GroupName TEXT NOT NULL,
                    UiType TEXT NOT NULL DEFAULT 'Table',
                    ReadFrequency TEXT NOT NULL DEFAULT 'Continuous',
                    CycleRunningVar TEXT,
                    ShowCycleStart INTEGER NOT NULL DEFAULT 1,
                    ShowCycleEnd INTEGER NOT NULL DEFAULT 1,
                    ShowCycleDuration INTEGER NOT NULL DEFAULT 0,
                    AlarmHistVar TEXT,
                    LayoutWidth INTEGER,
                    LayoutHeight INTEGER,
                    LayoutPinned INTEGER NOT NULL DEFAULT 0,
                    RunningBitVar TEXT,
                    DonutMode TEXT NOT NULL DEFAULT 'LAST',
                    ShowInMaintenance INTEGER NOT NULL DEFAULT 0,
                    CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
                    UpdatedAt TEXT NOT NULL DEFAULT (datetime('now'))
                )");
            await context.Database.ExecuteSqlRawAsync(@"CREATE UNIQUE INDEX IF NOT EXISTS IX_SMM_Groups_GroupName ON SMM_Groups(GroupName)");

            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS SMM_Elements (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ElementName TEXT NOT NULL,
                    ComponentLocation3D TEXT,
                    SkuAquafrisch TEXT,
                    Manufacturer TEXT,
                    Model TEXT,
                    Notes TEXT,
                    ImagePath TEXT,
                    Model3DPath TEXT,
                    CreatedAt TEXT NOT NULL DEFAULT (datetime('now'))
                )");
            await context.Database.ExecuteSqlRawAsync(@"CREATE UNIQUE INDEX IF NOT EXISTS IX_SMM_Elements_ElementName ON SMM_Elements(ElementName)");

            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS SMM_Variables (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    GroupId INTEGER NOT NULL,
                    ElementId INTEGER,
                    VarName TEXT NOT NULL,
                    PlcVariable TEXT,
                    Unit TEXT,
                    DataType TEXT NOT NULL DEFAULT 'REAL',
                    Formula TEXT,
                    FormulaScope TEXT,
                    Warning REAL,
                    Critical REAL,
                    ResetOnMaintenance INTEGER NOT NULL DEFAULT 0,
                    RunningBitVar TEXT,
                    MaxValue REAL,
                    SortOrder INTEGER NOT NULL DEFAULT 0,
                    CHECK ((PlcVariable IS NOT NULL AND Formula IS NULL) OR (PlcVariable IS NULL AND Formula IS NOT NULL)),
                    FOREIGN KEY (GroupId) REFERENCES SMM_Groups(Id) ON DELETE CASCADE,
                    FOREIGN KEY (ElementId) REFERENCES SMM_Elements(Id) ON DELETE SET NULL
                )");
            await context.Database.ExecuteSqlRawAsync(@"CREATE UNIQUE INDEX IF NOT EXISTS IX_SMM_Variables_Group_Var ON SMM_Variables(GroupId, VarName)");
            await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS IX_SMM_Variables_ElementId ON SMM_Variables(ElementId)");

            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS SMM_Consumables (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ElementId INTEGER NOT NULL,
                    TaskName TEXT NOT NULL,
                    PartSku TEXT NOT NULL,
                    PartDescription TEXT NOT NULL,
                    PartUnit TEXT NOT NULL DEFAULT 'ud',
                    PartDefaultQuantity REAL NOT NULL DEFAULT 1.0,
                    FOREIGN KEY (ElementId) REFERENCES SMM_Elements(Id) ON DELETE CASCADE
                )");
            await context.Database.ExecuteSqlRawAsync(@"CREATE UNIQUE INDEX IF NOT EXISTS IX_SMM_Consumables_Elt_Task_Sku ON SMM_Consumables(ElementId, TaskName, PartSku)");

            // ── Captura ────────────────────────────────────────────────────
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS SMM_Cycles (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    GroupId INTEGER NOT NULL,
                    StartedAt TEXT NOT NULL,
                    CompletedAt TEXT,
                    Status TEXT NOT NULL DEFAULT 'Running',
                    EndedReason TEXT,
                    AlarmsCount INTEGER NOT NULL DEFAULT 0,
                    AlarmTime_s REAL NOT NULL DEFAULT 0,
                    HadAlarms INTEGER NOT NULL DEFAULT 0,
                    IsDeleted INTEGER NOT NULL DEFAULT 0,
                    DeletedBy TEXT,
                    DeletedAt TEXT,
                    DeleteReason TEXT,
                    FOREIGN KEY (GroupId) REFERENCES SMM_Groups(Id) ON DELETE CASCADE
                )");
            await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS IX_SMM_Cycles_Group ON SMM_Cycles(GroupId)");
            await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS IX_SMM_Cycles_Status ON SMM_Cycles(Status, IsDeleted)");
            await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS IX_SMM_Cycles_StartedAt ON SMM_Cycles(StartedAt)");

            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS SMM_CycleAlarms (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CycleId INTEGER NOT NULL,
                    AlarmCode TEXT NOT NULL,
                    AlarmText TEXT,
                    Severity INTEGER NOT NULL DEFAULT 0,
                    RaisedAt TEXT NOT NULL,
                    ClearedAt TEXT,
                    DurationInCycle_s REAL NOT NULL DEFAULT 0,
                    FOREIGN KEY (CycleId) REFERENCES SMM_Cycles(Id) ON DELETE CASCADE
                )");
            await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS IX_SMM_CycleAlarms_Cycle ON SMM_CycleAlarms(CycleId)");

            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS SMM_Readings (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    GroupId INTEGER NOT NULL,
                    VariableId INTEGER NOT NULL,
                    CycleId INTEGER,
                    Timestamp TEXT NOT NULL,
                    Value REAL,
                    StringValue TEXT,
                    Source TEXT NOT NULL DEFAULT 'Plc',
                    IsError INTEGER NOT NULL DEFAULT 0,
                    ErrorReason TEXT,
                    PlcVariable TEXT,
                    FOREIGN KEY (GroupId) REFERENCES SMM_Groups(Id) ON DELETE CASCADE,
                    FOREIGN KEY (VariableId) REFERENCES SMM_Variables(Id) ON DELETE CASCADE,
                    FOREIGN KEY (CycleId) REFERENCES SMM_Cycles(Id) ON DELETE SET NULL
                )");
            await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS IX_SMM_Readings_Group_Var_Time ON SMM_Readings(GroupId, VariableId, Timestamp)");
            await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS IX_SMM_Readings_Cycle ON SMM_Readings(CycleId)");

            // Migración idempotente: añadir StringValue a BDs creadas antes de DEC-027
            try { await context.Database.ExecuteSqlRawAsync(@"ALTER TABLE SMM_Readings ADD COLUMN StringValue TEXT"); }
            catch { /* columna ya existe */ }

            // Migración idempotente DEC-028: CaptureMode (Snapshot|Delta) para variables tipo contador
            try { await context.Database.ExecuteSqlRawAsync(@"ALTER TABLE SMM_Variables ADD COLUMN CaptureMode TEXT NOT NULL DEFAULT 'Snapshot'"); }
            catch { /* columna ya existe */ }

            // Migración idempotente: MaxValue (límite físico del counter HW para detectar wrap-around en CaptureMode=Delta)
            try { await context.Database.ExecuteSqlRawAsync(@"ALTER TABLE SMM_Variables ADD COLUMN MaxValue REAL"); }
            catch { /* columna ya existe */ }

            // Migración idempotente: SortOrder (preserva el orden del Excel para listados en HMI)
            try { await context.Database.ExecuteSqlRawAsync(@"ALTER TABLE SMM_Variables ADD COLUMN SortOrder INTEGER NOT NULL DEFAULT 0"); }
            catch { /* columna ya existe */ }

            // Migración idempotente: LayoutColor para Stats_Groups
            try { await context.Database.ExecuteSqlRawAsync(@"ALTER TABLE SMM_Groups ADD COLUMN LayoutColor TEXT"); }
            catch { /* columna ya existe */ }

            // Migración idempotente: ContinuousReadIntervalSec + ContinuousRetentionDays por grupo
            try { await context.Database.ExecuteSqlRawAsync(@"ALTER TABLE SMM_Groups ADD COLUMN ContinuousReadIntervalSec INTEGER"); }
            catch { /* columna ya existe */ }
            try { await context.Database.ExecuteSqlRawAsync(@"ALTER TABLE SMM_Groups ADD COLUMN ContinuousRetentionDays INTEGER"); }
            catch { /* columna ya existe */ }

            // Migración idempotente: RunningBitVar a nivel de GRUPO (gating global previo al per-variable)
            try { await context.Database.ExecuteSqlRawAsync(@"ALTER TABLE SMM_Groups ADD COLUMN RunningBitVar TEXT"); }
            catch { /* columna ya existe */ }

            // Migración idempotente: DonutMode (modo de agregación por defecto para UiType=DonutChart)
            try { await context.Database.ExecuteSqlRawAsync(@"ALTER TABLE SMM_Groups ADD COLUMN DonutMode TEXT NOT NULL DEFAULT 'LAST'"); }
            catch { /* columna ya existe */ }

            // Migración idempotente: ShowInMaintenance (oculta el grupo de Statistics, lo deja solo para vista Mantenimiento)
            try { await context.Database.ExecuteSqlRawAsync(@"ALTER TABLE SMM_Groups ADD COLUMN ShowInMaintenance INTEGER NOT NULL DEFAULT 0"); }
            catch { /* columna ya existe */ }

            // Migración idempotente: ImagePath para SMM_Elements (foto opcional del elemento)
            try { await context.Database.ExecuteSqlRawAsync(@"ALTER TABLE SMM_Elements ADD COLUMN ImagePath TEXT"); }
            catch { /* columna ya existe */ }

            // Migración idempotente: Model3DPath para SMM_Elements (modelo 3D opcional - GLB/GLTF)
            try { await context.Database.ExecuteSqlRawAsync(@"ALTER TABLE SMM_Elements ADD COLUMN Model3DPath TEXT"); }
            catch { /* columna ya existe */ }

            // ── Mantenimiento ──────────────────────────────────────────────
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS SMM_ElementLifecycles (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ElementId INTEGER NOT NULL,
                    StartedAt TEXT NOT NULL,
                    EndedAt TEXT,
                    AccumulatedValueAtStartJson TEXT NOT NULL DEFAULT '{{}}',
                    EndingInterventionId INTEGER,
                    FOREIGN KEY (ElementId) REFERENCES SMM_Elements(Id) ON DELETE CASCADE
                )");
            await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS IX_SMM_Lifecycles_Element ON SMM_ElementLifecycles(ElementId, EndedAt)");

            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS SMM_Interventions (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ElementId INTEGER NOT NULL,
                    ElementLifecycleId INTEGER NOT NULL,
                    TaskName TEXT NOT NULL,
                    InterventionType TEXT NOT NULL DEFAULT 'Maintenance',
                    PerformedAt TEXT NOT NULL,
                    PerformedByRole TEXT NOT NULL DEFAULT 'CustomerMaintainer',
                    PerformedByUser TEXT,
                    WorkOrderRef TEXT,
                    AccumulatedValueAtMaintenance REAL,
                    Notes TEXT,
                    TriggeredByPredictionId INTEGER,
                    CreatedBy TEXT NOT NULL DEFAULT 'system',
                    CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
                    LastModifiedBy TEXT,
                    LastModifiedAt TEXT,
                    RowVersion BLOB,
                    FOREIGN KEY (ElementId) REFERENCES SMM_Elements(Id) ON DELETE CASCADE,
                    FOREIGN KEY (ElementLifecycleId) REFERENCES SMM_ElementLifecycles(Id) ON DELETE CASCADE
                )");
            await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS IX_SMM_Interventions_Element ON SMM_Interventions(ElementId, PerformedAt)");
            await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS IX_SMM_Interventions_Lifecycle ON SMM_Interventions(ElementLifecycleId)");

            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS SMM_ConsumableUsage (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    InterventionId INTEGER NOT NULL,
                    PartSku TEXT NOT NULL,
                    PartDescription TEXT,
                    PartUnit TEXT NOT NULL DEFAULT 'ud',
                    Quantity REAL NOT NULL DEFAULT 1.0,
                    FOREIGN KEY (InterventionId) REFERENCES SMM_Interventions(Id) ON DELETE CASCADE
                )");
            await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS IX_SMM_ConsumableUsage_Int ON SMM_ConsumableUsage(InterventionId)");

            // ── IA (DEC-021/022) ───────────────────────────────────────────
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS SMM_DerivedErrorStats (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    GroupId INTEGER NOT NULL,
                    VarName TEXT NOT NULL,
                    TotalEvaluations INTEGER NOT NULL DEFAULT 0,
                    ErrorCount INTEGER NOT NULL DEFAULT 0,
                    LastUpdatedAt TEXT NOT NULL DEFAULT (datetime('now'))
                )");
            await context.Database.ExecuteSqlRawAsync(@"CREATE UNIQUE INDEX IF NOT EXISTS IX_SMM_DerivedErrorStats_Group_Var ON SMM_DerivedErrorStats(GroupId, VarName)");

            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS SMM_Predictions (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    PredictionType TEXT NOT NULL DEFAULT 'Anomaly',
                    RelatedElementId INTEGER,
                    RelatedVariableId INTEGER,
                    CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
                    ResolvedAt TEXT,
                    ResolvedByInterventionId INTEGER,
                    Severity INTEGER NOT NULL DEFAULT 0,
                    Description TEXT,
                    Confidence REAL
                )");
            await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS IX_SMM_Predictions_Element ON SMM_Predictions(RelatedElementId, ResolvedAt)");

            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS SMM_PredictionInterventions (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    PredictionId INTEGER NOT NULL,
                    InterventionId INTEGER NOT NULL
                )");
            await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS IX_SMM_PredInt_Pred ON SMM_PredictionInterventions(PredictionId)");
            await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS IX_SMM_PredInt_Int ON SMM_PredictionInterventions(InterventionId)");

            // ── UI / Audit ─────────────────────────────────────────────────
            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS SMM_UserDashboardLayouts (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER NOT NULL,
                    LayoutJson TEXT NOT NULL DEFAULT '{{}}',
                    UpdatedAt TEXT NOT NULL DEFAULT (datetime('now'))
                )");
            await context.Database.ExecuteSqlRawAsync(@"CREATE UNIQUE INDEX IF NOT EXISTS IX_SMM_UserLayouts_User ON SMM_UserDashboardLayouts(UserId)");

            await context.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS SMM_ExportLog (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    ExportedBy TEXT,
                    ExportedAt TEXT NOT NULL DEFAULT (datetime('now')),
                    Format TEXT NOT NULL DEFAULT 'XLSX',
                    RowCount INTEGER NOT NULL DEFAULT 0,
                    FilterJson TEXT
                )");
            await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS IX_SMM_ExportLog_At ON SMM_ExportLog(ExportedAt)");
        }
        catch (Exception ex)
        {
            // Idempotente para CREATE IF NOT EXISTS — pero logueamos para no
            // ocultar errores reales (ej. cambios de esquema, SQL inválido).
            Console.WriteLine($"[SMM] EnsureSmmTablesAsync error: {ex.Message}");
        }
    }
}

