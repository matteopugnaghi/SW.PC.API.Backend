using SW.PC.API.Backend.Services;
using SW.PC.API.Backend.Data;
using SW.PC.API.Backend.Hubs;
using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Models.Smm;
using SW.PC.API.Backend.Middleware;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using ClosedXML.Excel;

// Cuando se ejecuta como servicio de Windows, el working directory es System32.
// Solo forzar ContentRoot si no estamos en desarrollo (dotnet run ya lo gestiona).
string? serviceContentRoot = null;
if (Environment.ProcessPath is string exePath
    && !string.IsNullOrEmpty(exePath)
    && !File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json")))
{
    // El working dir no tiene appsettings.json → estamos corriendo como servicio
    serviceContentRoot = Path.GetDirectoryName(exePath);
}

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = serviceContentRoot  // null = usar default (desarrollo), ruta = servicio
});

// 🪟 Windows Service: permite ejecutar como servicio de Windows (sc.exe)
// En modo consola/desarrollo funciona igual que antes
builder.Host.UseWindowsService(o =>
{
    o.ServiceName = "AquafrischSupervisor";
});

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true; // 🔧 Acepta camelCase del frontend
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        // 📋 Serializar enums como strings para mejor legibilidad en Audit Logs
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();

// Configurar límites de upload para ficheros grandes
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 52_428_800; // 50 MB
});
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 52_428_800; // 50 MB
});

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { 
        Title = "SW.PC.API.Backend - SCADA/HMI System", 
        Version = "v1",
        Description = "API Backend for Industrial SCADA/HMI with TwinCAT3 PLC Communication"
    });
    // c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "SW.PC.API.Backend.xml"));
});

// Configure CORS for React frontend + SignalR
builder.Services.AddCors(options =>
{
    options.AddPolicy("ReactFrontend", policy =>
    {
        // Permitir orígenes HTTP y HTTPS de la red local
        policy.SetIsOriginAllowed(origin =>
              {
                  // Permitir localhost en cualquier puerto (HTTP y HTTPS)
                  if (origin.StartsWith("http://localhost:") || origin.StartsWith("https://localhost:"))
                      return true;
                  if (origin.StartsWith("http://127.0.0.1:") || origin.StartsWith("https://127.0.0.1:"))
                      return true;
                  
                  // Permitir IPs de la red local 192.168.x.x (HTTP y HTTPS)
                  if (origin.StartsWith("http://192.168.") || origin.StartsWith("https://192.168."))
                      return true;
                  
                  return false;
              })
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();  // Necesario para SignalR
    });
    
    // Política permisiva para recursos estáticos (imágenes, etc.) - sin credenciales
    options.AddPolicy("StaticResources", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Configure SignalR for real-time communication
// Timeouts generosos para entornos SCADA/kiosk donde el navegador puede quedar inactivo
builder.Services.AddSignalR(options =>
{
    // Tiempo que el servidor espera un mensaje del cliente antes de considerarlo desconectado
    // Default: 30s. Ampliado a 90s para evitar desconexiones cuando el navegador queda en segundo plano
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(90);
    
    // Intervalo de ping al cliente para mantener la conexión viva
    // Default: 15s. Debe ser < ClientTimeoutInterval/2 (recomendación Microsoft)
    options.KeepAliveInterval = TimeSpan.FromSeconds(30);
    
    // Habilitar logs detallados en desarrollo
    options.EnableDetailedErrors = true;
}).AddJsonProtocol(options =>
{
    // 🔧 Configurar SignalR para usar camelCase (igual que el resto de la API)
    options.PayloadSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.PayloadSerializerOptions.PropertyNameCaseInsensitive = true;
});

// Configure JWT Authentication
// Los valores deben coincidir con la configuración de Excel (Auth_JwtSecretKey, Auth_JwtIssuer, Auth_JwtAudience)
var jwtKey = builder.Configuration["Jwt:Key"] ?? "AquafrischSupervisorSecretKey2024!Min32Chars";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "AquafrischSupervisor";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "AquafrischClients";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
        
        // Configuración para SignalR con JWT
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// Configure PlcPolling settings
builder.Services.Configure<PlcPollingConfiguration>(
    builder.Configuration.GetSection("PlcPolling"));

// 🔔 Configure AlarmNotification settings (ADS push notifications for alarms)
builder.Services.Configure<AlarmNotificationConfiguration>(
    builder.Configuration.GetSection("AlarmNotification"));

// ═══════════════════════════════════════════════════════════════════════════════
// 🔐 PHASE 2: Authentication System - SQLite Database (EU CRA / CADRA Compliance)
// ═══════════════════════════════════════════════════════════════════════════════

// 📁 Multi-Project Database Support
// La base de datos ahora es POR PROYECTO:
// - default: Data/Aquafrisch.db (modo legacy)
// - proyecto-x: Projects/proyecto-x/data/project.db
// Usamos una factory para crear DbContext con la ruta correcta según el request

// Registrar DbContext con una configuración base (se sobrescribe en runtime)
var defaultDbPath = "Data/Aquafrisch.db";
builder.Services.AddDbContext<AquafrischDbContext>(options =>
    options.UseSqlite($"Data Source={defaultDbPath}"), 
    ServiceLifetime.Scoped);

// ⭐ NUEVO: Registrar DbContextFactory para servicios que lo necesitan (ej: RolePermissionsService)
builder.Services.AddDbContextFactory<AquafrischDbContext>(options =>
    options.UseSqlite($"Data Source={defaultDbPath}"),
    ServiceLifetime.Scoped);

// Registrar la factory de DbContext multi-proyecto
builder.Services.AddScoped<IProjectDbContextFactory, ProjectDbContextFactory>();

// Register Authentication Service
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IRecoveryCodeService, RecoveryCodeService>(); // 🔐 EU CRA - Recovery Codes Offline
builder.Services.AddScoped<IRolePermissionsService, RolePermissionsService>(); // 👥 Gestión de permisos por rol

// Register SCADA Services
builder.Services.AddSingleton<IProjectContextService, ProjectContextService>(); // 📁 Multi-Project Support (global)
builder.Services.AddScoped<IRequestProjectContext, RequestProjectContextService>(); // 📁 Multi-Project per-request (development multi-tenant)

// 📊 SMM (Statistics & Maintenance Module) — DEC-022 feature flag AquarIA Tier
builder.Services.Configure<SmmOptions>(builder.Configuration.GetSection(SmmOptions.SectionName));
builder.Services.AddScoped<IModelService, ModelService>();
builder.Services.AddScoped<IConfigurationService, ConfigurationService>();
builder.Services.AddSingleton<IExcelConfigService, ExcelConfigService>(); // ✅ SINGLETON para mantener caché
builder.Services.AddScoped<IPumpElementService, PumpElementService>();
builder.Services.AddSingleton<ITwinCATService, TwinCATService>();
builder.Services.AddSingleton<IMetricsService, MetricsService>(); // ✅ Servicio de métricas
builder.Services.AddSingleton<ISoftwareIntegrityService, SoftwareIntegrityService>(); // 🔐 Servicio de integridad
builder.Services.AddSingleton<IGitOperationsService, GitOperationsService>(); // 🔧 Git operations service
builder.Services.AddScoped<ISbomService, SbomService>(); // 📋 SBOM - EU CRA Compliance
builder.Services.AddScoped<IVulnerabilityService, VulnerabilityService>(); // 🛡️ Vulnerability Scanner - EU CRA
builder.Services.AddSingleton<IIpcInfoService, IpcInfoService>(); // 💻 IPC System Info
builder.Services.AddSingleton<IAuditLogService, AuditLogService>(); // 📋 Audit Log (Nivel 1) - EU CRA Compliance
builder.Services.AddSingleton<IOperationLogService, OperationLogService>(); // 📋 Operation Log (Nivel 2) - Acciones de operador
builder.Services.AddSingleton<ISystemLogService, SystemLogService>(); // 📋 System Log (Nivel 3) - In-memory diagnostic buffer
builder.Services.AddSingleton<INxLogFileService, NxLogFileService>(); // 📋 NxLog JSONL Export - TISSEO SOC PIVOT (TLS_M3_ALS_EXI_CYB_SYS_00516)
builder.Services.AddSingleton<IESIParserService, ESIParserService>(); // 🌐 ESI Parser - EtherCAT Slave Info files
builder.Services.AddSingleton<IEtherCATDiagnosticsService, EtherCATDiagnosticsService>(); // 🌐 EtherCAT Topology Diagnostics

// 🌐 OPC/UA Server - Industrial Communication Protocol
// Read OPC/UA enabled flag from Excel BEFORE registering the service
bool opcUaEnabledInExcel = false;
try
{
    // Determine Excel path from active-project.json (same logic as ProjectContextService)
    var contentRoot = builder.Environment.ContentRootPath;
    var activeProjectFile = Path.Combine(contentRoot, "active-project.json");
    string excelPath;
    
    if (File.Exists(activeProjectFile))
    {
        var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(activeProjectFile));
        var activeProject = json.RootElement.TryGetProperty("activeProject", out var prop) 
            ? prop.GetString() ?? "default" : "default";
        
        if (activeProject != "default")
        {
            // Multi-project mode: Projects/{id}/config/ProjectConfig.xlsm
            var configDir = Path.Combine(contentRoot, "Projects", activeProject, "config");
            excelPath = Directory.Exists(configDir) 
                ? (Directory.GetFiles(configDir, "*.xlsm").FirstOrDefault() 
                   ?? Directory.GetFiles(configDir, "*.xlsx").FirstOrDefault() 
                   ?? Path.Combine(configDir, "ProjectConfig.xlsm"))
                : Path.Combine(configDir, "ProjectConfig.xlsm");
        }
        else
        {
            // Legacy mode
            excelPath = Path.Combine(contentRoot, "ExcelConfigs", "ProjectConfig.xlsm");
        }
    }
    else
    {
        excelPath = Path.Combine(contentRoot, "ExcelConfigs", "ProjectConfig.xlsm");
    }
    
    if (File.Exists(excelPath))
    {
        using var stream = new FileStream(excelPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var workbook = new XLWorkbook(stream);
        var ws = workbook.Worksheets.FirstOrDefault(s => 
            s.Name.Equals("System Config", StringComparison.OrdinalIgnoreCase) ||
            s.Name.Equals("SystemConfig", StringComparison.OrdinalIgnoreCase));
        if (ws != null)
        {
            var lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;
            for (int row = 1; row <= lastRow; row++)
            {
                var key = ws.Cell(row, 1).GetString()?.Trim();
                // Match same keys as ExcelConfigService: "opcuaenabled", "opcua_enabled", "OpcUaEnabled" etc.
                var keyNorm = key?.ToLowerInvariant()?.Replace(" ", "") ?? "";
                if (keyNorm == "opcuaenabled" || keyNorm == "opcua_enabled")
                {
                    var val = ws.Cell(row, 2).GetString()?.Trim()?.ToLowerInvariant() ?? "";
                    opcUaEnabledInExcel = val == "true" || val == "1" 
                                        || val == "si" || val == "yes" || val == "on";
                    break;
                }
            }
        }
    }
    Console.WriteLine($"🌐 OPC/UA Enabled in Excel: {opcUaEnabledInExcel}");
}
catch (Exception ex)
{
    Console.WriteLine($"⚠️ Could not read OPC/UA config from Excel: {ex.Message}");
}

if (opcUaEnabledInExcel)
{
    builder.Services.AddSingleton<IOpcUaServerService, OpcUaServerService>();
    builder.Services.AddHostedService(sp => (OpcUaServerService)sp.GetRequiredService<IOpcUaServerService>());
    builder.Services.AddSingleton<IOpcUaCertificateService, OpcUaCertificateService>();
    builder.Services.AddSingleton<IOpcUaSftpService, OpcUaSftpService>();
    builder.Services.AddHostedService<OpcUaSftpSyncService>();
    builder.Services.AddHostedService<OpcUaCrlDownloadService>();
    Console.WriteLine("🌐 OPC/UA Server service REGISTERED (with certificate management + SFTP + CRL)");
}
else
{
    // Register a disabled stub so IOpcUaServerService can still be injected
    builder.Services.AddSingleton<IOpcUaServerService, DisabledOpcUaServerService>();
    // Certificate service available even when OPC/UA server is disabled (for pre-provisioning)
    builder.Services.AddSingleton<IOpcUaCertificateService, OpcUaCertificateService>();
    builder.Services.AddSingleton<IOpcUaSftpService, DisabledOpcUaSftpService>();
    Console.WriteLine("🌐 OPC/UA Server DISABLED — service not started");
}

// ═══════════════════════════════════════════════════════════════════════════════
// 💾 DATA MANAGEMENT: Sistema de Backup/Restore (EU CRA Anexo I, Parte I, 2f)
// ═══════════════════════════════════════════════════════════════════════════════
builder.Services.AddSingleton<IBackupCertificateService, BackupCertificateService>(); // 🔐 Certificados de backup
builder.Services.AddScoped<IBackupService, BackupService>(); // 💾 Servicio de backup/restore

// 📄 DMS: Sistema de Gestión Documental (EU CRA - Trazabilidad documental)
// ═══════════════════════════════════════════════════════════════════════════════
builder.Services.AddScoped<IDocumentService, DocumentService>(); // 📄 Servicio de gestión documental (solo lectura)

// Register HttpClient for Vulnerability Scanner
builder.Services.AddHttpClient("VulnerabilityScanner", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "SW.PC.SUPERVISOR/1.0 (EU-CRA-Compliance)");
});

// Register HttpClient for Audit Log External SOC
builder.Services.AddHttpClient("AuditExternal", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "AquafrischSupervisor/1.0 (EU-CRA-Audit)");
});

// Register Background Services
// builder.Services.AddHostedService<PlcNotificationService>(); // Servicio legacy - reemplazado por PlcPollingService
builder.Services.AddSingleton<PlcPollingService>(); // ✅ Registrar como Singleton para poder acceder desde controllers
builder.Services.AddHostedService(sp => sp.GetRequiredService<PlcPollingService>()); // ✅ También como HostedService
builder.Services.AddSingleton<AlarmNotificationService>(); // 🔔 ADS Notifications para alarmas (más eficiente que polling)
builder.Services.AddHostedService(sp => sp.GetRequiredService<AlarmNotificationService>()); // 🔔 También como HostedService
builder.Services.AddHostedService<IntegrityVerificationService>(); // 🔐 Verificación periódica de integridad (cada 2 min)
builder.Services.AddHostedService<BackupSchedulerService>(); // 💾 Backup automático programado (DATA MANAGEMENT)
builder.Services.AddHostedService<WashRecipeAutoLoadService>(); // 🚿 Auto-carga de recetas de lavado desde PLC
builder.Services.AddHostedService<TrainRecipeAutoLoadService>(); // 🚆 Auto-carga de tipos de tren desde PLC
builder.Services.AddHostedService<ClientConnectionTrackerService>(); // ⏱️ Tracker de clientes conectados + contador al PLC
builder.Services.AddHostedService<TmeSensorService>(); // 🌡️ Sondas de temperatura Papouch TME (HTTP → ADS)

// Add logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
    logging.SetMinimumLevel(LogLevel.Information);
});

var app = builder.Build();

// ═══════════════════════════════════════════════════════════════════════════════
// � L3 SYSTEM LOGS: Connect ILogger pipeline to in-memory buffer
// ═══════════════════════════════════════════════════════════════════════════════
var systemLogService = app.Services.GetRequiredService<ISystemLogService>();
var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
loggerFactory.AddProvider(new SystemLogBufferProvider(systemLogService));

// ═══════════════════════════════════════════════════════════════════════════════
// �📁 MULTI-PROJECT: Initialize Project Context Service
// ═══════════════════════════════════════════════════════════════════════════════
{
    var projectContext = app.Services.GetRequiredService<IProjectContextService>();
    var excelConfigService = app.Services.GetRequiredService<IExcelConfigService>();
    
    // Configurar ExcelConfigService con el contexto de proyecto
    excelConfigService.SetProjectContext(projectContext);

    // � EU CRA - Conectar audit log a ExcelConfigService (setter pattern evita circular DI).
    // Esto habilita que los warnings de schema del Excel se persistan en L1 (audit log).
    try
    {
        var auditLogServiceForExcel = app.Services.GetRequiredService<IAuditLogService>();
        excelConfigService.SetAuditLogService(auditLogServiceForExcel);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "⚠️ Could not wire AuditLogService into ExcelConfigService (L1 schema logging disabled)");
    }

    // �🔄 Suscribir ExcelConfigService al evento de cambio de proyecto
    // para que recargue cache y rutas cuando se cambia de proyecto
    projectContext.OnProjectChanged += (_) =>
    {
        excelConfigService.SetProjectContext(projectContext);
        app.Logger.LogInformation("🔄 ExcelConfigService: Cache invalidado y rutas actualizadas por cambio de proyecto");
    };
    
    app.Logger.LogInformation("═══════════════════════════════════════════════════════════════");
    app.Logger.LogInformation("📁 MULTI-PROJECT SYSTEM INITIALIZED");
    app.Logger.LogInformation("   Active Project: {ProjectId}", projectContext.ActiveProjectId);
    app.Logger.LogInformation("   Mode: {Mode}", projectContext.IsMultiProjectMode ? "Multi-Project" : "Legacy");
    if (projectContext.IsMultiProjectMode)
    {
        app.Logger.LogInformation("   Config Path: {Path}", projectContext.ConfigPath);
        app.Logger.LogInformation("   Models Path: {Path}", projectContext.ModelsPath);
        app.Logger.LogInformation("   Data Path: {Path}", projectContext.DataPath);
        app.Logger.LogInformation("   Excel Path: {Path}", projectContext.ExcelConfigPath);
    }
    app.Logger.LogInformation("═══════════════════════════════════════════════════════════════");
}

// 🔐 Conectar servicio de integridad con métricas y TwinCAT
{
    var metricsService = app.Services.GetRequiredService<IMetricsService>();
    var integrityService = app.Services.GetRequiredService<ISoftwareIntegrityService>();
    var twinCatService = app.Services.GetRequiredService<ITwinCATService>();
    var excelConfigService = app.Services.GetRequiredService<IExcelConfigService>();
    var projectContext = app.Services.GetRequiredService<IProjectContextService>();
    
    metricsService.SetSoftwareIntegrityService(integrityService);
    
    // NOTA: La info de TwinCAT se actualiza después de ConnectAsync() más abajo
    
    // 📋 Cargar rutas Git desde Excel (System Config filas A20-A22)
    try
    {
        // Usar ruta del proyecto activo si está en modo multi-proyecto
        string? excelPath = null;
        
        if (projectContext.IsMultiProjectMode)
        {
            excelPath = projectContext.ExcelConfigPath;
            if (!File.Exists(excelPath))
            {
                app.Logger.LogWarning("⚠️ Project Excel not found at: {Path}", excelPath);
                excelPath = null;
            }
        }
        
        // Fallback: buscar Excel en ubicaciones legacy
        if (excelPath == null)
        {
            var possiblePaths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ExcelConfigs", "ProjectConfig.xlsm"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "ExcelConfigs", "ProjectConfig.xlsm"),
                @"C:\Users\mpugnaghi.AQUAFRISCH\Documents\Work_In_Process\_Web\AI test\SW.PC.API.Backend_\ExcelConfigs\ProjectConfig.xlsm"
            };
            
            excelPath = possiblePaths.FirstOrDefault(File.Exists);
        }
        
        if (excelPath != null)
        {
            app.Logger.LogInformation("📋 Excel found at: {Path}", excelPath);
            var systemConfig = excelConfigService.LoadSystemConfigurationAsync(excelPath).GetAwaiter().GetResult();
            
            // 🔍 Debug: mostrar qué rutas se leyeron del Excel
            app.Logger.LogInformation("📋 Excel Git paths read:");
            app.Logger.LogInformation("   Backend: '{Path}'", systemConfig.GitRepoBackend ?? "(empty)");
            app.Logger.LogInformation("   Frontend: '{Path}'", systemConfig.GitRepoFrontend ?? "(empty)");
            app.Logger.LogInformation("   TwinCAT: '{Path}'", systemConfig.GitRepoTwinCatPlc ?? "(empty)");
            
            integrityService.ConfigureGitPaths(
                systemConfig.GitRepoBackend,
                systemConfig.GitRepoFrontend,
                systemConfig.GitRepoTwinCatPlc
            );
            
            app.Logger.LogInformation("🔐 Git paths configured from Excel");
        }
        else
        {
            app.Logger.LogWarning("⚠️ Excel not found in any location, using default Git paths");
        }
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Could not load Git paths from Excel, using defaults");
    }
    
    app.Logger.LogInformation("🔐 Software Integrity Service initialized with Git-based versioning");
}

// ═══════════════════════════════════════════════════════════════════════════════
// 🔐 PHASE 2: Initialize Authentication System (SQLite + Default Admin)
// ═══════════════════════════════════════════════════════════════════════════════
using (var scope = app.Services.CreateScope())
{
    try
    {
        var authService = scope.ServiceProvider.GetRequiredService<IAuthenticationService>();
        await authService.InitializeAsync();
        app.Logger.LogInformation("🔐 Authentication system initialized successfully");
        
        // Asegurar todas las tablas de la base de datos (incluyendo WashTypes)
        var dbContext = scope.ServiceProvider.GetRequiredService<AquafrischDbContext>();
        await AquafrischDbContextFactory.EnsureDatabaseCreatedAsync(dbContext);
        app.Logger.LogInformation("📦 Database tables ensured (MachineSettings, OperationLogs, WashTypes)");
        
        // ✅ Actualizar estado de Database a conectado
        var metricsForDb = scope.ServiceProvider.GetRequiredService<IMetricsService>();
        metricsForDb.SetDatabaseStatus(true, true, "SQLite conectado");
        app.Logger.LogInformation("✅ SQLite database connected and ready");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "❌ Error initializing authentication system");
        
        // ❌ Actualizar estado de Database a error
        var metricsForDb = scope.ServiceProvider.GetRequiredService<IMetricsService>();
        metricsForDb.SetDatabaseStatus(true, false, $"Error: {ex.Message}");
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "SW.PC.API.Backend v1");
        // c.RoutePrefix = string.Empty; // Set Swagger UI at the root
    });
}

// HTTPS redirection: solo en producción para forzar canal cifrado
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Log paths for debugging
var webRootPath = app.Environment.WebRootPath;
var contentRootPath = app.Environment.ContentRootPath;
app.Logger.LogInformation("WebRootPath: {WebRootPath}", webRootPath);
app.Logger.LogInformation("ContentRootPath: {ContentRootPath}", contentRootPath);

// Verify wwwroot exists
var wwwrootExists = Directory.Exists(webRootPath);
app.Logger.LogInformation("wwwroot directory exists: {Exists}", wwwrootExists);
if (wwwrootExists)
{
    var modelsPath = Path.Combine(webRootPath, "models");
    var modelsExists = Directory.Exists(modelsPath);
    app.Logger.LogInformation("models directory exists: {Exists} at {Path}", modelsExists, modelsPath);
    if (modelsExists)
    {
        var files = Directory.GetFiles(modelsPath, "*.glb");
        app.Logger.LogInformation("GLB files found: {Count} - {Files}", files.Length, string.Join(", ", files.Select(Path.GetFileName)));
    }
}

// ✅ Inicializar y conectar TwinCAT Service al inicio
using (var scope = app.Services.CreateScope())
{
    var twinCATService = scope.ServiceProvider.GetRequiredService<ITwinCATService>();
    var metricsService = scope.ServiceProvider.GetRequiredService<IMetricsService>();
    var excelConfigService = scope.ServiceProvider.GetRequiredService<IExcelConfigService>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    try
    {
        // Leer configuración del sistema desde Excel
        var systemConfig = await excelConfigService.LoadSystemConfigurationAsync("ProjectConfig.xlsm");
        
        // Inicializar estado de SignalR
        metricsService.SetSignalRStatus(systemConfig.EnableSignalR, false, "Esperando conexiones...");
        
        // Nota: El estado de Database se actualiza después de inicializar AuthService
        // para reflejar el estado real de conexión SQLite
        
        // 🔐 Actualizar estado de Database en el servicio de integridad (desde Excel)
        var integrityServiceForDb = app.Services.GetRequiredService<ISoftwareIntegrityService>();
        integrityServiceForDb.UpdateDatabaseStatus(
            systemConfig.EnableDatabase, 
            false, // No está conectada aún
            systemConfig.EnableDatabase ? "Configured from Excel" : "Disabled in Excel configuration"
        );
        
        // Establecer si usa PLC simulado (desde Excel)
        metricsService.SetUseSimulatedPlc(systemConfig.UseSimulatedPlc);
        
        var connected = await twinCATService.ConnectAsync();
        if (connected)
        {
            logger.LogInformation("✅ TwinCAT Service connected successfully");
            
            // 🔐 Actualizar info de TwinCAT en el servicio de integridad DESPUÉS de conectar
            var integrityService = app.Services.GetRequiredService<ISoftwareIntegrityService>();
            var twinCatInfo = twinCATService.GetVersionInfo();
            
            // Obtener Task Cycle Time real del PLC
            var taskCycleTimeMs = await twinCATService.GetTaskCycleTimeAsync();
            
            integrityService.UpdateTwinCATRuntimeInfo(
                twinCatInfo.RuntimeVersion,
                twinCatInfo.AdsVersion,
                twinCatInfo.IsConnected,
                twinCatInfo.IsSimulated,
                taskCycleTimeMs
            );
            logger.LogInformation("🔐 TwinCAT integrity info updated: {Version} (Connected={Connected}, Simulated={Simulated}, CycleTime={CycleTime}ms)",
                twinCatInfo.RuntimeVersion, twinCatInfo.IsConnected, twinCatInfo.IsSimulated, taskCycleTimeMs);
        }
        else
        {
            logger.LogWarning("⚠️ TwinCAT Service failed to connect - updating status as DISCONNECTED");
            
            // 🔐 Actualizar estado como DESCONECTADO (no simulado)
            var integrityService = app.Services.GetRequiredService<ISoftwareIntegrityService>();
            var twinCatInfo = twinCATService.GetVersionInfo();
            integrityService.UpdateTwinCATRuntimeInfo(
                "Connection Failed",
                twinCatInfo.AdsVersion,
                false,  // isConnected = false
                false,  // isSimulated = false (NO es simulado, es que falló la conexión)
                0
            );
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "❌ Error initializing TwinCAT Service");
    }
}

// ⭐ CORS MIDDLEWARE PARA IMÁGENES - ANTES de cualquier otro middleware
// Maneja CORS manualmente para /api/machine-settings/image (necesario para canvas/WebGL)
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api/machine-settings/image"))
    {
        var logger = context.RequestServices.GetService<ILogger<Program>>();
        logger?.LogInformation("🖼️ CORS Middleware: Procesando {Method} {Path} desde Origin: {Origin}", 
            context.Request.Method, 
            context.Request.Path,
            context.Request.Headers.Origin.FirstOrDefault() ?? "sin-origin");
        
        // Agregar headers CORS para TODAS las respuestas de este endpoint
        context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
        context.Response.Headers.Append("Access-Control-Allow-Methods", "GET, OPTIONS");
        context.Response.Headers.Append("Access-Control-Allow-Headers", "*");
        
        // Si es preflight OPTIONS, responder inmediatamente
        if (context.Request.Method == "OPTIONS")
        {
            logger?.LogInformation("🖼️ CORS Middleware: Respondiendo a preflight OPTIONS");
            context.Response.Headers.Append("Access-Control-Max-Age", "86400");
            context.Response.StatusCode = 204; // No Content
            return;
        }
    }
    await next();
});

// Enable CORS FIRST - ANTES de cualquier otro middleware
app.UseCors("ReactFrontend");

// 📁 MULTI-TENANT: Project Context Middleware (solo activo en Development)
// Permite seleccionar proyecto via header X-Project-Id o query param ?projectId=
app.UseProjectContext();

// ═══════════════════════════════════════════════════════════════════════════════
// 🌐 SERVE REACT SPA - Default Files & Static Files
// ═══════════════════════════════════════════════════════════════════════════════

// UseDefaultFiles MUST come before UseStaticFiles
// This makes index.html the default document for "/"
app.UseDefaultFiles(new DefaultFilesOptions
{
    DefaultFileNames = new List<string> { "index.html" }
});

// Configure MIME types for 3D models
var provider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
provider.Mappings[".glb"] = "model/gltf-binary";
provider.Mappings[".gltf"] = "model/gltf+json";
provider.Mappings[".obj"] = "application/object";
provider.Mappings[".mtl"] = "text/plain";
provider.Mappings[".stl"] = "application/sla";

// 📁 MULTI-PROJECT: Serve /models from project folder FIRST (before general wwwroot)
// This MUST come before the general UseStaticFiles() to take precedence
{
    var projectContextForModels = app.Services.GetRequiredService<IProjectContextService>();
    var modelsPhysicalPath = projectContextForModels.ModelsPath;
    
    // Asegurar que existe la carpeta de modelos
    if (!Directory.Exists(modelsPhysicalPath))
    {
        Directory.CreateDirectory(modelsPhysicalPath);
        app.Logger.LogInformation("📁 Created models directory: {Path}", modelsPhysicalPath);
    }
    
    app.Logger.LogInformation("📁 Serving models from: {Path}", modelsPhysicalPath);
    
    // Serve /models directory explicitly with MIME types
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(modelsPhysicalPath),
        RequestPath = "/models",
        ContentTypeProvider = provider,
        ServeUnknownFileTypes = false,
        HttpsCompression = Microsoft.AspNetCore.Http.Features.HttpsCompressionMode.Compress,
        OnPrepareResponse = ctx =>
        {
            app.Logger.LogInformation("✅ Serving model file: {Path} with ContentType: {ContentType}", 
                ctx.File.PhysicalPath, ctx.Context.Response.ContentType);
            ctx.Context.Response.Headers["Access-Control-Allow-Origin"] = "*";
            ctx.Context.Response.Headers["Access-Control-Allow-Methods"] = "GET, HEAD, OPTIONS";
            ctx.Context.Response.Headers["Access-Control-Allow-Headers"] = "*";
            
            // 🔄 CACHE-BUSTING: Deshabilitar caché para modelos 3D (GLB/GLTF)
            // Esto asegura que los cambios en los archivos se reflejen inmediatamente
            var fileName = ctx.File.Name.ToLower();
            if (fileName.EndsWith(".glb") || fileName.EndsWith(".gltf") || 
                fileName.EndsWith(".obj") || fileName.EndsWith(".mtl"))
            {
                ctx.Context.Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
                ctx.Context.Response.Headers["Pragma"] = "no-cache";
                ctx.Context.Response.Headers["Expires"] = "0";
                // Añadir ETag basado en la fecha de modificación del archivo
                ctx.Context.Response.Headers["ETag"] = $"\"{ctx.File.LastModified.Ticks}\"";
            }
        }
    });
}

// Enable static files for wwwroot (frontend, etc.) AFTER /models
// This serves everything else from wwwroot, but /models requests are already handled above
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = provider,
    ServeUnknownFileTypes = false,
    HttpsCompression = Microsoft.AspNetCore.Http.Features.HttpsCompressionMode.Compress,
    OnPrepareResponse = ctx =>
    {
        // Don't log model files here - they should be handled by the project-specific middleware above
        if (!ctx.File.PhysicalPath.Contains("\\models\\") && !ctx.File.PhysicalPath.Contains("/models/"))
        {
            app.Logger.LogDebug("📄 Serving static file from wwwroot: {Path}", ctx.File.PhysicalPath);
        }
        // Add CORS headers to static files
        ctx.Context.Response.Headers["Access-Control-Allow-Origin"] = "*";
        ctx.Context.Response.Headers["Access-Control-Allow-Methods"] = "GET, HEAD, OPTIONS";
        ctx.Context.Response.Headers["Access-Control-Allow-Headers"] = "*";
    }
});

// Enable routing AFTER static files
app.UseRouting();

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// Map Controllers
app.MapControllers();

// Map SignalR Hub
app.MapHub<ScadaHub>("/hubs/scada");

// ═══════════════════════════════════════════════════════════════════════════════
// 🌐 SPA FALLBACK - React Router Support
// ═══════════════════════════════════════════════════════════════════════════════
// For React SPA: any route not matching API or static files falls back to index.html
// This allows React Router to handle client-side routing
app.MapFallbackToFile("index.html");

// ═══════════════════════════════════════════════════════════════════════════════
// 📋 AUDIT LOG: System Startup & Shutdown Events (EU CRA / CADRA Compliance)
// 🌐 Se registra en TODOS los proyectos (evento global)
// ═══════════════════════════════════════════════════════════════════════════════
{
    var auditLogService = app.Services.GetRequiredService<IAuditLogService>();
    
    // 🟢 Log System Startup - 🌐 A todos los proyectos
    await auditLogService.LogToAllProjectsAsync(
        AuditCategory.System,
        AuditAction.SystemStart,
        AuditResult.Success,
        $"Sistema iniciado - Versión Backend, Environment: {app.Environment.EnvironmentName}",
        null, "System");
    
    app.Logger.LogInformation("📋 System startup logged to ALL projects");
    
    // 🔴 Register System Shutdown Event - 🌐 A todos los proyectos
    app.Lifetime.ApplicationStopping.Register(() =>
    {
        try
        {
            auditLogService.LogToAllProjectsAsync(
                AuditCategory.System,
                AuditAction.SystemStop,
                AuditResult.Success,
                "Sistema detenido normalmente",
                null, "System").GetAwaiter().GetResult();
            
            app.Logger.LogInformation("📋 System shutdown logged to ALL projects");
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "Failed to log system shutdown to audit");
        }
    });
}

app.Run();
