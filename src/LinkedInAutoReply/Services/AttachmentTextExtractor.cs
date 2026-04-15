using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace LinkedInAutoReply.Services;

/// <summary>
/// Extracts plain text from PDF and Word (.docx / .doc) attachments.
/// </summary>
public class AttachmentTextExtractor(ILogger<AttachmentTextExtractor> logger)
{
    public string ExtractText(string fileName, byte[] content)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        try
        {
            return ext switch
            {
                ".pdf" => ExtractPdf(content),
                ".docx" => ExtractDocx(content),
                ".doc" => $"[.doc format not supported — attachment: {fileName}]",
                _ => string.Empty
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to extract text from {FileName}", fileName);
            return $"[Attachment '{fileName}' could not be extracted: {ex.Message}]";
        }
    }

    private static string ExtractPdf(byte[] content)
    {
        var sb = new StringBuilder();
        using var pdf = PdfDocument.Open(content);
        foreach (Page page in pdf.GetPages())
            sb.AppendLine(page.Text);
        return sb.ToString().Trim();
    }

    private static string ExtractDocx(byte[] content)
    {
        using var stream = new MemoryStream(content);
        using var doc = WordprocessingDocument.Open(stream, isEditable: false);

        var body = doc.MainDocumentPart?.Document?.Body;
        if (body == null) return string.Empty;

        var sb = new StringBuilder();
        foreach (var para in body.Descendants<Paragraph>())
        {
            var line = para.InnerText.Trim();
            if (line.Length > 0)
                sb.AppendLine(line);
        }
        return sb.ToString().Trim();
    }
}
