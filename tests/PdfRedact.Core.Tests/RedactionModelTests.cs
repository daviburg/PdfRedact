using PdfRedact.Core.Models;

namespace PdfRedact.Core.Tests;

public class RedactionModelTests
{
    [Fact]
    public void RedactionPlan_TotalRedactions_ReturnsCorrectCount()
    {
        // Arrange
        var plan = new RedactionPlan
        {
            SourcePdfPath = "/test/path.pdf",
            Regions = new List<RedactionRegion>
            {
                new RedactionRegion { PageNumber = 1 },
                new RedactionRegion { PageNumber = 2 },
                new RedactionRegion { PageNumber = 3 }
            }
        };

        // Act
        var count = plan.TotalRedactions;

        // Assert
        Assert.Equal(3, count);
    }

    [Fact]
    public void RedactionPlan_EmptyRegions_ReturnsZero()
    {
        // Arrange
        var plan = new RedactionPlan
        {
            SourcePdfPath = "/test/path.pdf"
        };

        // Act
        var count = plan.TotalRedactions;

        // Assert
        Assert.Equal(0, count);
    }

    [Fact]
    public void RedactionRegion_CanSetAllProperties()
    {
        // Arrange & Act
        var region = new RedactionRegion
        {
            PageNumber = 5,
            X = 100.5,
            Y = 200.3,
            Width = 150.7,
            Height = 20.1,
            MatchedText = "test text",
            RulePattern = @"\d{3}-\d{3}-\d{4}"
        };

        // Assert
        Assert.Equal(5, region.PageNumber);
        Assert.Equal(100.5, region.X);
        Assert.Equal(200.3, region.Y);
        Assert.Equal(150.7, region.Width);
        Assert.Equal(20.1, region.Height);
        Assert.Equal("test text", region.MatchedText);
        Assert.Equal(@"\d{3}-\d{3}-\d{4}", region.RulePattern);
    }

    [Fact]
    public void RedactionRule_DefaultValues()
    {
        // Arrange & Act
        var rule = new RedactionRule();

        // Assert
        Assert.False(rule.IsRegex);
        Assert.True(rule.CaseSensitive);
        Assert.Equal(string.Empty, rule.Pattern);
    }

    [Fact]
    public void RedactionRule_CanSetAllProperties()
    {
        // Arrange & Act
        var rule = new RedactionRule
        {
            Pattern = @"\d{3}-\d{2}-\d{4}",
            IsRegex = true,
            CaseSensitive = false,
            Description = "SSN pattern"
        };

        // Assert
        Assert.Equal(@"\d{3}-\d{2}-\d{4}", rule.Pattern);
        Assert.True(rule.IsRegex);
        Assert.False(rule.CaseSensitive);
        Assert.Equal("SSN pattern", rule.Description);
    }
}
