# Flatten Mode

## Overview
The "flatten" mode provides an additional layer of security by rendering PDF pages to bitmap images using **PDFium**, completely removing the underlying text layer.

## Purpose
While the current redaction approach applies opaque masks over sensitive text, the underlying text content remains in the PDF file. The flatten mode:

1. **Renders each page to a bitmap** using PDFium at the requested DPI
2. **Reconstructs the PDF** with bitmap images as the page content
3. **Ensures no text content remains** in the PDF file structure

**Verification**: Automated tests prove that text extraction returns empty results from flattened PDFs.

## Implementation

### Core Technology
- **PDFium**: Industry-standard PDF rendering engine (via PDFtoImage library)
- **PDFsharp**: PDF reconstruction with embedded images
- **SkiaSharp**: Image processing and encoding

### Core Components

#### Services
- `IPageFlattener`: Interface for page flattening service
- `PdfToImageFlattener`: Implementation using PDFtoImage (PDFium-based) and PDFsharp
  - Uses PDFium for high-quality bitmap rendering
  - Supports configurable DPI (72-600)
  - Outputs JPEG-encoded images at 90% quality for efficiency
  - Sequential page processing minimizes memory usage

#### CLI Command
- `flatten` command: Converts a PDF to an image-only PDF
  - `-i, --input` (required): Input PDF file path
  - `-o, --output` (required): Output flattened PDF file path
  - `--dpi` (optional): Resolution for rendering (default: 300, range: 72-600)

## Platform Support

**Supported Platforms**: Windows, Linux, macOS

The flatten command requires native PDFium binaries, which are included as NuGet dependencies:
- Windows: `bblanchon.PDFium.Win32`
- Linux: `bblanchon.PDFium.Linux`
- macOS: `bblanchon.PDFium.macOS`

**Runtime Validation**: If PDFium cannot be loaded, the command will fail with a clear error message indicating the platform support issue.

## Usage

```bash
# Flatten a PDF with default 300 DPI
dotnet run --project src/PdfRedact.CLI/PdfRedact.CLI.csproj -- flatten \
  -i input.pdf \
  -o flattened.pdf

# Flatten with custom DPI
dotnet run --project src/PdfRedact.CLI/PdfRedact.CLI.csproj -- flatten \
  -i input.pdf \
  -o flattened.pdf \
  --dpi 150

# Typical workflow: redact then flatten
# Step 1: Apply redactions
dotnet run --project src/PdfRedact.CLI/PdfRedact.CLI.csproj -- redact \
  -i document.pdf \
  -o redacted.pdf \
  -p "\d{3}-\d{2}-\d{4}" \
  --regex

# Step 2: Flatten to remove text layer
dotnet run --project src/PdfRedact.CLI/PdfRedact.CLI.csproj -- flatten \
  -i redacted.pdf \
  -o flattened.pdf
```

## Security Considerations
- **Flattening is irreversible** - all text becomes image data
- **No text extraction possible** - verified by automated tests using PdfPig
- **PDFium-based rendering** - industry-standard PDF engine ensures complete rasterization
- **Higher DPI = better quality but larger file size**
- **Recommended DPI**: 300 for standard documents, 150 for smaller files
- This prevents ALL text extraction tools from accessing any content
- **Verified guarantee**: Flattened PDFs have zero extractable characters

## Technical Details
- **Rendering Engine**: PDFium (Google's PDF library)
- **Image Format**: JPEG at 90% quality for balanced size/quality
- **Supported Platforms**: Windows, Linux, and macOS (native binaries included)
- **Memory Management**: Sequential page processing with immediate disposal
- **Memory Impact**: ~(width × height × 4) bytes per page during processing
- For large PDFs, process one page at a time to minimize memory footprint
