using System.Text.RegularExpressions;

namespace PdfRedact.Core.Models;

/// <summary>
/// Defines a rule for locating text in a PDF document.
/// </summary>
public class RedactionRule
{
    /// <summary>
    /// The pattern to match. Can be a regex pattern or literal text depending on IsRegex.
    /// </summary>
    public string Pattern { get; set; } = string.Empty;

    /// <summary>
    /// Whether the pattern should be treated as a regular expression.
    /// If false, the pattern is treated as literal text.
    /// </summary>
    public bool IsRegex { get; set; }

    /// <summary>
    /// Whether the search should be case-sensitive.
    /// </summary>
    public bool CaseSensitive { get; set; } = true;

    /// <summary>
    /// Optional description of what this rule is matching.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Optional explicit RegexOptions for regex matching.
    /// If not specified, defaults are: CultureInvariant | IgnoreCase (when CaseSensitive=false).
    /// Allows control over culture behavior, compilation, and other advanced options.
    /// </summary>
    public RegexOptions? RegexOptions { get; set; }
}
