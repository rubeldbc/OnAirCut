namespace OnAirCut.Core.Constants;

public static class FolderNames
{
    // Root-level folders
    public const string Assets = "Assets";
    public const string Ingest = "Ingest";
    public const string Jobs = "Jobs";
    public const string Working = "Working";
    public const string Output = "Output";
    public const string Logs = "Logs";

    // Assets subfolders
    public const string AdSets = "AdSets";
    public const string OcrProfiles = "OcrProfiles";

    // Ingest subfolders
    public const string RawClips = "RawClips";

    // Jobs subfolders
    public const string Pending = "Pending";
    public const string Processing = "Processing";
    public const string Done = "Done";
    public const string Failed = "Failed";

    // Working subfolders (per job)
    public const string Frames = "frames";

    // Combined paths
    public static string AssetsAdSets => Path.Combine(Assets, AdSets);
    public static string AssetsOcrProfiles => Path.Combine(Assets, OcrProfiles);
    public static string IngestRawClips => Path.Combine(Ingest, RawClips);
    public static string JobsPending => Path.Combine(Jobs, Pending);
    public static string JobsProcessing => Path.Combine(Jobs, Processing);
    public static string JobsDone => Path.Combine(Jobs, Done);
    public static string JobsFailed => Path.Combine(Jobs, Failed);

    /// <summary>
    /// All top-level subdirectories that must exist under the shared root.
    /// </summary>
    public static readonly string[] RequiredSubfolders =
    [
        AssetsAdSets,
        AssetsOcrProfiles,
        IngestRawClips,
        JobsPending,
        JobsProcessing,
        JobsDone,
        JobsFailed,
        Working,
        Output,
        Logs
    ];
}
