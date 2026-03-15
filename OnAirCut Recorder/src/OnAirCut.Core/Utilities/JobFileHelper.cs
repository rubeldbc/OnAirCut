using System.Text.Json;
using OnAirCut.Core.Constants;
using OnAirCut.Core.Models;

namespace OnAirCut.Core.Utilities;

public static class JobFileHelper
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Generate a unique job ID in the format JOB-yyyyMMdd-HHmmss-NNN.
    /// </summary>
    public static string GenerateJobId(int sequence = 1)
    {
        return $"JOB-{DateTime.Now:yyyyMMdd}-{DateTime.Now:HHmmss}-{sequence:D3}";
    }

    /// <summary>
    /// Write a job file to the Pending folder using atomic write (temp file + rename).
    /// </summary>
    public static async Task WriteJobAsync(string sharedFolderPath, JobFile job, CancellationToken cancellationToken = default)
    {
        var pendingDir = Path.Combine(sharedFolderPath, FolderNames.JobsPending);
        Directory.CreateDirectory(pendingDir);

        var targetPath = Path.Combine(pendingDir, $"{job.JobId}{FileExtensions.JobFile}");
        var tempPath = targetPath + FileExtensions.TempSuffix;

        var json = JsonSerializer.Serialize(job, SerializerOptions);
        await File.WriteAllTextAsync(tempPath, json, cancellationToken);

        // Atomic rename
        File.Move(tempPath, targetPath, overwrite: false);
    }

    /// <summary>
    /// Read and deserialize a job file.
    /// </summary>
    public static async Task<JobFile?> ReadJobAsync(string jobFilePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(jobFilePath))
            return null;

        var json = await File.ReadAllTextAsync(jobFilePath, cancellationToken);
        return JsonSerializer.Deserialize<JobFile>(json, SerializerOptions);
    }

    /// <summary>
    /// Atomically move a job file from one folder to another (e.g., Pending → Processing).
    /// Returns true if the move succeeded (this process claimed the job).
    /// </summary>
    public static bool TryMoveJob(string sourceFilePath, string destinationFolder)
    {
        try
        {
            Directory.CreateDirectory(destinationFolder);
            var fileName = Path.GetFileName(sourceFilePath);
            var destPath = Path.Combine(destinationFolder, fileName);

            File.Move(sourceFilePath, destPath, overwrite: false);
            return true;
        }
        catch (IOException)
        {
            // File was already moved by another process, or destination exists
            return false;
        }
    }

    /// <summary>
    /// Create a lock file for a job being processed.
    /// </summary>
    public static async Task CreateLockFileAsync(string sharedFolderPath, string jobId, CancellationToken cancellationToken = default)
    {
        var lockDir = Path.Combine(sharedFolderPath, FolderNames.JobsProcessing);
        Directory.CreateDirectory(lockDir);

        var lockPath = Path.Combine(lockDir, $"{jobId}{FileExtensions.LockFile}");
        var lockContent = $"{Environment.MachineName}|{Environment.ProcessId}|{DateTime.UtcNow:O}";
        await File.WriteAllTextAsync(lockPath, lockContent, cancellationToken);
    }

    /// <summary>
    /// Remove the lock file for a completed/failed job.
    /// </summary>
    public static void RemoveLockFile(string sharedFolderPath, string jobId)
    {
        var lockPath = Path.Combine(sharedFolderPath, FolderNames.JobsProcessing, $"{jobId}{FileExtensions.LockFile}");
        if (File.Exists(lockPath))
            File.Delete(lockPath);
    }

    /// <summary>
    /// List all pending job files.
    /// </summary>
    public static IEnumerable<string> GetPendingJobFiles(string sharedFolderPath)
    {
        var pendingDir = Path.Combine(sharedFolderPath, FolderNames.JobsPending);
        if (!Directory.Exists(pendingDir))
            return [];

        return Directory.EnumerateFiles(pendingDir, $"*{FileExtensions.JobFile}")
            .OrderBy(f => File.GetCreationTimeUtc(f));
    }
}
