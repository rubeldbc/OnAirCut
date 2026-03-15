using System.IO;
using Serilog;
using Serilog.Events;

namespace OnAirCut.RenderServer.Services;

public static class LoggingService
{
    private static readonly string LogFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OnAirCut", "Logs");

    public static string LogFilePath { get; private set; } = string.Empty;

    public static void Configure()
    {
        Directory.CreateDirectory(LogFolder);
        LogFilePath = Path.Combine(LogFolder, "renderserver_.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                LogFilePath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}",
                shared: true)
            .CreateLogger();

        Log.Information("OnAirCut Render Server logging initialized");
    }

    public static string GetCurrentLogFilePath()
    {
        var date = DateTime.Now.ToString("yyyyMMdd");
        return Path.Combine(LogFolder, $"renderserver_{date}.log");
    }
}
