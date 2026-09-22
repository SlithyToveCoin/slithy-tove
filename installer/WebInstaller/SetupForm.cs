using System.Diagnostics;
using System.Security.Cryptography;

namespace Slithy_Tove;

internal partial class SetupForm : Form
{
    // The web installer has its own feed. The desktop ZIP feed is not an installer.
    private static readonly Uri Feed = new("https://slithy.io/updates/windows/installer/stable.json");
    private CancellationTokenSource _cancel = new();
    private bool _installing;
    private bool _runningSetup;

    internal SetupForm()
    {
        InitializeComponent();
        using Stream? source = typeof(SetupForm).Assembly.GetManifestResourceStream("Slithy.InstallerArtwork");
        if (source is not null)
        {
            using Image original = Image.FromStream(source);
            artwork.Image = new Bitmap(original);
        }
    }

    private async void Install_Click(object? sender, EventArgs e)
    {
        _cancel.Dispose();
        _cancel = new CancellationTokenSource();
        _installing = true;
        install.Enabled = false;
        string work = Path.Combine(Path.GetTempPath(), "Slithy-Setup-" + Guid.NewGuid().ToString("N"));
        try
        {
            progress.Style = ProgressBarStyle.Marquee;
            status.Text = "Checking the signed release information...";
            using UpdateService updates = new(work, allowRedirects: false);
            UpdateCheckResult result = await updates.CheckAsync(Feed, ReleaseTrust.UpdatePublicKeyPem,
                new Version(0, 0), _cancel.Token);
            UpdateManifest manifest = result.Manifest ?? throw new InvalidDataException("No installer is available.");
            ValidateInstaller(manifest);
            using (Microsoft.Win32.RegistryKey? installed = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Uninstall\{B6358ED9-8389-4851-85D2-76236D2D1FCB}_is1"))
            {
                if (Version.TryParse(installed?.GetValue("DisplayVersion") as string, out Version? existing) &&
                    existing > Version.Parse(manifest.Version))
                    throw new InvalidDataException("A newer Slithy version is already installed. This setup will not downgrade it.");
            }
            status.Text = $"Downloading Slithy Tove {manifest.Version}...";
            bool downloading = true;
            Progress<int> reporter = new(value =>
            {
                if (IsDisposed || !_installing || !downloading) return;
                progress.Style = ProgressBarStyle.Continuous;
                progress.Value = value;
                status.Text = $"Downloading Slithy Tove {manifest.Version}: {value}%";
            });
            string file = await updates.DownloadVerifiedPackageAsync(manifest, reporter, _cancel.Token);
            downloading = false;
            status.Text = "Checking the Windows publisher signature...";
            // Hold the verified file read-only until setup exits so it cannot change between checks.
            using FileStream held = new(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            string hash = Convert.ToHexString(await SHA256.HashDataAsync(held, _cancel.Token));
            if (!hash.Equals(manifest.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new CryptographicException("The downloaded installer changed before verification.");
            await VerifyPublisherAsync(file, _cancel.Token);
            _cancel.Token.ThrowIfCancellationRequested();
            _runningSetup = true;
            cancel.Enabled = false;
            progress.Style = ProgressBarStyle.Marquee;
            status.Text = "Follow the installation window to finish. Your wallet files will not be removed.";
            using Process setup = Process.Start(new ProcessStartInfo(file) { UseShellExecute = false })
                ?? throw new IOException("Windows could not open the installer.");
            await setup.WaitForExitAsync();
            if (setup.ExitCode != 0)
                throw new IOException($"Setup did not finish (code {setup.ExitCode}). You can try again.");
            progress.Style = ProgressBarStyle.Continuous;
            progress.Value = 100;
            status.Text = "Installation finished. You can close this window.";
            install.Visible = false;
            cancel.Text = "Close";
        }
        catch (OperationCanceledException)
        {
            progress.Style = ProgressBarStyle.Continuous;
            progress.Value = 0;
            status.Text = "Download canceled. Nothing was installed.";
        }
        catch (Exception ex)
        {
            progress.Style = ProgressBarStyle.Continuous;
            progress.Value = 0;
            status.Text = "Setup could not finish. " + ex.Message;
        }
        finally
        {
            _runningSetup = false;
            _installing = false;
            install.Enabled = true;
            cancel.Enabled = true;
            try { if (Directory.Exists(work)) Directory.Delete(work, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    internal static void ValidateInstaller(UpdateManifest manifest)
    {
        Uri uri = new(manifest.PackageUrl);
        if (uri.Scheme != "https" || uri.Host != "slithy.io" || !uri.IsDefaultPort ||
            uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0 ||
            !uri.AbsolutePath.StartsWith("/downloads/windows/Slithy-Tove-Setup-", StringComparison.Ordinal) ||
            !uri.AbsolutePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
            Version.Parse(manifest.Version) < new Version(0, 1, 42))
            throw new InvalidDataException("The feed does not contain a supported Windows installer.");
    }

    internal static async Task VerifyPublisherAsync(string file, CancellationToken cancellation)
    {
        // Ask Windows to validate Authenticode, including the signing timestamp and trust chain.
        // The full PowerShell path avoids executing a program placed earlier in PATH.
        string script = "$ErrorActionPreference='Stop'; $ProgressPreference='SilentlyContinue'; $s=Get-AuthenticodeSignature -LiteralPath '" +
            file.Replace("'", "''") + "'; if($s.Status -ne 'Valid'){Write-Output $s.Status;exit 1}; if(" +
            "$s.SignerCertificate.GetNameInfo([System.Security.Cryptography.X509Certificates.X509NameType]::SimpleName,$false) " +
            "-cne 'CS Idea Labs LLC'){Write-Output 'PublisherMismatch';exit 1}; exit 0";
        ProcessStartInfo start = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
            "WindowsPowerShell", "v1.0", "powershell.exe"))
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        foreach (string arg in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-EncodedCommand",
            Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script)) })
            start.ArgumentList.Add(arg);
        // Do not inherit PowerShell 7 or user-installed modules when running Windows PowerShell.
        start.Environment["PSModulePath"] = Path.Combine(
            Path.GetDirectoryName(start.FileName)!, "Modules");
        using Process check = Process.Start(start) ?? throw new IOException("Windows signature verification could not start.");
        Task<string> output = check.StandardOutput.ReadToEndAsync(cancellation);
        Task<string> errors = check.StandardError.ReadToEndAsync(cancellation);
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(60));
        try { await check.WaitForExitAsync(deadline.Token); }
        catch { if (!check.HasExited) check.Kill(entireProcessTree: true); throw; }
        string result = (await output).Trim();
        string diagnostics = await errors;
        Trace.WriteLine(diagnostics);
        if (check.ExitCode != 0)
            throw new CryptographicException("Windows could not verify CS Idea Labs LLC as the installer publisher. " +
                (result.Length is > 0 and < 80 ? result + ". " : "") + "Nothing was installed.");
    }

    private void SetupForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!_installing) return;
        e.Cancel = true;
        if (!_runningSetup) _cancel.Cancel();
    }
}
