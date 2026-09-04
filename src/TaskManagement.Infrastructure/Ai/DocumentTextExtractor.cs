using System.Text;
using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using TaskManagement.Application.Abstractions;
using UglyToad.PdfPig;

namespace TaskManagement.Infrastructure.Ai;

/// <summary>
/// Pulls plain text out of an uploaded .xlsx/.pdf/.docx so it can be handed to the AI assistant as extra
/// drafting context. Cell/paragraph text only: a flowchart drawn with Excel shapes/text boxes, for
/// instance, has no cell value to read and won't be captured.
/// </summary>
public sealed class DocumentTextExtractor : IDocumentTextExtractor
{
    private const int MaxChars = 20_000;

    public Task<string> ExtractAsync(Stream content, string fileName, CancellationToken ct = default)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var text = extension switch
        {
            ".xlsx" => ExtractXlsx(content),
            ".pdf" => ExtractPdf(content),
            ".docx" => ExtractDocx(content),
            _ => throw new NotSupportedException($"Unsupported file type '{extension}'. Use .xlsx, .pdf or .docx."),
        };

        return Task.FromResult(Cap(text));
    }

    private static string ExtractXlsx(Stream content)
    {
        using var workbook = new XLWorkbook(content);
        var sb = new StringBuilder();

        foreach (var sheet in workbook.Worksheets)
        {
            var range = sheet.RangeUsed();
            if (range is null) continue;

            sb.AppendLine($"Sheet: {sheet.Name}");
            foreach (var row in range.RowsUsed())
            {
                var cells = row.CellsUsed().Select(c => c.GetFormattedString().Trim()).Where(v => v.Length > 0);
                var line = string.Join('\t', cells);
                if (line.Length > 0)
                    sb.AppendLine(line);
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string ExtractPdf(Stream content)
    {
        using var document = PdfDocument.Open(content);
        var sb = new StringBuilder();
        foreach (var page in document.GetPages())
            sb.AppendLine(page.Text);

        return sb.ToString();
    }

    private static string ExtractDocx(Stream content)
    {
        using var document = WordprocessingDocument.Open(content, isEditable: false);
        return document.MainDocumentPart?.Document?.Body?.InnerText ?? "";
    }

    private static string Cap(string text)
        => text.Length <= MaxChars ? text : text[..MaxChars] + "\n… (truncated)";
}
