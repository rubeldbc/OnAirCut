using System.Text;
using System.Text.RegularExpressions;

namespace OnAirCut.Core.Utilities;

public static partial class TitleSanitizer
{
    private const int MaxFolderNameLength = 100;

    private static readonly char[] InvalidPathChars =
        ['\\', '/', ':', '*', '?', '"', '<', '>', '|'];

    /// <summary>
    /// Normalize raw OCR text for display purposes.
    /// Trims whitespace, removes control characters, normalizes Unicode, collapses spaces.
    /// </summary>
    public static string NormalizeTitle(string? rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return string.Empty;

        // Normalize Unicode to NFC
        var normalized = rawText.Normalize(NormalizationForm.FormC);

        // Remove control characters
        normalized = ControlCharsRegex().Replace(normalized, "");

        // Collapse multiple whitespace into single space
        normalized = MultiSpaceRegex().Replace(normalized, " ");

        return normalized.Trim();
    }

    /// <summary>
    /// Convert raw OCR text to a filesystem-safe folder/file name.
    /// </summary>
    public static string ToSafeFolderName(string? rawText, string fallbackJobId = "")
    {
        var normalized = NormalizeTitle(rawText);

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.IsNullOrWhiteSpace(fallbackJobId)
                ? $"Untitled_{DateTime.Now:yyyyMMdd_HHmmss}"
                : $"Untitled_{fallbackJobId}";
        }

        // Replace invalid path characters with underscore
        var safe = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (InvalidPathChars.Contains(c))
                safe.Append('_');
            else if (c == ' ')
                safe.Append('_');
            else
                safe.Append(c);
        }

        var result = safe.ToString();

        // Remove leading/trailing dots and spaces
        result = result.Trim('.', ' ', '_');

        // Collapse multiple underscores
        result = MultiUnderscoreRegex().Replace(result, "_");

        // Truncate to max length
        if (result.Length > MaxFolderNameLength)
            result = result[..MaxFolderNameLength].TrimEnd('_');

        // Final fallback if everything was stripped
        if (string.IsNullOrWhiteSpace(result))
        {
            return string.IsNullOrWhiteSpace(fallbackJobId)
                ? $"Untitled_{DateTime.Now:yyyyMMdd_HHmmss}"
                : $"Untitled_{fallbackJobId}";
        }

        return result;
    }

    /// <summary>
    /// Ensures uniqueness by appending _002, _003, etc. if the name already exists.
    /// </summary>
    public static string EnsureUniqueName(string baseName, Func<string, bool> nameExists)
    {
        if (!nameExists(baseName))
            return baseName;

        for (int i = 2; i < 1000; i++)
        {
            var candidate = $"{baseName}_{i:D3}";
            if (!nameExists(candidate))
                return candidate;
        }

        // Extreme fallback
        return $"{baseName}_{Guid.NewGuid():N}";
    }

    [GeneratedRegex(@"[\p{Cc}]")]
    private static partial Regex ControlCharsRegex();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex MultiSpaceRegex();

    [GeneratedRegex(@"_{2,}")]
    private static partial Regex MultiUnderscoreRegex();
}
