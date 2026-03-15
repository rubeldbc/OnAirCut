using System.IO;
using System.Text.Json;
using OnAirCut.Recorder.Models;
using Serilog;

namespace OnAirCut.Recorder.Services;

public class SettingsService : ISettingsService
{
    private static readonly string SettingsFilePath =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private RecorderSettings _settings = new();

    public RecorderSettings Settings => _settings;

    public event EventHandler? SettingsChanged;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                var json = await File.ReadAllTextAsync(SettingsFilePath, cancellationToken);
                var loaded = JsonSerializer.Deserialize<RecorderSettings>(json, JsonOptions);
                if (loaded is not null)
                {
                    _settings = loaded;
                }
            }
            else
            {
                _settings = new RecorderSettings();
                await SaveAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load settings, using defaults");
            _settings = new RecorderSettings();
        }
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var json = JsonSerializer.Serialize(_settings, JsonOptions);
            await File.WriteAllTextAsync(SettingsFilePath, json, cancellationToken);
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save settings");
        }
    }

    public void UpdateSettings(Action<RecorderSettings> updateAction)
    {
        updateAction(_settings);
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }
}
