using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Slithy_Tove;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        System.Diagnostics.Trace.Listeners.Add(new System.Diagnostics.TextWriterTraceListener(Console.Out));
        System.Diagnostics.Trace.AutoFlush = true;
        int count = 0;
        void Check(bool value, string name)
        {
            if (!value) throw new Exception(name);
            Console.WriteLine("PASS " + name);
            count++;
        }
        UpdateManifest valid = new("0.1.42",
            "https://slithy.io/downloads/windows/Slithy-Tove-Setup-0.1.42.exe",
            new string('a', 64), "Beta", DateTimeOffset.UtcNow);
        SetupForm.ValidateInstaller(valid);
        Check(true, "Accept current full installer");
        foreach (UpdateManifest bad in new[] {
            valid with { Version = "0.1.41" },
            valid with { PackageUrl = "http://slithy.io/downloads/windows/Slithy-Tove-Setup-0.1.42.exe" },
            valid with { PackageUrl = "https://example.com/downloads/windows/Slithy-Tove-Setup-0.1.42.exe" },
            valid with { PackageUrl = "https://slithy.io/updates/windows/package.zip" },
            valid with { PackageUrl = valid.PackageUrl + "?redirect=elsewhere" },
            valid with { PackageUrl = valid.PackageUrl.Replace("slithy.io/", "slithy.io:8443/") },
            valid with { PackageUrl = valid.PackageUrl.Replace("https://", "https://user:password@") }
        })
        {
            bool rejected = false;
            try { SetupForm.ValidateInstaller(bad); }
            catch (InvalidDataException) { rejected = true; }
            Check(rejected, "Reject unsupported installer version or URL");
        }
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(valid);
        byte[] signature = key.SignData(payload, HashAlgorithmName.SHA256,
            DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        Check(UpdateService.VerifySignedPayload(payload, signature, key.ExportSubjectPublicKeyInfoPem()).Version == "0.1.42",
            "Verify signed manifest");
        payload[0] ^= 1;
        bool tampered = false;
        try { UpdateService.VerifySignedPayload(payload, signature, key.ExportSubjectPublicKeyInfoPem()); }
        catch (CryptographicException) { tampered = true; }
        Check(tampered, "Reject changed manifest");
        bool unsigned = false;
        try { SetupForm.VerifyPublisherAsync(typeof(Program).Assembly.Location, CancellationToken.None).GetAwaiter().GetResult(); }
        catch (CryptographicException) { unsigned = true; }
        Check(unsigned, "Reject unsigned Windows program");
        Check(ChainSyncDisplay.From(25, 100, false, false).Percent == 25, "Known block progress");
        Check(ChainSyncDisplay.From(0, 0, false, false).Waiting, "Unknown target uses waiting state");
        Check(ChainSyncDisplay.From(100, 100, false, false).Waiting, "Equal heights do not claim ready");
        Check(!ChainSyncDisplay.From(100, 100, true, false).Visible, "Hide progress after sync");
        Check(ChainSyncDisplay.From(5, 100, false, true).Text.Contains("last minute"), "Explain stalled sync");
        Check(ChainSyncDisplay.From(long.MaxValue - 1, long.MaxValue, false, false).Percent == 99,
            "Large heights cannot overflow or claim completion");
        Check(ChainSyncDisplay.From(50, 200, false, false).Percent == 25, "New headers update progress target");
        if (args.Length == 2 && args[0] == "--verify-publisher")
        {
            SetupForm.VerifyPublisherAsync(Path.GetFullPath(args[1]), CancellationToken.None).GetAwaiter().GetResult();
            Check(true, "Accept trusted Windows publisher signature");
        }
        if (args.Length == 2 && args[0] == "--verify-feed")
        {
            using UpdateService service = new(Path.GetFullPath(args[1]), allowRedirects: false);
            UpdateCheckResult result = service.CheckAsync(
                new Uri("https://slithy.io/updates/windows/installer/stable.json"),
                ReleaseTrust.UpdatePublicKeyPem, new Version(0, 0)).GetAwaiter().GetResult();
            UpdateManifest release = result.Manifest ?? throw new Exception("Missing installer manifest");
            SetupForm.ValidateInstaller(release);
            string package = service.DownloadVerifiedPackageAsync(release).GetAwaiter().GetResult();
            SetupForm.VerifyPublisherAsync(package, CancellationToken.None).GetAwaiter().GetResult();
            Check(true, "Live installer feed, package hash and publisher signature");
        }
        if (args.Length == 1)
        {
            ApplicationConfiguration.Initialize();
            using SetupForm form = new();
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(-4000, -4000);
            form.Show();
            Application.DoEvents();
            using Bitmap bitmap = new(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
            bitmap.Save(Path.GetFullPath(args[0]));
            Console.WriteLine("Saved installer layout preview.");
        }
        Console.WriteLine($"{count} checks passed.");
    }
}
