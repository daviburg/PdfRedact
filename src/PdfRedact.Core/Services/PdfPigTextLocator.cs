using System.Text.RegularExpressions;
using PdfRedact.Core.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace PdfRedact.Core.Services;

/// <summary>
/// Implementation of text location using PdfPig library.
/// </summary>
public class PdfPigTextLocator : ITextLocator
{
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
            var pageText = page.Text;
            var words = page.GetWords().ToList();

            foreach (var rule in rulesList)
            {
                var regions = FindMatchingRegions(page, pageText, words, rule);
                plan.Regions.AddRange(regions);
            }
        }

        return plan;
    }

    private List<RedactionRegion> FindMatchingRegions(
        Page page,
        string pageText,
        List<Word> words,
        RedactionRule rule)
    {
        var regions = new List<RedactionRegion>();

        if (rule.IsRegex)
        {
            regions.AddRange(FindRegexMatches(page, pageText, words, rule));
        }
        else
        {
            regions.AddRange(FindLiteralMatches(page, pageText, words, rule));
        }

        return regions;
    }

    private List<RedactionRegion> FindRegexMatches(
        Page page,
        string pageText,
        List<Word> words,
        RedactionRule rule)
    {
        var regions = new List<RedactionRegion>();
        var options = rule.CaseSensitive ? RegexOptions.None : RegexOptions.IgnoreCase;
        var regex = new Regex(rule.Pattern, options);

        var matches = regex.Matches(pageText);
        foreach (Match match in matches)
        {
            // Find words that overlap with this match
            var matchStart = match.Index;
            var matchEnd = match.Index + match.Length;

            var region = GetRegionForTextRange(page, words, matchStart, matchEnd, match.Value, rule.Pattern);
            if (region != null)
            {
                regions.Add(region);
            }
        }

        return regions;
    }

    private List<RedactionRegion> FindLiteralMatches(
        Page page,
        string pageText,
        List<Word> words,
        RedactionRule rule)
    {
        var regions = new List<RedactionRegion>();
        var comparison = rule.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        int index = 0;
        while ((index = pageText.IndexOf(rule.Pattern, index, comparison)) != -1)
        {
            var matchEnd = index + rule.Pattern.Length;
            var region = GetRegionForTextRange(page, words, index, matchEnd, rule.Pattern, rule.Pattern);
            if (region != null)
            {
                regions.Add(region);
            }
            index = matchEnd;
        }

        return regions;
    }

    private RedactionRegion? GetRegionForTextRange(
        Page page,
        List<Word> words,
        int startIndex,
        int endIndex,
        string matchedText,
        string pattern)
    {
        // NOTE: This method rebuilds position mapping for each match.
        // Future optimization: Build mapping once per page and reuse for all matches.
        
        // Build a mapping of text positions to words
        var currentIndex = 0;
        var matchingWords = new List<Word>();

        foreach (var word in words)
        {
            var wordText = word.Text;
            var wordStart = currentIndex;
            var wordEnd = currentIndex + wordText.Length;

            // Check if this word overlaps with our match
            if (wordEnd > startIndex && wordStart < endIndex)
            {
                matchingWords.Add(word);
            }

            currentIndex = wordEnd;
            
            // Account for spaces between words
            // NOTE: This is a heuristic that works for most cases but may need refinement
            // for edge cases with unusual spacing or non-standard text layouts.
            if (currentIndex < startIndex + matchedText.Length)
            {
                currentIndex++;
            }
        }

        if (!matchingWords.Any())
        {
            return null;
        }

        // Calculate bounding box for all matching words
        var minX = matchingWords.Min(w => w.BoundingBox.Left);
        var minY = matchingWords.Min(w => w.BoundingBox.Bottom);
        var maxX = matchingWords.Max(w => w.BoundingBox.Right);
        var maxY = matchingWords.Max(w => w.BoundingBox.Top);

        return new RedactionRegion
        {
            PageNumber = page.Number,
            X = minX,
            Y = minY,
            Width = maxX - minX,
            Height = maxY - minY,
            MatchedText = matchedText,
            RulePattern = pattern
        };
    }
}
