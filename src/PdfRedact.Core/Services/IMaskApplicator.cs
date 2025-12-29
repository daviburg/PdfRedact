using PdfRedact.Core.Models;

namespace PdfRedact.Core.Services;

/// <summary>
/// Service for applying redaction masks to PDF documents.
/// </summary>
public interface IMaskApplicator
{
    /// <summary>
    /// Applies opaque masks to the PDF based on the redaction plan.
    /// </summary>
    /// <param name="plan">The redaction plan containing regions to redact.</param>
    /// <param name="outputPath">Path where the redacted PDF should be saved.</param>
    void ApplyMasks(RedactionPlan plan, string outputPath);
}
