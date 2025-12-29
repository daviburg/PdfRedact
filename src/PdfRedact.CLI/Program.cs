using System.CommandLine;
using PdfRedact.CLI.Commands;

var rootCommand = new RootCommand("PdfRedact - A tool for redacting sensitive text in PDF documents");

// Add commands
rootCommand.AddCommand(PlanCommand.Create());
rootCommand.AddCommand(ApplyCommand.Create());
rootCommand.AddCommand(RedactCommand.Create());

return await rootCommand.InvokeAsync(args);
