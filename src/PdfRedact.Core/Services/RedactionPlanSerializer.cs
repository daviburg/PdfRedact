using System.Text.Json;
using PdfRedact.Core.Models;

namespace PdfRedact.Core.Services;

/// <summary>
/// Service for serializing and deserializing redaction plans.
/// </summary>
public interface IRedactionPlanSerializer
{
    /// <summary>
    /// Saves a redaction plan to a JSON file.
    /// </summary>
    /// <param name="plan">The redaction plan to save.</param>
    /// <param name="filePath">Path where the plan should be saved.</param>
    void SavePlan(RedactionPlan plan, string filePath);

    /// <summary>
    /// Loads a redaction plan from a JSON file.
    /// </summary>
    /// <param name="filePath">Path to the plan file.</param>
    /// <returns>The loaded redaction plan.</returns>
    RedactionPlan LoadPlan(string filePath);
}

/// <summary>
/// JSON-based implementation of redaction plan serialization.
/// </summary>
public class JsonRedactionPlanSerializer : IRedactionPlanSerializer
{
    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <inheritdoc/>
    public void SavePlan(RedactionPlan plan, string filePath)
    {
        if (plan == null)
        {
            throw new ArgumentNullException(nameof(plan));
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
        }

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(plan, _options);
        File.WriteAllText(filePath, json);
    }

    /// <inheritdoc/>
    public RedactionPlan LoadPlan(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Plan file not found", filePath);
        }

        var json = File.ReadAllText(filePath);
        var plan = JsonSerializer.Deserialize<RedactionPlan>(json, _options);

        if (plan == null)
        {
            throw new InvalidOperationException("Failed to deserialize redaction plan");
        }

        return plan;
    }
}
