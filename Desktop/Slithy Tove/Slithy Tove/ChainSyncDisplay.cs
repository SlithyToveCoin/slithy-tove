namespace Slithy_Tove;

internal sealed record ChainSyncDisplay(bool Visible, bool Waiting, int Percent, string Text)
{
    internal static ChainSyncDisplay From(long height, long headers, bool ready, bool stalled)
    {
        height = Math.Max(0, height);
        long target = Math.Max(height, headers);
        if (ready) return new(false, false, 100, "Chain is up to date.");
        bool waiting = target <= height;
        int percent = waiting ? 0 : Math.Clamp((int)(100m * height / target), 0, 99);
        string pause = stalled ? " No new blocks in the last minute. Check Network for connection details." : "";
        string text = waiting
            ? $"Checking for newer blocks. Local height: {height:N0}.{pause}"
            : $"Catching up: {height:N0} of {target:N0} known blocks ({percent}%).{pause}";
        return new(true, waiting, percent, text);
    }
}
