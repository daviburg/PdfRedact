# Future Enhancement: Flatten Mode

## Overview
The "flatten" mode is a planned enhancement that will provide an additional layer of security by rendering PDF pages to bitmap images, completely removing the underlying text layer.

## Purpose
While the current redaction approach applies opaque masks over sensitive text, the underlying text content remains in the PDF file. For maximum security, the flatten mode will:

1. Render each page of the PDF to a high-resolution bitmap image
2. Reconstruct the PDF with these images as the page content
3. Ensure no text content remains in the PDF file structure

## Implementation Structure

### Core Components
The structure for this feature is prepared for future implementation:

#### Models
- `FlattenOptions`: Configuration for flattening (DPI, image format, compression)

#### Services
- `IPageFlattener`: Interface for page flattening service
  - `FlattenPdf(string sourcePath, string outputPath, FlattenOptions options)`
  - Renders each page to bitmap and reconstructs PDF

#### CLI Command
- `flatten` command: Applies redactions and flattens the PDF in one operation
  - `--dpi`: Resolution for rendering (default: 300)
  - `--format`: Image format (PNG, JPEG)
  - `--quality`: JPEG quality if applicable

### Implementation Notes
When implementing this feature:
1. Consider using libraries like Magick.NET or SkiaSharp for rendering
2. Ensure high-fidelity rendering to preserve non-redacted content quality
3. Optimize file size with appropriate compression
4. Provide progress feedback for large PDFs
5. Handle memory efficiently when processing large documents

## Usage (Planned)
```bash
# Apply redactions and flatten in one step
pdfredact flatten -i input.pdf -o output.pdf -p "sensitive" --dpi 300

# Flatten an already redacted PDF
pdfredact flatten -i redacted.pdf -o flattened.pdf
```

## Security Considerations
- Flattening is irreversible - all text becomes image data
- Higher DPI = better quality but larger file size
- Recommended DPI: 300 for standard documents, 600 for documents requiring OCR later
- This prevents text extraction tools from accessing redacted content
