namespace PdfRedact.Core.Models;

/// <summary>
/// Represents a rectangular region on a PDF page that should be redacted.
/// Coordinates follow PDF user space convention: bottom-left origin, measured in points.
/// </summary>
public class RedactionRegion
{
    /// <summary>
    /// The page number (1-based) where the redaction should be applied.
    /// </summary>
    public int PageNumber { get; set; }

    /// <summary>
    /// X coordinate of the lower-left corner of the redaction region (in points).
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// Y coordinate of the lower-left corner of the redaction region (in points).
    /// </summary>
    public double Y { get; set; }

    /// <summary>
    /// Width of the redaction region (in points).
    /// </summary>
    public double Width { get; set; }

    /// <summary>
    /// Height of the redaction region (in points).
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

    /// <summary>
    /// Page rotation in degrees (0, 90, 180, or 270).
    /// Used to correctly apply coordinate transforms during mask application.
    /// </summary>
    public int PageRotation { get; set; }
}
