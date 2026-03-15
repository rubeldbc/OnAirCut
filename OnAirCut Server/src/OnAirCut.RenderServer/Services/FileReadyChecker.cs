using System.IO;
using Serilog;

namespace OnAirCut.RenderServer.Services;

public class FileReadyChecker
{
    private readonly ISettingsService _settingsService;

    public FileReadyChecker(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public async Task<bool> WaitForFileReadyAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var timeout = TimeSpan.FromSeconds(60);
        var checkInterval = TimeSpan.FromMilliseconds(_settingsService.Settings.FileReadyCheckIntervalMs);
        var stableSeconds = _settingsService.Settings.FileReadyStableSeconds;

        var startTime = DateTime.UtcNow;
        long lastSize = -1;
        int stableChecks = 0;
        int requiredStableChecks = (int)(stableSeconds * 1000.0 / _settingsService.Settings.FileReadyCheckIntervalMs);
        if (requiredStableChecks < 1) requiredStableChecks = 1;

        while (DateTime.UtcNow - startTime < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!File.Exists(filePath))
            {
                Log.Debug("File not found yet: {Path}", filePath);
                await Task.Delay(checkInterval, cancellationToken);
                continue;
            }

            try
            {
                var info = new FileInfo(filePath);
                if (info.Length == 0)
                {
                    stableChecks = 0;
                    lastSize = 0;
                    await Task.Delay(checkInterval, cancellationToken);
                    continue;
                }

                if (info.Length == lastSize)
                {
                    stableChecks++;
                    if (stableChecks >= requiredStableChecks)
                    {
                        // Verify the file is not locked
                        if (!IsFileLocked(filePath))
                        {
                            Log.Debug("File ready: {Path} ({Size} bytes)", filePath, info.Length);
                            return true;
                        }
                    }
                }
                else
                {
                    lastSize = info.Length;
                    stableChecks = 0;
                }
            }
            catch (IOException)
            {
                stableChecks = 0;
            }

            await Task.Delay(checkInterval, cancellationToken);
        }

        Log.Warning("File ready check timed out: {Path}", filePath);
        return false;
    }

    private static bool IsFileLocked(string filePath)
    {
        try
        {
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
    }
}
