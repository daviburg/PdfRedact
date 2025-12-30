using PdfSharp.Pdf;
using PdfSharp.Drawing;
using SkiaSharp;
using System.Runtime.Versioning;

namespace PdfRedact.Core.Services;

/// <summary>
/// Implementation of page flattening using PDFtoImage for rendering
/// and PDFsharp for PDF reconstruction.
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

        // Create a new PDF document
        using var document = new PdfDocument();

        // Convert each page to an image and add to the new PDF
        using var pdfStream = File.OpenRead(sourcePath);
        var renderOptions = new PDFtoImage.RenderOptions(Dpi: dpi);
        
        var pageIndex = 0;
        foreach (var bitmap in PDFtoImage.Conversion.ToImages(pdfStream, options: renderOptions))
        {
            using (bitmap)
            {
                // Create a new page in the output PDF with the same dimensions as the image
                var page = document.AddPage();
                
                // Set page size based on image dimensions
                // Convert pixels back to points (1 inch = 72 points, dpi = pixels per inch)
                page.Width = XUnit.FromInch(bitmap.Width / (double)dpi);
                page.Height = XUnit.FromInch(bitmap.Height / (double)dpi);

                // Draw the bitmap onto the page
                using var graphics = XGraphics.FromPdfPage(page);
                using var stream = new MemoryStream();
                
                // Encode the bitmap to a stream
                bitmap.Encode(stream, SKEncodedImageFormat.Jpeg, quality: 90);
                stream.Position = 0;

                // Create an XImage from the stream and draw it
                using var image = XImage.FromStream(stream);
                graphics.DrawImage(image, 0, 0, page.Width.Point, page.Height.Point);
            }

            pageIndex++;
        }

        // Save the flattened PDF
        document.Save(outputPath);
    }
}
