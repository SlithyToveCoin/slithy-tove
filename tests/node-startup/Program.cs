using System.Net;
using System.Net.Sockets;
using Slithy_Tove;

int passed = 0;

async Task Check(string name, Func<Task> test)
{
    await test();
    Console.WriteLine($"PASS: {name}");
    passed++;
}

void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

async Task<T> Throws<T>(Func<Task> action) where T : Exception
{
    try { await action(); }
    catch (T error) { return error; }
    throw new InvalidOperationException($"Expected {typeof(T).Name}.");
}

Task Wait(Scenario scene, CancellationToken token = default) => NodeStartupWaiter.WaitAsync(
    scene.Probe, scene.ReadActivity, scene.Messages.Add, token, scene.Clock);

await Check("a 30-second block-index load finishes without a false timeout", async () =>
{
    Scenario scene = new();
    scene.ProbeResult = () => new(scene.Seconds >= 30, "Loading block index...");
    await Wait(scene);
    Require(scene.Seconds == 30, "Readiness was not awaited.");
});

await Check("advancing work can run for five minutes with no total deadline", async () =>
{
    Scenario scene = new();
    scene.Activity = () => new(true, TimeSpan.Zero, (long)(scene.Seconds / 20), "Checking blocks...");
    scene.ProbeResult = () => new(scene.Seconds >= 300, "Checking blocks...");
    await Wait(scene);
    Require(scene.Seconds == 300, "Long startup was cut short.");
});

await Check("changing startup stages count as progress", async () =>
{
    Scenario scene = new();
    scene.ProbeResult = () => new(scene.Seconds >= 180, $"Loading stage {(int)(scene.Seconds / 60)}");
    await Wait(scene);
    Require(scene.Seconds == 180, "Stage progress was ignored.");
    Require(scene.Messages.Count <= 6, "Unchanged messages were repeatedly reported.");
});

await Check("silent startup can make CPU progress before RPC opens", async () =>
{
    Scenario scene = new();
    scene.Activity = () => new(true, TimeSpan.FromSeconds(scene.Seconds * 0.25), 0, null);
    scene.ProbeResult = () => scene.Seconds >= 180
        ? new(true, "")
        : throw new HttpRequestException("Connection refused", new SocketException((int)SocketError.ConnectionRefused));
    await Wait(scene);
    Require(scene.Seconds == 180, "CPU progress did not extend startup.");
});

await Check("a frozen startup reports a stall after 90 seconds", async () =>
{
    Scenario scene = new();
    scene.ProbeResult = () => new(false, "Loading block index...");
    NodeStartupStalledException error = await Throws<NodeStartupStalledException>(() => Wait(scene));
    Require(scene.Seconds == 90, "The inactivity timeout was not measured from the last progress.");
    Require(error.Message.Contains("no startup progress", StringComparison.Ordinal), "The error did not explain the stall.");
    Require(typeof(TimeoutException).IsAssignableFrom(error.GetType()), "The stall error no longer supports existing timeout handling.");
});

await Check("repeated warmup messages and small polling CPU costs do not reset the stall timer", async () =>
{
    Scenario scene = new();
    scene.Activity = () => new(true, TimeSpan.FromMilliseconds(scene.Seconds * 2), 0, "Loading block index...");
    scene.ProbeResult = () => new(false, "Loading block index...");
    await Throws<TimeoutException>(() => Wait(scene));
    Require(scene.Seconds == 90, "Repeated replies postponed the stall.");
    Require(scene.Messages.Count == 1, "Repeated replies caused progress text churn.");
});

await Check("replaying earlier stages does not look like new work", async () =>
{
    Scenario scene = new();
    scene.ProbeResult = () => new(false, scene.Seconds % 2 < 1 ? "Loading A" : "Loading B");
    await Throws<TimeoutException>(() => Wait(scene));
    Require(scene.Seconds == 91, "Old stage names postponed the stall.");
});

await Check("the inactivity clock restarts after genuine progress", async () =>
{
    Scenario scene = new();
    scene.Activity = () => new(true, TimeSpan.Zero, scene.Seconds >= 50 ? 1 : 0, "Loading block index...");
    await Throws<TimeoutException>(() => Wait(scene));
    Require(scene.Seconds == 140, "The clock did not follow the last work advance.");
});

await Check("an exited process fails on the next poll", async () =>
{
    Scenario scene = new();
    scene.Activity = () => new(scene.Seconds < 5, TimeSpan.Zero, 0, "Loading block index...");
    InvalidOperationException error = await Throws<InvalidOperationException>(() => Wait(scene));
    Require(scene.Seconds == 5 && error.Message.Contains("stopped", StringComparison.Ordinal), "Process exit was not reported promptly.");
});

await Check("a process that exits during a successful probe is not reported ready", async () =>
{
    Scenario scene = new();
    bool running = true;
    scene.Activity = () => new(running, TimeSpan.Zero, 0, "Loading block index...");
    scene.ProbeResult = () => { running = false; return new(true, ""); };
    await Throws<InvalidOperationException>(() => Wait(scene));
    Require(scene.Seconds == 0, "Process exit waited for the stall timeout.");
});

await Check("cancellation stops a pending probe and is not retried", async () =>
{
    Scenario scene = new();
    using CancellationTokenSource stop = new();
    int calls = 0;
    await Throws<OperationCanceledException>(() => NodeStartupWaiter.WaitAsync(
        token => { calls++; stop.Cancel(); return Task.FromCanceled<NodeStartupProbe>(token); },
        scene.ReadActivity, scene.Messages.Add, stop.Token, scene.Clock));
    Require(calls == 1 && scene.Seconds == 0, "Cancellation was treated as a request timeout.");
});

await Check("a cancelled startup does not read or start anything", async () =>
{
    using CancellationTokenSource stop = new();
    stop.Cancel();
    await Throws<OperationCanceledException>(() => NodeStartupWaiter.WaitAsync(
        _ => throw new InvalidOperationException("Probe should not run."),
        () => throw new InvalidOperationException("Activity should not be read."),
        _ => throw new InvalidOperationException("Progress should not be reported."), stop.Token));
});

await Check("individual RPC request timeouts are retried", async () =>
{
    Scenario scene = new();
    scene.ProbeResult = () => scene.Seconds >= 10 ? new(true, "") : throw new TaskCanceledException("Request timed out.");
    await Wait(scene);
    Require(scene.Seconds == 10, "A request timeout cancelled startup.");
});

await Check("authentication failures remain specific and fail immediately", async () =>
{
    Scenario scene = new();
    HttpRequestException original = new("RPC authorization failed.", null, HttpStatusCode.Unauthorized);
    scene.ProbeResult = () => throw original;
    HttpRequestException actual = await Throws<HttpRequestException>(() => Wait(scene));
    Require(ReferenceEquals(original, actual) && scene.Seconds == 0, "An authorization error was hidden by retrying.");
});

await Check("fatal RPC errors are preserved", async () =>
{
    Scenario scene = new();
    InvalidOperationException original = new("The local node is on the wrong network.");
    scene.ProbeResult = () => throw original;
    InvalidOperationException actual = await Throws<InvalidOperationException>(() => Wait(scene));
    Require(ReferenceEquals(original, actual) && scene.Seconds == 0, "A fatal RPC error was hidden.");
});

await Check("unknown HTTP errors are not mistaken for connection warmup", async () =>
{
    Scenario scene = new();
    scene.ProbeResult = () => throw new HttpRequestException("Bad HTTP response.", null, HttpStatusCode.InternalServerError);
    await Throws<HttpRequestException>(() => Wait(scene));
    Require(scene.Seconds == 0, "An HTTP failure was retried without a connection cause.");
});

await Check("test policy can shorten the inactivity threshold", async () =>
{
    Scenario scene = new();
    await Throws<TimeoutException>(() => NodeStartupWaiter.WaitAsync(
        scene.Probe, scene.ReadActivity, scene.Messages.Add, CancellationToken.None,
        scene.Clock, TimeSpan.FromSeconds(3), TimeSpan.FromMilliseconds(250)));
    Require(scene.Seconds == 3, "The supplied policy was ignored.");
});

Console.WriteLine($"All {passed} node startup checks passed.");

sealed class Scenario
{
    public AdvancingTimeProvider Clock { get; } = new();
    public List<string> Messages { get; } = [];
    public double Seconds => Clock.GetElapsedTime(0).TotalSeconds;
    public Func<NodeStartupActivity> Activity { get; set; } = () => new(true, TimeSpan.Zero, 0, "Loading block index...");
    public Func<NodeStartupProbe> ProbeResult { get; set; } = () => new(false, "Loading block index...");
    public NodeStartupActivity ReadActivity() => Activity();
    public Task<NodeStartupProbe> Probe(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return Task.FromResult(ProbeResult());
    }
}

// Each awaited delay moves the clock forward. The tests exercise minutes of
// startup without making the test runner sleep through those minutes.
sealed class AdvancingTimeProvider : TimeProvider
{
    private long _ticks;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() => Interlocked.Read(ref _ticks);
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(GetTimestamp());
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        if (period != Timeout.InfiniteTimeSpan) throw new InvalidOperationException("Periodic timers are not part of this fixture.");
        Interlocked.Add(ref _ticks, dueTime.Ticks);
        TestTimer timer = new(callback, state);
        ThreadPool.QueueUserWorkItem(_ => timer.Fire());
        return timer;
    }

    private sealed class TestTimer(TimerCallback callback, object? state) : ITimer
    {
        private int _disposed;
        public void Fire()
        {
            if (Volatile.Read(ref _disposed) == 0) callback(state);
        }
        public bool Change(TimeSpan dueTime, TimeSpan period) => throw new NotSupportedException();
        public void Dispose() => Interlocked.Exchange(ref _disposed, 1);
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}
