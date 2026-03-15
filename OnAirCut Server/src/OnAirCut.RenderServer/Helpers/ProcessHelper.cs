using System.Text;
using CliWrap;
using CliWrap.Buffered;
using Serilog;

namespace OnAirCut.RenderServer.Helpers;

public static class ProcessHelper
{
    public static async Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(
        string executable, string arguments, CancellationToken cancellationToken = default,
        int timeoutSeconds = 300)
    {
        Log.Debug("Running: {Exe} {Args}", executable, arguments);

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            var result = await Cli.Wrap(executable)
                .WithArguments(arguments)
                .WithValidation(CommandResultValidation.None)
                .ExecuteBufferedAsync(cts.Token);

            return (result.ExitCode, result.StandardOutput, result.StandardError);
        }
        catch (OperationCanceledException)
        {
            Log.Warning("Process timed out or cancelled: {Exe}", executable);
            return (-1, string.Empty, "Process timed out or was cancelled");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to run process: {Exe}", executable);
            return (-1, string.Empty, ex.Message);
        }
    }

    public static async Task<bool> IsExecutableAvailableAsync(string executable)
    {
        try
        {
            var result = await Cli.Wrap(executable)
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
}
