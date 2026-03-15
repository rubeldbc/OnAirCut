namespace OnAirCut.Core.Enums;

public enum JobStatus
{
    Pending,
    Processing,
    ExtractingFrames,
    RunningOcr,
    Rendering,
    Organizing,
    Completed,
    Failed
}
