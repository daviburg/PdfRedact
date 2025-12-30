namespace PdfRedact.Core.Models;

/// <summary>
/// Represents a complete plan for redacting a PDF document.
/// </summary>
public class RedactionPlan
{
    /// <summary>
    /// The source PDF file path.
    /// </summary>
    public string SourcePdfPath { get; set; } = string.Empty;

    /// <summary>
    /// List of regions to redact in the PDF.
    /// </summary>
    public List<RedactionRegion> Regions { get; set; } = new();

    /// <summary>
    /// Total number of redactions in the plan.
    /// </summary>
    public int TotalRedactions => Regions.Count;
}
