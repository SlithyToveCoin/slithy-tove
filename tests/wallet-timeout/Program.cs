using Slithy_Tove;
using System.Text.Json;

static void Check(bool passed, string name)
{
    if (!passed) throw new Exception(name);
    Console.WriteLine("PASS " + name);
}
var now = DateTimeOffset.UtcNow;
var settings = JsonSerializer.Deserialize<AppSettings>("{\"AutoLockMinutes\":12}")!;
Check(settings.AutoLockEnabled, "Existing settings retain automatic locking");
Check(!settings.ShouldAutoLock(now.AddMinutes(-11), now), "Wait for the configured timeout");
Check(settings.ShouldAutoLock(now.AddMinutes(-12), now), "Lock at the configured timeout");
settings.AutoLockEnabled = false;
Check(!settings.ShouldAutoLock(now.AddDays(-100), now), "Disabled timeout does not lock an idle wallet");
settings.AutoLockEnabled = true;
settings.AutoLockMinutes = -1;
Check(!settings.ShouldAutoLock(now.AddSeconds(-30), now), "Invalid timeout is clamped to one minute");
Check(settings.ShouldAutoLock(now.AddMinutes(-1), now), "Clamped timeout expires");
settings.AutoLockMinutes = int.MaxValue;
Check(!settings.ShouldAutoLock(now.AddMinutes(-119), now), "Long timeouts are clamped to two hours");
Check(settings.ShouldAutoLock(now.AddMinutes(-120), now), "Upper timeout limit expires");
Check(!settings.ShouldAutoLock(now.AddMinutes(1), now), "A clock adjustment does not lock a recently active wallet");
string temporary = Path.Combine(Path.GetTempPath(), "slithy-timeout-test-" + Guid.NewGuid().ToString("N"));
try
{
    var store = new AppSettingsStore(temporary);
    settings.AutoLockEnabled = false;
    settings.AutoLockMinutes = 25;
    store.Save(settings);
    var loaded = new AppSettingsStore(temporary).Load();
    Check(!loaded.AutoLockEnabled && loaded.AutoLockMinutes == 25, "Disabled preference and duration survive reload");
    loaded.AutoLockEnabled = true;
    store.Save(loaded);
    Check(store.Load().AutoLockEnabled, "Re-enabling survives reload");
}
finally { if (Directory.Exists(temporary)) Directory.Delete(temporary, true); }

namespace Slithy_Tove
{
    internal static class UpdateService { public static bool IsAllowedUpdateUri(Uri uri) => false; }
    internal static class ReleaseTrust { public static bool IsConfigured => false; }
}
