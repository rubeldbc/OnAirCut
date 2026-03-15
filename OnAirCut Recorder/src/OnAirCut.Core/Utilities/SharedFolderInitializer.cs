using OnAirCut.Core.Constants;

namespace OnAirCut.Core.Utilities;

public static class SharedFolderInitializer
{
    /// <summary>
    /// Create the full shared folder directory tree. Idempotent — safe to call multiple times.
    /// </summary>
    public static void Initialize(string sharedFolderPath)
    {
        if (string.IsNullOrWhiteSpace(sharedFolderPath))
            throw new ArgumentException("Shared folder path cannot be empty.", nameof(sharedFolderPath));

        // Create root if it doesn't exist
        Directory.CreateDirectory(sharedFolderPath);

        // Create all required subfolders
        foreach (var subfolder in FolderNames.RequiredSubfolders)
        {
            var fullPath = Path.Combine(sharedFolderPath, subfolder);
            Directory.CreateDirectory(fullPath);
        }
    }

    /// <summary>
    /// Ensure a date-based subfolder exists under the given parent.
    /// Example: creates {parent}\2026-03-15\ and returns the full path.
    /// </summary>
    public static string EnsureDateSubfolder(string sharedFolderPath, string parentRelativePath, DateTime date)
    {
        var dateFolderName = date.ToString("yyyy-MM-dd");
        var fullPath = Path.Combine(sharedFolderPath, parentRelativePath, dateFolderName);
        Directory.CreateDirectory(fullPath);
        return fullPath;
    }
}
