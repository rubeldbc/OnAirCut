namespace OnAirCut.Core.Constants;

public static class AppPaths
{
    /// <summary>
    /// Get the application's base directory (where the exe lives).
    /// </summary>
    public static string BaseDirectory => AppDomain.CurrentDomain.BaseDirectory;

    /// <summary>
    /// Get the lib folder path: {app}/lib/
    /// </summary>
    public static string LibDirectory => Path.Combine(BaseDirectory, "lib");

    /// <summary>
    /// Get the AppData folder path: {app}/AppData/
    /// </summary>
    public static string AppDataDirectory => Path.Combine(BaseDirectory, "AppData");

    /// <summary>
    /// Get the settings file path: {app}/AppData/settings.json
    /// </summary>
    public static string SettingsFilePath => Path.Combine(AppDataDirectory, "settings.json");

    /// <summary>
    /// Get FFmpeg executable path: {app}/lib/ffmpeg/ffmpeg.exe
    /// </summary>
    public static string FFmpegPath => Path.Combine(LibDirectory, "ffmpeg", "ffmpeg.exe");

    /// <summary>
    /// Get FFprobe executable path: {app}/lib/ffmpeg/ffprobe.exe
    /// </summary>
    public static string FFprobePath => Path.Combine(LibDirectory, "ffmpeg", "ffprobe.exe");

    /// <summary>
    /// Get Tesseract data directory: {app}/lib/tessdata/
    /// </summary>
    public static string TessdataDirectory => Path.Combine(LibDirectory, "tessdata");

    /// <summary>
    /// Get logs directory: {app}/AppData/Logs/
    /// </summary>
    public static string LogsDirectory => Path.Combine(AppDataDirectory, "Logs");

    /// <summary>
    /// Ensure all required app directories exist.
    /// </summary>
    public static void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(AppDataDirectory);
        Directory.CreateDirectory(LibDirectory);
        Directory.CreateDirectory(LogsDirectory);
    }
}
