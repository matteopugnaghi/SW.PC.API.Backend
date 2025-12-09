namespace SW.PC.API.Backend.Middleware
{
    /// <summary>
    /// Middleware que configura el contexto del proyecto para cada request.
    /// 
    /// En Development: Lee el header X-Project-Id para permitir multi-tenant
    /// En Production: Siempre usa el proyecto configurado en active-project.json
    /// 
    /// Header: X-Project-Id: nombre-proyecto
    /// </summary>
    public class ProjectContextMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ProjectContextMiddleware> _logger;
        private readonly IWebHostEnvironment _environment;
        
        public const string PROJECT_HEADER = "X-Project-Id";

        public ProjectContextMiddleware(
            RequestDelegate next,
            ILogger<ProjectContextMiddleware> logger,
            IWebHostEnvironment environment)
        {
            _next = next;
            _logger = logger;
            _environment = environment;
        }

        public async Task InvokeAsync(HttpContext context, Services.IRequestProjectContext requestContext)
        {
            // En producción, SIEMPRE usar el proyecto global (seguridad)
            if (!_environment.IsDevelopment())
            {
                // No hacer nada, usar el default del servicio
                await _next(context);
                return;
            }
            
            // En desarrollo, permitir seleccionar proyecto via header
            if (context.Request.Headers.TryGetValue(PROJECT_HEADER, out var projectIdHeader))
            {
                var projectId = projectIdHeader.ToString();
                
                if (!string.IsNullOrWhiteSpace(projectId))
                {
                    _logger.LogInformation("🔄 Middleware: Request with X-Project-Id header: {ProjectId} - Path: {Path}", 
                        projectId, context.Request.Path);
                    requestContext.SetProject(projectId);
                }
            }
            
            // También soportar query parameter para facilitar pruebas
            if (context.Request.Query.TryGetValue("projectId", out var projectIdQuery))
            {
                var projectId = projectIdQuery.ToString();
                
                if (!string.IsNullOrWhiteSpace(projectId))
                {
                    _logger.LogInformation("🔄 Middleware: Request with projectId query param: {ProjectId} - Path: {Path}", 
                        projectId, context.Request.Path);
                    requestContext.SetProject(projectId);
                }
            }

            await _next(context);
        }
    }

    /// <summary>
    /// Extension methods para registrar el middleware
    /// </summary>
    public static class ProjectContextMiddlewareExtensions
    {
        public static IApplicationBuilder UseProjectContext(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ProjectContextMiddleware>();
        }
    }
}
