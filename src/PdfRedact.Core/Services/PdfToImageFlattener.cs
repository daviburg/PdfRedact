using PdfSharp.Pdf;
using PdfSharp.Drawing;
using SkiaSharp;
using System.Runtime.Versioning;

namespace PdfRedact.Core.Services;

/// <summary>
/// Implementation of page flattening using PDFtoImage for rendering
/// and PDFsharp for PDF reconstruction.
/// 
/// This implementation uses PDFium (via PDFtoImage library) to render PDF pages
/// to bitmap images, ensuring complete removal of text layers and creating
/// image-only PDFs suitable for secure redaction scenarios.
/// 
/// Platform Support: Windows, Linux, macOS (requires native PDFium binaries)
/// </summary>
[SupportedOSPlatform("Windows")]
[SupportedOSPlatform("Linux")]
[SupportedOSPlatform("macOS")]
public class PdfToImageFlattener : IPageFlattener
{
    /// <inheritdoc/>
    public void FlattenPdf(string sourcePath, string outputPath, int dpi = 300)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            throw new ArgumentException("Source path cannot be null or empty", nameof(sourcePath));
        }

        if (string.IsNullOrWhiteSpace(outputPath))
        {
            throw new ArgumentException("Output path cannot be null or empty", nameof(outputPath));
        }

        if (dpi < 72 || dpi > 600)
        {
            throw new ArgumentException("DPI must be between 72 and 600", nameof(dpi));
        }

        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException("Source PDF file not found", sourcePath);
        }

        // Create output directory if it doesn't exist
        var outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        try
        {
            RenderAndFlattenPdf(sourcePath, outputPath, dpi);
        }
        catch (DllNotFoundException ex)
        {
            throw new InvalidOperationException(
                "PDF rendering library (PDFium) could not be loaded. " +
                "Ensure the platform-specific native binaries are available. " +
                "Supported platforms: Windows, Linux, macOS.", ex);
        }
        catch (Exception ex) when (ex.Message.Contains("PDFium") || ex.Message.Contains("pdfium"))
        {
            throw new InvalidOperationException(
                "PDF rendering failed. This may indicate a PDFium library issue. " +
                $"Original error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Performs the actual PDF rendering and flattening operation.
    /// Each page is rendered to a bitmap using PDFium, then embedded as an image in a new PDF.
    /// 
    /// Memory Management: Pages are processed sequentially to minimize memory usage.
    /// For large PDFs with many pages, peak memory usage will be approximately:
    /// (page_width_pixels * page_height_pixels * 4 bytes) per page during processing.
    /// </summary>
    private void RenderAndFlattenPdf(string sourcePath, string outputPath, int dpi)
    {
        // Create a new PDF document for the flattened output
        using var document = new PdfDocument();

        // Open source PDF and configure rendering options
        // PDFtoImage uses PDFium internally for high-fidelity bitmap rendering
        using var pdfStream = File.OpenRead(sourcePath);
        var renderOptions = new PDFtoImage.RenderOptions(Dpi: dpi);
        
        // Process each page: render to bitmap, embed as image, dispose immediately
        // This sequential processing approach minimizes memory footprint
        foreach (var bitmap in PDFtoImage.Conversion.ToImages(pdfStream, options: renderOptions))
        {
            using (bitmap)
            {
                // Create a new page in the output PDF with dimensions matching the rendered bitmap
                var page = document.AddPage();
                
                // Set page size based on bitmap dimensions
                // Convert pixels back to points (1 inch = 72 points, dpi = pixels per inch)
                page.Width = XUnit.FromInch(bitmap.Width / (double)dpi);
                page.Height = XUnit.FromInch(bitmap.Height / (double)dpi);

                // Embed the bitmap as a JPEG image in the PDF page
                using var graphics = XGraphics.FromPdfPage(page);
                using var stream = new MemoryStream();
                
                // Encode bitmap to JPEG at 90% quality (balance between file size and quality)
                bitmap.Encode(stream, SKEncodedImageFormat.Jpeg, quality: 90);
                stream.Position = 0;

                // Create XImage from stream and draw it to fill the entire page
                // This ensures the page contains only the rendered bitmap, no text layer
                using var image = XImage.FromStream(stream);
                graphics.DrawImage(image, 0, 0, page.Width.Point, page.Height.Point);
                
                // Bitmap, graphics, stream, and image are all disposed here via 'using'
                // This is critical for processing large PDFs without exhausting memory
            }
        }

        // Save the flattened PDF (image-only, no text layers)
        document.Save(outputPath);
    }
}
