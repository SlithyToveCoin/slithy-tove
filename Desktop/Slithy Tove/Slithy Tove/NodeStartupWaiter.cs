using System.Net.Http;
using System.Net.Sockets;

namespace Slithy_Tove;

internal sealed record NodeStartupProbe(bool Ready, string Stage);

internal sealed record NodeStartupActivity(bool Running, TimeSpan CpuTime, long WorkSequence, string? Stage);

// The form can offer another wait without treating a slow node as a failed wallet.
internal sealed class NodeStartupStalledException(string message) : TimeoutException(message)
{
}

// Loading an existing block index can take a while. Watch for work instead of
// assuming that a running node must answer RPC within a fixed startup deadline.
internal static class NodeStartupWaiter
{
    internal static async Task WaitAsync(
        Func<CancellationToken, Task<NodeStartupProbe>> probe,
        Func<NodeStartupActivity> readActivity,
        Action<string> reportProgress,
        CancellationToken cancellationToken,
        TimeProvider? timeProvider = null,
        TimeSpan? stallTimeout = null,
        TimeSpan? pollInterval = null)
    {
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(readActivity);
        ArgumentNullException.ThrowIfNull(reportProgress);
        TimeProvider clock = timeProvider ?? TimeProvider.System;
        TimeSpan stallAfter = stallTimeout ?? TimeSpan.FromSeconds(90);
        TimeSpan pollEvery = pollInterval ?? TimeSpan.FromMilliseconds(500);
        if (stallAfter <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(stallTimeout));
        if (pollEvery <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(pollInterval));

        cancellationToken.ThrowIfCancellationRequested();
        NodeStartupActivity previous = readActivity();
        ThrowIfStopped(previous);
        long lastProgress = clock.GetTimestamp();
        HashSet<string> stagesSeen = new(StringComparer.Ordinal);
        string lastReported = "";

        void Report(string? stage)
        {
            if (string.IsNullOrWhiteSpace(stage)) return;
            string text = stage.Trim();
            if (text == lastReported) return;
            lastReported = text;
            reportProgress(text);
        }

        bool NewStage(string? stage)
        {
            if (string.IsNullOrWhiteSpace(stage)) return false;
            return stagesSeen.Add(stage.Trim());
        }

        NewStage(previous.Stage);
        Report(string.IsNullOrWhiteSpace(previous.Stage) ? "Starting the local Slithy node..." : previous.Stage);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfStopped(readActivity());
            NodeStartupProbe? result = null;
            try
            {
                result = await probe(cancellationToken);
            }
            catch (HttpRequestException ex) when (IsConnectionFailure(ex))
            {
                // The process can be alive before it has opened its RPC socket.
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // A request timeout is not an instruction to cancel startup.
            }

            cancellationToken.ThrowIfCancellationRequested();
            NodeStartupActivity current = readActivity();
            ThrowIfStopped(current);
            if (result?.Ready == true)
            {
                Report("Local node is ready.");
                return;
            }

            bool activityStageChanged = NewStage(current.Stage);
            bool rpcStageChanged = NewStage(result?.Stage);
            bool workAdvanced = current.WorkSequence > previous.WorkSequence;
            // Ignore tiny CPU increments caused by our own RPC polls. A frozen
            // loading message must not extend this wait just by being repeated.
            bool cpuAdvanced = current.CpuTime - previous.CpuTime >= TimeSpan.FromMilliseconds(50);
            if (activityStageChanged || rpcStageChanged || workAdvanced || cpuAdvanced)
                lastProgress = clock.GetTimestamp();

            if (rpcStageChanged) Report(result?.Stage);
            else if (activityStageChanged) Report(current.Stage);
            previous = current;

            if (clock.GetElapsedTime(lastProgress) >= stallAfter)
                throw new NodeStartupStalledException(
                    $"The local node has shown no startup progress for {stallAfter.TotalSeconds:0} seconds. " +
                    "Check the technical log for details.");

            await Task.Delay(pollEvery, clock, cancellationToken);
        }
    }

    private static void ThrowIfStopped(NodeStartupActivity activity)
    {
        if (!activity.Running)
            throw new InvalidOperationException(
                "The local node stopped while it was starting. Check the technical log for details.");
    }

    private static bool IsConnectionFailure(HttpRequestException error) =>
        error.StatusCode is null &&
        (error.HttpRequestError is HttpRequestError.ConnectionError or HttpRequestError.NameResolutionError ||
         error.InnerException is SocketException);
}
