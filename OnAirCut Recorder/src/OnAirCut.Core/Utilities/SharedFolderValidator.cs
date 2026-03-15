using OnAirCut.Core.Constants;

namespace OnAirCut.Core.Utilities;

public static class SharedFolderValidator
{
    /// <summary>
    /// Validate that the shared folder path exists and has the required subfolder structure.
    /// Returns a list of issues found (empty list = valid).
    /// </summary>
    public static List<string> Validate(string sharedFolderPath)
    {
        var issues = new List<string>();

        if (string.IsNullOrWhiteSpace(sharedFolderPath))
        {
            issues.Add("Shared folder path is not configured.");
            return issues;
        }

        if (!Directory.Exists(sharedFolderPath))
        {
            issues.Add($"Shared folder does not exist: {sharedFolderPath}");
            return issues;
        }

        // Check each required subfolder
        foreach (var subfolder in FolderNames.RequiredSubfolders)
        {
            var fullPath = Path.Combine(sharedFolderPath, subfolder);
            if (!Directory.Exists(fullPath))
            {
                issues.Add($"Missing subfolder: {subfolder}");
            }
        }

        // Check write access
        if (!IsWritable(sharedFolderPath))
        {
            issues.Add("Shared folder is not writable.");
        }

        return issues;
    }

    /// <summary>
    /// Quick check if the shared folder is accessible and writable.
    /// </summary>
    public static bool IsAccessibleAndWritable(string sharedFolderPath)
    {
        if (string.IsNullOrWhiteSpace(sharedFolderPath))
            return false;

        return Directory.Exists(sharedFolderPath) && IsWritable(sharedFolderPath);
    }

    /// <summary>
    /// Check available disk space in bytes.
    /// </summary>
    public static long GetAvailableSpaceBytes(string path)
    {
        try
        {
            var driveInfo = new DriveInfo(Path.GetPathRoot(path) ?? path);
            return driveInfo.AvailableFreeSpace;
        }
        catch
        {
            return -1;
        }
    }

    private static bool IsWritable(string path)
    {
        try
        {
            var testFile = Path.Combine(path, $".write_test_{Guid.NewGuid():N}");
            File.WriteAllText(testFile, "test");
            File.Delete(testFile);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
