using PdfRedact.Core.Models;

namespace PdfRedact.Core.Services;

/// <summary>
/// Service for locating text in PDF documents that should be redacted.
/// </summary>
public interface ITextLocator
{
    /// <summary>
    /// Locates text regions in a PDF based on the provided rules.
    /// </summary>
    /// <param name="pdfPath">Path to the PDF file.</param>
    /// <param name="rules">Rules for matching text to redact.</param>
    /// <returns>A redaction plan containing all matched regions.</returns>
    RedactionPlan LocateText(string pdfPath, IEnumerable<RedactionRule> rules);
}
