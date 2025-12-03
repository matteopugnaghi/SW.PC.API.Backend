using SW.PC.API.Backend.Services;
using SW.PC.API.Backend.Data;
using SW.PC.API.Backend.Hubs;
using SW.PC.API.Backend.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { 
        Title = "SW.PC.API.Backend - SCADA/HMI System", 
        Version = "v1",
        Description = "API Backend for Industrial SCADA/HMI with TwinCAT3 PLC Communication"
    });
    // c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "SW.PC.API.Backend.xml"));
});

// Configure Database (SQL Server)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Server=(localdb)\\mssqllocaldb;Database=ScadaDB;Trusted_Connection=true;MultipleActiveResultSets=true";

builder.Services.AddDbContext<ScadaDbContext>(options =>
    options.UseSqlServer(connectionString));

// Configure CORS for React frontend + SignalR
builder.Services.AddCors(options =>
{
    options.AddPolicy("ReactFrontend", policy =>
    {
        // En desarrollo, permitir cualquier origen de la red local
        policy.SetIsOriginAllowed(origin =>
              {
                  // Permitir localhost en cualquier puerto
                  if (origin.StartsWith("http://localhost:") || origin.StartsWith("http://127.0.0.1:"))
                      return true;
                  
                  // Permitir IPs de la red local 192.168.x.x
                  if (origin.StartsWith("http://192.168."))
                      return true;
                  
                  return false;
              })
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();  // Necesario para SignalR
    });
});

// Configure SignalR for real-time communication
builder.Services.AddSignalR();

// Configure JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? "YourSuperSecretKeyMinimum32Characters!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "SW.PC.API.Backend";

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
            ValidAudience = jwtIssuer,
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

// Register SCADA Services
builder.Services.AddScoped<IModelService, ModelService>();
builder.Services.AddScoped<IConfigurationService, ConfigurationService>();
builder.Services.AddSingleton<IExcelConfigService, ExcelConfigService>(); // ✅ SINGLETON para mantener caché
builder.Services.AddScoped<IPumpElementService, PumpElementService>();
builder.Services.AddSingleton<ITwinCATService, TwinCATService>();
builder.Services.AddSingleton<IMetricsService, MetricsService>(); // ✅ Servicio de métricas
builder.Services.AddSingleton<ISoftwareIntegrityService, SoftwareIntegrityService>(); // 🔐 Servicio de integridad

// Register Background Services
// builder.Services.AddHostedService<PlcNotificationService>(); // Servicio legacy - reemplazado por PlcPollingService
builder.Services.AddHostedService<PlcPollingService>(); // ✅ Nuevo servicio profesional
builder.Services.AddHostedService<IntegrityVerificationService>(); // 🔐 Verificación periódica de integridad (cada 2 min)

// Add logging
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
    logging.SetMinimumLevel(LogLevel.Information);
});

var app = builder.Build();

// 🔐 Conectar servicio de integridad con métricas y TwinCAT
{
    var metricsService = app.Services.GetRequiredService<IMetricsService>();
    var integrityService = app.Services.GetRequiredService<ISoftwareIntegrityService>();
    var twinCatService = app.Services.GetRequiredService<ITwinCATService>();
    var excelConfigService = app.Services.GetRequiredService<IExcelConfigService>();
    
    metricsService.SetSoftwareIntegrityService(integrityService);
    
    // NOTA: La info de TwinCAT se actualiza después de ConnectAsync() más abajo
    
    // 📋 Cargar rutas Git desde Excel (System Config filas A20-A22)
    try
    {
        // Buscar Excel en múltiples ubicaciones
        var possiblePaths = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ExcelConfigs", "ProjectConfig.xlsm"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "ExcelConfigs", "ProjectConfig.xlsm"),
            @"C:\Users\mpugnaghi.AQUAFRISCH\Documents\Work_In_Process\_Web\AI test\SW.PC.API.Backend_\ExcelConfigs\ProjectConfig.xlsm"
        };
        
        var excelPath = possiblePaths.FirstOrDefault(File.Exists);
        
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

// Initialize Database (Comentado temporalmente - descomentar cuando tengas SQL Server listo)
/*
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ScadaDbContext>();
    try
    {
        db.Database.EnsureCreated(); // Crear base de datos si no existe
        // Para producción, usar: db.Database.Migrate();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Error creating database");
    }
}
*/

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

// Deshabilitar HTTPS redirection en desarrollo para evitar problemas de CORS
// app.UseHttpsRedirection();

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
        
        // Inicializar estado de Database
        metricsService.SetDatabaseStatus(systemConfig.EnableDatabase, false, 
            systemConfig.EnableDatabase ? "Iniciando..." : "Deshabilitado");
        
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
            integrityService.UpdateTwinCATRuntimeInfo(
                twinCatInfo.RuntimeVersion,
                twinCatInfo.AdsVersion,
                twinCatInfo.IsConnected,
                twinCatInfo.IsSimulated
            );
            logger.LogInformation("🔐 TwinCAT integrity info updated: {Version} (Connected={Connected}, Simulated={Simulated})",
                twinCatInfo.RuntimeVersion, twinCatInfo.IsConnected, twinCatInfo.IsSimulated);
        }
        else
        {
            logger.LogWarning("⚠️ TwinCAT Service failed to connect");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "❌ Error initializing TwinCAT Service");
    }
}

// Enable CORS FIRST (debe ir al principio)
app.UseCors("ReactFrontend");

// Configure MIME types for 3D models
var provider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
provider.Mappings[".glb"] = "model/gltf-binary";
provider.Mappings[".gltf"] = "model/gltf+json";
provider.Mappings[".obj"] = "application/object";
provider.Mappings[".mtl"] = "text/plain";
provider.Mappings[".stl"] = "application/sla";

// Enable static files BEFORE routing - this is critical!
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = provider,
    ServeUnknownFileTypes = false,
    HttpsCompression = Microsoft.AspNetCore.Http.Features.HttpsCompressionMode.Compress,
    OnPrepareResponse = ctx =>
    {
        app.Logger.LogInformation("✅ Serving static file: {Path}", ctx.File.PhysicalPath);
        // Add CORS headers to static files
        ctx.Context.Response.Headers["Access-Control-Allow-Origin"] = "*";
        ctx.Context.Response.Headers["Access-Control-Allow-Methods"] = "GET, HEAD, OPTIONS";
        ctx.Context.Response.Headers["Access-Control-Allow-Headers"] = "*";
    }
});

// Serve /models directory explicitly with MIME types
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
        Path.Combine(app.Environment.WebRootPath, "models")),
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

// Enable routing AFTER static files
app.UseRouting();

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// Map Controllers
app.MapControllers();

// Map SignalR Hub
app.MapHub<ScadaHub>("/hubs/scada");

app.Run();
