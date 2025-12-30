# Flatten Mode

## Overview
The "flatten" mode provides an additional layer of security by rendering PDF pages to bitmap images, completely removing the underlying text layer.

## Purpose
While the current redaction approach applies opaque masks over sensitive text, the underlying text content remains in the PDF file. The flatten mode:

1. Renders each page of the PDF to a high-resolution bitmap image
2. Reconstructs the PDF with these images as the page content
3. Ensures no text content remains in the PDF file structure

## Implementation

### Core Components

#### Services
- `IPageFlattener`: Interface for page flattening service
- `PdfToImageFlattener`: Implementation using PDFtoImage and PDFsharp
  - Uses PDFium for high-quality rendering
  - Supports configurable DPI (72-600)
  - Outputs JPEG-encoded images for efficiency

#### CLI Command
- `flatten` command: Converts a PDF to an image-only PDF
  - `-i, --input` (required): Input PDF file path
  - `-o, --output` (required): Output flattened PDF file path
  - `--dpi` (optional): Resolution for rendering (default: 300, range: 72-600)

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
- Flattening is irreversible - all text becomes image data
- Higher DPI = better quality but larger file size
- Recommended DPI: 300 for standard documents, 150 for smaller files
- This prevents text extraction tools from accessing any content
- Verified: Flattened PDFs have zero extractable characters

## Technical Details
- Uses PDFtoImage library (based on PDFium) for rendering
- Encodes images as JPEG at 90% quality for balanced size/quality
- Supports Windows, Linux, and macOS platforms
- Memory-efficient processing with streaming approach
