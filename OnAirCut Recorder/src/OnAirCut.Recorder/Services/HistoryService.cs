using System.Net.Http;
using System.Net.Http.Json;
using OnAirCut.Core.Enums;
using OnAirCut.Core.Models;
using Serilog;

namespace OnAirCut.Recorder.Services;

public class HistoryService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ISettingsService _settingsService;
    private bool _disposed;

    public HistoryService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    private string BaseUrl => _settingsService.Settings.RenderServerApiUrl.TrimEnd('/');

    public async Task<List<ProcessedStory>> SearchAsync(string? searchText = null,
        DateTime? dateFrom = null, DateTime? dateTo = null, JobStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var queryParams = new List<string>();
            if (!string.IsNullOrWhiteSpace(searchText))
                queryParams.Add($"q={Uri.EscapeDataString(searchText)}");
            if (dateFrom.HasValue)
                queryParams.Add($"from={dateFrom.Value:yyyy-MM-dd}");
            if (dateTo.HasValue)
                queryParams.Add($"to={dateTo.Value:yyyy-MM-dd}");
            if (status.HasValue)
                queryParams.Add($"status={status.Value}");

            var query = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";
            var url = $"{BaseUrl}/api/stories/search{query}";

            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<ProcessedStory>>(cancellationToken: cancellationToken) ?? [];
            }

            Log.Warning("Search API returned {StatusCode}", response.StatusCode);
            return [];
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to search stories from render server");
            return [];
        }
    }

    public async Task<List<ProcessedStory>> GetRecentAsync(int count = 50, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{BaseUrl}/api/stories/recent?count={count}";
            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<ProcessedStory>>(cancellationToken: cancellationToken) ?? [];
            }
            return [];
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to get recent stories from render server");
            return [];
        }
    }

    public async Task<Dictionary<string, object>> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{BaseUrl}/api/jobs/stats";
            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<Dictionary<string, object>>(cancellationToken: cancellationToken) ?? [];
            }
            return [];
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to get job stats from render server");
            return [];
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _httpClient.Dispose();
    }
}
