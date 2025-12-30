# PdfRedact

PdfRedact is a free, open-source .NET 8 tool for masking sensitive text in PDFs. It detects text using configurable rules, applies visual mask overlays with PDFsharp, and includes page flattening to convert PDFs to image-only format, preventing underlying text extraction.

## Features

- **Text Location**: Uses PdfPig to locate sensitive text via regex patterns or literal text matching
- **Redaction Masking**: Applies opaque black overlays using PDFsharp to visually hide sensitive information
- **Page Flattening**: Converts PDFs to image-only format, removing all text layers for maximum security
- **Two-Step Workflow**: Create redaction plans first, review them, then apply
- **One-Step Workflow**: Combine plan creation and application in a single command
- **Case-Sensitive/Insensitive Matching**: Flexible text matching options
- **Multiple Patterns**: Support for multiple search patterns in a single operation
- **JSON Plan Format**: Human-readable redaction plans for review and reuse
- **Cross-Platform**: Built on .NET 8, runs on Windows, macOS, and Linux

## Project Structure

```
PdfRedact/
├── src/
│   ├── PdfRedact.Core/         # Core library with redaction logic
│   │   ├── Models/              # Data models (RedactionPlan, RedactionRegion, RedactionRule)
│   │   └── Services/            # Services (text location, mask application, serialization)
│   └── PdfRedact.CLI/           # Command-line interface
│       └── Commands/            # CLI commands (plan, apply, redact, flatten)
├── tests/
│   └── PdfRedact.Core.Tests/   # Unit tests for Core library
└── docs/
    └── FLATTEN_MODE.md          # Flatten mode documentation
```

## Installation

### Prerequisites
- .NET 8.0 SDK or later

### Build from Source
```bash
git clone https://github.com/daviburg/PdfRedact.git
cd PdfRedact
dotnet build --configuration Release
```

### Run the CLI
```bash
dotnet run --project src/PdfRedact.CLI/PdfRedact.CLI.csproj -- [command] [options]
```

Or build and use the executable:
```bash
cd src/PdfRedact.CLI/bin/Release/net8.0
./PdfRedact.CLI [command] [options]
```

## Usage

### Commands

#### 1. `plan` - Create a Redaction Plan

Analyze a PDF and create a plan identifying text regions to redact:

```bash
# Find and mark SSN patterns (regex)
dotnet run --project src/PdfRedact.CLI/PdfRedact.CLI.csproj -- plan \
  -i input.pdf \
  -o redaction-plan.json \
  -p "\d{3}-\d{2}-\d{4}" \
  --regex

# Find literal text (case-insensitive)
dotnet run --project src/PdfRedact.CLI/PdfRedact.CLI.csproj -- plan \
  -i input.pdf \
  -o plan.json \
  -p "confidential" \
  -p "internal use only" \
  --case-sensitive false

# Multiple patterns with regex
dotnet run --project src/PdfRedact.CLI/PdfRedact.CLI.csproj -- plan \
  -i input.pdf \
  -o plan.json \
  -p "\d{3}-\d{2}-\d{4}" \
  -p "\d{4}-\d{4}-\d{4}-\d{4}" \
  -p "[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}" \
  --regex
```

**Options:**
- `-i, --input` (required): Input PDF file path
- `-o, --output` (required): Output JSON plan file path
- `-p, --pattern` (required): Text pattern(s) to redact (can specify multiple times)
- `-r, --regex`: Treat patterns as regular expressions (default: false)
- `-c, --case-sensitive`: Case-sensitive matching (default: true)

#### 2. `apply` - Apply a Redaction Plan

Apply a previously created redaction plan to generate a redacted PDF:

```bash
dotnet run --project src/PdfRedact.CLI/PdfRedact.CLI.csproj -- apply \
  -p redaction-plan.json \
  -o redacted-output.pdf
```

**Options:**
- `-p, --plan` (required): Redaction plan JSON file path
- `-o, --output` (required): Output redacted PDF file path

#### 3. `redact` - One-Step Redaction

Combine plan creation and application in a single command:

```bash
# Quick redaction with literal text
dotnet run --project src/PdfRedact.CLI/PdfRedact.CLI.csproj -- redact \
  -i input.pdf \
  -o redacted.pdf \
  -p "CONFIDENTIAL"

# With regex patterns and save the plan
dotnet run --project src/PdfRedact.CLI/PdfRedact.CLI.csproj -- redact \
  -i input.pdf \
  -o redacted.pdf \
  -p "\d{3}-\d{2}-\d{4}" \
  --regex \
  --save-plan plan.json
```

**Options:**
- `-i, --input` (required): Input PDF file path
- `-o, --output` (required): Output redacted PDF file path
- `-p, --pattern` (required): Text pattern(s) to redact (can specify multiple times)
- `-r, --regex`: Treat patterns as regular expressions (default: false)
- `-c, --case-sensitive`: Case-sensitive matching (default: true)
- `-s, --save-plan`: Optional path to save the redaction plan

#### 4. `flatten` - Convert PDF to Image-Only Format

Flatten a PDF by converting each page to a bitmap image, removing all text layers:

```bash
# Flatten a PDF with default 300 DPI
dotnet run --project src/PdfRedact.CLI/PdfRedact.CLI.csproj -- flatten \
  -i input.pdf \
  -o flattened.pdf

# Flatten with custom DPI for smaller file size
dotnet run --project src/PdfRedact.CLI/PdfRedact.CLI.csproj -- flatten \
  -i input.pdf \
  -o flattened.pdf \
  --dpi 150
```

**Options:**
- `-i, --input` (required): Input PDF file path
- `-o, --output` (required): Output flattened PDF file path
- `-d, --dpi`: Resolution for rendering (default: 300, range: 72-600)

**Note:** Flattening is useful after redaction to ensure no underlying text can be extracted. Higher DPI produces better quality but larger files.

## Examples

### Redact Social Security Numbers
```bash
dotnet run --project src/PdfRedact.CLI/PdfRedact.CLI.csproj -- redact \
  -i document.pdf \
  -o redacted.pdf \
  -p "\d{3}-\d{2}-\d{4}" \
  --regex
```

### Redact Credit Card Numbers
```bash
dotnet run --project src/PdfRedact.CLI/PdfRedact.CLI.csproj -- redact \
  -i financial.pdf \
  -o redacted.pdf \
  -p "\d{4}[- ]?\d{4}[- ]?\d{4}[- ]?\d{4}" \
  --regex
```

### Redact Email Addresses
```bash
dotnet run --project src/PdfRedact.CLI/PdfRedact.CLI.csproj -- redact \
  -i emails.pdf \
  -o redacted.pdf \
  -p "[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}" \
  --regex
```

### Review Before Applying
```bash
# Step 1: Create plan
dotnet run --project src/PdfRedact.CLI/PdfRedact.CLI.csproj -- plan \
  -i document.pdf \
  -o plan.json \
  -p "sensitive_keyword"

# Step 2: Review plan.json, modify if needed

# Step 3: Apply the plan
dotnet run --project src/PdfRedact.CLI/PdfRedact.CLI.csproj -- apply \
  -p plan.json \
  -o redacted.pdf
```

### Secure Redaction with Flattening
```bash
# Step 1: Redact sensitive content
dotnet run --project src/PdfRedact.CLI/PdfRedact.CLI.csproj -- redact \
  -i document.pdf \
  -o redacted.pdf \
  -p "\d{3}-\d{2}-\d{4}" \
  --regex

# Step 2: Flatten to remove all text layers
dotnet run --project src/PdfRedact.CLI/PdfRedact.CLI.csproj -- flatten \
  -i redacted.pdf \
  -o secure-redacted.pdf
```

## Redaction Plan Format

The redaction plan is stored as JSON:

```json
{
  "sourcePdfPath": "input.pdf",
  "regions": [
    {
      "pageNumber": 1,
      "x": 100.5,
      "y": 200.3,
      "width": 150.7,
      "height": 20.1,
      "matchedText": "123-45-6789",
      "rulePattern": "\\d{3}-\\d{2}-\\d{4}"
    }
  ],
  "totalRedactions": 1
}
```

## Testing

Run the test suite:
```bash
dotnet test
```

## Security Considerations

- **Visual Redaction**: The `redact` command applies opaque masks over text. The underlying text remains in the PDF file structure.
- **Maximum Security**: Use the `flatten` command after redaction to convert pages to bitmap images, completely removing text layers.
  - **Guarantee**: Flattened PDFs contain NO extractable text - verified by automated tests
  - **Implementation**: Uses PDFium-based rendering to create image-only PDFs
  - **Irreversible**: Once flattened, text cannot be recovered
- **Platform Requirements**: Flatten command requires PDFium native libraries (included via PDFtoImage package)
  - Supported: Windows, Linux, macOS
  - Runtime validation will provide clear error if PDFium cannot be loaded
- **Review Plans**: Always review redaction plans before applying to ensure correct regions are identified.
- **Test First**: Test on sample documents to verify patterns match correctly.
- **Recommended Workflow**: `redact` → `flatten` for forensically secure redaction.

## Dependencies

- **PdfPig** (0.1.13): Text extraction and location
- **PDFsharp** (6.2.3): PDF manipulation and rendering
- **PDFtoImage** (5.2.0): PDF to image conversion for flattening
- **System.CommandLine** (2.0.0-beta4): CLI framework

## Contributing

Contributions are welcome! Please feel free to submit pull requests or open issues for bugs and feature requests.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Third-Party Licenses

This project uses the following third-party libraries:

- **PdfPig** (0.1.13) - Licensed under [Apache License 2.0](https://github.com/UglyToad/PdfPig/blob/master/LICENSE)
  - Used for PDF text extraction and location
  - Copyright © 2017-2024 UglyToad Software

- **PDFsharp** (6.2.3) - Licensed under [MIT License](https://github.com/empira/PDFsharp/blob/master/LICENSE.md)
  - Used for PDF manipulation and rendering
  - Copyright © 2005-2024 empira Software GmbH

- **PDFtoImage** (5.2.0) - Licensed under [MIT License](https://github.com/sungaila/PDFtoImage/blob/master/LICENSE)
  - Used for PDF page rendering to images (flattening)
  - Copyright © 2021-2024 David Sungaila

- **System.CommandLine** (2.0.0-beta4) - Licensed under [MIT License](https://github.com/dotnet/command-line-api/blob/main/LICENSE.md)
  - Used for command-line interface framework
  - Copyright © .NET Foundation and Contributors

For complete license texts and additional dependencies, please refer to the individual package documentation.

## Credits

Built with:
- [PdfPig](https://github.com/UglyToad/PdfPig) - PDF text extraction
- [PDFsharp](https://www.pdfsharp.net/) - PDF creation and manipulation
- [PDFtoImage](https://github.com/sungaila/PDFtoImage) - PDF to image conversion
