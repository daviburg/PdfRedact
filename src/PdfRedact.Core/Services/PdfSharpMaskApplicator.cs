using PdfRedact.Core.Models;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfSharp.Drawing;

namespace PdfRedact.Core.Services;

/// <summary>
/// Implementation of mask application using PDFsharp library.
/// </summary>
public class PdfSharpMaskApplicator : IMaskApplicator
{
    /// <inheritdoc/>
    public void ApplyMasks(RedactionPlan plan, string outputPath)
    {
        if (plan == null)
        {
            throw new ArgumentNullException(nameof(plan));
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("Output path cannot be null or empty", nameof(outputPath));
        }

        if (!File.Exists(plan.SourcePdfPath))
        {
            throw new FileNotFoundException("Source PDF file not found", plan.SourcePdfPath);
        }

        // Create output directory if it doesn't exist
        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        // Open the PDF document
        using var document = PdfReader.Open(plan.SourcePdfPath, PdfDocumentOpenMode.Modify);

        // Group regions by page for efficient processing
        var regionsByPage = plan.Regions.GroupBy(r => r.PageNumber);

        foreach (var pageGroup in regionsByPage)
        {
            var pageNumber = pageGroup.Key;

            // PDFsharp uses 0-based page indexing, but our model uses 1-based
            if (pageNumber < 1 || pageNumber > document.PageCount)
            {
                continue;
            }

            var page = document.Pages[pageNumber - 1];

            // Create graphics context for drawing
            using var gfx = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);

            // Apply each redaction region
            foreach (var region in pageGroup)
            {
                DrawRedactionMask(gfx, region, page);
            }
        }

        // Save the modified document
        document.Save(outputPath);
    }

    private void DrawRedactionMask(XGraphics gfx, RedactionRegion region, PdfPage page)
    {
        // Create a black brush for the redaction mask
        var brush = XBrushes.Black;

        // PDFsharp uses points as units, and coordinates start from top-left
        // We need to convert from PDF coordinates (bottom-left origin) to PDFsharp coordinates (top-left origin)
        var pageHeight = page.Height.Point;
        
        // Convert Y coordinate from bottom-left to top-left origin
        var y = pageHeight - region.Y - region.Height;

        // Draw a filled rectangle as the redaction mask
        gfx.DrawRectangle(brush, region.X, y, region.Width, region.Height);
    }
}
