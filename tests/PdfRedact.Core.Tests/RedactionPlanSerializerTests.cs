using PdfRedact.Core.Models;
using PdfRedact.Core.Services;

namespace PdfRedact.Core.Tests;

public class RedactionPlanSerializerTests
{
    [Fact]
    public void SavePlan_CreatesValidJsonFile()
    {
        // Arrange
        var serializer = new JsonRedactionPlanSerializer();
        var plan = new RedactionPlan
        {
            SourcePdfPath = "/test/path.pdf",
            Regions = new List<RedactionRegion>
            {
                new RedactionRegion
                {
                    PageNumber = 1,
                    X = 100,
                    Y = 200,
                    Width = 150,
                    Height = 20,
                    MatchedText = "sensitive",
                    RulePattern = "sensitive"
                }
            }
        };
        
        var tempFile = Path.Combine(Path.GetTempPath(), $"test-plan-{Guid.NewGuid()}.json");

        try
        {
            // Act
            serializer.SavePlan(plan, tempFile);

            // Assert
            Assert.True(File.Exists(tempFile));
            var content = File.ReadAllText(tempFile);
            Assert.Contains("sourcePdfPath", content);
            Assert.Contains("/test/path.pdf", content);
            Assert.Contains("sensitive", content);
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void LoadPlan_ReturnsValidPlan()
    {
        // Arrange
        var serializer = new JsonRedactionPlanSerializer();
        var originalPlan = new RedactionPlan
        {
            SourcePdfPath = "/test/path.pdf",
            Regions = new List<RedactionRegion>
            {
                new RedactionRegion
                {
                    PageNumber = 1,
                    X = 100,
                    Y = 200,
                    Width = 150,
                    Height = 20,
                    MatchedText = "test",
                    RulePattern = "test"
                }
            }
        };
        
        var tempFile = Path.Combine(Path.GetTempPath(), $"test-plan-{Guid.NewGuid()}.json");

        try
        {
            serializer.SavePlan(originalPlan, tempFile);

            // Act
            var loadedPlan = serializer.LoadPlan(tempFile);

            // Assert
            Assert.NotNull(loadedPlan);
            Assert.Equal(originalPlan.SourcePdfPath, loadedPlan.SourcePdfPath);
            Assert.Single(loadedPlan.Regions);
            Assert.Equal(originalPlan.Regions[0].PageNumber, loadedPlan.Regions[0].PageNumber);
            Assert.Equal(originalPlan.Regions[0].MatchedText, loadedPlan.Regions[0].MatchedText);
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void LoadPlan_ThrowsFileNotFoundException_WhenFileDoesNotExist()
    {
        // Arrange
        var serializer = new JsonRedactionPlanSerializer();
        var nonExistentFile = "/path/that/does/not/exist.json";

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() => serializer.LoadPlan(nonExistentFile));
    }
}
