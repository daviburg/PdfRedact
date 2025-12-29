namespace PdfRedact.Core.Models;

/// <summary>
/// Represents a rectangular region on a PDF page that should be redacted.
/// </summary>
public class RedactionRegion
{
    /// <summary>
    /// The page number (1-based) where the redaction should be applied.
    /// </summary>
    public int PageNumber { get; set; }

    /// <summary>
    /// X coordinate of the lower-left corner of the redaction region.
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// Y coordinate of the lower-left corner of the redaction region.
    /// </summary>
    public double Y { get; set; }

    /// <summary>
    /// Width of the redaction region.
    /// </summary>
    public double Width { get; set; }

    /// <summary>
    /// Height of the redaction region.
    /// </summary>
    public double Height { get; set; }

    /// <summary>
    /// The text that was found and will be redacted.
    /// </summary>
    public string? MatchedText { get; set; }

    /// <summary>
    /// The rule pattern that matched this region.
    /// </summary>
    public string? RulePattern { get; set; }
}
