using System.Reflection;
using System.Text;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        string root = Path.Combine(Path.GetTempPath(), "SlithyTermsTest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Assembly app = Assembly.Load("Slithy Tove");
        Type storeType = app.GetType("Slithy_Tove.TermsAcceptanceStore", true)!;
        Type dialogType = app.GetType("Slithy_Tove.TermsDialog", true)!;
        object store = Activator.CreateInstance(storeType, root)!;
        using Stream stream = app.GetManifestResourceStream("Slithy.BetaTerms")!;
        using MemoryStream bytes = new();
        stream.CopyTo(bytes);
        byte[] terms = bytes.ToArray();
        bool Accepted(byte[] text) => (bool)storeType.GetMethod("IsAccepted")!.Invoke(store, [text])!;
        void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS " + message); }
        Check(!Accepted(terms), "First run requires agreement");
        Check(Encoding.UTF8.GetString(terms).Contains("11. Changes and questions"), "Full terms bundled offline");
        Type updaterType = app.GetType("Slithy_Tove.UpdateInstaller", true)!;
        string updates = Path.Combine(root, "updates");
        updaterType.GetMethod("CreatePendingUpdate")!.Invoke(null,
            [updates, "0.1.40", "0.1.41", Path.Combine(root, "app", "Slithy Tove.exe"), ""]);
        using (Form dialog = (Form)Activator.CreateInstance(dialogType, store, terms, root)!)
        {
            T Field<T>(string name) => (T)dialogType.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(dialog)!;
            Check(!Field<CheckBox>("agreement").Checked && !Field<Button>("acceptButton").Enabled, "Agreement starts unchecked");
            Check(Field<Button>("filesButton").Enabled, "Wallet files accessible before agreement");
            // Close the test window automatically, without touching the real user's settings.
            using System.Windows.Forms.Timer timer = new() { Interval = 150 };
            timer.Tick += (_, _) => { timer.Stop(); Field<Button>("declineButton").PerformClick(); };
            timer.Start();
            Check(dialog.ShowDialog() == DialogResult.Cancel && !Accepted(terms), "Decline does not save acceptance");
            Check(File.Exists(Path.Combine(updates, "healthy-update.txt")), "Terms screen confirms update startup before acceptance");
        }
        using (Form dialog = (Form)Activator.CreateInstance(dialogType, store, terms, root)!)
        {
            using System.Windows.Forms.Timer timer = new() { Interval = 150 };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                var check = (CheckBox)dialogType.GetField("agreement", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(dialog)!;
                var button = (Button)dialogType.GetField("acceptButton", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(dialog)!;
                using Bitmap bitmap = new(dialog.Width, dialog.Height);
                dialog.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                bitmap.Save(Path.Combine(root, "agreement.png"));
                check.Checked = true;
                Check(button.Enabled, "Checkbox enables agreement button");
                button.PerformClick();
            };
            timer.Start();
            Check(dialog.ShowDialog() == DialogResult.OK && Accepted(terms), "Explicit agreement is saved");
        }
        object reopened = Activator.CreateInstance(storeType, root)!;
        Check((bool)storeType.GetMethod("IsAccepted")!.Invoke(reopened, [terms])!, "Acceptance survives restart");
        Check(!Accepted(Encoding.UTF8.GetBytes("Updated terms")), "Changed terms need fresh agreement");
        File.WriteAllText(Path.Combine(root, "terms-acceptance.json"), "broken");
        Check(!Accepted(terms), "Damaged acceptance record fails closed");
        File.WriteAllText(Path.Combine(root, "terms-acceptance.json"), "{\"Version\":\"old\"}");
        Check(!Accepted(terms), "Old version needs fresh agreement");
        Console.WriteLine("Test files: " + root);
    }
}
