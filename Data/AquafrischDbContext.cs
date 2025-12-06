// ============================================================================
// AquafrischDbContext.cs - Contexto de Base de Datos SQLite
// ============================================================================
// Entity Framework Core DbContext para la base de datos principal del sistema
// Incluye: Usuarios, Roles, Sesiones, Intentos de Login
// ============================================================================

using Microsoft.EntityFrameworkCore;
using SW.PC.API.Backend.Models;

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
    public DbSet<UserRole> UserRoles { get; set; } = null!;
    
    /// <summary>Sesiones activas</summary>
    public DbSet<UserSession> UserSessions { get; set; } = null!;
    
    /// <summary>Historial de intentos de login</summary>
    public DbSet<LoginAttempt> LoginAttempts { get; set; } = null!;

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
        modelBuilder.Entity<UserRole>(entity =>
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
        // Seed Data - Roles del Sistema
        // ============================================
        SeedRoles(modelBuilder);
    }

    /// <summary>
    /// Seed inicial de roles del sistema
    /// </summary>
    private static void SeedRoles(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Role>().HasData(
            new Role
            {
                Id = 1,
                Name = "Administrator",
                Description = "Administrador del sistema - Acceso total a todas las funciones incluyendo gestión de usuarios y seguridad",
                SystemRole = SystemRole.Administrator,
                IsSystemRole = true,
                PermissionsJson = """
                {
                    "users": ["create", "read", "update", "delete"],
                    "roles": ["create", "read", "update", "delete"],
                    "audit": ["read", "export"],
                    "config": ["read", "update"],
                    "plc": ["read", "write", "config"],
                    "alarms": ["read", "acknowledge", "config"],
                    "recipes": ["create", "read", "update", "delete", "execute"],
                    "reports": ["read", "create", "export"],
                    "security": ["read", "update"],
                    "backup": ["create", "restore"]
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
    }
}
