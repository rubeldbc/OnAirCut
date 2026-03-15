using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using OnAirCut.Core.Constants;
using OnAirCut.Core.Interfaces;
using OnAirCut.Core.Models;
using OnAirCut.Core.Utilities;
using Serilog;

namespace OnAirCut.Recorder.Services;

public class JobSubmissionService
{
    private readonly ISharedFolderService _sharedFolderService;
    private readonly ISettingsService _settingsService;
    private int _dailySequence;
    private DateTime _lastSequenceDate = DateTime.MinValue;
    private readonly object _sequenceLock = new();
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(10) };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public JobSubmissionService(ISharedFolderService sharedFolderService, ISettingsService settingsService)
    {
        _sharedFolderService = sharedFolderService;
        _settingsService = settingsService;
    }

    public int TodaySubmissionCount => _dailySequence;

    public async Task<string> SubmitJobAsync(RecordingResult recordingResult, string? adSetName,
        string? ocrProfileName, CancellationToken cancellationToken = default)
    {
        var jobId = GenerateNextJobId();

        var jobFile = new JobFile
        {
            JobId = jobId,
            RawClipPath = recordingResult.FilePath,
            SourceType = Core.Enums.SourceType.LocalFile,
            SourceName = Path.GetFileName(recordingResult.FilePath),
            ClipStartTime = recordingResult.StartTime,
            ClipEndTime = recordingResult.EndTime,
            DurationSeconds = recordingResult.DurationSeconds,
            AdSetName = adSetName,
            OcrProfileName = ocrProfileName,
            Priority = 0,
            SubmittedBy = _settingsService.Settings.OperatorName,
            SubmittedAt = DateTime.Now
        };

        // Try API submission first (reliable, no FileSystemWatcher dependency)
        var apiUrl = _settingsService.Settings.RenderServerApiUrl?.TrimEnd('/');
        if (!string.IsNullOrEmpty(apiUrl))
        {
            try
            {
                var json = JsonSerializer.Serialize(jobFile, JsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{apiUrl}/api/jobs/submit", content, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    Log.Information("Job submitted via API: {JobId}", jobId);
                    return jobId;
                }

                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                Log.Warning("API submission returned {StatusCode}: {Body}, falling back to file", response.StatusCode, errorBody);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "API submission failed, falling back to file-based submission");
            }
        }

        // Fallback: write job file to shared folder (original method)
        await JobFileHelper.WriteJobAsync(_sharedFolderService.SharedFolderPath, jobFile, cancellationToken);
        Log.Information("Job submitted via file: {JobId}", jobId);

        return jobId;
    }

    private string GenerateNextJobId()
    {
        lock (_sequenceLock)
        {
            var today = DateTime.Today;
            if (_lastSequenceDate != today)
            {
                _dailySequence = 0;
                _lastSequenceDate = today;

                try
                {
                    var pendingDir = _sharedFolderService.GetFullPath(FolderNames.JobsPending);
                    if (Directory.Exists(pendingDir))
                    {
                        var todayPrefix = $"JOB-{today:yyyyMMdd}";
                        _dailySequence = Directory.EnumerateFiles(pendingDir, $"{todayPrefix}*").Count();
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to count existing jobs for sequence");
                }
            }

            _dailySequence++;
            return JobFileHelper.GenerateJobId(_dailySequence);
        }
    }
}
