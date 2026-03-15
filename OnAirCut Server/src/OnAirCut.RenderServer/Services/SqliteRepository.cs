using System.IO;
using Microsoft.Data.Sqlite;
using OnAirCut.Core.Enums;
using OnAirCut.Core.Models;
using OnAirCut.RenderServer.Models;
using Serilog;

namespace OnAirCut.RenderServer.Services;

public class SqliteRepository : IDisposable
{
    private readonly ISettingsService _settingsService;
    private SqliteConnection? _connection;

    public SqliteRepository(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var dbPath = _settingsService.Settings.LocalDatabasePath;
        var dbDir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dbDir))
            Directory.CreateDirectory(dbDir);

        _connection = new SqliteConnection($"Data Source={dbPath}");
        await _connection.OpenAsync(cancellationToken);

        // Enable WAL mode
        using var walCmd = _connection.CreateCommand();
        walCmd.CommandText = "PRAGMA journal_mode=WAL;";
        await walCmd.ExecuteNonQueryAsync(cancellationToken);

        await CreateSchemaAsync(cancellationToken);
    }

    private async Task CreateSchemaAsync(CancellationToken cancellationToken)
    {
        if (_connection == null) return;

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS ProcessedStories (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                JobId TEXT UNIQUE NOT NULL,
                TitleRaw TEXT,
                TitleNormalized TEXT,
                SafeFolderName TEXT,
                SourceType TEXT,
                SourceName TEXT,
                OnAirDateTime TEXT,
                ClipStartTime TEXT,
                ClipEndTime TEXT,
                DurationSeconds REAL,
                AdSetName TEXT,
                OverlaySetName TEXT,
                RawClipPath TEXT,
                OutputFolderPath TEXT,
                OutputVideoPath TEXT,
                FramesPath TEXT,
                OcrConfidence REAL,
                OcrProfileUsed TEXT,
                SubmittedBy TEXT,
                SubmittedAt TEXT,
                ProcessingStartedAt TEXT,
                ProcessedAt TEXT,
                Status TEXT NOT NULL DEFAULT 'Pending',
                ErrorMessage TEXT,
                CreatedAt TEXT NOT NULL DEFAULT (datetime('now'))
            );

            CREATE TABLE IF NOT EXISTS JobLog (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                JobId TEXT NOT NULL,
                Step TEXT NOT NULL,
                Status TEXT NOT NULL,
                Message TEXT,
                StartedAt TEXT,
                CompletedAt TEXT,
                DurationMs INTEGER
            );

            CREATE TABLE IF NOT EXISTS OcrResults (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                JobId TEXT NOT NULL,
                FrameIndex INTEGER,
                FramePath TEXT,
                RawText TEXT,
                Confidence REAL,
                ProfileUsed TEXT,
                CreatedAt TEXT NOT NULL DEFAULT (datetime('now'))
            );

            CREATE INDEX IF NOT EXISTS IX_ProcessedStories_JobId ON ProcessedStories(JobId);
            CREATE INDEX IF NOT EXISTS IX_ProcessedStories_Status ON ProcessedStories(Status);
            CREATE INDEX IF NOT EXISTS IX_ProcessedStories_CreatedAt ON ProcessedStories(CreatedAt);
            CREATE INDEX IF NOT EXISTS IX_JobLog_JobId ON JobLog(JobId);
            CREATE INDEX IF NOT EXISTS IX_OcrResults_JobId ON OcrResults(JobId);
            """;
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task InsertStoryAsync(ProcessedStory story, CancellationToken cancellationToken = default)
    {
        if (_connection == null) return;

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO ProcessedStories (JobId, TitleRaw, TitleNormalized, SafeFolderName, SourceType,
                SourceName, OnAirDateTime, ClipStartTime, ClipEndTime, DurationSeconds, AdSetName, OverlaySetName,
                RawClipPath, OutputFolderPath, OutputVideoPath, FramesPath, OcrConfidence, OcrProfileUsed,
                SubmittedBy, SubmittedAt, ProcessingStartedAt, ProcessedAt, Status, ErrorMessage, CreatedAt)
            VALUES ($jobId, $titleRaw, $titleNorm, $safeName, $srcType, $srcName, $onAir, $clipStart, $clipEnd,
                $duration, $adSet, $overlaySet, $rawClip, $outFolder, $outVideo, $frames, $ocrConf, $ocrProfile,
                $submittedBy, $submittedAt, $procStart, $processedAt, $status, $error, $createdAt)
            """;
        cmd.Parameters.AddWithValue("$jobId", story.JobId);
        cmd.Parameters.AddWithValue("$titleRaw", (object?)story.TitleRaw ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$titleNorm", (object?)story.TitleNormalized ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$safeName", (object?)story.SafeFolderName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$srcType", story.SourceType.ToString());
        cmd.Parameters.AddWithValue("$srcName", (object?)story.SourceName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$onAir", story.OnAirDateTime.ToString("O"));
        cmd.Parameters.AddWithValue("$clipStart", story.ClipStartTime?.ToString("O") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$clipEnd", story.ClipEndTime?.ToString("O") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$duration", story.DurationSeconds);
        cmd.Parameters.AddWithValue("$adSet", (object?)story.AdSetName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$overlaySet", (object?)story.OverlaySetName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$rawClip", story.RawClipPath);
        cmd.Parameters.AddWithValue("$outFolder", (object?)story.OutputFolderPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$outVideo", (object?)story.OutputVideoPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$frames", (object?)story.FramesPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$ocrConf", story.OcrConfidence ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$ocrProfile", (object?)story.OcrProfileUsed ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$submittedBy", (object?)story.SubmittedBy ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$submittedAt", story.SubmittedAt.ToString("O"));
        cmd.Parameters.AddWithValue("$procStart", story.ProcessingStartedAt?.ToString("O") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$processedAt", story.ProcessedAt?.ToString("O") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$status", story.Status.ToString());
        cmd.Parameters.AddWithValue("$error", (object?)story.ErrorMessage ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$createdAt", story.CreatedAt.ToString("O"));
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateStoryStatusAsync(string jobId, JobStatus status, string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        if (_connection == null) return;

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            UPDATE ProcessedStories SET Status = $status, ErrorMessage = $error,
                ProcessingStartedAt = CASE WHEN $status = 'Processing' THEN datetime('now') ELSE ProcessingStartedAt END,
                ProcessedAt = CASE WHEN $status IN ('Completed','Failed') THEN datetime('now') ELSE ProcessedAt END
            WHERE JobId = $jobId
            """;
        cmd.Parameters.AddWithValue("$jobId", jobId);
        cmd.Parameters.AddWithValue("$status", status.ToString());
        cmd.Parameters.AddWithValue("$error", (object?)errorMessage ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateStoryOcrAsync(string jobId, string? titleRaw, string? titleNormalized,
        string? safeFolderName, double? confidence, string? ocrProfile, CancellationToken cancellationToken = default)
    {
        if (_connection == null) return;

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            UPDATE ProcessedStories SET TitleRaw = $titleRaw, TitleNormalized = $titleNorm,
                SafeFolderName = $safeName, OcrConfidence = $ocrConf, OcrProfileUsed = $ocrProfile
            WHERE JobId = $jobId
            """;
        cmd.Parameters.AddWithValue("$jobId", jobId);
        cmd.Parameters.AddWithValue("$titleRaw", (object?)titleRaw ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$titleNorm", (object?)titleNormalized ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$safeName", (object?)safeFolderName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$ocrConf", confidence ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$ocrProfile", (object?)ocrProfile ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateStoryOutputAsync(string jobId, string? outputFolderPath, string? outputVideoPath,
        string? framesPath, CancellationToken cancellationToken = default)
    {
        if (_connection == null) return;

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            UPDATE ProcessedStories SET OutputFolderPath = $outFolder, OutputVideoPath = $outVideo,
                FramesPath = $frames WHERE JobId = $jobId
            """;
        cmd.Parameters.AddWithValue("$jobId", jobId);
        cmd.Parameters.AddWithValue("$outFolder", (object?)outputFolderPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$outVideo", (object?)outputVideoPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$frames", (object?)framesPath ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<List<ProcessedStory>> SearchStoriesAsync(string? searchText = null, DateTime? dateFrom = null,
        DateTime? dateTo = null, JobStatus? status = null, int limit = 200, CancellationToken cancellationToken = default)
    {
        if (_connection == null) return [];

        var conditions = new List<string>();
        using var cmd = _connection.CreateCommand();

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            conditions.Add("(TitleRaw LIKE $search OR TitleNormalized LIKE $search OR JobId LIKE $search OR SourceName LIKE $search)");
            cmd.Parameters.AddWithValue("$search", $"%{searchText}%");
        }
        if (dateFrom.HasValue)
        {
            conditions.Add("CreatedAt >= $dateFrom");
            cmd.Parameters.AddWithValue("$dateFrom", dateFrom.Value.ToString("O"));
        }
        if (dateTo.HasValue)
        {
            conditions.Add("CreatedAt <= $dateTo");
            cmd.Parameters.AddWithValue("$dateTo", dateTo.Value.Date.AddDays(1).ToString("O"));
        }
        if (status.HasValue)
        {
            conditions.Add("Status = $status");
            cmd.Parameters.AddWithValue("$status", status.Value.ToString());
        }

        var where = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";
        cmd.CommandText = $"SELECT * FROM ProcessedStories {where} ORDER BY CreatedAt DESC LIMIT $limit";
        cmd.Parameters.AddWithValue("$limit", limit);

        return await ReadStoriesAsync(cmd, cancellationToken);
    }

    public async Task<List<ProcessedStory>> GetRecentStoriesAsync(int count = 50,
        CancellationToken cancellationToken = default)
    {
        if (_connection == null) return [];

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM ProcessedStories ORDER BY CreatedAt DESC LIMIT $count";
        cmd.Parameters.AddWithValue("$count", count);
        return await ReadStoriesAsync(cmd, cancellationToken);
    }

    public async Task<ProcessedStory?> GetStoryByJobIdAsync(string jobId,
        CancellationToken cancellationToken = default)
    {
        if (_connection == null) return null;

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM ProcessedStories WHERE JobId = $jobId";
        cmd.Parameters.AddWithValue("$jobId", jobId);

        var results = await ReadStoriesAsync(cmd, cancellationToken);
        return results.FirstOrDefault();
    }

    public async Task<DailyStats> GetTodayStatsAsync(CancellationToken cancellationToken = default)
    {
        var stats = new DailyStats();
        if (_connection == null) return stats;

        var today = DateTime.Today.ToString("O");

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT Status, COUNT(*) as Cnt FROM ProcessedStories
            WHERE CreatedAt >= $today GROUP BY Status
            """;
        cmd.Parameters.AddWithValue("$today", today);

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var statusStr = reader.GetString(0);
            var count = reader.GetInt32(1);
            if (Enum.TryParse<JobStatus>(statusStr, out var s))
            {
                switch (s)
                {
                    case JobStatus.Pending: stats.PendingCount = count; break;
                    case JobStatus.Processing:
                    case JobStatus.ExtractingFrames:
                    case JobStatus.RunningOcr:
                    case JobStatus.Rendering:
                    case JobStatus.Organizing:
                        stats.ProcessingCount += count; break;
                    case JobStatus.Completed: stats.CompletedCount = count; break;
                    case JobStatus.Failed: stats.FailedCount = count; break;
                }
            }
        }

        // Average render time
        using var avgCmd = _connection.CreateCommand();
        avgCmd.CommandText = """
            SELECT AVG(CAST((julianday(ProcessedAt) - julianday(ProcessingStartedAt)) * 86400 AS REAL)),
                   SUM(DurationSeconds),
                   CAST(SUM(CASE WHEN OcrConfidence > 0 THEN 1 ELSE 0 END) AS REAL) / MAX(COUNT(*), 1)
            FROM ProcessedStories WHERE CreatedAt >= $today AND Status = 'Completed'
            """;
        avgCmd.Parameters.AddWithValue("$today", today);

        using var avgReader = await avgCmd.ExecuteReaderAsync(cancellationToken);
        if (await avgReader.ReadAsync(cancellationToken))
        {
            if (!avgReader.IsDBNull(0)) stats.AverageRenderTimeSeconds = avgReader.GetDouble(0);
            if (!avgReader.IsDBNull(1)) stats.TotalOutputDurationSeconds = avgReader.GetDouble(1);
            if (!avgReader.IsDBNull(2)) stats.OcrSuccessRate = avgReader.GetDouble(2);
        }

        return stats;
    }

    public async Task InsertJobLogAsync(string jobId, string step, string status, string? message,
        DateTime? startedAt, DateTime? completedAt, long? durationMs,
        CancellationToken cancellationToken = default)
    {
        if (_connection == null) return;

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO JobLog (JobId, Step, Status, Message, StartedAt, CompletedAt, DurationMs)
            VALUES ($jobId, $step, $status, $message, $startedAt, $completedAt, $durationMs)
            """;
        cmd.Parameters.AddWithValue("$jobId", jobId);
        cmd.Parameters.AddWithValue("$step", step);
        cmd.Parameters.AddWithValue("$status", status);
        cmd.Parameters.AddWithValue("$message", (object?)message ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$startedAt", startedAt?.ToString("O") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$completedAt", completedAt?.ToString("O") ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("$durationMs", durationMs ?? (object)DBNull.Value);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task InsertOcrResultAsync(string jobId, int frameIndex, string? framePath, string? rawText,
        double confidence, string? profileUsed, CancellationToken cancellationToken = default)
    {
        if (_connection == null) return;

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO OcrResults (JobId, FrameIndex, FramePath, RawText, Confidence, ProfileUsed)
            VALUES ($jobId, $frameIndex, $framePath, $rawText, $confidence, $profileUsed)
            """;
        cmd.Parameters.AddWithValue("$jobId", jobId);
        cmd.Parameters.AddWithValue("$frameIndex", frameIndex);
        cmd.Parameters.AddWithValue("$framePath", (object?)framePath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$rawText", (object?)rawText ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$confidence", confidence);
        cmd.Parameters.AddWithValue("$profileUsed", (object?)profileUsed ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<List<(string Step, string Status, string? Message, DateTime? StartedAt, DateTime? CompletedAt, long? DurationMs)>>
        GetJobLogsAsync(string jobId, CancellationToken cancellationToken = default)
    {
        var results = new List<(string, string, string?, DateTime?, DateTime?, long?)>();
        if (_connection == null) return results;

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT Step, Status, Message, StartedAt, CompletedAt, DurationMs FROM JobLog WHERE JobId = $jobId ORDER BY Id";
        cmd.Parameters.AddWithValue("$jobId", jobId);

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add((
                reader.GetString(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : DateTime.Parse(reader.GetString(3)),
                reader.IsDBNull(4) ? null : DateTime.Parse(reader.GetString(4)),
                reader.IsDBNull(5) ? null : reader.GetInt64(5)
            ));
        }

        return results;
    }

    public async Task<List<(int FrameIndex, string? FramePath, string? RawText, double Confidence, string? ProfileUsed)>>
        GetOcrResultsAsync(string jobId, CancellationToken cancellationToken = default)
    {
        var results = new List<(int, string?, string?, double, string?)>();
        if (_connection == null) return results;

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT FrameIndex, FramePath, RawText, Confidence, ProfileUsed FROM OcrResults WHERE JobId = $jobId ORDER BY FrameIndex";
        cmd.Parameters.AddWithValue("$jobId", jobId);

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add((
                reader.GetInt32(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? 0.0 : reader.GetDouble(3),
                reader.IsDBNull(4) ? null : reader.GetString(4)
            ));
        }

        return results;
    }

    private static async Task<List<ProcessedStory>> ReadStoriesAsync(SqliteCommand cmd,
        CancellationToken cancellationToken)
    {
        var stories = new List<ProcessedStory>();

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var story = new ProcessedStory
            {
                Id = reader.GetInt32(reader.GetOrdinal("Id")),
                JobId = reader.GetString(reader.GetOrdinal("JobId")),
                TitleRaw = GetNullableString(reader, "TitleRaw"),
                TitleNormalized = GetNullableString(reader, "TitleNormalized"),
                SafeFolderName = GetNullableString(reader, "SafeFolderName"),
                SourceName = GetNullableString(reader, "SourceName"),
                DurationSeconds = GetNullableDouble(reader, "DurationSeconds"),
                AdSetName = GetNullableString(reader, "AdSetName"),
                OverlaySetName = GetNullableString(reader, "OverlaySetName"),
                RawClipPath = reader.GetString(reader.GetOrdinal("RawClipPath")),
                OutputFolderPath = GetNullableString(reader, "OutputFolderPath"),
                OutputVideoPath = GetNullableString(reader, "OutputVideoPath"),
                FramesPath = GetNullableString(reader, "FramesPath"),
                OcrProfileUsed = GetNullableString(reader, "OcrProfileUsed"),
                SubmittedBy = GetNullableString(reader, "SubmittedBy"),
                ErrorMessage = GetNullableString(reader, "ErrorMessage"),
            };

            var srcTypeStr = GetNullableString(reader, "SourceType");
            if (srcTypeStr != null && Enum.TryParse<SourceType>(srcTypeStr, out var st))
                story.SourceType = st;

            var statusStr = reader.GetString(reader.GetOrdinal("Status"));
            if (Enum.TryParse<JobStatus>(statusStr, out var js))
                story.Status = js;

            var onAir = GetNullableString(reader, "OnAirDateTime");
            if (onAir != null && DateTime.TryParse(onAir, out var onAirDt))
                story.OnAirDateTime = onAirDt;

            var clipStart = GetNullableString(reader, "ClipStartTime");
            if (clipStart != null && DateTime.TryParse(clipStart, out var clipStartDt))
                story.ClipStartTime = clipStartDt;

            var clipEnd = GetNullableString(reader, "ClipEndTime");
            if (clipEnd != null && DateTime.TryParse(clipEnd, out var clipEndDt))
                story.ClipEndTime = clipEndDt;

            var ocrConf = GetNullableDouble(reader, "OcrConfidence");
            story.OcrConfidence = ocrConf > 0 ? ocrConf : null;

            var submittedAt = GetNullableString(reader, "SubmittedAt");
            if (submittedAt != null && DateTime.TryParse(submittedAt, out var subDt))
                story.SubmittedAt = subDt;

            var procStart = GetNullableString(reader, "ProcessingStartedAt");
            if (procStart != null && DateTime.TryParse(procStart, out var procStartDt))
                story.ProcessingStartedAt = procStartDt;

            var processedAt = GetNullableString(reader, "ProcessedAt");
            if (processedAt != null && DateTime.TryParse(processedAt, out var procDt))
                story.ProcessedAt = procDt;

            var createdAt = GetNullableString(reader, "CreatedAt");
            if (createdAt != null && DateTime.TryParse(createdAt, out var crDt))
                story.CreatedAt = crDt;

            stories.Add(story);
        }

        return stories;
    }

    private static string? GetNullableString(SqliteDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static double GetNullableDouble(SqliteDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? 0.0 : reader.GetDouble(ordinal);
    }

    public void Dispose()
    {
        _connection?.Dispose();
    }
}
