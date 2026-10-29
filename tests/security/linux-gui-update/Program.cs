if (args.Length == 2 && args[0] == "--validate-package")
{
    await new Slithy.Linux.LinuxGuiUpdateProcesses().ValidateAsync(args[1], CancellationToken.None);
    Console.WriteLine("PASS: packaged GUI and native compatibility preflight");
    return 0;
}
if (args.Contains("-version"))
{
    if (Environment.GetEnvironmentVariable("SLITHY_GUI_UPDATE_FIXTURE_NATIVE_FAIL") == "1") return 27;
    Console.WriteLine("Slithy Tove Core fixture for updater tests");
    return 0;
}
if (args.Contains("--self-test"))
    return Environment.GetEnvironmentVariable("SLITHY_GUI_UPDATE_FIXTURE_SELFTEST_FAIL") == "1" ? 27 : 0;
if (args.Contains("--update-handoff"))
{
    if (Environment.GetEnvironmentVariable("SLITHY_GUI_UPDATE_FIXTURE_EXIT") == "1") return 27;
    if (OperatingSystem.IsLinux() && Environment.GetEnvironmentVariable("SLITHY_GUI_UPDATE_FIXTURE_IGNORE_TERM") == "1")
    {
        using var registration = System.Runtime.InteropServices.PosixSignalRegistration.Create(
            System.Runtime.InteropServices.PosixSignal.SIGTERM, context => context.Cancel = true);
        await File.WriteAllTextAsync(Path.Combine(AppContext.BaseDirectory, "fixture-waiting.txt"), Environment.ProcessId.ToString());
        await Task.Delay(TimeSpan.FromMinutes(2));
        return 0;
    }
    await Slithy.Linux.LinuxGuiUpdateInstaller.AcknowledgeStartupAsync(args);
    await File.WriteAllTextAsync(Path.Combine(AppContext.BaseDirectory, "fixture-started.txt"), Environment.ProcessId.ToString());
    await Task.Delay(TimeSpan.FromMinutes(2));
    return 0;
}
return await Slithy.Linux.LinuxGuiUpdateSelfTest.RunAsync();
