using System.Text;
using System.Text.RegularExpressions;
using PdfRedact.Core.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace PdfRedact.Core.Services;

/// <summary>
/// Implementation of text location using PdfPig library.
/// Coordinates are in PDF user space with bottom-left origin, measured in points.
/// Page numbers are 1-based.
/// </summary>
public class PdfPigTextLocator : ITextLocator
{
    private const double LineGroupingTolerance = 2.0; // Points tolerance for grouping words on same line

    /// <inheritdoc/>
    public RedactionPlan LocateText(string pdfPath, IEnumerable<RedactionRule> rules)
    {
        if (string.IsNullOrWhiteSpace(pdfPath))
        {
            throw new ArgumentException("PDF path cannot be null or empty", nameof(pdfPath));
        }

        if (!File.Exists(pdfPath))
        {
            throw new FileNotFoundException("PDF file not found", pdfPath);
        }

        var plan = new RedactionPlan
        {
            SourcePdfPath = pdfPath
        };

        var rulesList = rules.ToList();
        if (!rulesList.Any())
        {
            return plan;
        }

        using var document = PdfDocument.Open(pdfPath);

        foreach (var page in document.GetPages())
        {
            var regions = ProcessPage(page, rulesList);
            plan.Regions.AddRange(regions);
        }

        return plan;
    }

    private List<RedactionRegion> ProcessPage(Page page, List<RedactionRule> rules)
    {
        var words = page.GetWords().OrderBy(w => w.BoundingBox.Bottom).ThenBy(w => w.BoundingBox.Left).ToList();
        
        // Build synthetic searchable string with deterministic word-span mapping
        var (searchText, wordSpans) = BuildSearchableText(words);

        var regions = new List<RedactionRegion>();

        foreach (var rule in rules)
        {
            var matches = FindMatches(searchText, rule);
            
            foreach (var match in matches)
            {
                // Find words that overlap with this match
                var matchingWords = GetMatchingWords(wordSpans, match.Start, match.End);
                
                if (matchingWords.Any())
                {
                    // Group words by line to avoid over-masking content between lines
                    var lineGroups = GroupWordsByLine(matchingWords);
                    
                    foreach (var lineGroup in lineGroups)
                    {
                        var region = CreateRedactionRegion(page, lineGroup, match.Text, rule.Pattern);
                        regions.Add(region);
                    }
                }
            }
        }

        return regions;
    }

    /// <summary>
    /// Builds a searchable text string from words with deterministic span mapping.
    /// Each word is separated by a single space delimiter.
    /// </summary>
    private (string searchText, List<WordSpan> wordSpans) BuildSearchableText(List<Word> words)
    {
        var stringBuilder = new StringBuilder();
        var wordSpans = new List<WordSpan>();

        foreach (var word in words)
        {
            var startIndex = stringBuilder.Length;
            stringBuilder.Append(word.Text);
            var endIndex = stringBuilder.Length;

            wordSpans.Add(new WordSpan
            {
                Word = word,
                StartIndex = startIndex,
                EndIndex = endIndex
            });

            // Add space delimiter between words
            stringBuilder.Append(' ');
        }

        return (stringBuilder.ToString(), wordSpans);
    }

    private List<MatchInfo> FindMatches(string searchText, RedactionRule rule)
    {
        var matches = new List<MatchInfo>();

        if (rule.IsRegex)
        {
            var options = BuildRegexOptions(rule);
            var regex = new Regex(rule.Pattern, options);
            
            foreach (Match match in regex.Matches(searchText))
            {
                matches.Add(new MatchInfo
                {
                    Start = match.Index,
                    End = match.Index + match.Length,
                    Text = match.Value
                });
            }
        }
        else
        {
            var comparison = rule.CaseSensitive 
                ? StringComparison.Ordinal 
                : StringComparison.OrdinalIgnoreCase;

            int index = 0;
            while ((index = searchText.IndexOf(rule.Pattern, index, comparison)) != -1)
            {
                matches.Add(new MatchInfo
                {
                    Start = index,
                    End = index + rule.Pattern.Length,
                    Text = rule.Pattern
                });
                index += rule.Pattern.Length;
            }
        }

        return matches;
    }

    private RegexOptions BuildRegexOptions(RedactionRule rule)
    {
        var options = RegexOptions.CultureInvariant;
        
        if (!rule.CaseSensitive)
        {
            options |= RegexOptions.IgnoreCase;
        }

        // Use explicit RegexOptions from rule if provided, otherwise use defaults
        if (rule.RegexOptions.HasValue)
        {
            options = rule.RegexOptions.Value;
        }

        return options;
    }

    private List<Word> GetMatchingWords(List<WordSpan> wordSpans, int matchStart, int matchEnd)
    {
        return wordSpans
            .Where(span => span.EndIndex > matchStart && span.StartIndex < matchEnd)
            .Select(span => span.Word)
            .ToList();
    }

    /// <summary>
    /// Groups words by line based on Y-coordinate proximity.
    /// Words within LineGroupingTolerance of each other are considered on the same line.
    /// </summary>
    private List<List<Word>> GroupWordsByLine(List<Word> words)
    {
        if (!words.Any())
        {
            return new List<List<Word>>();
        }

        var sorted = words.OrderBy(w => w.BoundingBox.Bottom).ToList();
        var lineGroups = new List<List<Word>>();
        var currentLine = new List<Word> { sorted[0] };
        var currentBaseline = sorted[0].BoundingBox.Bottom;

        for (int i = 1; i < sorted.Count; i++)
        {
            var word = sorted[i];
            var wordBaseline = word.BoundingBox.Bottom;

            if (Math.Abs(wordBaseline - currentBaseline) <= LineGroupingTolerance)
            {
                // Same line
                currentLine.Add(word);
            }
            else
            {
                // New line
                lineGroups.Add(currentLine);
                currentLine = new List<Word> { word };
                currentBaseline = wordBaseline;
            }
        }

        // Add the last line
        if (currentLine.Any())
        {
            lineGroups.Add(currentLine);
        }

        return lineGroups;
    }

    /// <summary>
    /// Creates a redaction region from a group of words.
    /// Coordinates are in PDF user space (bottom-left origin, points).
    /// PageNumber is 1-based as per PdfPig convention.
    /// </summary>
    private RedactionRegion CreateRedactionRegion(Page page, List<Word> words, string matchedText, string pattern)
    {
        var minX = words.Min(w => w.BoundingBox.Left);
        var minY = words.Min(w => w.BoundingBox.Bottom);
        var maxX = words.Max(w => w.BoundingBox.Right);
        var maxY = words.Max(w => w.BoundingBox.Top);

        return new RedactionRegion
        {
            PageNumber = page.Number, // 1-based page number
            X = minX,
            Y = minY,
            Width = maxX - minX,
            Height = maxY - minY,
            MatchedText = matchedText,
            RulePattern = pattern,
            PageRotation = page.Rotation.Value // Store rotation for proper application
        };
    }

    private class WordSpan
    {
        public Word Word { get; set; } = null!;
        public int StartIndex { get; set; }
        public int EndIndex { get; set; }
    }

    private class MatchInfo
    {
        public int Start { get; set; }
        public int End { get; set; }
        public string Text { get; set; } = string.Empty;
    }
}
