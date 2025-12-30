using System.CommandLine;
using System.Runtime.Versioning;
using PdfRedact.Core.Services;

namespace PdfRedact.CLI.Commands;

/// <summary>
/// Command to flatten a PDF by converting pages to bitmap images.
/// This removes all text layers and creates an image-only PDF.
/// </summary>
[SupportedOSPlatform("Windows")]
[SupportedOSPlatform("Linux")]
[SupportedOSPlatform("macOS")]
public static class FlattenCommand
{
    public static Command Create()
    {
        var inputOption = CreateInputOption();
        var outputOption = CreateOutputOption();
        var dpiOption = CreateDpiOption();

        var command = new Command("flatten", "Flatten a PDF by converting pages to bitmap images, removing all text layers")
        {
            inputOption,
            outputOption,
            dpiOption
        };

        command.SetHandler(async (input, output, dpi) =>
        {
            await ExecuteFlattenCommand(input, output, dpi);
        },
        inputOption,
        outputOption,
        dpiOption);

        return command;
    }

    private static Option<string> CreateInputOption()
    {
        var option = new Option<string>(
            aliases: new[] { "--input", "-i" },
            description: "Path to the input PDF file"
        );
        option.IsRequired = true;
        return option;
    }

    private static Option<string> CreateOutputOption()
    {
        var option = new Option<string>(
            aliases: new[] { "--output", "-o" },
            description: "Path to save the flattened PDF file"
        );
        option.IsRequired = true;
        return option;
    }

    private static Option<int> CreateDpiOption()
    {
        return new Option<int>(
            aliases: new[] { "--dpi", "-d" },
            description: "Resolution for rendering pages (72-600 DPI)",
            getDefaultValue: () => 300
        );
    }

    private static async Task ExecuteFlattenCommand(string input, string output, int dpi)
    {
        try
        {
            Console.WriteLine($"Flattening PDF: {input}");
            Console.WriteLine($"DPI: {dpi}");
            Console.WriteLine();

            var flattener = new PdfToImageFlattener();
            flattener.FlattenPdf(input, output, dpi);

            Console.WriteLine($"Flattened PDF saved to: {output}");
            Console.WriteLine();
            Console.WriteLine("Note: All text layers have been removed. The PDF now contains only bitmap images.");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }

        await Task.CompletedTask;
    }
}
