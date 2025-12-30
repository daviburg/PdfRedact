using PdfRedact.Core.Services;
using PdfSharp.Pdf;
using PdfSharp.Drawing;
using System.Runtime.Versioning;

namespace PdfRedact.Core.Tests;

[SupportedOSPlatform("Windows")]
[SupportedOSPlatform("Linux")]
[SupportedOSPlatform("macOS")]
public class PageFlattenerTests
{
    [Fact]
    public void FlattenPdf_RemovesAllExtractableText()
    {
        // Arrange
        var inputPath = Path.GetTempFileName() + ".pdf";
        var outputPath = Path.GetTempFileName() + ".pdf";
        const string testText = "This text should not be extractable after flattening";

        try
        {
            // Create a simple test PDF with text using Python (more reliable for testing)
            CreateTestPdfWithPython(inputPath, testText);

            // Verify the input PDF contains extractable text
            using (var inputDoc = UglyToad.PdfPig.PdfDocument.Open(inputPath))
            {
                var inputPage = inputDoc.GetPage(1);
                var inputText = inputPage.Text;
                Assert.Contains("extractable", inputText.ToLower());
            }

            var flattener = new PdfToImageFlattener();

            // Act - Flatten the PDF
            flattener.FlattenPdf(inputPath, outputPath, dpi: 150);

            // Assert - Verify output PDF exists
            Assert.True(File.Exists(outputPath), "Flattened PDF should exist");

            // Assert - Verify NO text can be extracted from the flattened PDF
            using (var outputDoc = UglyToad.PdfPig.PdfDocument.Open(outputPath))
            {
                var outputPage = outputDoc.GetPage(1);
                var extractedText = outputPage.Text.Trim();
                
                // The flattened PDF should contain NO extractable text
                Assert.True(
                    string.IsNullOrWhiteSpace(extractedText),
                    $"Flattened PDF should contain no extractable text, but found: '{extractedText}'"
                );
            }
        }
        finally
        {
            // Cleanup
            if (File.Exists(inputPath)) File.Delete(inputPath);
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }
    }

    [Fact]
    public void FlattenPdf_PreservesPageDimensions()
    {
        // Arrange
        var inputPath = Path.GetTempFileName() + ".pdf";
        var outputPath = Path.GetTempFileName() + ".pdf";

        try
        {
            // Create a test PDF with known dimensions
            CreateTestPdfWithPython(inputPath, "Test content");

            double inputWidth, inputHeight;
            using (var inputDoc = PdfSharp.Pdf.IO.PdfReader.Open(inputPath, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import))
            {
                var inputPage = inputDoc.Pages[0];
                inputWidth = inputPage.Width.Point;
                inputHeight = inputPage.Height.Point;
            }

            var flattener = new PdfToImageFlattener();

            // Act
            flattener.FlattenPdf(inputPath, outputPath, dpi: 150);

            // Assert - Page dimensions should be preserved (within reasonable tolerance for DPI conversion)
            using (var outputDoc = PdfSharp.Pdf.IO.PdfReader.Open(outputPath, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import))
            {
                var outputPage = outputDoc.Pages[0];
                var outputWidth = outputPage.Width.Point;
                var outputHeight = outputPage.Height.Point;

                // Allow 1 point tolerance for rounding
                Assert.True(Math.Abs(inputWidth - outputWidth) <= 1.0, 
                    $"Width mismatch: expected ~{inputWidth}, got {outputWidth}");
                Assert.True(Math.Abs(inputHeight - outputHeight) <= 1.0,
                    $"Height mismatch: expected ~{inputHeight}, got {outputHeight}");
            }
        }
        finally
        {
            if (File.Exists(inputPath)) File.Delete(inputPath);
            if (File.Exists(outputPath)) File.Delete(outputPath);
        }
    }

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

    /// <summary>
    /// Helper method to create a test PDF with text content using Python's reportlab.
    /// This is more reliable than PDFsharp for test PDF creation.
    /// </summary>
    private void CreateTestPdfWithPython(string path, string text)
    {
        var pythonScript = $@"
import sys
try:
    from reportlab.pdfgen import canvas
    from reportlab.lib.pagesizes import letter
    
    c = canvas.Canvas(r'{path}', pagesize=letter)
    c.setFont('Helvetica', 20)
    c.drawString(50, 750, r'{text}')
    c.save()
    sys.exit(0)
except ImportError:
    # reportlab not available, skip this test
    sys.exit(42)
except Exception as e:
    print(f'Error: {{e}}', file=sys.stderr)
    sys.exit(1)
";

        var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "python3",
                Arguments = "-c \"" + pythonScript.Replace("\"", "\\\"") + "\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        process.Start();
        process.WaitForExit();

        if (process.ExitCode == 42)
        {
            // reportlab not available, skip test
            throw new Exception("SKIPPED: Python reportlab not available for test PDF creation");
        }

        if (process.ExitCode != 0 || !File.Exists(path))
        {
            throw new InvalidOperationException($"Failed to create test PDF: {process.StandardError.ReadToEnd()}");
        }
    }
}
