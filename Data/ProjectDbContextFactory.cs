// ============================================================================
// ProjectDbContextFactory.cs - Factory para DbContext Multi-Proyecto
// ============================================================================
// Crea instancias de AquafrischDbContext con la ruta de base de datos
// correspondiente al proyecto activo del request.
// ============================================================================

using Microsoft.EntityFrameworkCore;
using SW.PC.API.Backend.Services;

namespace SW.PC.API.Backend.Data;

/// <summary>
/// Interface para la factory de DbContext que soporta multi-proyecto
/// </summary>
public interface IProjectDbContextFactory
{
    /// <summary>
    /// Crea un DbContext usando la base de datos del proyecto del request actual
    /// </summary>
    AquafrischDbContext CreateDbContext();
    
    /// <summary>
    /// Crea un DbContext para un proyecto específico
    /// </summary>
    AquafrischDbContext CreateDbContext(string projectId);
    
    /// <summary>
    /// Obtiene la ruta de la base de datos del proyecto actual
    /// </summary>
    string GetCurrentDatabasePath();
    
    /// <summary>
    /// Asegura que la base de datos existe y está migrada
    /// </summary>
    Task EnsureDatabaseExistsAsync(string? projectId = null);
}

/// <summary>
/// Factory que crea DbContext con la ruta de base de datos del proyecto activo
/// </summary>
public class ProjectDbContextFactory : IProjectDbContextFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IProjectContextService _globalContext;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<ProjectDbContextFactory> _logger;
    
    // Ruta legacy para modo default
    private readonly string _legacyDbPath;
    private readonly string _projectsRootPath;

    public ProjectDbContextFactory(
        IServiceProvider serviceProvider,
        IProjectContextService globalContext,
        IWebHostEnvironment environment,
        ILogger<ProjectDbContextFactory> logger)
    {
        _serviceProvider = serviceProvider;
        _globalContext = globalContext;
        _environment = environment;
        _logger = logger;
        
        _legacyDbPath = Path.Combine(environment.ContentRootPath, "Data", "Aquafrisch.db");
        _projectsRootPath = Path.Combine(environment.ContentRootPath, "Projects");
    }

    /// <summary>
    /// Crea un DbContext usando la base de datos del proyecto del request actual
    /// </summary>
    public AquafrischDbContext CreateDbContext()
    {
        // Intentar obtener el contexto de request si estamos en un scope HTTP
        var requestContext = _serviceProvider.GetService<IRequestProjectContext>();
        
        string dbPath;
        string projectId;
        
        if (requestContext != null)
        {
            dbPath = requestContext.DatabasePath;
            projectId = requestContext.ProjectId;
            _logger.LogInformation("📁 DbContextFactory: Usando DB del REQUEST context - Proyecto: {ProjectId}, Path: {Path}", projectId, dbPath);
        }
        else
        {
            // Fallback al contexto global (para background services, etc.)
            dbPath = _globalContext.DatabasePath;
            projectId = _globalContext.ActiveProjectId;
            _logger.LogInformation("📁 DbContextFactory: Usando DB del GLOBAL context - Proyecto: {ProjectId}, Path: {Path}", projectId, dbPath);
        }
        
        return CreateDbContextForPath(dbPath);
    }

    /// <summary>
    /// Crea un DbContext para un proyecto específico
    /// </summary>
    public AquafrischDbContext CreateDbContext(string projectId)
    {
        var dbPath = GetDatabasePathForProject(projectId);
        _logger.LogDebug("📁 DbContextFactory: Creando DbContext para proyecto {ProjectId}: {Path}", projectId, dbPath);
        return CreateDbContextForPath(dbPath);
    }

    /// <summary>
    /// Obtiene la ruta de la base de datos del proyecto actual
    /// </summary>
    public string GetCurrentDatabasePath()
    {
        var requestContext = _serviceProvider.GetService<IRequestProjectContext>();
        return requestContext?.DatabasePath ?? _globalContext.DatabasePath;
    }

    /// <summary>
    /// Asegura que la base de datos existe y está migrada
    /// </summary>
    public async Task EnsureDatabaseExistsAsync(string? projectId = null)
    {
        string dbPath;
        
        if (string.IsNullOrEmpty(projectId))
        {
            dbPath = GetCurrentDatabasePath();
        }
        else
        {
            dbPath = GetDatabasePathForProject(projectId);
        }
        
        // Asegurar que el directorio existe
        var dbDir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dbDir) && !Directory.Exists(dbDir))
        {
            Directory.CreateDirectory(dbDir);
            _logger.LogInformation("📁 Creado directorio de datos: {Path}", dbDir);
        }
        
        // Crear DbContext y asegurar que la base de datos existe
        using var context = CreateDbContextForPath(dbPath);
        
        // EnsureCreated crea la base de datos si no existe
        var created = await context.Database.EnsureCreatedAsync();
        
        if (created)
        {
            _logger.LogInformation("✅ Base de datos creada: {Path}", dbPath);
            
            // Seed de datos iniciales (roles por defecto, usuario admin, etc.)
            await SeedInitialDataAsync(context);
        }
        else
        {
            _logger.LogDebug("📁 Base de datos ya existe: {Path}", dbPath);
        }
        
        // 🔧 Siempre asegurar que la tabla OperationLogs existe (para bases de datos existentes)
        await EnsureOperationLogsTableAsync(context);
    }
    
    /// <summary>
    /// Crear tabla OperationLogs si no existe (para bases de datos existentes que no la tienen)
    /// </summary>
    private async Task EnsureOperationLogsTableAsync(AquafrischDbContext context)
    {
        try
        {
            // Crear tabla si no existe
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
            
            // Añadir columna ActionKey si no existe (migración para tablas antiguas)
            try
            {
                await context.Database.ExecuteSqlRawAsync(@"ALTER TABLE OperationLogs ADD COLUMN ActionKey TEXT");
            }
            catch { /* Columna ya existe */ }
            
            // Crear índices
            await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS IX_OperationLogs_Timestamp ON OperationLogs(Timestamp)");
            await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS IX_OperationLogs_Category ON OperationLogs(Category)");
            await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS IX_OperationLogs_Action ON OperationLogs(Action)");
            await context.Database.ExecuteSqlRawAsync(@"CREATE INDEX IF NOT EXISTS IX_OperationLogs_IsAcknowledged ON OperationLogs(IsAcknowledged)");
            
            _logger.LogInformation("✅ Tabla OperationLogs verificada/creada correctamente");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Error verificando tabla OperationLogs (puede que ya exista)");
        }
    }

    private AquafrischDbContext CreateDbContextForPath(string dbPath)
    {
        // Asegurar que el directorio existe
        var dbDir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dbDir) && !Directory.Exists(dbDir))
        {
            Directory.CreateDirectory(dbDir);
        }
        
        var optionsBuilder = new DbContextOptionsBuilder<AquafrischDbContext>();
        optionsBuilder.UseSqlite($"Data Source={dbPath}");
        
        return new AquafrischDbContext(optionsBuilder.Options);
    }

    private string GetDatabasePathForProject(string projectId)
    {
        if (string.IsNullOrEmpty(projectId) || projectId.Equals("default", StringComparison.OrdinalIgnoreCase))
        {
            return _legacyDbPath;
        }
        
        return Path.Combine(_projectsRootPath, projectId, "data", "project.db");
    }

    private async Task SeedInitialDataAsync(AquafrischDbContext context)
    {
        _logger.LogInformation("🌱 Seeding initial data...");
        
        // Crear roles por defecto si no existen
        if (!context.Roles.Any())
        {
            var roles = new[]
            {
                new Models.Role 
                { 
                    Id = 1, 
                    Name = "SuperAdmin", 
                    SystemRole = Models.SystemRole.SuperAdmin,
                    Description = "Super Administrator - Manufacturer access only",
                    IsSystemRole = true
                },
                new Models.Role 
                { 
                    Id = 2, 
                    Name = "Administrator", 
                    SystemRole = Models.SystemRole.Administrator,
                    Description = "Client Administrator - Full system access",
                    IsSystemRole = true
                },
                new Models.Role 
                { 
                    Id = 3, 
                    Name = "Operator", 
                    SystemRole = Models.SystemRole.Operator,
                    Description = "Standard operator access",
                    IsSystemRole = true
                },
                new Models.Role 
                { 
                    Id = 4, 
                    Name = "Maintenance", 
                    SystemRole = Models.SystemRole.Maintenance,
                    Description = "Technical maintenance access",
                    IsSystemRole = true
                },
                new Models.Role 
                { 
                    Id = 5, 
                    Name = "Viewer", 
                    SystemRole = Models.SystemRole.Viewer,
                    Description = "Read-only access",
                    IsSystemRole = true
                }
            };
            
            context.Roles.AddRange(roles);
            await context.SaveChangesAsync();
            _logger.LogInformation("✅ Created {Count} default roles", roles.Length);
        }
        
        // Crear usuario admin por defecto si no existe ningún usuario
        if (!context.Users.Any())
        {
            // Hash de "admin" usando BCrypt (mismo que AuthenticationService)
            var adminPasswordHash = BCrypt.Net.BCrypt.HashPassword("admin", BCrypt.Net.BCrypt.GenerateSalt(12));
            
            var adminUser = new Models.User
            {
                Id = 1,
                Username = "admin",
                PasswordHash = adminPasswordHash,
                FullName = "System Administrator",
                Email = "admin@aquafrisch.local",
                Status = Models.UserStatus.Active,
                IsActiveDirectoryUser = false,
                FailedLoginAttempts = 0,
                CreatedAt = DateTime.Now,
                CreatedBy = "System",
                MustChangePassword = true // Forzar cambio en primer login
            };
            
            context.Users.Add(adminUser);
            await context.SaveChangesAsync();
            
            // Asignar rol de Administrator al usuario admin
            var adminRole = context.Roles.FirstOrDefault(r => r.SystemRole == Models.SystemRole.Administrator);
            if (adminRole != null)
            {
                context.UserRoles.Add(new Models.UserRole
                {
                    UserId = adminUser.Id,
                    RoleId = adminRole.Id,
                    AssignedAt = DateTime.Now,
                    AssignedBy = "System"
                });
                await context.SaveChangesAsync();
            }
            
            _logger.LogInformation("✅ Created default admin user (password: admin)");
        }
    }
}
