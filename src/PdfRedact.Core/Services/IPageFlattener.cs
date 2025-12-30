namespace PdfRedact.Core.Services;

/// <summary>
/// Interface for flattening PDFs by converting pages to bitmap images.
/// This removes all text layers and creates an image-only PDF.
/// </summary>
public interface IPageFlattener
{
    /// <summary>
    /// Flattens a PDF by rendering each page to a bitmap and creating a new image-only PDF.
    /// </summary>
    /// <param name="sourcePath">Path to the source PDF file</param>
    /// <param name="outputPath">Path where the flattened PDF should be saved</param>
    /// <param name="dpi">Resolution for rendering pages (default: 300 DPI)</param>
    void FlattenPdf(string sourcePath, string outputPath, int dpi = 300);
}
