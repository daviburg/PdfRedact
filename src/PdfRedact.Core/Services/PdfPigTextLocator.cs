using System.Text;
using System.Text.RegularExpressions;
using PdfRedact.Core.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;

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
        var regions = new List<RedactionRegion>();

        // Group rules by fragment-aware mode to optimize processing
        var fragmentAwareRules = new List<RedactionRule>();
        var wordBasedRules = new List<RedactionRule>();

        foreach (var rule in rules)
        {
            if (ShouldUseFragmentAwareMode(rule))
            {
                fragmentAwareRules.Add(rule);
            }
            else
            {
                wordBasedRules.Add(rule);
            }
        }

        // Process fragment-aware rules using letter-level tokenization
        if (fragmentAwareRules.Any())
        {
            var tokenizer = new FragmentAwareTokenizer();
            var tokens = tokenizer.TokenizePage(page);
            var (searchText, tokenSpans) = BuildSearchableTextFromTokens(tokens);

            foreach (var rule in fragmentAwareRules)
            {
                var matches = FindMatches(searchText, rule);
                
                foreach (var match in matches)
                {
                    var matchingTokens = GetMatchingTokens(tokenSpans, match.Start, match.End);
                    
                    if (matchingTokens.Any())
                    {
                        var lineGroups = GroupTokensByLine(matchingTokens);
                        
                        foreach (var lineGroup in lineGroups)
                        {
                            var region = CreateRedactionRegionFromTokens(page, lineGroup, match.Text, rule.Pattern);
                            regions.Add(region);
                        }
                    }
                }
            }
        }

        // Process word-based rules using existing word tokenization
        if (wordBasedRules.Any())
        {
            var words = page.GetWords().OrderBy(w => w.BoundingBox.Bottom).ThenBy(w => w.BoundingBox.Left).ToList();
            var (searchText, wordSpans) = BuildSearchableText(words);

            foreach (var rule in wordBasedRules)
            {
                var matches = FindMatches(searchText, rule);
                
                foreach (var match in matches)
                {
                    var matchingWords = GetMatchingWords(wordSpans, match.Start, match.End);
                    
                    if (matchingWords.Any())
                    {
                        var lineGroups = GroupWordsByLine(matchingWords);
                        
                        foreach (var lineGroup in lineGroups)
                        {
                            var region = CreateRedactionRegion(page, lineGroup, match.Text, rule.Pattern);
                            regions.Add(region);
                        }
                    }
                }
            }
        }

        return regions;
    }

    /// <summary>
    /// Determines whether to use fragment-aware mode for a given rule.
    /// Auto-enables for numeric patterns when FragmentAware is null.
    /// </summary>
    private bool ShouldUseFragmentAwareMode(RedactionRule rule)
    {
        if (rule.FragmentAware.HasValue)
        {
            return rule.FragmentAware.Value;
        }

        // Auto-detect: Conservative approach - only enable for literal numeric patterns
        // For regex patterns, require explicit --fragment-aware flag to avoid false positives
        if (rule.IsRegex)
        {
            // Do NOT auto-enable for regex patterns due to complexity of detection
            // User should explicitly use --fragment-aware if needed
            return false;
        }
        else
        {
            // For literal patterns, enable if all characters are digits or common separators
            // This is safe and covers the primary use case (boxed digit forms)
            return rule.Pattern.All(c => char.IsDigit(c) || c == '-' || c == ' ' || c == '/');
        }
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

    /// <summary>
    /// Builds a searchable text string from tokens with deterministic span mapping.
    /// Each token is separated by a single space delimiter.
    /// Returns a tuple of (searchable text, span mappings) where each TokenSpan
    /// maps a substring range [StartIndex, EndIndex) to its corresponding Token with bounding box.
    /// </summary>
    private (string searchText, List<TokenSpan> tokenSpans) BuildSearchableTextFromTokens(List<Token> tokens)
    {
        var stringBuilder = new StringBuilder();
        var tokenSpans = new List<TokenSpan>();

        foreach (var token in tokens)
        {
            var startIndex = stringBuilder.Length;
            stringBuilder.Append(token.Text);
            var endIndex = stringBuilder.Length;

            tokenSpans.Add(new TokenSpan
            {
                Token = token,
                StartIndex = startIndex,
                EndIndex = endIndex
            });

            // Add space delimiter between tokens
            stringBuilder.Append(' ');
        }

        return (stringBuilder.ToString(), tokenSpans);
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

    private List<Token> GetMatchingTokens(List<TokenSpan> tokenSpans, int matchStart, int matchEnd)
    {
        return tokenSpans
            .Where(span => span.EndIndex > matchStart && span.StartIndex < matchEnd)
            .Select(span => span.Token)
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
    /// Groups tokens by line based on Y-coordinate proximity.
    /// Tokens within LineGroupingTolerance of each other are considered on the same line.
    /// </summary>
    private List<List<Token>> GroupTokensByLine(List<Token> tokens)
    {
        if (!tokens.Any())
        {
            return new List<List<Token>>();
        }

        var sorted = tokens.OrderBy(t => t.BoundingBox.Bottom).ToList();
        var lineGroups = new List<List<Token>>();
        var currentLine = new List<Token> { sorted[0] };
        var currentBaseline = sorted[0].BoundingBox.Bottom;

        for (int i = 1; i < sorted.Count; i++)
        {
            var token = sorted[i];
            var tokenBaseline = token.BoundingBox.Bottom;

            if (Math.Abs(tokenBaseline - currentBaseline) <= LineGroupingTolerance)
            {
                // Same line
                currentLine.Add(token);
            }
            else
            {
                // New line
                lineGroups.Add(currentLine);
                currentLine = new List<Token> { token };
                currentBaseline = tokenBaseline;
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

    /// <summary>
    /// Creates a redaction region from a group of tokens.
    /// Coordinates are in PDF user space (bottom-left origin, points).
    /// PageNumber is 1-based as per PdfPig convention.
    /// </summary>
    private RedactionRegion CreateRedactionRegionFromTokens(Page page, List<Token> tokens, string matchedText, string pattern)
    {
        var minX = tokens.Min(t => t.BoundingBox.Left);
        var minY = tokens.Min(t => t.BoundingBox.Bottom);
        var maxX = tokens.Max(t => t.BoundingBox.Right);
        var maxY = tokens.Max(t => t.BoundingBox.Top);

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

    private class TokenSpan
    {
        public Token Token { get; set; } = null!;
        public int StartIndex { get; set; }
        public int EndIndex { get; set; }
    }

    private class MatchInfo
    {
        public int Start { get; set; }
        public int End { get; set; }
        public string Text { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents a text token with its bounding box.
    /// Used for fragment-aware tokenization.
    /// </summary>
    private class Token
    {
        public string Text { get; set; } = string.Empty;
        public PdfRectangle BoundingBox { get; set; }
        public double Bottom => BoundingBox.Bottom;
        public double Left => BoundingBox.Left;
    }

    /// <summary>
    /// Internal tokenizer that builds tokens from letters for fragment-aware matching.
    /// Groups letters into lines, then into runs (words or digit sequences).
    /// Uses a two-pass approach: first creates conservative word tokens, then joins digit sequences.
    /// </summary>
    private class FragmentAwareTokenizer
    {
        // Conservative threshold for normal word formation (tight kerning)
        private const double WordGapMultiplier = 1.5;              // 1.5× width for normal words
        private const double WordHeightMultiplier = 0.5;           // 0.5× height for normal words
        
        // Permissive threshold for digit-run formation (boxed forms with wide spacing)
        private const double DigitRunGapMultiplier = 5.0;          // 5× width handles ~25pt gaps
        private const double DigitRunHeightMultiplier = 2.5;       // 2.5× height adapts to font size
        
        private const double MinGapThreshold = 2.0;                 // Minimum to avoid joining touching chars

        public List<Token> TokenizePage(Page page)
        {
            var letters = page.Letters.ToList();
            if (!letters.Any())
            {
                return new List<Token>();
            }

            // Calculate median letter dimensions for adaptive thresholds
            var letterHeights = letters.Select(l => l.GlyphRectangle.Height).OrderBy(h => h).ToList();
            var letterWidths = letters.Select(l => l.GlyphRectangle.Width).OrderBy(w => w).ToList();
            var medianHeight = letterHeights[letterHeights.Count / 2];
            var medianWidth = letterWidths[letterWidths.Count / 2];

            // Group letters into lines
            var lines = GroupLettersIntoLines(letters, medianHeight);

            // Build tokens from each line
            var tokens = new List<Token>();
            foreach (var line in lines)
            {
                var lineTokens = BuildTokensFromLine(line, medianWidth, medianHeight);
                tokens.AddRange(lineTokens);
            }

            return tokens;
        }

        private List<List<Letter>> GroupLettersIntoLines(List<Letter> letters, double medianHeight)
        {
            if (!letters.Any())
            {
                return new List<List<Letter>>();
            }

            // Sort by Y coordinate (top to bottom for reading order)
            // Use Top descending so topmost text is processed first
            var sorted = letters.OrderByDescending(l => l.GlyphRectangle.Top).ToList();
            
            // Use adaptive tolerance based on median height
            var yTolerance = Math.Max(LineGroupingTolerance, medianHeight * 0.3);
            
            var lines = new List<List<Letter>>();
            var currentLine = new List<Letter> { sorted[0] };
            var currentBaseline = sorted[0].GlyphRectangle.Top;

            for (int i = 1; i < sorted.Count; i++)
            {
                var letter = sorted[i];
                var letterBaseline = letter.GlyphRectangle.Top;

                if (Math.Abs(letterBaseline - currentBaseline) <= yTolerance)
                {
                    currentLine.Add(letter);
                }
                else
                {
                    lines.Add(currentLine);
                    currentLine = new List<Letter> { letter };
                    currentBaseline = letterBaseline;
                }
            }

            if (currentLine.Any())
            {
                lines.Add(currentLine);
            }

            return lines;
        }

        private List<Token> BuildTokensFromLine(List<Letter> lineLetters, double medianWidth, double medianHeight)
        {
            if (!lineLetters.Any())
            {
                return new List<Token>();
            }

            // Sort letters left to right
            var sorted = lineLetters.OrderBy(l => l.GlyphRectangle.Left).ToList();

            // PASS 1: Build conservative word tokens with tight gap threshold
            var wordGapThreshold = Math.Max(MinGapThreshold, 
                Math.Max(medianWidth * WordGapMultiplier, medianHeight * WordHeightMultiplier));
            
            var baseTokens = new List<Token>();
            var currentRun = new List<Letter> { sorted[0] };

            for (int i = 1; i < sorted.Count; i++)
            {
                var prevLetter = sorted[i - 1];
                var currLetter = sorted[i];
                var gap = currLetter.GlyphRectangle.Left - prevLetter.GlyphRectangle.Right;

                if (gap <= wordGapThreshold)
                {
                    currentRun.Add(currLetter);
                }
                else
                {
                    baseTokens.Add(CreateTokenFromLetters(currentRun));
                    currentRun = new List<Letter> { currLetter };
                }
            }

            if (currentRun.Any())
            {
                baseTokens.Add(CreateTokenFromLetters(currentRun));
            }

            // PASS 2: Join adjacent single-digit tokens into digit-run tokens
            var digitRunGapThreshold = Math.Max(MinGapThreshold,
                Math.Max(medianWidth * DigitRunGapMultiplier, medianHeight * DigitRunHeightMultiplier));
            
            var finalTokens = new List<Token>();
            var digitRunTokens = new List<Token>();

            for (int i = 0; i < baseTokens.Count; i++)
            {
                var token = baseTokens[i];
                
                // Check if this is a single digit
                if (IsSingleDigitToken(token))
                {
                    digitRunTokens.Add(token);
                    
                    // Check if we should continue accumulating or finalize the run
                    bool shouldContinue = false;
                    if (i + 1 < baseTokens.Count)
                    {
                        var nextToken = baseTokens[i + 1];
                        if (IsSingleDigitToken(nextToken))
                        {
                            var gap = nextToken.BoundingBox.Left - token.BoundingBox.Right;
                            shouldContinue = gap <= digitRunGapThreshold;
                        }
                    }
                    
                    if (!shouldContinue)
                    {
                        // Finalize the digit run
                        if (digitRunTokens.Count > 1)
                        {
                            // Merge into a single digit-run token
                            finalTokens.Add(MergeTokens(digitRunTokens));
                        }
                        else
                        {
                            // Single digit, keep as is
                            finalTokens.Add(token);
                        }
                        digitRunTokens.Clear();
                    }
                }
                else
                {
                    // Not a digit - finalize any pending digit run first
                    if (digitRunTokens.Any())
                    {
                        if (digitRunTokens.Count > 1)
                        {
                            finalTokens.Add(MergeTokens(digitRunTokens));
                        }
                        else
                        {
                            finalTokens.Add(digitRunTokens[0]);
                        }
                        digitRunTokens.Clear();
                    }
                    
                    // Add the non-digit token
                    finalTokens.Add(token);
                }
            }

            return finalTokens;
        }

        private bool IsSingleDigitToken(Token token)
        {
            // Check if token is a single character and it's a digit
            // Allow single separators like '-' to be included in digit runs
            if (string.IsNullOrEmpty(token.Text))
                return false;
            
            if (token.Text.Length == 1)
            {
                var ch = token.Text[0];
                return char.IsDigit(ch) || ch == '-';
            }
            
            return false;
        }

        private Token MergeTokens(List<Token> tokens)
        {
            var text = string.Concat(tokens.Select(t => t.Text));
            var minX = tokens.Min(t => t.BoundingBox.Left);
            var minY = tokens.Min(t => t.BoundingBox.Bottom);
            var maxX = tokens.Max(t => t.BoundingBox.Right);
            var maxY = tokens.Max(t => t.BoundingBox.Top);

            return new Token
            {
                Text = text,
                BoundingBox = new PdfRectangle(minX, minY, maxX, maxY)
            };
        }

        private Token CreateTokenFromLetters(List<Letter> letters)
        {
            var text = string.Concat(letters.Select(l => l.Value));
            
            var minX = letters.Min(l => l.GlyphRectangle.Left);
            var minY = letters.Min(l => l.GlyphRectangle.Bottom);
            var maxX = letters.Max(l => l.GlyphRectangle.Right);
            var maxY = letters.Max(l => l.GlyphRectangle.Top);

            return new Token
            {
                Text = text,
                BoundingBox = new PdfRectangle(minX, minY, maxX, maxY)
            };
        }
    }
}
