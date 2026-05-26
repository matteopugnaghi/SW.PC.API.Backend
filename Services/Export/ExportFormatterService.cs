// ============================================================================
// ExportFormatterService.cs — Convierte ExportDataset al formato pedido
// ============================================================================
// Formatos soportados (Fase 1):
//   - xlsx : ClosedXML, 1 hoja "Datos", cabeceras en negrita.
//   - csv  : separador ',', escaping RFC 4180, BOM UTF-8.
//   - json : System.Text.Json, array de objetos {columna: valor}.
//   - html : HTML generado a mano (sin librerías externas) con diseño opcional.
//   - png  : passthrough. El frontend envía el base64 de la captura del
//            gráfico en ExportDataset.Metadata["pngBase64"] (lo decodifica
//            tal cual). No genera gráficos en backend.
//
// PROHIBIDO añadir librerías nuevas (CRA/SBOM). PROHIBIDO PDF.
// ============================================================================

using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using SW.PC.API.Backend.Models.Export;

namespace SW.PC.API.Backend.Services.Export;

public interface IExportFormatterService
{
    /// <summary>
    /// Convierte el dataset al formato pedido.
    /// </summary>
    /// <param name="dataset">Datos resueltos por un IExportDatasetProvider.</param>
    /// <param name="format">"xlsx" | "csv" | "json" | "html" | "png".</param>
    /// <param name="report">Diseño opcional del informe (solo aplica a xlsx/html). Si null, layout básico.</param>
    /// <returns>Bytes + content-type MIME + extensión recomendada (sin punto).</returns>
    FormattedExport Format(ExportDataset dataset, string format, ReportDesignConfig? report = null);
}

public record FormattedExport(byte[] Bytes, string ContentType, string Extension);

public class ExportFormatterService : IExportFormatterService
{
    public FormattedExport Format(ExportDataset dataset, string format, ReportDesignConfig? report = null)
    {
        if (dataset is null) throw new ArgumentNullException(nameof(dataset));
        if (string.IsNullOrWhiteSpace(format)) throw new ArgumentException("Format vacío", nameof(format));

        return format.ToLowerInvariant() switch
        {
            "xlsx" => FormatXlsx(dataset, report),
            "csv"  => FormatCsv(dataset),
            "json" => FormatJson(dataset),
            "html" => FormatHtml(dataset, report),
            "png"  => FormatPng(dataset),
            _ => throw new NotSupportedException($"Formato '{format}' no soportado.")
        };
    }

    // ───────────────────────── XLSX ─────────────────────────
    private static FormattedExport FormatXlsx(ExportDataset ds, ReportDesignConfig? rpt)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Datos");

        var headerColor = ParseColor(rpt?.HeaderColor, XLColor.FromArgb(23, 162, 184));
        var accentColor = ParseColor(rpt?.AccentColor, XLColor.FromArgb(11, 85, 102));
        var colCount = Math.Max(1, ds.Columns.Count);
        int currentRow = 1;

        // ─── Cabecera (logo + título + meta) ───
        if (rpt is not null && rpt.IncludeHeader)
        {
            int logoCols = 0;
            int logoEndRow = currentRow;
            var logoBytes = TryDecodeBase64Image(rpt.LogoBase64);
            if (logoBytes is not null && colCount >= 2)
            {
                try
                {
                    using var imgStream = new MemoryStream(logoBytes);
                    var pic = ws.AddPicture(imgStream)
                        .MoveTo(ws.Cell(currentRow, 1))
                        .WithSize(120, 80);
                    logoCols = 1;
                    logoEndRow = currentRow + 3;
                }
                catch { /* logo corrupto → seguimos sin él */ }
            }

            int textStartCol = logoCols > 0 ? 2 : 1;
            int textEndCol = colCount;

            if (!string.IsNullOrWhiteSpace(rpt.Title))
            {
                var titleCell = ws.Cell(currentRow, textStartCol);
                titleCell.Value = rpt.Title;
                titleCell.Style.Font.Bold = true;
                titleCell.Style.Font.FontSize = 18;
                titleCell.Style.Font.FontColor = accentColor;
                if (textEndCol > textStartCol)
                    ws.Range(currentRow, textStartCol, currentRow, textEndCol).Merge();
                currentRow++;
            }
            if (!string.IsNullOrWhiteSpace(rpt.Subtitle))
            {
                var subCell = ws.Cell(currentRow, textStartCol);
                subCell.Value = rpt.Subtitle;
                subCell.Style.Font.Italic = true;
                subCell.Style.Font.FontSize = 11;
                subCell.Style.Font.FontColor = XLColor.FromArgb(90, 90, 90);
                if (textEndCol > textStartCol)
                    ws.Range(currentRow, textStartCol, currentRow, textEndCol).Merge();
                currentRow++;
            }

            var metaParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(rpt.CompanyName)) metaParts.Add(rpt.CompanyName!);
            if (rpt.ShowDate) metaParts.Add($"Fecha: {DateTime.Now:yyyy-MM-dd HH:mm}");
            if (rpt.ShowProject && ds.Metadata.TryGetValue("projectId", out var pid) && pid is not null)
                metaParts.Add($"Proyecto: {pid}");
            if (metaParts.Count > 0)
            {
                var metaCell = ws.Cell(currentRow, textStartCol);
                metaCell.Value = string.Join("  ·  ", metaParts);
                metaCell.Style.Font.FontSize = 10;
                metaCell.Style.Font.FontColor = XLColor.FromArgb(110, 110, 110);
                if (textEndCol > textStartCol)
                    ws.Range(currentRow, textStartCol, currentRow, textEndCol).Merge();
                currentRow++;
            }

            currentRow = Math.Max(currentRow, logoEndRow);
            currentRow++; // fila en blanco
        }

        // ─── Filtros aplicados ───
        if (rpt is not null && rpt.IncludeFilters
            && ds.Metadata.TryGetValue("appliedFilters", out var afRaw) && afRaw is not null)
        {
            var filters = NormalizeAppliedFilters(afRaw);
            if (filters.Count > 0)
            {
                var hdr = ws.Cell(currentRow, 1);
                hdr.Value = "Filtros aplicados";
                hdr.Style.Font.Bold = true;
                hdr.Style.Font.FontColor = XLColor.White;
                hdr.Style.Fill.BackgroundColor = accentColor;
                if (colCount > 1) ws.Range(currentRow, 1, currentRow, colCount).Merge();
                currentRow++;
                foreach (var (label, value) in filters)
                {
                    ws.Cell(currentRow, 1).Value = label;
                    ws.Cell(currentRow, 1).Style.Font.Bold = true;
                    var valCell = ws.Cell(currentRow, 2);
                    valCell.Value = value;
                    if (colCount > 2) ws.Range(currentRow, 2, currentRow, colCount).Merge();
                    currentRow++;
                }
                currentRow++;
            }
        }

        // ─── Tabla de datos ───
        int dataHeaderRow = currentRow;
        for (int c = 0; c < ds.Columns.Count; c++)
        {
            var cell = ws.Cell(dataHeaderRow, c + 1);
            cell.Value = ds.Columns[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = headerColor;
            cell.Style.Font.FontColor = XLColor.White;
        }
        currentRow = dataHeaderRow + 1;

        for (int r = 0; r < ds.Rows.Count; r++)
        {
            var row = ds.Rows[r];
            for (int c = 0; c < row.Length && c < ds.Columns.Count; c++)
            {
                SetCellValue(ws.Cell(currentRow + r, c + 1), row[c]);
            }
        }
        currentRow += ds.Rows.Count;

        // ─── Autofiltro en la cabecera de la tabla de datos ───
        if (ds.Columns.Count > 0)
        {
            try
            {
                int lastDataRow = ds.Rows.Count > 0 ? dataHeaderRow + ds.Rows.Count : dataHeaderRow;
                ws.Range(dataHeaderRow, 1, lastDataRow, ds.Columns.Count).SetAutoFilter();
            }
            catch { /* no crítico */ }
        }

        // ─── Resumen (totales) ───
        // Layout: sub-tabla con una fila por columna numérica y una sub-columna
        // por agregación. Más legible que el layout anterior (matriz invertida).
        //
        //   ┌──────────────────────┬────────┬────────┬─────┬─────┬───────┐
        //   │ Columna              │  SUM   │  AVG   │ MIN │ MAX │ COUNT │
        //   ├──────────────────────┼────────┼────────┼─────┼─────┼───────┤
        //   │ Agua Reciclada (L)   │ 77.00  │ 38.50  │ 0   │ 77  │   2   │
        //   │ Agua de red (L)      │ 77.00  │ 38.50  │ 0   │ 77  │   2   │
        //   └──────────────────────┴────────┴────────┴─────┴─────┴───────┘
        if (rpt is not null && !string.Equals(rpt.SummaryMode, "off", StringComparison.OrdinalIgnoreCase))
        {
            var summary = BuildSummary(ds, rpt);
            if (summary.Count > 0)
            {
                // Columnas que aparecen al menos en una agregación (preservando el
                // orden original de ds.Columns).
                var dataColumns = ds.Columns
                    .Where(col => summary.Any(s => s.Values.ContainsKey(col)))
                    .ToList();
                var aggNames = summary.Select(s => s.AggName).ToList();
                var subColCount = 1 + aggNames.Count; // "Columna" + 1 por agregación

                currentRow++; // separador
                // Título de bloque (ocupa toda la anchura del informe)
                var hdr = ws.Cell(currentRow, 1);
                hdr.Value = "Resumen";
                hdr.Style.Font.Bold = true;
                hdr.Style.Font.FontColor = XLColor.White;
                hdr.Style.Fill.BackgroundColor = accentColor;
                if (colCount > 1) ws.Range(currentRow, 1, currentRow, colCount).Merge();
                currentRow++;

                // Cabecera de la sub-tabla
                var colHeaderCell = ws.Cell(currentRow, 1);
                colHeaderCell.Value = "Columna";
                colHeaderCell.Style.Font.Bold = true;
                colHeaderCell.Style.Font.FontColor = XLColor.White;
                colHeaderCell.Style.Fill.BackgroundColor = headerColor;
                for (int a = 0; a < aggNames.Count; a++)
                {
                    var aggHeader = ws.Cell(currentRow, 2 + a);
                    aggHeader.Value = aggNames[a].ToUpperInvariant();
                    aggHeader.Style.Font.Bold = true;
                    aggHeader.Style.Font.FontColor = XLColor.White;
                    aggHeader.Style.Fill.BackgroundColor = headerColor;
                    aggHeader.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }
                currentRow++;

                // Una fila por columna
                foreach (var dataCol in dataColumns)
                {
                    var labelCell = ws.Cell(currentRow, 1);
                    labelCell.Value = dataCol;
                    labelCell.Style.Font.Bold = true;
                    labelCell.Style.Fill.BackgroundColor = XLColor.FromArgb(235, 240, 245);
                    for (int a = 0; a < aggNames.Count; a++)
                    {
                        var (_, values) = summary[a];
                        if (values.TryGetValue(dataCol, out var v))
                        {
                            var valCell = ws.Cell(currentRow, 2 + a);
                            SetCellValue(valCell, v);
                            valCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                        }
                    }
                    currentRow++;
                }
                _ = subColCount; // reservado por si se quiere mergear/encuadrar la sub-tabla en el futuro
            }
        }

        // ─── Pie ───
        if (rpt is not null && rpt.IncludeFooter && !string.IsNullOrWhiteSpace(rpt.FooterText))
        {
            currentRow += 2;
            var foot = ws.Cell(currentRow, 1);
            foot.Value = rpt.FooterText;
            foot.Style.Font.Italic = true;
            foot.Style.Font.FontSize = 9;
            foot.Style.Font.FontColor = XLColor.FromArgb(120, 120, 120);
            if (colCount > 1) ws.Range(currentRow, 1, currentRow, colCount).Merge();
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

    // ───────────────────────── HTML ─────────────────────────
    private static FormattedExport FormatHtml(ExportDataset ds, ReportDesignConfig? rpt)
    {
        var headerColor = SanitizeColor(rpt?.HeaderColor, "#17a2b8");
        var accentColor = SanitizeColor(rpt?.AccentColor, "#0b5566");
        var sb = new StringBuilder();

        // ─── Cabecera ───
        if (rpt is not null && rpt.IncludeHeader)
        {
            sb.Append("<header class=\"rpt-header\">");
            var logoUri = NormalizeLogoDataUri(rpt.LogoBase64);
            if (!string.IsNullOrEmpty(logoUri))
            {
                sb.Append($"<img class=\"rpt-logo\" src=\"{HtmlEscape(logoUri)}\" alt=\"logo\" />");
            }
            sb.Append("<div class=\"rpt-headtext\">");
            if (!string.IsNullOrWhiteSpace(rpt.Title))
                sb.Append($"<h1>{HtmlEscape(rpt.Title!)}</h1>");
            if (!string.IsNullOrWhiteSpace(rpt.Subtitle))
                sb.Append($"<h2>{HtmlEscape(rpt.Subtitle!)}</h2>");
            var metaParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(rpt.CompanyName)) metaParts.Add(HtmlEscape(rpt.CompanyName!));
            if (rpt.ShowDate) metaParts.Add($"Fecha: {DateTime.Now:yyyy-MM-dd HH:mm}");
            if (rpt.ShowProject && ds.Metadata.TryGetValue("projectId", out var pid) && pid is not null)
                metaParts.Add($"Proyecto: {HtmlEscape(pid.ToString() ?? "")}");
            if (metaParts.Count > 0)
                sb.Append($"<p class=\"rpt-meta\">{string.Join(" &middot; ", metaParts)}</p>");
            sb.Append("</div></header>");
        }
        else
        {
            sb.Append("<header class=\"rpt-header rpt-header-basic\"><h1>Informe</h1>");
            if (ds.Metadata.TryGetValue("generatedAt", out var when))
                sb.Append($"<p class=\"rpt-meta\">Generado: {HtmlEscape(when?.ToString() ?? "")}</p>");
            sb.Append("</header>");
        }

        // ─── Filtros aplicados ───
        if (rpt is not null && rpt.IncludeFilters
            && ds.Metadata.TryGetValue("appliedFilters", out var afRaw) && afRaw is not null)
        {
            var filters = NormalizeAppliedFilters(afRaw);
            if (filters.Count > 0)
            {
                sb.Append("<section class=\"rpt-filters\"><h3>Filtros aplicados</h3><dl>");
                foreach (var (label, value) in filters)
                    sb.Append($"<dt>{HtmlEscape(label)}</dt><dd>{HtmlEscape(value)}</dd>");
                sb.Append("</dl></section>");
            }
        }

        // ─── Tabla de datos ───
        sb.Append("<table class=\"rpt-table\"><thead><tr>");
        foreach (var col in ds.Columns)
            sb.Append($"<th>{HtmlEscape(col)}</th>");
        sb.Append("</tr></thead><tbody>");
        foreach (var row in ds.Rows)
        {
            sb.Append("<tr>");
            for (int c = 0; c < ds.Columns.Count; c++)
            {
                var v = c < row.Length ? row[c] : null;
                sb.Append($"<td>{HtmlEscape(FormatScalar(v))}</td>");
            }
            sb.Append("</tr>");
        }
        sb.Append("</tbody>");

        // ─── Resumen ───
        if (rpt is not null && !string.Equals(rpt.SummaryMode, "off", StringComparison.OrdinalIgnoreCase))
        {
            var summary = BuildSummary(ds, rpt);
            if (summary.Count > 0)
            {
                // Cierra tabla principal y abre sub-tabla con layout
                // [Columna | SUM | AVG | MIN | MAX | COUNT]
                sb.Append("</table>");
                var dataColumns = ds.Columns
                    .Where(col => summary.Any(s => s.Values.ContainsKey(col)))
                    .ToList();
                var aggNames = summary.Select(s => s.AggName).ToList();
                sb.Append("<h3 class=\"rpt-summary-title\">Resumen</h3>");
                sb.Append("<table class=\"rpt-summary-table\"><thead><tr>");
                sb.Append("<th>Columna</th>");
                foreach (var a in aggNames)
                    sb.Append($"<th>{HtmlEscape(a.ToUpperInvariant())}</th>");
                sb.Append("</tr></thead><tbody>");
                foreach (var dataCol in dataColumns)
                {
                    sb.Append("<tr>");
                    sb.Append($"<th scope=\"row\">{HtmlEscape(dataCol)}</th>");
                    for (int a = 0; a < aggNames.Count; a++)
                    {
                        var (_, values) = summary[a];
                        sb.Append(values.TryGetValue(dataCol, out var v)
                            ? $"<td>{HtmlEscape(FormatScalar(v))}</td>"
                            : "<td></td>");
                    }
                    sb.Append("</tr>");
                }
                sb.Append("</tbody>");
            }
        }
        sb.Append("</table>");

        // ─── Pie ───
        if (rpt is not null && rpt.IncludeFooter && !string.IsNullOrWhiteSpace(rpt.FooterText))
            sb.Append($"<footer class=\"rpt-footer\">{HtmlEscape(rpt.FooterText!)}</footer>");

        var full = $@"<!DOCTYPE html>
<html lang=""es""><head><meta charset=""utf-8"" />
<title>Informe</title>
<style>
 body{{font-family:Segoe UI,Arial,sans-serif;margin:24px;color:#222;}}
 .rpt-header{{display:flex;gap:16px;align-items:center;border-bottom:3px solid {accentColor};padding-bottom:12px;margin-bottom:16px;}}
 .rpt-header-basic{{display:block;}}
 .rpt-logo{{max-height:80px;max-width:160px;object-fit:contain;}}
 .rpt-headtext h1{{margin:0;color:{accentColor};font-size:22px;}}
 .rpt-headtext h2{{margin:4px 0 0;font-weight:400;font-style:italic;color:#555;font-size:14px;}}
 .rpt-meta{{margin:6px 0 0;color:#777;font-size:12px;}}
 .rpt-filters{{background:#f6f9fb;border-left:4px solid {accentColor};padding:10px 14px;margin-bottom:16px;border-radius:4px;}}
 .rpt-filters h3{{margin:0 0 6px;font-size:13px;color:{accentColor};text-transform:uppercase;letter-spacing:.5px;}}
 .rpt-filters dl{{display:grid;grid-template-columns:auto 1fr;gap:4px 12px;margin:0;font-size:13px;}}
 .rpt-filters dt{{font-weight:700;color:#444;}}
 .rpt-filters dd{{margin:0;color:#222;}}
 .rpt-table{{border-collapse:collapse;width:100%;}}
 .rpt-table th,.rpt-table td{{border:1px solid #ccc;padding:6px 10px;text-align:left;font-size:13px;}}
 .rpt-table thead th{{background:{headerColor};color:#fff;}}
 .rpt-table tbody tr:nth-child(even) td{{background:#f6f9fb;}}
 .rpt-summary th,.rpt-summary td{{background:#ebf0f5;font-weight:700;}}
 .rpt-footer{{margin-top:18px;font-size:11px;color:#888;font-style:italic;text-align:center;border-top:1px solid #ddd;padding-top:8px;}}
</style></head><body>
{sb}
</body></html>";

        return new FormattedExport(Encoding.UTF8.GetBytes(full), "text/html; charset=utf-8", "html");
    }

    private static string HtmlEscape(string? value)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;");
    }

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

    // ───── Helpers de diseño del informe ─────
    private static XLColor ParseColor(string? hex, XLColor fallback)
    {
        if (string.IsNullOrWhiteSpace(hex)) return fallback;
        var h = hex.Trim().TrimStart('#');
        if (h.Length == 6 && int.TryParse(h, System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out var rgb))
        {
            return XLColor.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
        }
        return fallback;
    }

    private static string SanitizeColor(string? hex, string fallback)
    {
        if (string.IsNullOrWhiteSpace(hex)) return fallback;
        var h = hex.Trim();
        if (!h.StartsWith("#")) h = "#" + h;
        if (h.Length != 7) return fallback;
        for (int i = 1; i < h.Length; i++)
        {
            var c = h[i];
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                return fallback;
        }
        return h;
    }

    private static byte[]? TryDecodeBase64Image(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var s = raw.Trim();
        var comma = s.IndexOf(',');
        if (s.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma > 0)
            s = s[(comma + 1)..];
        try { return Convert.FromBase64String(s); }
        catch { return null; }
    }

    private static string? NormalizeLogoDataUri(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var s = raw.Trim();
        if (s.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return s;
        // Asumimos PNG si no llega prefijo
        return "data:image/png;base64," + s;
    }

    /// <summary>
    /// Convierte <c>Metadata["appliedFilters"]</c> en lista de pares (etiqueta, valor)
    /// para mostrar arriba del informe. Acepta Dictionary o JsonElement.
    /// </summary>
    private static List<(string Label, string Value)> NormalizeAppliedFilters(object? raw)
    {
        var result = new List<(string, string)>();
        if (raw is null) return result;

        if (raw is JsonElement je && je.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in je.EnumerateObject())
                AddFilterEntry(result, prop.Name, JsonValueToString(prop.Value));
            return result;
        }
        if (raw is System.Collections.IDictionary dict)
        {
            foreach (System.Collections.DictionaryEntry e in dict)
            {
                var key = e.Key?.ToString() ?? "";
                // Los valores deserializados desde JSON llegan como JsonElement.
                // FormatScalar(JsonElement) imprime el JSON crudo; aquí pasamos
                // por JsonValueToString → FormatJsonObject para obtener un texto
                // legible (p.ej. "Últimas 24 h" o "2026-05-25 → 2026-05-26").
                var text = e.Value is JsonElement jev
                    ? JsonValueToString(jev)
                    : FormatScalar(e.Value);
                AddFilterEntry(result, key, text);
            }
            return result;
        }
        // Fallback: lo serializamos y volcamos como única entrada.
        var s = raw.ToString();
        if (!string.IsNullOrWhiteSpace(s)) result.Add(("filtros", s));
        return result;
    }

    /// <summary>
    /// Añade una entrada a la lista de filtros aplicados solo si tiene valor real.
    /// Aplica etiquetas legibles para claves técnicas conocidas.
    /// </summary>
    private static void AddFilterEntry(List<(string, string)> list, string key, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;            // omitir vacíos
        if (string.Equals(value, "null", StringComparison.OrdinalIgnoreCase)) return;
        var label = key switch
        {
            "dateRange" => "Rango de fechas",
            "dateFrom"   => "Desde",
            "dateTo"     => "Hasta",
            "groupId"    => "Grupo (id)",
            "groupName"  => "Grupo",
            "uiType"     => "Tipo de vista",
            _ => key
        };
        list.Add((label, value));
    }

    private static string JsonValueToString(JsonElement v) => v.ValueKind switch
    {
        JsonValueKind.String => v.GetString() ?? string.Empty,
        JsonValueKind.Number => v.ToString(),
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Null => string.Empty,
        JsonValueKind.Array => string.Join(", ", v.EnumerateArray().Select(JsonValueToString)),
        JsonValueKind.Object => FormatJsonObject(v),
        _ => v.ToString()
    };

    /// <summary>
    /// Renderiza objetos JSON dentro del bloque "filtros aplicados".
    /// Caso especial: rangos {from,to} se muestran como "from → to";
    /// si ambos están vacíos devuelve string vacío para que AddFilterEntry lo omita.
    /// El resto se renderiza como "k=v, k=v".
    /// </summary>
    private static string FormatJsonObject(JsonElement obj)
    {
        // Caso 1: rango relativo { mode:"relative", value:N, unit:"m|h|d" }
        if (obj.TryGetProperty("mode", out var modeEl)
            && modeEl.ValueKind == JsonValueKind.String
            && string.Equals(modeEl.GetString(), "relative", StringComparison.OrdinalIgnoreCase))
        {
            var v = obj.TryGetProperty("value", out var vEl) ? JsonValueToString(vEl) : "?";
            var u = obj.TryGetProperty("unit",  out var uEl) ? JsonValueToString(uEl) : "";
            var unitLabel = u switch
            {
                "m" => "min",
                "h" => "h",
                "d" => "días",
                _   => u
            };
            return $"Últimas {v} {unitLabel}".Trim();
        }

        // Caso 2: rango absoluto {from, to} (con o sin mode:"absolute")
        var hasFrom = obj.TryGetProperty("from", out var fromEl);
        var hasTo = obj.TryGetProperty("to", out var toEl);
        if (hasFrom || hasTo)
        {
            var from = hasFrom ? JsonValueToString(fromEl) : string.Empty;
            var to = hasTo ? JsonValueToString(toEl) : string.Empty;
            if (string.IsNullOrWhiteSpace(from) && string.IsNullOrWhiteSpace(to)) return string.Empty;
            if (string.IsNullOrWhiteSpace(from)) return $"… → {to}";
            if (string.IsNullOrWhiteSpace(to)) return $"{from} → …";
            return $"{from} → {to}";
        }
        var parts = new List<string>();
        foreach (var p in obj.EnumerateObject())
        {
            var val = JsonValueToString(p.Value);
            if (!string.IsNullOrWhiteSpace(val)) parts.Add($"{p.Name}={val}");
        }
        return string.Join(", ", parts);
    }

    /// <summary>
    /// Calcula agregaciones (SUM/AVG/MIN/MAX/COUNT) por columna. En modo "auto"
    /// detecta columnas numéricas; en "manual" usa <c>SummaryColumns</c>.
    /// Devuelve dict ordenado: nombre agregación → (columna → valor formateado).
    /// </summary>
    private static List<(string AggName, Dictionary<string, object?> Values)> BuildSummary(
        ExportDataset ds, ReportDesignConfig rpt)
    {
        var result = new List<(string, Dictionary<string, object?>)>();
        if (ds.Columns.Count == 0 || ds.Rows.Count == 0) return result;

        // 1) Detectar columnas numéricas candidatas
        var numericIdx = new List<int>();
        var numericData = new Dictionary<int, List<double>>();
        for (int c = 0; c < ds.Columns.Count; c++)
        {
            var values = new List<double>();
            foreach (var row in ds.Rows)
            {
                if (c >= row.Length) continue;
                if (TryToDouble(row[c], out var d)) values.Add(d);
            }
            if (values.Count > 0) { numericIdx.Add(c); numericData[c] = values; }
        }

        // 2) Filtrar por modo
        IEnumerable<int> selected = numericIdx;
        if (string.Equals(rpt.SummaryMode, "manual", StringComparison.OrdinalIgnoreCase)
            && rpt.SummaryColumns.Count > 0)
        {
            var wanted = new HashSet<string>(rpt.SummaryColumns, StringComparer.OrdinalIgnoreCase);
            // Algunos providers (p.ej. statistics.rows) cargan etiquetas en
            // ds.Columns pero el wizard guarda los IDs/keys de campo en
            // SummaryColumns. Para que el match funcione en ambas direcciones,
            // intentamos resolver por key alineado en ds.Metadata["columnKeys"].
            List<string>? colKeys = null;
            if (ds.Metadata.TryGetValue("columnKeys", out var rawKeys) && rawKeys is not null)
            {
                colKeys = rawKeys switch
                {
                    IEnumerable<string> es => es.ToList(),
                    System.Collections.IEnumerable en when rawKeys is not string
                        => en.Cast<object?>().Select(o => o?.ToString() ?? string.Empty).ToList(),
                    _ => null
                };
            }
            selected = numericIdx.Where(i =>
                wanted.Contains(ds.Columns[i])
                || (colKeys is not null && i < colKeys.Count && wanted.Contains(colKeys[i])));
        }
        var selectedList = selected.ToList();
        if (selectedList.Count == 0) return result;

        // 3) Calcular cada agregación pedida
        var aggs = rpt.SummaryAggregations.Count > 0
            ? rpt.SummaryAggregations
            : new List<string> { "sum", "avg" };

        foreach (var agg in aggs)
        {
            var values = new Dictionary<string, object?>();
            foreach (var c in selectedList)
            {
                var data = numericData[c];
                double? r = agg.ToLowerInvariant() switch
                {
                    "sum"   => data.Sum(),
                    "avg"   => data.Average(),
                    "min"   => data.Min(),
                    "max"   => data.Max(),
                    "count" => data.Count,
                    _ => (double?)null
                };
                if (r.HasValue)
                    values[ds.Columns[c]] = Math.Round(r.Value, 4);
            }
            if (values.Count > 0) result.Add((agg, values));
        }
        return result;
    }

    private static bool TryToDouble(object? v, out double d)
    {
        switch (v)
        {
            case null: d = 0; return false;
            case double dd: d = dd; return true;
            case float f: d = f; return true;
            case int i: d = i; return true;
            case long l: d = l; return true;
            case decimal dec: d = (double)dec; return true;
            case bool: d = 0; return false;
            case DateTime: d = 0; return false;
            default:
                var s = v.ToString();
                return double.TryParse(s, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out d);
        }
    }
}
