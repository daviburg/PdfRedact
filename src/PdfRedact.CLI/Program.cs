using System.CommandLine;
using PdfRedact.CLI.Commands;

var rootCommand = new RootCommand("PdfRedact - A tool for redacting sensitive text in PDF documents");

// Add commands
rootCommand.AddCommand(PlanCommand.Create());
rootCommand.AddCommand(ApplyCommand.Create());
rootCommand.AddCommand(RedactCommand.Create());
#pragma warning disable CA1416 // Validate platform compatibility
rootCommand.AddCommand(FlattenCommand.Create());
#pragma warning restore CA1416 // Validate platform compatibility

return await rootCommand.InvokeAsync(args);
