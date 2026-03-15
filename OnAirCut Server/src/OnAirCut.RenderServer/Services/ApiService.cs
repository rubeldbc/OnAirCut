using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using OnAirCut.Core.Enums;
using OnAirCut.Core.Models;
using OnAirCut.RenderServer.Models;
using Serilog;

namespace OnAirCut.RenderServer.Services;

public class ApiService : IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly SqliteRepository _repository;
    private readonly JobWatcherService _jobWatcher;
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public ApiService(ISettingsService settingsService, SqliteRepository repository, JobWatcherService jobWatcher)
    {
        _settingsService = settingsService;
        _repository = repository;
        _jobWatcher = jobWatcher;
    }

    public bool IsRunning { get; private set; }

    public void Start()
    {
        if (IsRunning) return;
        var settings = _settingsService.Settings;
        if (!settings.ApiEnabled) { Log.Information("API is disabled in settings"); return; }

        var port = settings.ApiPort;
        _cts = new CancellationTokenSource();

        try
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://+:{port}/");
            _listener.Start();
            IsRunning = true;
            _listenTask = Task.Run(() => ListenAsync(_cts.Token));
            Log.Information("API server started on port {Port}", port);
        }
        catch (HttpListenerException ex) when (ex.ErrorCode == 5)
        {
            // Access denied — try localhost only
            _listener?.Close();
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{port}/");
            _listener.Start();
            IsRunning = true;
            _listenTask = Task.Run(() => ListenAsync(_cts.Token));
            Log.Information("API server started on localhost:{Port}", port);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to start API server on port {Port}", port);
        }
    }

    public void Stop()
    {
        if (!IsRunning) return;
        _cts?.Cancel();
        _listener?.Stop();
        _listener?.Close();
        IsRunning = false;
        Log.Information("API server stopped");
    }

    private async Task ListenAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && _listener is { IsListening: true })
        {
            try
            {
                var ctx = await _listener.GetContextAsync().WaitAsync(ct);
                _ = Task.Run(() => HandleRequestAsync(ctx), ct);
            }
            catch (OperationCanceledException) { break; }
            catch (HttpListenerException) { break; }
            catch (Exception ex) { Log.Warning(ex, "API listener error"); }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext ctx)
    {
        var path = ctx.Request.Url?.AbsolutePath?.TrimEnd('/') ?? "";
        var method = ctx.Request.HttpMethod;

        try
        {
            var (status, body) = (path, method) switch
            {
                ("/api/jobs/submit", "POST") => await HandleSubmitJobAsync(ctx.Request),
                ("/api/jobs/status", "GET") => await HandleJobStatusAsync(ctx.Request),
                ("/api/stories/search", "GET") => await HandleSearchStoriesAsync(ctx.Request),
                ("/api/stories/recent", "GET") => await HandleRecentStoriesAsync(ctx.Request),
                ("/api/health", "GET") => (200, "{\"status\":\"ok\"}"),
                _ => (404, "{\"error\":\"Not found\"}")
            };

            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = "application/json; charset=utf-8";
            var buffer = Encoding.UTF8.GetBytes(body);
            ctx.Response.ContentLength64 = buffer.Length;
            await ctx.Response.OutputStream.WriteAsync(buffer);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "API request error: {Path}", path);
            try
            {
                ctx.Response.StatusCode = 500;
                var err = Encoding.UTF8.GetBytes($"{{\"error\":\"{ex.Message.Replace("\"", "''")}\"}}");
                await ctx.Response.OutputStream.WriteAsync(err);
            }
            catch { }
        }
        finally
        {
            ctx.Response.Close();
        }
    }

    // POST /api/jobs/submit — Recorder sends job JSON, server enqueues it
    private async Task<(int status, string body)> HandleSubmitJobAsync(HttpListenerRequest request)
    {
        using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
        var json = await reader.ReadToEndAsync();
        var job = JsonSerializer.Deserialize<JobFile>(json, JsonOptions);

        if (job is null || string.IsNullOrEmpty(job.JobId))
            return (400, "{\"error\":\"Invalid job\"}");

        // Insert into DB as Queued
        var story = new ProcessedStory
        {
            JobId = job.JobId,
            SourceType = job.SourceType,
            SourceName = job.SourceName,
            RawClipPath = job.RawClipPath,
            DurationSeconds = job.DurationSeconds,
            AdSetName = job.AdSetName,
            SubmittedBy = job.SubmittedBy,
            SubmittedAt = job.SubmittedAt,
            Status = JobStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        await _repository.InsertStoryAsync(story);

        // Enqueue into the job channel for the pipeline orchestrator
        await _jobWatcher.EnqueueJobAsync(job);
        Log.Information("Job submitted via API: {JobId}, clip: {Clip}", job.JobId, job.RawClipPath);

        return (200, $"{{\"jobId\":\"{job.JobId}\",\"status\":\"queued\"}}");
    }

    // GET /api/jobs/status?jobId=xxx
    private async Task<(int, string)> HandleJobStatusAsync(HttpListenerRequest request)
    {
        var jobId = request.QueryString["jobId"];
        if (string.IsNullOrEmpty(jobId)) return (400, "{\"error\":\"jobId required\"}");

        var story = await _repository.GetStoryByJobIdAsync(jobId);
        if (story is null) return (404, "{\"error\":\"Job not found\"}");

        return (200, JsonSerializer.Serialize(story, JsonOptions));
    }

    // GET /api/stories/search?q=text&from=date&to=date&status=x
    private async Task<(int, string)> HandleSearchStoriesAsync(HttpListenerRequest request)
    {
        var q = request.QueryString["q"];
        JobStatus? status = Enum.TryParse<JobStatus>(request.QueryString["status"], out var s) ? s : null;
        var stories = await _repository.SearchStoriesAsync(q, null, null, status);
        return (200, JsonSerializer.Serialize(stories, JsonOptions));
    }

    // GET /api/stories/recent?count=20
    private async Task<(int, string)> HandleRecentStoriesAsync(HttpListenerRequest request)
    {
        var count = int.TryParse(request.QueryString["count"], out var c) ? c : 20;
        var stories = await _repository.GetRecentStoriesAsync(count);
        return (200, JsonSerializer.Serialize(stories, JsonOptions));
    }

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
    }
}
