using System.CommandLine;
using PdfRedact.Core.Services;

namespace PdfRedact.CLI.Commands;

/// <summary>
/// Command to apply an existing redaction plan to a PDF.
/// </summary>
public static class ApplyCommand
{
    public static Command Create()
    {
        var planOption = CreatePlanOption();
        var outputOption = CreateOutputOption();

        var command = new Command("apply", "Apply an existing redaction plan to a PDF")
        {
            planOption,
            outputOption
        };

        command.SetHandler(async (plan, output) =>
        {
            await ExecuteApplyCommand(plan, output);
        },
        planOption,
        outputOption);

        return command;
    }

    private static Option<string> CreatePlanOption()
    {
        var option = new Option<string>(
            aliases: new[] { "--plan", "-p" },
            description: "Path to the redaction plan JSON file"
        );
        option.IsRequired = true;
        return option;
    }

    private static Option<string> CreateOutputOption()
    {
        var option = new Option<string>(
            aliases: new[] { "--output", "-o" },
            description: "Path to save the redacted PDF file"
        );
        option.IsRequired = true;
        return option;
    }

    private static async Task ExecuteApplyCommand(string planPath, string output)
    {
        try
        {
            Console.WriteLine($"Loading redaction plan from: {planPath}");

            var serializer = new JsonRedactionPlanSerializer();
            var plan = serializer.LoadPlan(planPath);

            Console.WriteLine($"Source PDF: {plan.SourcePdfPath}");
            Console.WriteLine($"Redactions to apply: {plan.TotalRedactions}");
            Console.WriteLine();

            if (plan.TotalRedactions == 0)
            {
                Console.WriteLine("Warning: No redactions in plan. Copying source PDF to output.");
                File.Copy(plan.SourcePdfPath, output, overwrite: true);
            }
            else
            {
                var applicator = new PdfSharpMaskApplicator();
                applicator.ApplyMasks(plan, output);

                Console.WriteLine($"Applied {plan.TotalRedactions} redaction(s)");
            }

            Console.WriteLine($"Redacted PDF saved to: {output}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }

        await Task.CompletedTask;
    }
}
