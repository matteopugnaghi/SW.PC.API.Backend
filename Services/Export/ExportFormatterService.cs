// ============================================================================
// ExportFormatterService.cs — Convierte ExportDataset al formato pedido
// ============================================================================
// Formatos soportados (Fase 1):
//   - xlsx : ClosedXML, 1 hoja "Datos", cabeceras en negrita.
//   - csv  : separador ',', escaping RFC 4180, BOM UTF-8.
//   - json : System.Text.Json, array de objetos {columna: valor}.
//   - html : Markdig (tabla Markdown → HTML) + envoltorio mínimo.
//   - png  : passthrough. El frontend envía el base64 de la captura del
//            gráfico en ExportDataset.Metadata["pngBase64"] (lo decodifica
//            tal cual). No genera gráficos en backend.
//
// PROHIBIDO añadir librerías nuevas (CRA/SBOM). PROHIBIDO PDF.
// ============================================================================

using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using Markdig;
using SW.PC.API.Backend.Models.Export;

namespace SW.PC.API.Backend.Services.Export;

public interface IExportFormatterService
{
    /// <summary>
    /// Convierte el dataset al formato pedido.
    /// </summary>
    /// <param name="dataset">Datos resueltos por un IExportDatasetProvider.</param>
    /// <param name="format">"xlsx" | "csv" | "json" | "html" | "png".</param>
    /// <returns>Bytes + content-type MIME + extensión recomendada (sin punto).</returns>
    FormattedExport Format(ExportDataset dataset, string format);
}

public record FormattedExport(byte[] Bytes, string ContentType, string Extension);

public class ExportFormatterService : IExportFormatterService
{
    public FormattedExport Format(ExportDataset dataset, string format)
    {
        if (dataset is null) throw new ArgumentNullException(nameof(dataset));
        if (string.IsNullOrWhiteSpace(format)) throw new ArgumentException("Format vacío", nameof(format));

        return format.ToLowerInvariant() switch
        {
            "xlsx" => FormatXlsx(dataset),
            "csv"  => FormatCsv(dataset),
            "json" => FormatJson(dataset),
            "html" => FormatHtml(dataset),
            "png"  => FormatPng(dataset),
            _ => throw new NotSupportedException($"Formato '{format}' no soportado.")
        };
    }

    // ───────────────────────── XLSX ─────────────────────────
    private static FormattedExport FormatXlsx(ExportDataset ds)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Datos");

        for (int c = 0; c < ds.Columns.Count; c++)
        {
            var cell = ws.Cell(1, c + 1);
            cell.Value = ds.Columns[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(23, 162, 184);
            cell.Style.Font.FontColor = XLColor.White;
        }

        for (int r = 0; r < ds.Rows.Count; r++)
        {
            var row = ds.Rows[r];
            for (int c = 0; c < row.Length && c < ds.Columns.Count; c++)
            {
                SetCellValue(ws.Cell(r + 2, c + 1), row[c]);
            }
        }

        try { ws.Columns().AdjustToContents(); } catch { /* no crítico */ }

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return new FormattedExport(
            ms.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "xlsx");
    }

    private static void SetCellValue(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null:           cell.Value = string.Empty; break;
            case bool b:         cell.Value = b; break;
            case DateTime dt:    cell.Value = dt; cell.Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss"; break;
            case int i:          cell.Value = i; break;
            case long l:         cell.Value = l; break;
            case double d:       cell.Value = d; break;
            case decimal dec:    cell.Value = dec; break;
            case float f:        cell.Value = f; break;
            default:             cell.Value = value.ToString() ?? string.Empty; break;
        }
    }

    // ───────────────────────── CSV ─────────────────────────
    private static FormattedExport FormatCsv(ExportDataset ds)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", ds.Columns.Select(EscapeCsv)));

        foreach (var row in ds.Rows)
        {
            sb.AppendLine(string.Join(",", row.Select(v => EscapeCsv(FormatScalar(v)))));
        }

        // BOM UTF-8 para que Excel lo abra con acentos correctamente.
        var bytes = new List<byte>(Encoding.UTF8.GetPreamble());
        bytes.AddRange(Encoding.UTF8.GetBytes(sb.ToString()));
        return new FormattedExport(bytes.ToArray(), "text/csv; charset=utf-8", "csv");
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        var needsQuoting = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
        var v = value.Replace("\"", "\"\"");
        return needsQuoting ? $"\"{v}\"" : v;
    }

    // ───────────────────────── JSON ─────────────────────────
    private static FormattedExport FormatJson(ExportDataset ds)
    {
        var array = ds.Rows.Select(row =>
        {
            var dict = new Dictionary<string, object?>(ds.Columns.Count);
            for (int c = 0; c < ds.Columns.Count; c++)
            {
                dict[ds.Columns[c]] = c < row.Length ? row[c] : null;
            }
            return dict;
        }).ToList();

        var payload = new
        {
            metadata = ds.Metadata,
            totalRows = ds.TotalRows,
            rows = array
        };

        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        return new FormattedExport(Encoding.UTF8.GetBytes(json), "application/json; charset=utf-8", "json");
    }

    // ───────────────────────── HTML (vía Markdown) ─────────────────────────
    private static FormattedExport FormatHtml(ExportDataset ds)
    {
        var md = new StringBuilder();

        md.AppendLine("# Informe");
        if (ds.Metadata.TryGetValue("generatedAt", out var when))
            md.AppendLine($"_Generado: {when}_");
        md.AppendLine();

        if (ds.Columns.Count > 0)
        {
            md.AppendLine("| " + string.Join(" | ", ds.Columns.Select(EscapeMd)) + " |");
            md.AppendLine("|" + string.Join("|", ds.Columns.Select(_ => "---")) + "|");

            foreach (var row in ds.Rows)
            {
                md.AppendLine("| " + string.Join(" | ", row.Select(v => EscapeMd(FormatScalar(v)))) + " |");
            }
        }

        var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        var bodyHtml = Markdown.ToHtml(md.ToString(), pipeline);

        var full = $@"<!DOCTYPE html>
<html lang=""es""><head><meta charset=""utf-8"" />
<title>Informe</title>
<style>
 body{{font-family:Segoe UI,Arial,sans-serif;margin:24px;color:#222;}}
 table{{border-collapse:collapse;width:100%;}}
 th,td{{border:1px solid #ccc;padding:6px 10px;text-align:left;font-size:13px;}}
 th{{background:#17a2b8;color:#fff;}}
 tr:nth-child(even) td{{background:#f6f9fb;}}
</style></head><body>
{bodyHtml}
</body></html>";

        return new FormattedExport(Encoding.UTF8.GetBytes(full), "text/html; charset=utf-8", "html");
    }

    private static string EscapeMd(string? value)
        => string.IsNullOrEmpty(value) ? string.Empty
           : value.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");

    // ───────────────────────── PNG (passthrough) ─────────────────────────
    // El backend NO renderiza gráficos. El frontend captura el canvas de
    // echarts y envía el base64 en ExportDataset.Metadata["pngBase64"].
    private static FormattedExport FormatPng(ExportDataset ds)
    {
        if (!ds.Metadata.TryGetValue("pngBase64", out var raw) || raw is null)
            throw new InvalidOperationException("Formato 'png' requiere Metadata[\"pngBase64\"] con la captura del gráfico.");

        var b64 = raw.ToString() ?? string.Empty;

        // Acepta tanto "data:image/png;base64,XXXX" como solo "XXXX".
        var commaIdx = b64.IndexOf(',');
        if (b64.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && commaIdx > 0)
            b64 = b64[(commaIdx + 1)..];

        byte[] bytes;
        try { bytes = Convert.FromBase64String(b64); }
        catch (FormatException ex) { throw new InvalidOperationException("pngBase64 no es un base64 válido.", ex); }

        return new FormattedExport(bytes, "image/png", "png");
    }

    // ───────────────────────── Helpers ─────────────────────────
    private static string FormatScalar(object? v) => v switch
    {
        null => string.Empty,
        DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss"),
        bool b => b ? "true" : "false",
        IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
        _ => v.ToString() ?? string.Empty
    };
}
