using PdfRedact.Core.Services;
using System.Runtime.Versioning;

namespace PdfRedact.Core.Tests;

[SupportedOSPlatform("Windows")]
[SupportedOSPlatform("Linux")]
[SupportedOSPlatform("macOS")]
public class PageFlattenerTests
{
    [Fact]
    public void FlattenPdf_InvalidSourcePath_ThrowsFileNotFoundException()
    {
        // Arrange
        var flattener = new PdfToImageFlattener();
        var nonExistentPath = "/path/does/not/exist.pdf";
        var outputPath = Path.GetTempFileName() + ".pdf";

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() =>
            flattener.FlattenPdf(nonExistentPath, outputPath));
    }

    [Fact]
    public void FlattenPdf_InvalidDpi_ThrowsArgumentException()
    {
        // Arrange
        var flattener = new PdfToImageFlattener();
        
        // Act & Assert - Test DPI too low
        Assert.Throws<ArgumentException>(() =>
            flattener.FlattenPdf("dummy.pdf", "output.pdf", dpi: 50));
            
        // Act & Assert - Test DPI too high
        Assert.Throws<ArgumentException>(() =>
            flattener.FlattenPdf("dummy.pdf", "output.pdf", dpi: 700));
    }

    [Fact]
    public void FlattenPdf_EmptySourcePath_ThrowsArgumentException()
    {
        // Arrange
        var flattener = new PdfToImageFlattener();
        var outputPath = Path.GetTempFileName() + ".pdf";

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            flattener.FlattenPdf("", outputPath));
    }

    [Fact]
    public void FlattenPdf_EmptyOutputPath_ThrowsArgumentException()
    {
        // Arrange
        var flattener = new PdfToImageFlattener();
        var inputPath = "test.pdf";

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            flattener.FlattenPdf(inputPath, ""));
    }
}
