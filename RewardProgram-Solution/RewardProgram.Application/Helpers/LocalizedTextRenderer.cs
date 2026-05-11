using System.Text.Json;
using Microsoft.Extensions.Localization;
using RewardProgram.Application.Errors;

namespace RewardProgram.Application.Helpers;

/// <summary>
/// Helpers for serializing/rendering Tranche-3 localized content
/// (notification titles+bodies, wallet transaction descriptions). Services write
/// the resource key plus JSON-encoded args at event time; read paths render the
/// localized string at request time using the current culture.
/// </summary>
public static class LocalizedTextRenderer
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };

    /// <summary>Serialize a sequence of arg values to a JSON array string (or null if empty).</summary>
    public static string? SerializeArgs(params object?[]? args)
    {
        if (args is null || args.Length == 0)
            return null;
        // Convert each arg to its string representation for storage stability —
        // resx format placeholders ({0}, {1}, ...) work on strings naturally.
        var asStrings = args.Select(a => a?.ToString() ?? string.Empty).ToArray();
        return JsonSerializer.Serialize(asStrings, JsonOpts);
    }

    /// <summary>
    /// Render the localized string for a given key+args. Falls back to <paramref name="legacy"/>
    /// if the key is null/missing (so old DB rows render in whatever language they were
    /// written in — typically Arabic).
    /// </summary>
    public static string Render(
        IStringLocalizer<ErrorMessages> localizer,
        string? key,
        string? argsJson,
        string? legacy)
    {
        if (string.IsNullOrEmpty(key))
            return legacy ?? string.Empty;

        var entry = localizer[key];
        if (entry.ResourceNotFound)
            return legacy ?? key;

        if (string.IsNullOrEmpty(argsJson))
            return entry.Value;

        try
        {
            var args = JsonSerializer.Deserialize<string[]>(argsJson) ?? [];
            // Use the parameterised indexer so {0}, {1} placeholders resolve.
            // Re-localize because the parameterised LocalizedString re-formats.
            return localizer[key, args.Cast<object>().ToArray()].Value;
        }
        catch (JsonException)
        {
            // Corrupt args — return the unformatted template rather than crash.
            return entry.Value;
        }
    }
}
