using CliWrap;
using CliWrap.Buffered;
using Serilog;

namespace OnAirCut.Recorder.Helpers;

public static class ProcessHelper
{
    public static async Task<(int ExitCode, string Output, string Error)> RunFFmpegAsync(
        string ffmpegPath, string arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            Log.Debug("Running FFmpeg: {Path} {Args}", ffmpegPath, arguments);

            var result = await Cli.Wrap(ffmpegPath)
                .WithArguments(arguments)
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync(cancellationToken);

            return (result.ExitCode, result.StandardOutput, result.StandardError);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "FFmpeg execution failed");
            return (-1, string.Empty, ex.Message);
        }
    }

    public static async Task<(int ExitCode, string Output, string Error)> RunYtDlpAsync(
        string ytDlpPath, string arguments, CancellationToken cancellationToken = default)
    {
        try
        {
            Log.Debug("Running yt-dlp: {Path} {Args}", ytDlpPath, arguments);

            var result = await Cli.Wrap(ytDlpPath)
                .WithArguments(arguments)
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync(cancellationToken);

            return (result.ExitCode, result.StandardOutput, result.StandardError);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "yt-dlp execution failed");
            return (-1, string.Empty, ex.Message);
        }
    }

    public static async Task<bool> IsFFmpegAvailableAsync(string ffmpegPath)
    {
        try
        {
            var result = await Cli.Wrap(ffmpegPath)
                .WithArguments("-version")
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync();

            return result.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public static async Task<bool> IsYtDlpAvailableAsync(string ytDlpPath)
    {
        try
        {
            var result = await Cli.Wrap(ytDlpPath)
                .WithArguments("--version")
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync();

            return result.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
