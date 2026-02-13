// ============================================================================
// DocumentExportService.cs - Conversión de Markdown a PDF y DOCX
// ============================================================================
// Utiliza QuestPDF para PDF y DocumentFormat.OpenXml para DOCX.
// Parsea el AST de Markdig para generar documentos con formato correcto:
// headings, párrafos, bold/italic, listas, bloques de código, tablas, etc.
// ============================================================================

using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

// Alias para evitar conflictos con Bold/Italic/etc. de OpenXml vs QuestPDF
using OxBold = DocumentFormat.OpenXml.Wordprocessing.Bold;
using OxItalic = DocumentFormat.OpenXml.Wordprocessing.Italic;
using OxColor = DocumentFormat.OpenXml.Wordprocessing.Color;
using OxUnderline = DocumentFormat.OpenXml.Wordprocessing.Underline;
using OxBreak = DocumentFormat.OpenXml.Wordprocessing.Break;
using OxRun = DocumentFormat.OpenXml.Wordprocessing.Run;
using OxText = DocumentFormat.OpenXml.Wordprocessing.Text;
using OxDocument = DocumentFormat.OpenXml.Wordprocessing.Document;
using OxTable = DocumentFormat.OpenXml.Wordprocessing.Table;
using OxTableRow = DocumentFormat.OpenXml.Wordprocessing.TableRow;
using OxTableCell = DocumentFormat.OpenXml.Wordprocessing.TableCell;
using MdTable = Markdig.Extensions.Tables.Table;
using MdTableRow = Markdig.Extensions.Tables.TableRow;
using MdTableCell = Markdig.Extensions.Tables.TableCell;

namespace SW.PC.API.Backend.Services;

/// <summary>
/// Interfaz para exportar contenido Markdown a PDF y DOCX.
/// </summary>
public interface IDocumentExportService
{
    /// <summary>Convertir Markdown a PDF. Devuelve MemoryStream posicionado en 0.</summary>
    Stream ExportToPdf(string markdownContent, string title);

    /// <summary>Convertir Markdown a DOCX. Devuelve MemoryStream posicionado en 0.</summary>
    Stream ExportToDocx(string markdownContent, string title);

    /// <summary>Convertir un fichero DOCX a HTML para previsualización inline.</summary>
    string ConvertDocxToHtml(string docxPath);

    /// <summary>Convertir un stream DOCX a HTML para previsualización inline.</summary>
    string ConvertDocxToHtml(Stream docxStream);

    /// <summary>Convertir un stream DOCX a Markdown para importación.</summary>
    string ConvertDocxToMarkdown(Stream docxStream);
}

/// <summary>
/// Implementación: parsea Markdown con Markdig AST y genera PDF (QuestPDF) / DOCX (OpenXml).
/// </summary>
public class DocumentExportService : IDocumentExportService
{
    private readonly ILogger<DocumentExportService> _logger;

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseEmojiAndSmiley()
        .UseAutoLinks()
        .Build();

    public DocumentExportService(ILogger<DocumentExportService> logger)
    {
        _logger = logger;
    }

    // ════════════════════════════════════════════════════════════════════════
    //  PDF — QuestPDF
    // ════════════════════════════════════════════════════════════════════════

    public Stream ExportToPdf(string markdownContent, string title)
    {
        var ms = new MemoryStream();
        var mdDoc = Markdown.Parse(markdownContent ?? "", Pipeline);

        QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Segoe UI"));

                // Header
                page.Header()
                    .BorderBottom(1).BorderColor(Colors.Grey.Medium)
                    .PaddingBottom(5)
                    .Text(title)
                    .FontSize(9).FontColor(Colors.Grey.Medium);

                // Content
                page.Content().PaddingTop(10).Column(col =>
                {
                    col.Spacing(4);
                    foreach (var block in mdDoc)
                        RenderPdfBlock(col, block, 0);
                });

                // Footer
                page.Footer()
                    .AlignCenter()
                    .DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Grey.Medium))
                    .Text(text =>
                    {
                        text.Span("Página ");
                        text.CurrentPageNumber();
                        text.Span(" / ");
                        text.TotalPages();
                    });
            });
        }).GeneratePdf(ms);

        ms.Position = 0;
        return ms;
    }

    private void RenderPdfBlock(ColumnDescriptor col, Block block, int indent)
    {
        switch (block)
        {
            case HeadingBlock heading:
                var hFontSize = heading.Level switch
                {
                    1 => 24f, 2 => 20f, 3 => 16f, 4 => 14f, _ => 12f
                };
                col.Item().PaddingTop(heading.Level <= 2 ? 14 : 6).Text(text =>
                {
                    RenderPdfInlines(text, heading.Inline, hFontSize, isBold: true, isItalic: false);
                });
                break;

            case ParagraphBlock paragraph:
                col.Item().PaddingLeft(indent).Text(text =>
                {
                    RenderPdfInlines(text, paragraph.Inline, 11, isBold: false, isItalic: false);
                });
                break;

            case ListBlock list:
                int idx = 1;
                foreach (var item in list)
                {
                    if (item is ListItemBlock listItem)
                    {
                        foreach (var sub in listItem)
                        {
                            if (sub is ParagraphBlock p)
                            {
                                var bullet = list.IsOrdered ? $"{idx}. " : "• ";
                                col.Item().PaddingLeft(indent + 15).Text(text =>
                                {
                                    text.Span(bullet);
                                    RenderPdfInlines(text, p.Inline, 11, false, false);
                                });
                            }
                            else
                            {
                                RenderPdfBlock(col, sub, indent + 15);
                            }
                        }
                        idx++;
                    }
                }
                break;

            case FencedCodeBlock fenced:
                RenderPdfCodeBlock(col, fenced);
                break;

            case CodeBlock code:
                RenderPdfCodeBlock(col, code);
                break;

            case ThematicBreakBlock:
                col.Item().PaddingVertical(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                break;

            case QuoteBlock quote:
                col.Item()
                   .BorderLeft(3).BorderColor(Colors.Grey.Medium)
                   .PaddingLeft(10)
                   .Column(inner =>
                   {
                       inner.Spacing(3);
                       foreach (var child in quote)
                           RenderPdfBlock(inner, child, 0);
                   });
                break;

            case MdTable table:
                RenderPdfTable(col, table);
                break;
        }
    }

    private void RenderPdfCodeBlock(ColumnDescriptor col, CodeBlock codeBlock)
    {
        var code = GetCodeBlockText(codeBlock);
        col.Item()
           .Background(Colors.Grey.Lighten4)
           .Border(1).BorderColor(Colors.Grey.Lighten2)
           .Padding(8)
           .Text(text =>
           {
               text.DefaultTextStyle(x => x.FontFamily("Consolas").FontSize(9).FontColor(Colors.Grey.Darken3));
               text.Span(code);
           });
    }

    private void RenderPdfInlines(TextDescriptor text, ContainerInline? inlines, float fontSize, bool isBold, bool isItalic)
    {
        if (inlines == null) return;

        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    var span = text.Span(literal.Content.ToString()).FontSize(fontSize);
                    if (isBold) span.Bold();
                    if (isItalic) span.Italic();
                    break;

                case EmphasisInline emphasis:
                    bool emphBold = emphasis.DelimiterCount >= 2;
                    bool emphItalic = emphasis.DelimiterCount == 1 || emphasis.DelimiterCount == 3;
                    RenderPdfInlines(text, emphasis, fontSize, isBold || emphBold, isItalic || emphItalic);
                    break;

                case CodeInline code:
                    text.Span(code.Content)
                        .FontSize(fontSize - 1)
                        .FontFamily("Consolas")
                        .BackgroundColor(Colors.Grey.Lighten4);
                    break;

                case LinkInline link:
                    var linkText = GetInlineText(link);
                    text.Span(linkText).FontSize(fontSize).FontColor(Colors.Blue.Medium).Underline();
                    break;

                case LineBreakInline:
                    text.Span("\n");
                    break;
            }
        }
    }

    private void RenderPdfTable(ColumnDescriptor col, MdTable table)
    {
        var firstRow = table.FirstOrDefault() as MdTableRow;
        if (firstRow == null) return;
        var colCount = firstRow.Count;

        col.Item().Table(t =>
        {
            t.ColumnsDefinition(columns =>
            {
                for (int i = 0; i < colCount; i++)
                    columns.RelativeColumn();
            });

            bool isHeader = true;
            foreach (var row in table)
            {
                if (row is MdTableRow tableRow)
                {
                    foreach (var cell in tableRow)
                    {
                        if (cell is MdTableCell tableCell)
                        {
                            var cellText = GetBlockText(tableCell);
                            var cellEl = t.Cell()
                                .Border(1).BorderColor(Colors.Grey.Lighten2)
                                .Padding(4);

                            if (isHeader)
                                cellEl.Background(Colors.Grey.Lighten3)
                                      .Text(cellText).FontSize(10).Bold();
                            else
                                cellEl.Text(cellText).FontSize(10);
                        }
                    }
                    isHeader = false;
                }
            }
        });
    }

    // ════════════════════════════════════════════════════════════════════════
    //  DOCX — DocumentFormat.OpenXml
    // ════════════════════════════════════════════════════════════════════════

    public Stream ExportToDocx(string markdownContent, string title)
    {
        var ms = new MemoryStream();
        var mdDoc = Markdown.Parse(markdownContent ?? "", Pipeline);

        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
        {
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new OxDocument(new Body());
            AddDocxStyles(mainPart);

            var body = mainPart.Document.Body!;

            // Título del documento
            var titlePara = new Paragraph();
            titlePara.ParagraphProperties = new ParagraphProperties(
                new SpacingBetweenLines { After = "200" }
            );
            var titleRun = new OxRun();
            titleRun.RunProperties = new RunProperties(
                new OxBold(),
                new FontSize { Val = "48" },  // half-points → 24pt
                new OxColor { Val = "1F3864" }
            );
            titleRun.AppendChild(new OxText(title));
            titlePara.AppendChild(titleRun);
            body.AppendChild(titlePara);

            // Línea separadora bajo el título
            body.AppendChild(new Paragraph(new ParagraphProperties(
                new ParagraphBorders(
                    new BottomBorder { Val = BorderValues.Single, Size = 6, Color = "4472C4" }
                ),
                new SpacingBetweenLines { After = "200" }
            )));

            foreach (var block in mdDoc)
                RenderDocxBlock(body, block, 0);
        }

        ms.Position = 0;
        return ms;
    }

    private void AddDocxStyles(MainDocumentPart mainPart)
    {
        var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
        var styles = new Styles();

        for (int i = 1; i <= 6; i++)
        {
            var (fontSize, color) = i switch
            {
                1 => ("48", "1F3864"),
                2 => ("40", "2E5090"),
                3 => ("32", "2E5090"),
                4 => ("28", "404040"),
                5 => ("24", "404040"),
                _ => ("22", "404040")
            };

            var style = new Style
            {
                Type = StyleValues.Paragraph,
                StyleId = $"Heading{i}",
                CustomStyle = true
            };
            style.AppendChild(new StyleName { Val = $"heading {i}" });
            style.AppendChild(new StyleRunProperties(
                new OxBold(),
                new FontSize { Val = fontSize },
                new OxColor { Val = color }
            ));
            styles.AppendChild(style);
        }

        stylesPart.Styles = styles;
    }

    private void RenderDocxBlock(Body body, Block block, int indentLevel)
    {
        switch (block)
        {
            case HeadingBlock heading:
                var hPara = new Paragraph();
                hPara.ParagraphProperties = new ParagraphProperties(
                    new ParagraphStyleId { Val = $"Heading{heading.Level}" },
                    new SpacingBetweenLines { Before = heading.Level <= 2 ? "240" : "120", After = "80" }
                );
                AppendDocxInlines(hPara, heading.Inline, headingBold: true, isItalic: false);
                body.AppendChild(hPara);
                break;

            case ParagraphBlock paragraph:
                var para = new Paragraph();
                if (indentLevel > 0)
                {
                    para.ParagraphProperties = new ParagraphProperties(
                        new Indentation { Left = (indentLevel * 720).ToString() }
                    );
                }
                AppendDocxInlines(para, paragraph.Inline, false, false);
                body.AppendChild(para);
                break;

            case ListBlock list:
                int idx = 1;
                foreach (var item in list)
                {
                    if (item is ListItemBlock listItem)
                    {
                        foreach (var sub in listItem)
                        {
                            if (sub is ParagraphBlock p)
                            {
                                var lPara = new Paragraph();
                                lPara.ParagraphProperties = new ParagraphProperties(
                                    new Indentation { Left = ((indentLevel + 1) * 720).ToString() },
                                    new SpacingBetweenLines { After = "40" }
                                );
                                var bullet = list.IsOrdered ? $"{idx}. " : "•  ";
                                var bulletRun = new OxRun(
                                    new OxText(bullet) { Space = SpaceProcessingModeValues.Preserve }
                                );
                                lPara.AppendChild(bulletRun);
                                AppendDocxInlines(lPara, p.Inline, false, false);
                                body.AppendChild(lPara);
                            }
                            else
                            {
                                RenderDocxBlock(body, sub, indentLevel + 1);
                            }
                        }
                        idx++;
                    }
                }
                break;

            case FencedCodeBlock fenced:
                RenderDocxCodeBlock(body, fenced);
                break;

            case CodeBlock code:
                RenderDocxCodeBlock(body, code);
                break;

            case ThematicBreakBlock:
                body.AppendChild(new Paragraph(new ParagraphProperties(
                    new ParagraphBorders(
                        new BottomBorder { Val = BorderValues.Single, Size = 4, Color = "999999" }
                    ),
                    new SpacingBetweenLines { Before = "120", After = "120" }
                )));
                break;

            case QuoteBlock quote:
                foreach (var child in quote)
                {
                    if (child is ParagraphBlock qp)
                    {
                        var qPara = new Paragraph();
                        qPara.ParagraphProperties = new ParagraphProperties(
                            new Indentation { Left = "720" },
                            new ParagraphBorders(
                                new LeftBorder { Val = BorderValues.Single, Size = 12, Color = "4472C4", Space = 8 }
                            ),
                            new SpacingBetweenLines { After = "60" }
                        );
                        var rp = new RunProperties(new OxItalic(), new OxColor { Val = "555555" });
                        AppendDocxInlinesWithDefaults(qPara, qp.Inline, rp);
                        body.AppendChild(qPara);
                    }
                    else
                    {
                        RenderDocxBlock(body, child, indentLevel + 1);
                    }
                }
                break;

            case MdTable table:
                RenderDocxTable(body, table);
                break;
        }
    }

    private void RenderDocxCodeBlock(Body body, CodeBlock codeBlock)
    {
        var code = GetCodeBlockText(codeBlock);
        var lines = code.Split('\n');

        var codePara = new Paragraph();
        codePara.ParagraphProperties = new ParagraphProperties(
            new Shading { Val = ShadingPatternValues.Clear, Fill = "F2F2F2" },
            new ParagraphBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4, Color = "D9D9D9" },
                new BottomBorder { Val = BorderValues.Single, Size = 4, Color = "D9D9D9" },
                new LeftBorder { Val = BorderValues.Single, Size = 4, Color = "D9D9D9" },
                new RightBorder { Val = BorderValues.Single, Size = 4, Color = "D9D9D9" }
            ),
            new SpacingBetweenLines { Before = "60", After = "60" }
        );

        var codeRun = new OxRun();
        codeRun.RunProperties = new RunProperties(
            new RunFonts { Ascii = "Consolas", HighAnsi = "Consolas" },
            new FontSize { Val = "18" }
        );

        for (int i = 0; i < lines.Length; i++)
        {
            codeRun.AppendChild(new OxText(lines[i]) { Space = SpaceProcessingModeValues.Preserve });
            if (i < lines.Length - 1)
                codeRun.AppendChild(new OxBreak());
        }

        codePara.AppendChild(codeRun);
        body.AppendChild(codePara);
    }

    private void AppendDocxInlines(Paragraph para, ContainerInline? inlines, bool headingBold, bool isItalic)
    {
        if (inlines == null) return;

        foreach (var inline in inlines)
        {
            switch (inline)
            {
                case LiteralInline literal:
                    var run = new OxRun();
                    var rp = new RunProperties();
                    if (headingBold) rp.AppendChild(new OxBold());
                    if (isItalic) rp.AppendChild(new OxItalic());
                    if (rp.HasChildren) run.RunProperties = rp;
                    run.AppendChild(new OxText(literal.Content.ToString()) { Space = SpaceProcessingModeValues.Preserve });
                    para.AppendChild(run);
                    break;

                case EmphasisInline emphasis:
                    bool emphBold = emphasis.DelimiterCount >= 2;
                    bool emphItalic = emphasis.DelimiterCount == 1 || emphasis.DelimiterCount == 3;
                    AppendDocxInlines(para, emphasis, headingBold || emphBold, isItalic || emphItalic);
                    break;

                case CodeInline code:
                    var cRun = new OxRun();
                    cRun.RunProperties = new RunProperties(
                        new RunFonts { Ascii = "Consolas", HighAnsi = "Consolas" },
                        new FontSize { Val = "20" },
                        new Shading { Val = ShadingPatternValues.Clear, Fill = "F2F2F2" }
                    );
                    cRun.AppendChild(new OxText(code.Content) { Space = SpaceProcessingModeValues.Preserve });
                    para.AppendChild(cRun);
                    break;

                case LinkInline link:
                    var linkText = GetInlineText(link);
                    var linkRun = new OxRun();
                    linkRun.RunProperties = new RunProperties(
                        new OxColor { Val = "0563C1" },
                        new OxUnderline { Val = UnderlineValues.Single }
                    );
                    linkRun.AppendChild(new OxText(linkText) { Space = SpaceProcessingModeValues.Preserve });
                    para.AppendChild(linkRun);
                    break;

                case LineBreakInline:
                    para.AppendChild(new OxRun(new OxBreak()));
                    break;
            }
        }
    }

    /// <summary>Append inlines con RunProperties por defecto (usadas en blockquotes)</summary>
    private void AppendDocxInlinesWithDefaults(Paragraph para, ContainerInline? inlines, RunProperties defaults)
    {
        if (inlines == null) return;
        foreach (var inline in inlines)
        {
            if (inline is LiteralInline literal)
            {
                var run = new OxRun();
                run.RunProperties = (RunProperties)defaults.CloneNode(true);
                run.AppendChild(new OxText(literal.Content.ToString()) { Space = SpaceProcessingModeValues.Preserve });
                para.AppendChild(run);
            }
            else if (inline is EmphasisInline emphasis)
            {
                AppendDocxInlinesWithDefaults(para, emphasis, defaults);
            }
            else if (inline is CodeInline code)
            {
                var run = new OxRun();
                var rp = (RunProperties)defaults.CloneNode(true);
                rp.AppendChild(new RunFonts { Ascii = "Consolas", HighAnsi = "Consolas" });
                run.RunProperties = rp;
                run.AppendChild(new OxText(code.Content) { Space = SpaceProcessingModeValues.Preserve });
                para.AppendChild(run);
            }
        }
    }

    private void RenderDocxTable(Body body, MdTable table)
    {
        var oxTable = new OxTable();
        var tblPr = new TableProperties(
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4, Color = "999999" },
                new BottomBorder { Val = BorderValues.Single, Size = 4, Color = "999999" },
                new LeftBorder { Val = BorderValues.Single, Size = 4, Color = "999999" },
                new RightBorder { Val = BorderValues.Single, Size = 4, Color = "999999" },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = "999999" },
                new InsideVerticalBorder { Val = BorderValues.Single, Size = 4, Color = "999999" }
            ),
            new TableWidth { Type = TableWidthUnitValues.Pct, Width = "5000" }
        );
        oxTable.AppendChild(tblPr);

        bool isHeader = true;
        foreach (var row in table)
        {
            if (row is MdTableRow tableRow)
            {
                var tr = new OxTableRow();
                foreach (var cell in tableRow)
                {
                    if (cell is MdTableCell tableCell)
                    {
                        var tc = new OxTableCell();
                        var cellText = GetBlockText(tableCell);
                        var p = new Paragraph();
                        var r = new OxRun();

                        if (isHeader)
                        {
                            r.RunProperties = new RunProperties(new OxBold());
                            tc.TableCellProperties = new TableCellProperties(
                                new Shading { Val = ShadingPatternValues.Clear, Fill = "D9E2F3" }
                            );
                        }

                        r.AppendChild(new OxText(cellText) { Space = SpaceProcessingModeValues.Preserve });
                        p.AppendChild(r);
                        tc.AppendChild(p);
                        tr.AppendChild(tc);
                    }
                }
                oxTable.AppendChild(tr);
                isHeader = false;
            }
        }

        body.AppendChild(oxTable);
        body.AppendChild(new Paragraph()); // espacio después de tabla
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Helpers — extracción de texto del AST de Markdig
    // ════════════════════════════════════════════════════════════════════════

    private static string GetCodeBlockText(CodeBlock codeBlock)
    {
        var sb = new StringBuilder();
        if (codeBlock.Lines.Lines != null)
        {
            foreach (var line in codeBlock.Lines.Lines)
            {
                if (line.Slice.Text != null)
                    sb.AppendLine(line.Slice.ToString());
            }
        }
        return sb.ToString().TrimEnd();
    }

    private static string GetBlockText(ContainerBlock container)
    {
        var sb = new StringBuilder();
        foreach (var block in container)
        {
            if (block is ParagraphBlock p && p.Inline != null)
                sb.Append(GetInlineText(p.Inline));
        }
        return sb.ToString();
    }

    private static string GetInlineText(ContainerInline container)
    {
        var sb = new StringBuilder();
        foreach (var inline in container)
        {
            if (inline is LiteralInline literal)
                sb.Append(literal.Content);
            else if (inline is CodeInline code)
                sb.Append(code.Content);
            else if (inline is EmphasisInline emphasis)
                sb.Append(GetInlineText(emphasis));
            else if (inline is LinkInline link)
                sb.Append(GetInlineText(link));
        }
        return sb.ToString();
    }

    // ════════════════════════════════════════════════════════════════════════
    //  DOCX → HTML — Previsualización inline
    // ════════════════════════════════════════════════════════════════════════

    public string ConvertDocxToHtml(string docxPath)
    {
        try
        {
            using var stream = new FileStream(docxPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return ConvertDocxToHtml(stream);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error convirtiendo DOCX a HTML: {Path}", docxPath);
            return $"<p style=\"color:#f87171;\">Error leyendo DOCX: {System.Web.HttpUtility.HtmlEncode(ex.Message)}</p>";
        }
    }

    public string ConvertDocxToHtml(Stream docxStream)
    {
        try
        {
            using var doc = WordprocessingDocument.Open(docxStream, false);
            var body = doc.MainDocumentPart?.Document.Body;
            if (body == null) return "<p><em>Documento vacío</em></p>";

            var sb = new StringBuilder();
            foreach (var element in body.Elements())
            {
                switch (element)
                {
                    case Paragraph para:
                        RenderDocxParaToHtml(sb, para);
                        break;
                    case OxTable table:
                        RenderDocxTableToHtml(sb, table);
                        break;
                }
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error convirtiendo DOCX stream a HTML");
            return $"<p style=\"color:#f87171;\">Error leyendo DOCX: {System.Web.HttpUtility.HtmlEncode(ex.Message)}</p>";
        }
    }

    private void RenderDocxParaToHtml(StringBuilder sb, Paragraph para)
    {
        // Detectar heading por styleId
        var styleId = para.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        var tag = "p";
        if (!string.IsNullOrEmpty(styleId))
        {
            var lower = styleId.ToLower();
            if (lower.StartsWith("heading") || lower.StartsWith("ttulo"))
            {
                var lastChar = lower[^1];
                if (char.IsDigit(lastChar) && lastChar >= '1' && lastChar <= '6')
                    tag = $"h{lastChar}";
            }
        }

        // Comprobar si el párrafo tiene texto
        var runs = para.Elements<OxRun>().ToList();
        if (runs.Count == 0 && !para.Elements<Hyperlink>().Any())
        {
            sb.AppendLine("<br/>");
            return;
        }

        sb.Append($"<{tag}>");
        foreach (var run in runs)
        {
            var text = run.InnerText;
            if (string.IsNullOrEmpty(text)) continue;

            var rp = run.RunProperties;
            var encoded = System.Web.HttpUtility.HtmlEncode(text);

            bool isBold = rp?.GetFirstChild<OxBold>() != null;
            bool isItalic = rp?.GetFirstChild<OxItalic>() != null;
            bool isUnderline = rp?.GetFirstChild<OxUnderline>() != null;
            var fontFamily = rp?.GetFirstChild<RunFonts>()?.Ascii?.Value;
            bool isCode = fontFamily != null && (fontFamily.Contains("Consolas") || fontFamily.Contains("Courier"));

            if (isCode) sb.Append("<code>");
            if (isBold) sb.Append("<strong>");
            if (isItalic) sb.Append("<em>");
            if (isUnderline) sb.Append("<u>");

            sb.Append(encoded);

            if (isUnderline) sb.Append("</u>");
            if (isItalic) sb.Append("</em>");
            if (isBold) sb.Append("</strong>");
            if (isCode) sb.Append("</code>");
        }
        sb.AppendLine($"</{tag}>");
    }

    private void RenderDocxTableToHtml(StringBuilder sb, OxTable table)
    {
        sb.AppendLine("<table style=\"border-collapse:collapse;width:100%;margin:12px 0;\">");
        bool isFirstRow = true;
        foreach (var row in table.Elements<OxTableRow>())
        {
            sb.Append("<tr>");
            var cellTag = isFirstRow ? "th" : "td";
            foreach (var cell in row.Elements<OxTableCell>())
            {
                var cellText = System.Web.HttpUtility.HtmlEncode(cell.InnerText);
                var bgStyle = isFirstRow
                    ? "background:rgba(100,130,200,0.15);font-weight:bold;"
                    : "";
                sb.Append($"<{cellTag} style=\"border:1px solid rgba(255,255,255,0.15);padding:6px 10px;{bgStyle}\">{cellText}</{cellTag}>");
            }
            sb.AppendLine("</tr>");
            isFirstRow = false;
        }
        sb.AppendLine("</table>");
    }

    // ═══════════════════════════════════════════════════════════════════
    // DOCX → Markdown (importación)
    // ═══════════════════════════════════════════════════════════════════

    public string ConvertDocxToMarkdown(Stream docxStream)
    {
        try
        {
            using var doc = WordprocessingDocument.Open(docxStream, false);
            var body = doc.MainDocumentPart?.Document.Body;
            if (body == null) return "";

            var sb = new StringBuilder();
            foreach (var element in body.Elements())
            {
                switch (element)
                {
                    case Paragraph para:
                        RenderDocxParaToMarkdown(sb, para);
                        break;
                    case OxTable table:
                        RenderDocxTableToMarkdown(sb, table);
                        break;
                }
            }
            return sb.ToString().TrimEnd();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error convirtiendo DOCX stream a Markdown");
            throw;
        }
    }

    private void RenderDocxParaToMarkdown(StringBuilder sb, Paragraph para)
    {
        // Detectar heading por styleId
        var styleId = para.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
        int headingLevel = 0;
        if (!string.IsNullOrEmpty(styleId))
        {
            var lower = styleId.ToLower();
            if (lower.StartsWith("heading") || lower.StartsWith("ttulo"))
            {
                var lastChar = lower[^1];
                if (char.IsDigit(lastChar) && lastChar >= '1' && lastChar <= '6')
                    headingLevel = lastChar - '0';
            }
        }

        // Detectar lista
        var numId = para.ParagraphProperties?.NumberingProperties?.NumberingId?.Val?.Value;
        var ilvl = para.ParagraphProperties?.NumberingProperties?.NumberingLevelReference?.Val?.Value;
        bool isList = numId != null;

        var runs = para.Elements<OxRun>().ToList();
        if (runs.Count == 0)
        {
            sb.AppendLine();
            return;
        }

        // Construir texto con formato inline
        var textSb = new StringBuilder();
        foreach (var run in runs)
        {
            var text = run.InnerText;
            if (string.IsNullOrEmpty(text)) continue;

            var rp = run.RunProperties;
            bool isBold = rp?.GetFirstChild<OxBold>() != null;
            bool isItalic = rp?.GetFirstChild<OxItalic>() != null;
            var fontFamily = rp?.GetFirstChild<RunFonts>()?.Ascii?.Value;
            bool isCode = fontFamily != null && (fontFamily.Contains("Consolas") || fontFamily.Contains("Courier"));

            if (isCode) textSb.Append('`');
            if (isBold && isItalic) textSb.Append("***");
            else if (isBold) textSb.Append("**");
            else if (isItalic) textSb.Append('*');

            textSb.Append(text);

            if (isBold && isItalic) textSb.Append("***");
            else if (isBold) textSb.Append("**");
            else if (isItalic) textSb.Append('*');
            if (isCode) textSb.Append('`');
        }

        var lineText = textSb.ToString();
        if (string.IsNullOrWhiteSpace(lineText))
        {
            sb.AppendLine();
            return;
        }

        if (headingLevel > 0)
        {
            sb.Append(new string('#', headingLevel));
            sb.Append(' ');
            sb.AppendLine(lineText);
            sb.AppendLine();
        }
        else if (isList)
        {
            var indent = new string(' ', (ilvl ?? 0) * 2);
            sb.AppendLine($"{indent}- {lineText}");
        }
        else
        {
            sb.AppendLine(lineText);
            sb.AppendLine();
        }
    }

    private void RenderDocxTableToMarkdown(StringBuilder sb, OxTable table)
    {
        var rows = table.Elements<OxTableRow>().ToList();
        if (rows.Count == 0) return;

        sb.AppendLine();
        bool isFirstRow = true;
        foreach (var row in rows)
        {
            var cells = row.Elements<OxTableCell>().Select(c => c.InnerText.Trim()).ToList();
            sb.AppendLine("| " + string.Join(" | ", cells) + " |");
            if (isFirstRow)
            {
                sb.AppendLine("| " + string.Join(" | ", cells.Select(_ => "---")) + " |");
                isFirstRow = false;
            }
        }
        sb.AppendLine();
    }
}
