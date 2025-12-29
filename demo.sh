#!/bin/bash
set -e

echo "=== PdfRedact Demo ==="
echo ""

# Clean up any existing test outputs
rm -f test-data/demo-*.pdf test-data/demo-*.json

# Create test PDF if it doesn't exist
if [ ! -f test-data/test-document.pdf ]; then
    echo "Creating test PDF..."
    python3 -c "
from fpdf import FPDF
pdf = FPDF()
pdf.add_page()
pdf.set_font('Helvetica', size=12)
pdf.cell(0, 10, 'This is a test document.', ln=1)
pdf.cell(0, 10, 'It contains sensitive information like SSN: 123-45-6789.', ln=1)
pdf.cell(0, 10, 'Email: john.doe@example.com', ln=1)
pdf.cell(0, 10, 'Credit Card: 4532-1234-5678-9010', ln=1)
pdf.cell(0, 10, 'This text should remain visible.', ln=1)
pdf.add_page()
pdf.cell(0, 10, 'Page 2 content', ln=1)
pdf.cell(0, 10, 'More sensitive data: 987-65-4321', ln=1)
pdf.output('test-data/test-document.pdf')
" 2>/dev/null
fi

echo "1. Creating redaction plan to find SSN patterns..."
dotnet run --project src/PdfRedact.CLI/PdfRedact.CLI.csproj -- plan \
  -i test-data/test-document.pdf \
  -o test-data/demo-plan.json \
  -p "\d{3}-\d{2}-\d{4}" \
  --regex

echo ""
echo "2. Applying redaction plan..."
dotnet run --project src/PdfRedact.CLI/PdfRedact.CLI.csproj -- apply \
  -p test-data/demo-plan.json \
  -o test-data/demo-redacted.pdf

echo ""
echo "3. One-step redaction of 'Email:' text..."
dotnet run --project src/PdfRedact.CLI/PdfRedact.CLI.csproj -- redact \
  -i test-data/test-document.pdf \
  -o test-data/demo-email-redacted.pdf \
  -p "Email:"

echo ""
echo "=== Demo Complete ==="
echo "Generated files:"
ls -lh test-data/demo-*
