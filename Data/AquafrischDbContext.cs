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
        
        // Asegurar que la tabla OperationLogs existe (EnsureCreated no actualiza tablas existentes)
        await EnsureOperationLogsTableAsync(context);
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
}
