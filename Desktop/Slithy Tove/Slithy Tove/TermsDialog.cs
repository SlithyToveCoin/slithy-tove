using System.Diagnostics;
using System.Text;

namespace Slithy_Tove;

internal partial class TermsDialog : Form
{
    private readonly TermsAcceptanceStore store;
    private readonly byte[] terms;
    private readonly string walletRoot;

    public TermsDialog(TermsAcceptanceStore store, byte[] terms, string walletRoot)
    {
        this.store = store;
        this.terms = terms;
        this.walletRoot = walletRoot;
        InitializeComponent();
        termsText.Text = Encoding.UTF8.GetString(terms).ReplaceLineEndings();
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        termsText.Select(0, 0);
        // The updater must not roll back while someone is reading the agreement.
        // This confirms that the UI opened, not that the user accepted the terms.
        UpdateInstaller.MarkCurrentVersionHealthy(Path.Combine(walletRoot, "updates"),
            typeof(TermsDialog).Assembly.GetName().Version ?? new Version(0, 0));
    }

    internal static bool Confirm()
    {
        string directory = new AppSettingsStore().AppDataDirectory;
        using Stream stream = typeof(TermsDialog).Assembly.GetManifestResourceStream("Slithy.BetaTerms")
            ?? throw new IOException("The bundled terms could not be read. Reinstall Slithy from slithy.io. Your wallet files have not been changed.");
        using MemoryStream buffer = new();
        stream.CopyTo(buffer);
        byte[] terms = buffer.ToArray();
        TermsAcceptanceStore store = new(directory);
        if (store.IsAccepted(terms)) return true;
        using TermsDialog dialog = new(store, terms, directory);
        return dialog.ShowDialog() == DialogResult.OK;
    }

    private void agreement_CheckedChanged(object? sender, EventArgs e) => acceptButton.Enabled = agreement.Checked;

    private void acceptButton_Click(object? sender, EventArgs e)
    {
        if (!agreement.Checked) return;
        try { store.Accept(terms); DialogResult = DialogResult.OK; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, "Your agreement could not be saved. Slithy has not started.\n\n" + ex.Message,
                "Cannot save agreement", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void filesButton_Click(object? sender, EventArgs e)
    {
        if (!Directory.Exists(walletRoot))
        {
            MessageBox.Show(this, "No Slithy data folder exists for this Windows user yet.");
            return;
        }
        try
        {
            ProcessStartInfo start = new("explorer.exe");
            start.ArgumentList.Add(walletRoot);
            Process.Start(start);
        }
        catch (Exception ex) { MessageBox.Show(this, "Could not open your files.\n\n" + walletRoot + "\n\n" + ex.Message); }
    }
}
