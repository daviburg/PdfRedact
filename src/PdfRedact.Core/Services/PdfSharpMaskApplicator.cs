using PdfRedact.Core.Models;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using PdfSharp.Drawing;

namespace PdfRedact.Core.Services;

/// <summary>
/// Implementation of mask application using PDFsharp library.
/// Applies opaque masks to redaction regions following the coordinate contract:
/// - Coordinates are in PDF user space (bottom-left origin, measured in points)
/// - Page numbers are 1-based
/// - Units are points (1/72 inch)
/// </summary>
public class PdfSharpMaskApplicator : IMaskApplicator
{
    private const double MaskPadding = 1.0; // Points to expand mask on all sides to avoid anti-aliasing artifacts

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

        // Group regions by page and order them deterministically
        var regionsByPage = plan.Regions
            .GroupBy(region => region.PageNumber)
            .OrderBy(group => group.Key);

        foreach (var pageGroup in regionsByPage)
        {
            var pageNumber = pageGroup.Key;

            // PDFsharp uses 0-based page indexing, but our model uses 1-based
            if (pageNumber < 1 || pageNumber > document.PageCount)
            {
                continue;
            }

            var page = document.Pages[pageNumber - 1];

            // Order regions within page: Y descending (top to bottom), then X ascending (left to right)
            var orderedRegions = pageGroup
                .OrderByDescending(region => region.Y)
                .ThenBy(region => region.X)
                .ToList();

            // Create graphics context for drawing
            using var graphics = XGraphics.FromPdfPage(page, XGraphicsPdfPageOptions.Append);

            // Apply each redaction region
            foreach (var region in orderedRegions)
            {
                DrawRedactionMask(graphics, region, page);
            }
        }

        // Save the modified document
        document.Save(outputPath);
    }

    /// <summary>
    /// Draws a redaction mask for a single region.
    /// Handles coordinate conversion and applies padding to prevent text leakage.
    /// </summary>
    /// <remarks>
    /// Limitations:
    /// - CropBox/MediaBox offsets are not currently handled (assumes zero origin)
    /// - Page rotation support is fail-fast (throws on rotated pages)
    /// </remarks>
    private void DrawRedactionMask(XGraphics graphics, RedactionRegion region, PdfPage page)
    {
        // Fail fast on rotated pages - coordinate transform not yet implemented
        if (region.PageRotation != 0)
        {
            throw new NotSupportedException(
                $"Page rotation ({region.PageRotation}°) is not currently supported. " +
                "Redaction masks cannot be correctly applied to rotated pages. " +
                "Please rotate the PDF to 0° before applying redactions.");
        }

        // Create a black brush for the redaction mask
        var brush = XBrushes.Black;

        // PDFsharp uses points as units, and coordinates start from top-left
        // We need to convert from PDF coordinates (bottom-left origin) to PDFsharp coordinates (top-left origin)
        var pageHeight = page.Height.Point;
        
        // Apply padding to mask (inflate on all sides)
        var paddedX = Math.Max(0, region.X - MaskPadding);
        var paddedY = Math.Max(0, region.Y - MaskPadding);
        var paddedWidth = region.Width + (2 * MaskPadding);
        var paddedHeight = region.Height + (2 * MaskPadding);
        
        // Clamp to page bounds
        paddedWidth = Math.Min(paddedWidth, page.Width.Point - paddedX);
        paddedHeight = Math.Min(paddedHeight, pageHeight - paddedY);
        
        // Convert Y coordinate from bottom-left to top-left origin
        var y = pageHeight - paddedY - paddedHeight;

        // Draw a filled rectangle as the redaction mask
        graphics.DrawRectangle(brush, paddedX, y, paddedWidth, paddedHeight);
    }
}
