using PdfRedact.Core.Models;
using PdfRedact.Core.Services;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using PdfSharp.Snippets.Font;

namespace PdfRedact.Core.Tests;

/// <summary>
/// Tests for fragment-aware text location (boxed digits and fragmented glyphs).
/// </summary>
public class FragmentAwareTextLocatorTests
{
    private readonly string _testDir;

    public FragmentAwareTextLocatorTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "PdfRedactTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDir);
        
        // Set up font resolver for tests (required for PDFsharp 6.x)
        if (GlobalFontSettings.FontResolver == null)
        {
            GlobalFontSettings.FontResolver = new FailsafeFontResolver();
        }
    }

    [Fact]
    public void LocateText_BoxedDigits_LiteralPattern_FindsMatch()
    {
        // Arrange: Create a PDF with boxed digits "1234" as separate characters
        var pdfPath = Path.Combine(_testDir, "boxed_digits_literal.pdf");
        CreatePdfWithBoxedDigits(pdfPath, "1234", spacing: 15);

        var rule = new RedactionRule
        {
            Pattern = "1234",
            IsRegex = false,
            CaseSensitive = true,
            FragmentAware = true
        };

        var locator = new PdfPigTextLocator();

        // Act
        var plan = locator.LocateText(pdfPath, new[] { rule });

        // Assert
        Assert.Equal(1, plan.TotalRedactions);
        Assert.Single(plan.Regions);
        
        var region = plan.Regions[0];
        Assert.Equal(1, region.PageNumber);
        Assert.Equal("1234", region.MatchedText);
        Assert.Equal("1234", region.RulePattern);
        Assert.True(region.Width > 0);
        Assert.True(region.Height > 0);

        // Cleanup
        File.Delete(pdfPath);
    }

    [Fact]
    public void LocateText_BoxedDigits_RegexPattern_FindsMatch()
    {
        // Arrange: Create a PDF with boxed digits "5678" as separate characters
        var pdfPath = Path.Combine(_testDir, "boxed_digits_regex.pdf");
        CreatePdfWithBoxedDigits(pdfPath, "5678", spacing: 15);

        var rule = new RedactionRule
        {
            Pattern = @"\d{4}",
            IsRegex = true,
            CaseSensitive = true,
            FragmentAware = true
        };

        var locator = new PdfPigTextLocator();

        // Act
        var plan = locator.LocateText(pdfPath, new[] { rule });

        // Assert
        Assert.Equal(1, plan.TotalRedactions);
        Assert.Single(plan.Regions);
        
        var region = plan.Regions[0];
        Assert.Equal(1, region.PageNumber);
        Assert.Equal("5678", region.MatchedText);
        Assert.Equal(@"\d{4}", region.RulePattern);

        // Cleanup
        File.Delete(pdfPath);
    }

    [Fact]
    public void LocateText_BoxedDigits_AutoDetectNumericPattern()
    {
        // Arrange: Create a PDF with boxed digits "9876" as separate characters
        var pdfPath = Path.Combine(_testDir, "boxed_digits_autodetect.pdf");
        CreatePdfWithBoxedDigits(pdfPath, "9876", spacing: 15);

        var rule = new RedactionRule
        {
            Pattern = @"\d{4}",
            IsRegex = true,
            CaseSensitive = true
            // FragmentAware is null - should auto-detect and enable for numeric pattern
        };

        var locator = new PdfPigTextLocator();

        // Act
        var plan = locator.LocateText(pdfPath, new[] { rule });

        // Assert - Auto-detection should enable fragment-aware for numeric patterns
        Assert.Equal(1, plan.TotalRedactions);
        Assert.Single(plan.Regions);

        // Cleanup
        File.Delete(pdfPath);
    }

    [Fact]
    public void LocateText_BoxedDigits_DisabledFragmentAware_NoMatch()
    {
        // Arrange: Create a PDF with boxed digits "4321" as separate characters
        var pdfPath = Path.Combine(_testDir, "boxed_digits_disabled.pdf");
        CreatePdfWithBoxedDigits(pdfPath, "4321", spacing: 15);

        var rule = new RedactionRule
        {
            Pattern = "4321",
            IsRegex = false,
            CaseSensitive = true,
            FragmentAware = false // Explicitly disabled
        };

        var locator = new PdfPigTextLocator();

        // Act
        var plan = locator.LocateText(pdfPath, new[] { rule });

        // Assert - Should not find match when fragment-aware is disabled
        Assert.Equal(0, plan.TotalRedactions);
        Assert.Empty(plan.Regions);

        // Cleanup
        File.Delete(pdfPath);
    }

    [Fact]
    public void LocateText_SSNPattern_FindsFragmentedDigits()
    {
        // Arrange: Create a PDF with SSN-like boxed digits
        var pdfPath = Path.Combine(_testDir, "ssn_pattern.pdf");
        CreatePdfWithBoxedDigits(pdfPath, "123456789", spacing: 15);

        var rule = new RedactionRule
        {
            Pattern = @"\d{9}",
            IsRegex = true,
            FragmentAware = true
        };

        var locator = new PdfPigTextLocator();

        // Act
        var plan = locator.LocateText(pdfPath, new[] { rule });

        // Assert
        Assert.Equal(1, plan.TotalRedactions);
        Assert.Single(plan.Regions);
        Assert.Equal("123456789", plan.Regions[0].MatchedText);

        // Cleanup
        File.Delete(pdfPath);
    }

    [Fact]
    public void LocateText_MultipleBoxedSequences_FindsAllMatches()
    {
        // Arrange: Create a PDF with multiple boxed digit sequences
        var pdfPath = Path.Combine(_testDir, "multiple_sequences.pdf");
        CreatePdfWithMultipleBoxedSequences(pdfPath);

        var rule = new RedactionRule
        {
            Pattern = @"\d{4}",
            IsRegex = true,
            FragmentAware = true
        };

        var locator = new PdfPigTextLocator();

        // Act
        var plan = locator.LocateText(pdfPath, new[] { rule });

        // Assert - Should find both sequences
        Assert.Equal(2, plan.TotalRedactions);
        Assert.Equal(2, plan.Regions.Count);

        // Cleanup
        File.Delete(pdfPath);
    }

    [Fact]
    public void LocateText_MixedContent_FragmentAwareAndWordBased()
    {
        // Arrange: Create a PDF with both boxed digits and regular text
        var pdfPath = Path.Combine(_testDir, "mixed_content.pdf");
        CreatePdfWithMixedContent(pdfPath);

        var numericRule = new RedactionRule
        {
            Pattern = @"\d{4}",
            IsRegex = true,
            FragmentAware = true
        };

        var textRule = new RedactionRule
        {
            Pattern = "CONFIDENTIAL",
            IsRegex = false,
            FragmentAware = false
        };

        var locator = new PdfPigTextLocator();

        // Act
        var plan = locator.LocateText(pdfPath, new[] { numericRule, textRule });

        // Assert - Should find both the boxed digits and the regular text
        Assert.Equal(2, plan.TotalRedactions);
        Assert.Equal(2, plan.Regions.Count);

        // Cleanup
        File.Delete(pdfPath);
    }

    /// <summary>
    /// Creates a test PDF with individual digits spaced apart (simulating boxed form fields).
    /// </summary>
    private void CreatePdfWithBoxedDigits(string pdfPath, string digits, double spacing)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var gfx = XGraphics.FromPdfPage(page);
        var font = new XFont("Arial", 12);

        double x = 100;
        double y = 400;

        // Draw each digit separately with spacing
        foreach (var digit in digits)
        {
            gfx.DrawString(digit.ToString(), font, XBrushes.Black, x, y);
            x += spacing;
        }

        document.Save(pdfPath);
        document.Close();
    }

    /// <summary>
    /// Creates a PDF with multiple boxed digit sequences on the same page.
    /// </summary>
    private void CreatePdfWithMultipleBoxedSequences(string pdfPath)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var gfx = XGraphics.FromPdfPage(page);
        var font = new XFont("Arial", 12);

        // First sequence
        double x = 100;
        double y = 400;
        foreach (var digit in "1234")
        {
            gfx.DrawString(digit.ToString(), font, XBrushes.Black, x, y);
            x += 15;
        }

        // Second sequence on a different line
        x = 100;
        y = 350;
        foreach (var digit in "5678")
        {
            gfx.DrawString(digit.ToString(), font, XBrushes.Black, x, y);
            x += 15;
        }

        document.Save(pdfPath);
        document.Close();
    }

    /// <summary>
    /// Creates a PDF with both boxed digits and regular text.
    /// </summary>
    private void CreatePdfWithMixedContent(string pdfPath)
    {
        var document = new PdfDocument();
        var page = document.AddPage();
        var gfx = XGraphics.FromPdfPage(page);
        var font = new XFont("Arial", 12);

        // Regular text
        gfx.DrawString("CONFIDENTIAL", font, XBrushes.Black, 100, 450);

        // Boxed digits
        double x = 100;
        double y = 400;
        foreach (var digit in "9876")
        {
            gfx.DrawString(digit.ToString(), font, XBrushes.Black, x, y);
            x += 15;
        }

        document.Save(pdfPath);
        document.Close();
    }
}
