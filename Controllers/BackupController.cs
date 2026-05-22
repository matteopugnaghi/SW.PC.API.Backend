// ==================================================================
// Controllers/BackupController.cs
// DATA MANAGEMENT - API Endpoints para Backup/Restore
// Versión: 1.0.0
// Cumplimiento: EU CRA Anexo I, Parte I, 2f (Integridad de Datos)
// ==================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SW.PC.API.Backend.Models;
using SW.PC.API.Backend.Services;

namespace SW.PC.API.Backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BackupController : ControllerBase
    {
        private readonly ILogger<BackupController> _logger;
        private readonly IBackupService _backupService;
        private readonly IRequestProjectContext _requestProjectContext;
        private readonly IAuditLogService _auditLog;

        public BackupController(
            ILogger<BackupController> logger,
            IBackupService backupService,
            IRequestProjectContext requestProjectContext,
            IAuditLogService auditLog)
        {
            _logger = logger;
            _backupService = backupService;
            _requestProjectContext = requestProjectContext;
            _auditLog = auditLog;
        }

        /// <summary>
        /// Obtener lista de backups del proyecto
        /// </summary>
        [HttpGet]
        public async Task<ActionResult<BackupListResponse>> GetBackups()
        {
            var projectId = _requestProjectContext.ProjectId;
            _logger.LogInformation("GET /api/backup - Project: {ProjectId}", projectId);
            
            var result = await _backupService.ListBackupsAsync(projectId);
            return Ok(result);
        }

        /// <summary>
        /// Obtener estado del sistema de backup
        /// </summary>
        [HttpGet("status")]
        public async Task<ActionResult<BackupSystemStatus>> GetStatus()
        {
            var projectId = _requestProjectContext.ProjectId;
            _logger.LogInformation("GET /api/backup/status - Project: {ProjectId}", projectId);
            
            var result = await _backupService.GetSystemStatusAsync(projectId);
            return Ok(result);
        }

        /// <summary>
        /// Obtener información de un backup específico
        /// </summary>
        [HttpGet("{backupId}")]
        public async Task<ActionResult<BackupInfo>> GetBackup(string backupId)
        {
            var projectId = _requestProjectContext.ProjectId;
            _logger.LogInformation("GET /api/backup/{BackupId} - Project: {ProjectId}", backupId, projectId);
            
            var result = await _backupService.GetBackupAsync(projectId, backupId);
            
            if (result == null)
                return NotFound(new { message = "Backup not found" });
            
            return Ok(result);
        }

        /// <summary>
        /// Crear un nuevo backup
        /// </summary>
        [HttpPost]
        public async Task<ActionResult<BackupOperationResponse>> CreateBackup([FromBody] CreateBackupRequest? request = null)
        {
            var projectId = _requestProjectContext.ProjectId;
            var userId = User.Identity?.Name ?? "anonymous";
            
            _logger.LogInformation("POST /api/backup - Project: {ProjectId}, User: {UserId}", projectId, userId);
            
            request ??= new CreateBackupRequest();
            var result = await _backupService.CreateBackupAsync(projectId, request, userId);
            
            if (!result.Success)
                return BadRequest(result);
            
            return Ok(result);
        }

        /// <summary>
        /// Restaurar desde un backup
        /// </summary>
        [HttpPost("restore")]
        public async Task<ActionResult<BackupOperationResponse>> RestoreBackup([FromBody] RestoreBackupRequest request)
        {
            var projectId = _requestProjectContext.ProjectId;
            var userId = User.Identity?.Name ?? "anonymous";
            
            _logger.LogInformation("POST /api/backup/restore - Project: {ProjectId}, BackupId: {BackupId}, User: {UserId}", 
                projectId, request.BackupId, userId);
            
            var result = await _backupService.RestoreBackupAsync(projectId, request, userId);
            
            if (!result.Success)
                return BadRequest(result);
            
            return Ok(result);
        }

        /// <summary>
        /// Verificar integridad de un backup
        /// </summary>
        [HttpGet("{backupId}/verify")]
        public async Task<ActionResult<BackupVerificationResponse>> VerifyBackup(string backupId)
        {
            var projectId = _requestProjectContext.ProjectId;
            _logger.LogInformation("GET /api/backup/{BackupId}/verify - Project: {ProjectId}", backupId, projectId);
            
            var result = await _backupService.VerifyBackupAsync(projectId, backupId);
            return Ok(result);
        }

        /// <summary>
        /// Eliminar un backup
        /// </summary>
        [HttpDelete("{backupId}")]
        public async Task<ActionResult<BackupOperationResponse>> DeleteBackup(string backupId)
        {
            var projectId = _requestProjectContext.ProjectId;
            var userId = User.Identity?.Name ?? "anonymous";
            
            _logger.LogInformation("DELETE /api/backup/{BackupId} - Project: {ProjectId}, User: {UserId}", 
                backupId, projectId, userId);
            
            var result = await _backupService.DeleteBackupAsync(projectId, backupId, userId);
            
            if (!result.Success)
                return BadRequest(result);
            
            return Ok(result);
        }

        /// <summary>
        /// Exportar/Descargar un backup (archivo ZIP)
        /// </summary>
        [HttpGet("{backupId}/export")]
        public async Task<IActionResult> ExportBackup(string backupId)
        {
            var projectId = _requestProjectContext.ProjectId;
            var userId = User.Identity?.Name ?? "anonymous";
            _logger.LogInformation("GET /api/backup/{BackupId}/export - Project: {ProjectId}, User: {UserId}", backupId, projectId, userId);
            
            var filePath = await _backupService.GetBackupFilePathAsync(projectId, backupId);
            
            if (filePath == null || !System.IO.File.Exists(filePath))
            {
                // 📋 Audit L1: intento de descarga de backup inexistente
                await _auditLog.LogAsync(
                    AuditCategory.Backup,
                    AuditAction.BackupExport,
                    AuditResult.Failure,
                    $"Backup export failed: {backupId} not found (Project: {projectId})",
                    userId,
                    projectId: projectId);
                return NotFound(new { message = "Backup file not found" });
            }
            
            // Usar nombre custom del backup si existe
            var backupInfo = await _backupService.GetBackupAsync(projectId, backupId);
            var fileName = Path.GetFileName(filePath);
            if (backupInfo?.Name != null && backupInfo.Name != backupInfo.Id)
            {
                // Sanitizar el nombre custom para uso como filename
                var safeName = string.Join("_", backupInfo.Name.Split(Path.GetInvalidFileNameChars()));
                if (!string.IsNullOrWhiteSpace(safeName))
                    fileName = $"{safeName}.zip";
            }
            
            // 📋 Audit L1: registrar la descarga (exfiltración de datos — EU CRA Anexo I 2f)
            var fileSize = new FileInfo(filePath).Length;
            var sizeFormatted = fileSize >= 1024 * 1024
                ? $"{fileSize / (1024.0 * 1024.0):F2} MB"
                : $"{fileSize / 1024.0:F2} KB";
            await _auditLog.LogAsync(
                AuditCategory.Backup,
                AuditAction.BackupExport,
                AuditResult.Success,
                $"Backup exported: {fileName} (Project: {projectId}, Size: {sizeFormatted})",
                userId,
                projectId: projectId);
            
            var fileStream = System.IO.File.OpenRead(filePath);
            
            return File(fileStream, "application/zip", fileName);
        }

        /// <summary>
        /// Importar un backup desde archivo
        /// </summary>
        [HttpPost("import")]
        [DisableRequestSizeLimit]
        [RequestFormLimits(MultipartBodyLengthLimit = 524288000)] // 500 MB
        public async Task<ActionResult<BackupOperationResponse>> ImportBackup(IFormFile file)
        {
            var projectId = _requestProjectContext.ProjectId;
            var userId = User.Identity?.Name ?? "anonymous";
            
            _logger.LogInformation("POST /api/backup/import - Project: {ProjectId}, File: {FileName}, User: {UserId}", 
                projectId, file.FileName, userId);
            
            if (file == null || file.Length == 0)
                return BadRequest(new BackupOperationResponse 
                { 
                    Success = false, 
                    Message = "No file provided" 
                });
            
            if (!file.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new BackupOperationResponse 
                { 
                    Success = false, 
                    Message = "File must be a ZIP archive" 
                });
            
            // 🛡️ SCG-05: validación de magic bytes ZIP (defense-in-depth sobre extensión + content-type)
            // PK\x03\x04 = local file header | PK\x05\x06 = empty archive | PK\x07\x08 = spanned archive
            using (var probeStream = file.OpenReadStream())
            {
                var header = new byte[4];
                var readBytes = await probeStream.ReadAsync(header, 0, 4);
                if (readBytes < 4 ||
                    header[0] != 0x50 || header[1] != 0x4B ||
                    !((header[2] == 0x03 && header[3] == 0x04) ||
                      (header[2] == 0x05 && header[3] == 0x06) ||
                      (header[2] == 0x07 && header[3] == 0x08)))
                {
                    _logger.LogWarning(
                        "SCG-05: ZIP magic-bytes check failed for upload '{FileName}' (project={ProjectId}, user={UserId}). Header=[{B0:X2} {B1:X2} {B2:X2} {B3:X2}]",
                        file.FileName, projectId, userId, header[0], header[1], header[2], header[3]);
                    
                    // 📋 Audit L1: SCG-05 — rechazo por magic bytes inválidos (Security event)
                    await _auditLog.LogAsync(
                        AuditCategory.Security,
                        AuditAction.BackupImportRejected,
                        AuditResult.Warning,
                        $"Backup import rejected: invalid ZIP magic bytes for '{file.FileName}' (Project: {projectId})",
                        userId,
                        projectId: projectId);
                    
                    return BadRequest(new BackupOperationResponse
                    {
                        Success = false,
                        Message = "File contents do not match a valid ZIP archive (magic bytes mismatch)"
                    });
                }
            }
            // Validación adicional: content-type declarado (no determinante pero añade trazabilidad)
            if (!string.IsNullOrEmpty(file.ContentType) &&
                !file.ContentType.Contains("zip", StringComparison.OrdinalIgnoreCase) &&
                !file.ContentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "SCG-05: unexpected Content-Type '{ContentType}' for ZIP upload '{FileName}' (project={ProjectId}, user={UserId}) — proceeding because magic bytes passed",
                    file.ContentType, file.FileName, projectId, userId);
            }
            
            using var stream = file.OpenReadStream();
            var result = await _backupService.ImportBackupAsync(projectId, stream, file.FileName, userId);
            
            if (!result.Success)
                return BadRequest(result);
            
            return Ok(result);
        }

        /// <summary>
        /// Obtener configuración de backup
        /// </summary>
        [HttpGet("config")]
        public async Task<ActionResult<BackupConfig>> GetConfig()
        {
            var projectId = _requestProjectContext.ProjectId;
            _logger.LogInformation("GET /api/backup/config - Project: {ProjectId}", projectId);
            
            var result = await _backupService.GetBackupConfigAsync(projectId);
            return Ok(result);
        }

        /// <summary>
        /// Limpiar backups antiguos manualmente
        /// </summary>
        [HttpPost("cleanup")]
        [Authorize]
        public async Task<ActionResult<object>> CleanupOldBackups()
        {
            var projectId = _requestProjectContext.ProjectId;
            var userId = User.Identity?.Name ?? "anonymous";
            _logger.LogInformation("POST /api/backup/cleanup - Project: {ProjectId}, User: {UserId}", projectId, userId);
            
            var deleted = await _backupService.CleanupOldBackupsAsync(projectId);
            
            // 📋 Audit L1: registrar la invocación manual (incluso si deleted==0, queda trazada la acción del usuario)
            await _auditLog.LogAsync(
                AuditCategory.Backup,
                AuditAction.BackupCleanup,
                AuditResult.Success,
                $"Backup cleanup: {deleted} backup(s) removed (Project: {projectId})",
                userId,
                projectId: projectId,
                affectedItemCount: deleted);
            
            return Ok(new 
            { 
                success = true, 
                message = $"Cleanup complete: {deleted} backup(s) removed",
                deletedCount = deleted
            });
        }
    }
}
