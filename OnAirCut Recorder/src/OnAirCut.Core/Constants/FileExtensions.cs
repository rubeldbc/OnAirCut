namespace OnAirCut.Core.Constants;

public static class FileExtensions
{
    public static readonly string[] SupportedVideo =
        [".mp4", ".mkv", ".mov", ".avi", ".ts", ".flv", ".wmv", ".webm"];

    public static readonly string[] SupportedImage =
        [".jpg", ".jpeg", ".png", ".bmp", ".tiff"];

    public static readonly string[] SupportedOverlay =
        [".png", ".mov", ".webm"];

    public static bool IsOverlayFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return SupportedOverlay.Contains(ext);
    }

    public static bool IsVideoOverlay(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".mov" or ".webm";
    }

    public const string JobFile = ".json";
    public const string LockFile = ".lock";
    public const string TempSuffix = ".tmp";

    public static bool IsVideoFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return SupportedVideo.Contains(ext);
    }

    public static bool IsImageFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return SupportedImage.Contains(ext);
    }
}
