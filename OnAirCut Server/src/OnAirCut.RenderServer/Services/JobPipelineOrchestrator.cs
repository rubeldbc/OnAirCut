using System.IO;
using OnAirCut.Core.Constants;
using OnAirCut.Core.Enums;
using OnAirCut.Core.Interfaces;
using OnAirCut.Core.Models;
using OnAirCut.Core.Utilities;
using OnAirCut.RenderServer.Models;
using Serilog;

namespace OnAirCut.RenderServer.Services;

public class JobPipelineOrchestrator : IDisposable
{
    private readonly ISharedFolderService _sharedFolderService;
    private readonly ISettingsService _settingsService;
    private readonly SqliteRepository _repository;
    private readonly FileReadyChecker _fileReadyChecker;
    private readonly FrameExtractionService _frameExtractionService;
    private readonly FfmpegCommandBuilder _commandBuilder;
    private readonly FfmpegRenderService _renderService;
    private readonly OcrProcessor _ocrProcessor;
    private readonly OutputOrganizer _outputOrganizer;
    private readonly IAdSetProvider _adSetProvider;
    private readonly IOcrProfileProvider _ocrProfileProvider;
    private readonly JobWatcherService _jobWatcher;
    private CancellationTokenSource? _cts;
    private Task? _processingTask;

    public event EventHandler<JobProgressEventArgs>? JobProgressChanged;
    public event EventHandler<JobCompletedEventArgs>? JobCompleted;

    public JobContext? CurrentJob { get; private set; }
    public bool IsRunning { get; private set; }

    public JobPipelineOrchestrator(
        ISharedFolderService sharedFolderService,
        ISettingsService settingsService,
        SqliteRepository repository,
        FileReadyChecker fileReadyChecker,
        FrameExtractionService frameExtractionService,
        FfmpegCommandBuilder commandBuilder,
        FfmpegRenderService renderService,
        OcrProcessor ocrProcessor,
        OutputOrganizer outputOrganizer,
        IAdSetProvider adSetProvider,
        IOcrProfileProvider ocrProfileProvider,
        JobWatcherService jobWatcher)
    {
        _sharedFolderService = sharedFolderService;
        _settingsService = settingsService;
        _repository = repository;
        _fileReadyChecker = fileReadyChecker;
        _frameExtractionService = frameExtractionService;
        _commandBuilder = commandBuilder;
        _renderService = renderService;
        _ocrProcessor = ocrProcessor;
        _outputOrganizer = outputOrganizer;
        _adSetProvider = adSetProvider;
        _ocrProfileProvider = ocrProfileProvider;
        _jobWatcher = jobWatcher;

        _renderService.ProgressChanged += OnRenderProgress;
    }

    public void Start()
    {
        if (IsRunning) return;

        _cts = new CancellationTokenSource();
        IsRunning = true;
        _processingTask = Task.Run(() => ProcessJobsAsync(_cts.Token), _cts.Token);
        Log.Information("Job pipeline orchestrator started");
    }

    public void Stop()
    {
        if (!IsRunning) return;

        _cts?.Cancel();
        IsRunning = false;
        Log.Information("Job pipeline orchestrator stopping");
    }

    private async Task ProcessJobsAsync(CancellationToken cancellationToken)
    {
        await foreach (var job in _jobWatcher.JobReader.ReadAllAsync(cancellationToken))
        {
            try
            {
                await ProcessSingleJobAsync(job, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unhandled error processing job {JobId}", job.JobId);
            }
        }
    }

    private async Task ProcessSingleJobAsync(JobFile job, CancellationToken cancellationToken)
    {
        var context = new JobContext
        {
            JobFile = job,
            CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
        };
        CurrentJob = context;

        var stepStart = DateTime.UtcNow;
        bool retried = false;

        retry:
        try
        {
            // Step 1: Validate
            await UpdateStepAsync(context, "Validating", JobStatus.Processing);
            stepStart = DateTime.UtcNow;

            if (string.IsNullOrEmpty(job.RawClipPath))
                throw new InvalidOperationException("RawClipPath is empty");

            await LogStepAsync(job.JobId, "Validate", "Completed", null, stepStart);

            // Step 2: Wait for file ready
            await UpdateStepAsync(context, "Waiting for file", JobStatus.Processing);
            stepStart = DateTime.UtcNow;

            var fileReady = await _fileReadyChecker.WaitForFileReadyAsync(job.RawClipPath, context.CancellationTokenSource.Token);
            if (!fileReady)
                throw new InvalidOperationException($"File not ready: {job.RawClipPath}");

            await LogStepAsync(job.JobId, "WaitFileReady", "Completed", null, stepStart);

            // Step 3: Insert into SQLite
            await UpdateStepAsync(context, "Recording in database", JobStatus.Processing);
            stepStart = DateTime.UtcNow;

            var story = new ProcessedStory
            {
                JobId = job.JobId,
                SourceType = job.SourceType,
                SourceName = job.SourceName,
                OnAirDateTime = job.ClipStartTime,
                ClipStartTime = job.ClipStartTime,
                ClipEndTime = job.ClipEndTime,
                DurationSeconds = job.DurationSeconds,
                AdSetName = job.AdSetName,
                OverlaySetName = job.OverlaySetName,
                RawClipPath = job.RawClipPath,
                SubmittedBy = job.SubmittedBy,
                SubmittedAt = job.SubmittedAt,
                ProcessingStartedAt = DateTime.UtcNow,
                Status = JobStatus.Processing,
                CreatedAt = DateTime.UtcNow
            };
            await _repository.InsertStoryAsync(story, cancellationToken);
            await LogStepAsync(job.JobId, "InsertDB", "Completed", null, stepStart);

            // Step 4: FFprobe
            await UpdateStepAsync(context, "Probing input", JobStatus.Processing);
            stepStart = DateTime.UtcNow;

            var (duration, width, height, fps, codec) =
                await _frameExtractionService.ProbeInputAsync(job.RawClipPath, context.CancellationTokenSource.Token);
            context.InputDuration = duration > 0 ? duration : job.DurationSeconds;
            context.InputWidth = width;
            context.InputHeight = height;
            context.InputFps = fps;

            await LogStepAsync(job.JobId, "FFprobe", "Completed",
                $"{width}x{height}, {duration:F1}s, {fps:F1}fps, {codec}", stepStart);

            // Step 5: Create working directory
            var workDir = string.IsNullOrEmpty(_settingsService.Settings.TempWorkingFolder)
                ? _sharedFolderService.GetFullPath(Path.Combine(FolderNames.Working, job.JobId))
                : Path.Combine(_settingsService.Settings.TempWorkingFolder, job.JobId);
            Directory.CreateDirectory(workDir);
            context.WorkingDirectory = workDir;

            // Step 6: Extract frames
            await UpdateStepAsync(context, "Extracting frames", JobStatus.ExtractingFrames);
            await _repository.UpdateStoryStatusAsync(job.JobId, JobStatus.ExtractingFrames, cancellationToken: cancellationToken);
            stepStart = DateTime.UtcNow;

            var framesDir = Path.Combine(workDir, FolderNames.Frames);
            var frameCount = _settingsService.Settings.FrameExtractionCount;
            context.FramePaths = await _frameExtractionService.ExtractFramesAsync(
                job.RawClipPath, framesDir, frameCount, context.InputDuration, context.CancellationTokenSource.Token);

            await LogStepAsync(job.JobId, "ExtractFrames", "Completed",
                $"{context.FramePaths.Count} frames extracted", stepStart);

            // Step 7: OCR
            await UpdateStepAsync(context, "Running OCR", JobStatus.RunningOcr);
            await _repository.UpdateStoryStatusAsync(job.JobId, JobStatus.RunningOcr, cancellationToken: cancellationToken);
            stepStart = DateTime.UtcNow;

            OcrProfile? ocrProfile = null;
            if (!string.IsNullOrEmpty(job.OcrProfileName))
                ocrProfile = await _ocrProfileProvider.GetProfileByNameAsync(job.OcrProfileName, cancellationToken);
            ocrProfile ??= await _ocrProfileProvider.GetDefaultProfileAsync(cancellationToken);

            var (ocrTitle, ocrConfidence) = await _ocrProcessor.ProcessMultiFrameAsync(
                context.FramePaths, ocrProfile, job.JobId, context.CancellationTokenSource.Token);

            context.OcrTitle = ocrTitle;
            context.OcrConfidence = ocrConfidence;

            var normalizedTitle = TitleSanitizer.NormalizeTitle(ocrTitle);
            var safeFolderName = TitleSanitizer.ToSafeFolderName(ocrTitle, job.JobId);

            await _repository.UpdateStoryOcrAsync(job.JobId, ocrTitle, normalizedTitle,
                safeFolderName, ocrConfidence, ocrProfile?.ProfileName, cancellationToken);

            await LogStepAsync(job.JobId, "OCR", "Completed",
                $"Title: '{normalizedTitle}', Confidence: {ocrConfidence:F1}%", stepStart);

            // Step 8: Render
            await UpdateStepAsync(context, "Rendering", JobStatus.Rendering);
            await _repository.UpdateStoryStatusAsync(job.JobId, JobStatus.Rendering, cancellationToken: cancellationToken);
            stepStart = DateTime.UtcNow;

            AdSetConfig? adSet = null;
            if (!string.IsNullOrEmpty(job.AdSetName))
                adSet = await _adSetProvider.GetAdSetByNameAsync(job.AdSetName, cancellationToken);

            var tempOutputPath = Path.Combine(workDir, $"{job.JobId}_output.mp4");
            var ffmpegArgs = _commandBuilder.BuildArguments(job.RawClipPath, adSet, tempOutputPath, context.InputDuration);

            var (renderSuccess, renderError) = await _renderService.RenderAsync(
                ffmpegArgs, context.InputDuration, context.CancellationTokenSource.Token);

            if (!renderSuccess)
                throw new InvalidOperationException($"Render failed: {renderError}");

            await LogStepAsync(job.JobId, "Render", "Completed", null, stepStart);

            // Step 9: Organize output
            await UpdateStepAsync(context, "Organizing output", JobStatus.Organizing);
            await _repository.UpdateStoryStatusAsync(job.JobId, JobStatus.Organizing, cancellationToken: cancellationToken);
            stepStart = DateTime.UtcNow;

            var (outputFolder, outputVideo, framesOutput) =
                await _outputOrganizer.OrganizeOutputAsync(context, tempOutputPath, safeFolderName, context.CancellationTokenSource.Token);

            context.OutputVideoPath = outputVideo;

            await _repository.UpdateStoryOutputAsync(job.JobId, outputFolder, outputVideo, framesOutput, cancellationToken);
            await LogStepAsync(job.JobId, "Organize", "Completed", outputFolder, stepStart);

            // Step 10: Mark completed
            await _repository.UpdateStoryStatusAsync(job.JobId, JobStatus.Completed, cancellationToken: cancellationToken);

            // Step 11: Move job to Done
            var processingJobPath = Path.Combine(
                _sharedFolderService.GetFullPath(FolderNames.JobsProcessing),
                $"{job.JobId}{FileExtensions.JobFile}");
            var doneFolder = _sharedFolderService.GetFullPath(FolderNames.JobsDone);
            JobFileHelper.TryMoveJob(processingJobPath, doneFolder);
            JobFileHelper.RemoveLockFile(_sharedFolderService.SharedFolderPath, job.JobId);

            // Step 12: Cleanup working directory
            try
            {
                if (Directory.Exists(workDir))
                    Directory.Delete(workDir, recursive: true);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to cleanup working directory: {Dir}", workDir);
            }

            await UpdateStepAsync(context, "Completed", JobStatus.Completed);
            JobCompleted?.Invoke(this, new JobCompletedEventArgs { JobId = job.JobId, Success = true });

            Log.Information("Job {JobId} completed successfully", job.JobId);
        }
        catch (Exception ex) when (!retried && ex is not OperationCanceledException)
        {
            Log.Warning(ex, "Job {JobId} failed, retrying once", job.JobId);
            retried = true;
            await LogStepAsync(job.JobId, context.CurrentStep, "Retrying", ex.Message, stepStart);
            goto retry;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log.Error(ex, "Job {JobId} failed permanently", job.JobId);
            await HandleJobFailureAsync(job, context, ex.Message, cancellationToken);
        }
        finally
        {
            CurrentJob = null;
        }
    }

    private async Task HandleJobFailureAsync(JobFile job, JobContext context, string error,
        CancellationToken cancellationToken)
    {
        try
        {
            await _repository.UpdateStoryStatusAsync(job.JobId, JobStatus.Failed, error, cancellationToken);
            await LogStepAsync(job.JobId, context.CurrentStep, "Failed", error, DateTime.UtcNow);

            // Move job to Failed
            var processingJobPath = Path.Combine(
                _sharedFolderService.GetFullPath(FolderNames.JobsProcessing),
                $"{job.JobId}{FileExtensions.JobFile}");
            var failedFolder = _sharedFolderService.GetFullPath(FolderNames.JobsFailed);
            JobFileHelper.TryMoveJob(processingJobPath, failedFolder);
            JobFileHelper.RemoveLockFile(_sharedFolderService.SharedFolderPath, job.JobId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling job failure for {JobId}", job.JobId);
        }

        await UpdateStepAsync(context, "Failed", JobStatus.Failed);
        JobCompleted?.Invoke(this, new JobCompletedEventArgs { JobId = job.JobId, Success = false, Error = error });
    }

    private Task UpdateStepAsync(JobContext context, string step, JobStatus status)
    {
        context.CurrentStep = step;
        JobProgressChanged?.Invoke(this, new JobProgressEventArgs
        {
            JobId = context.JobFile.JobId,
            Step = step,
            Progress = context.Progress,
            Status = status
        });
        return Task.CompletedTask;
    }

    private async Task LogStepAsync(string jobId, string step, string status, string? message, DateTime startedAt)
    {
        var durationMs = (long)(DateTime.UtcNow - startedAt).TotalMilliseconds;
        await _repository.InsertJobLogAsync(jobId, step, status, message, startedAt, DateTime.UtcNow, durationMs);
    }

    private void OnRenderProgress(object? sender, RenderProgressEventArgs e)
    {
        if (CurrentJob != null)
        {
            CurrentJob.Progress = e.Progress;
            JobProgressChanged?.Invoke(this, new JobProgressEventArgs
            {
                JobId = CurrentJob.JobFile.JobId,
                Step = "Rendering",
                Progress = e.Progress,
                Speed = e.Speed,
                Status = JobStatus.Rendering
            });
        }
    }

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
        _renderService.ProgressChanged -= OnRenderProgress;
    }
}

public class JobProgressEventArgs : EventArgs
{
    public string JobId { get; init; } = string.Empty;
    public string Step { get; init; } = string.Empty;
    public double Progress { get; init; }
    public string? Speed { get; init; }
    public JobStatus Status { get; init; }
}

public class JobCompletedEventArgs : EventArgs
{
    public string JobId { get; init; } = string.Empty;
    public bool Success { get; init; }
    public string? Error { get; init; }
}
