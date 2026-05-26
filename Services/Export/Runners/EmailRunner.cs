// ============================================================================
// EmailRunner.cs — Envía el archivo como adjunto vía SMTP
// ============================================================================
// Usa System.Net.Mail (BCL, sin libs nuevas). Si SMTP no está configurado o
// faltan destinatarios → ExportResult.Success=false (no excepción).
// ExportService es quien decide si bloquear la tarea solo si TODOS los
// destinos fallan; un fallo aislado en "email" no rompe el envío "local".
// ============================================================================

using System.Net;
using System.Net.Mail;
using SW.PC.API.Backend.Models.Export;

namespace SW.PC.API.Backend.Services.Export.Runners;

public class EmailRunner : IExportRunner
{
    public string DestinationType => "email";

    public async Task<ExportResult> ExecuteAsync(ExportRunContext ctx, CancellationToken ct = default)
    {
        var result = new ExportResult { DestinationType = DestinationType };

        var email = ctx.Config?.Email;
        if (email is null || email.To.Count == 0)
        {
            result.Success = false;
            result.ErrorMessage = "Email destino sin destinatarios 'To'.";
            return result;
        }

        if (ctx.Smtp is null || !ctx.Smtp.IsConfigured)
        {
            result.Success = false;
            result.ErrorMessage = "SMTP no configurado (SystemConfig.Smtp:Host / Smtp:From).";
            return result;
        }

        using var message = new MailMessage();
        try
        {
            message.From = new MailAddress(ctx.Smtp.From);
            foreach (var to in email.To)  if (!string.IsNullOrWhiteSpace(to))  message.To.Add(to);
            foreach (var cc in email.Cc)  if (!string.IsNullOrWhiteSpace(cc))  message.CC.Add(cc);
            foreach (var bcc in email.Cco) if (!string.IsNullOrWhiteSpace(bcc)) message.Bcc.Add(bcc);

            message.Subject = string.IsNullOrWhiteSpace(email.Subject) ? ctx.Task.Name : email.Subject;
            message.Body = email.Body ?? string.Empty;
            message.IsBodyHtml = false;

            // Adjunto: el stream debe vivir hasta SendAsync, lo controla MailMessage.Dispose.
            var stream = new MemoryStream(ctx.File.Bytes);
            var attachment = new Attachment(stream, ctx.Filename, ctx.File.ContentType);
            message.Attachments.Add(attachment);

            using var smtp = new SmtpClient(ctx.Smtp.Host, ctx.Smtp.Port)
            {
                EnableSsl = ctx.Smtp.EnableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
            };
            if (!string.IsNullOrWhiteSpace(ctx.Smtp.Username))
            {
                smtp.Credentials = new NetworkCredential(ctx.Smtp.Username, ctx.Smtp.Password ?? string.Empty);
            }

            await smtp.SendMailAsync(message, ct);

            result.Success = true;
            result.SizeBytes = ctx.File.Bytes.LongLength;
        }
        catch (FormatException ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Dirección de correo inválida: {ex.Message}";
        }
        catch (SmtpException ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Error SMTP ({ex.StatusCode}): {ex.Message}";
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Error inesperado: {ex.Message}";
        }

        return result;
    }
}
