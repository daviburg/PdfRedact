using System.CommandLine;
using PdfRedact.Core.Models;
using PdfRedact.Core.Services;

namespace PdfRedact.CLI.Commands;

/// <summary>
/// Command to create a redaction plan by locating text in a PDF.
/// </summary>
public static class PlanCommand
{
    public static Command Create()
    {
        var inputOption = CreateInputOption();
        var outputOption = CreateOutputOption();
        var patternOption = CreatePatternOption();
        var regexOption = CreateRegexOption();
        var caseSensitiveOption = CreateCaseSensitiveOption();
        var fragmentAwareOption = CreateFragmentAwareOption();
        var noFragmentAwareOption = CreateNoFragmentAwareOption();

        var command = new Command("plan", "Create a redaction plan by locating text in a PDF")
        {
            inputOption,
            outputOption,
            patternOption,
            regexOption,
            caseSensitiveOption,
            fragmentAwareOption,
            noFragmentAwareOption
        };

        command.SetHandler(async (input, output, patterns, isRegex, caseSensitive, fragmentAware, noFragmentAware) =>
        {
            await ExecutePlanCommand(input, output, patterns, isRegex, caseSensitive, fragmentAware, noFragmentAware);
        },
        inputOption,
        outputOption,
        patternOption,
        regexOption,
        caseSensitiveOption,
        fragmentAwareOption,
        noFragmentAwareOption);

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
            description: "Path to save the redaction plan JSON file"
        );
        option.IsRequired = true;
        return option;
    }

    private static Option<string[]> CreatePatternOption()
    {
        var option = new Option<string[]>(
            aliases: new[] { "--pattern", "-p" },
            description: "Text pattern(s) to redact. Can be specified multiple times."
        );
        option.IsRequired = true;
        option.AllowMultipleArgumentsPerToken = true;
        return option;
    }

    private static Option<bool> CreateRegexOption()
    {
        return new Option<bool>(
            aliases: new[] { "--regex", "-r" },
            description: "Treat patterns as regular expressions",
            getDefaultValue: () => false
        );
    }

    private static Option<bool> CreateCaseSensitiveOption()
    {
        return new Option<bool>(
            aliases: new[] { "--case-sensitive", "-c" },
            description: "Perform case-sensitive matching",
            getDefaultValue: () => true
        );
    }

    private static Option<bool> CreateFragmentAwareOption()
    {
        return new Option<bool>(
            aliases: new[] { "--fragment-aware" },
            description: "Force enable fragment-aware mode for matching boxed digits and fragmented text",
            getDefaultValue: () => false
        );
    }

    private static Option<bool> CreateNoFragmentAwareOption()
    {
        return new Option<bool>(
            aliases: new[] { "--no-fragment-aware" },
            description: "Force disable fragment-aware mode (use word-based matching only)",
            getDefaultValue: () => false
        );
    }

    private static async Task ExecutePlanCommand(
        string input,
        string output,
        string[] patterns,
        bool isRegex,
        bool caseSensitive,
        bool fragmentAware,
        bool noFragmentAware)
    {
        try
        {
            Console.WriteLine($"Creating redaction plan for: {input}");
            Console.WriteLine($"Patterns ({patterns.Length}): {string.Join(", ", patterns)}");
            Console.WriteLine($"Mode: {(isRegex ? "Regex" : "Literal")}");
            Console.WriteLine($"Case-sensitive: {caseSensitive}");
            
            // Determine fragment-aware setting
            bool? fragmentAwareSetting = null;
            if (fragmentAware && noFragmentAware)
            {
                Console.Error.WriteLine("Error: Cannot specify both --fragment-aware and --no-fragment-aware");
                Environment.Exit(1);
            }
            else if (fragmentAware)
            {
                fragmentAwareSetting = true;
                Console.WriteLine($"Fragment-aware: enabled (forced)");
            }
            else if (noFragmentAware)
            {
                fragmentAwareSetting = false;
                Console.WriteLine($"Fragment-aware: disabled (forced)");
            }
            else
            {
                Console.WriteLine($"Fragment-aware: auto-detect (enabled for numeric patterns)");
            }
            
            Console.WriteLine();

            var rules = patterns.Select(p => new RedactionRule
            {
                Pattern = p,
                IsRegex = isRegex,
                CaseSensitive = caseSensitive,
                FragmentAware = fragmentAwareSetting
            }).ToList();

            var locator = new PdfPigTextLocator();
            var plan = locator.LocateText(input, rules);

            Console.WriteLine($"Found {plan.TotalRedactions} region(s) to redact");

            var serializer = new JsonRedactionPlanSerializer();
            serializer.SavePlan(plan, output);

            Console.WriteLine($"Redaction plan saved to: {output}");
            
            // Display summary
            if (plan.TotalRedactions > 0)
            {
                Console.WriteLine();
                Console.WriteLine("Summary:");
                var pageGroups = plan.Regions.GroupBy(r => r.PageNumber);
                foreach (var group in pageGroups.OrderBy(g => g.Key))
                {
                    Console.WriteLine($"  Page {group.Key}: {group.Count()} redaction(s)");
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }

        await Task.CompletedTask;
    }
}
