# PdfRedact

PdfRedact is a free, open-source .NET 8 tool for masking sensitive text in PDFs. It detects text using configurable rules, applies visual mask overlays with PDFsharp, and is structured for future enhancements like flattening pages into image-only PDFs to prevent underlying text extraction.

## Features

- **Text Location**: Uses PdfPig to locate sensitive text via regex patterns or literal text matching
- **Fragment-Aware Matching**: Intelligently handles "boxed digits" and fragmented text sequences (common in government forms like IRS Form 8879)
- **Redaction Masking**: Applies opaque black overlays using PDFsharp to visually hide sensitive information
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
│       └── Commands/            # CLI commands (plan, apply, redact)
├── tests/
│   └── PdfRedact.Core.Tests/   # Unit tests for Core library
└── docs/
    └── FLATTEN_MODE.md          # Future enhancement documentation
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
- `--fragment-aware`: Force enable fragment-aware mode for boxed digits/fragmented text
- `--no-fragment-aware`: Force disable fragment-aware mode (use word-based matching only)

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
- `--fragment-aware`: Force enable fragment-aware mode for boxed digits/fragmented text
- `--no-fragment-aware`: Force disable fragment-aware mode (use word-based matching only)

## Fragment-Aware Matching

PdfRedact includes intelligent fragment-aware matching for handling "boxed digits" and other fragmented text sequences commonly found in government forms (e.g., IRS Form 8879) and standardized documents where each character is placed in a separate box.

### How It Works

- **Two-Pass Tokenization**: First creates conservative word tokens with tight spacing, then joins adjacent single-digit tokens into digit-run tokens with permissive spacing
- **Conservative Auto-Detection**: Automatically enabled only for literal numeric patterns (e.g., "1234", "5678-9012") to avoid false positives
- **Explicit Control**: For regex patterns or mixed content, use `--fragment-aware` flag to enable

### When to Use

Fragment-aware mode is ideal for:
- Tax forms with boxed SSN or EIN fields
- Government forms with separated digit entry fields
- Standardized forms where each character has its own box
- Any PDF where numeric sequences are visually fragmented

**Important**: For regex patterns like `\d{4}`, you must explicitly enable fragment-aware mode with the `--fragment-aware` flag. Auto-detection only works for literal numeric patterns to prevent over-redaction.

### Usage

```bash
# Literal numeric patterns - auto-enabled
dotnet run --project src/PdfRedact.CLI/PdfRedact.CLI.csproj -- plan \
  -i form8879.pdf \
  -o plan.json \
  -p "1234"

# Regex patterns - requires explicit flag
dotnet run --project src/PdfRedact.CLI/PdfRedact.CLI.csproj -- plan \
  -i form8879.pdf \
  -o plan.json \
  -p "\d{4}" \
  --regex \
  --fragment-aware

# Force enable for all patterns
dotnet run --project src/PdfRedact.CLI/PdfRedact.CLI.csproj -- plan \
  -i form.pdf \
  -o plan.json \
  -p "ABC123" \
  --fragment-aware

# Force disable to use only word-based matching
dotnet run --project src/PdfRedact.CLI/PdfRedact.CLI.csproj -- plan \
  -i document.pdf \
  -o plan.json \
  -p "1234" \
  --no-fragment-aware
```

### Tuning

The algorithm uses a two-pass approach to avoid over-redaction:
1. **Word Formation** (Pass 1): Conservative gap threshold of 1.5× median width or 0.5× median height
2. **Digit-Run Formation** (Pass 2): Permissive gap threshold of 5× median width or 2.5× median height, but only joins single-digit tokens

This ensures that only actual digit sequences are joined, preventing entire lines from being redacted.

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

### Redact Boxed Digits (Government Forms)
```bash
# Redact last 4 digits of SSN in form with boxed entry fields
# Note: Regex patterns require explicit --fragment-aware flag
dotnet run --project src/PdfRedact.CLI/PdfRedact.CLI.csproj -- redact \
  -i form8879.pdf \
  -o redacted.pdf \
  -p "\d{4}" \
  --regex \
  --fragment-aware

# Literal numeric patterns auto-enable fragment-aware mode
dotnet run --project src/PdfRedact.CLI/PdfRedact.CLI.csproj -- redact \
  -i form8879.pdf \
  -o redacted.pdf \
  -p "5678"
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

- **Visual Redaction Only**: Current implementation applies opaque masks over text. The underlying text remains in the PDF file structure.
- **Not Forensically Secure**: For maximum security, see the planned [Flatten Mode](docs/FLATTEN_MODE.md) which will render pages to bitmap images.
- **Review Plans**: Always review redaction plans before applying to ensure correct regions are identified.
- **Test First**: Test on sample documents to verify patterns match correctly.

## Future Enhancements

See [docs/FLATTEN_MODE.md](docs/FLATTEN_MODE.md) for planned features including:
- Page flattening to bitmap PDFs
- Complete text layer removal
- Enhanced security for forensic scenarios

## Dependencies

- **PdfPig** (0.1.13): Text extraction and location
- **PDFsharp** (6.2.3): PDF manipulation and rendering
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

- **System.CommandLine** (2.0.0-beta4) - Licensed under [MIT License](https://github.com/dotnet/command-line-api/blob/main/LICENSE.md)
  - Used for command-line interface framework
  - Copyright © .NET Foundation and Contributors

For complete license texts and additional dependencies, please refer to the individual package documentation.

## Credits

Built with:
- [PdfPig](https://github.com/UglyToad/PdfPig) - PDF text extraction
- [PDFsharp](https://www.pdfsharp.net/) - PDF creation and manipulation
