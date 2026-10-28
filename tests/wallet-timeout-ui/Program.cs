using System.Reflection;
using Slithy_Tove;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        string temporary = Path.Combine(Path.GetTempPath(), "slithy-lock-ui-" + Guid.NewGuid().ToString("N"));
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        object Field(Form1 form, string name) => typeof(Form1).GetField(name, flags)!.GetValue(form)!;
        void Check(bool passed, string message)
        {
            if (!passed) throw new Exception(message);
            Console.WriteLine("PASS " + message);
        }
        try
        {
            Type storeType = typeof(Form1).Assembly.GetType("Slithy_Tove.AppSettingsStore")!;
            object store = Activator.CreateInstance(storeType, [temporary])!;
            using Form1 form = (Form1)Activator.CreateInstance(typeof(Form1), flags, null, [store], null)!;
            var enabled = (CheckBox)Field(form, "autoLockLabel");
            var minutes = (NumericUpDown)Field(form, "autoLockMinutesNumericUpDown");
            Check(enabled.Checked && minutes.Enabled, "Auto-lock starts enabled");
            enabled.Checked = false;
            Check(!minutes.Enabled, "Duration is disabled when automatic lock is off");
            var loaded = storeType.GetMethod("Load")!.Invoke(store, null)!;
            Check(!(bool)loaded.GetType().GetProperty("AutoLockEnabled")!.GetValue(loaded)!, "Checkbox saves the preference");
            using (Form1 reopened = (Form1)Activator.CreateInstance(typeof(Form1), flags, null, [store], null)!)
            {
                Check(!((CheckBox)Field(reopened, "autoLockLabel")).Checked, "A new window loads the disabled preference");
                Check(!((NumericUpDown)Field(reopened, "autoLockMinutesNumericUpDown")).Enabled, "A new window disables the duration control");
            }
            enabled.Checked = true;
            Check(minutes.Enabled, "Re-enabling automatic lock restores the duration control");
            minutes.Value = 25;
            loaded = storeType.GetMethod("Load")!.Invoke(store, null)!;
            Check((bool)loaded.GetType().GetProperty("AutoLockEnabled")!.GetValue(loaded)!
                && (int)loaded.GetType().GetProperty("AutoLockMinutes")!.GetValue(loaded)! == 25,
                "The enabled preference and changed duration are saved together");
            enabled.Checked = false;
            typeof(Form1).GetField("_miningStartedByApp", flags)!.SetValue(form, true);
            typeof(Form1).GetMethod("ClearWallet", flags)!.Invoke(form, [true]);
            Check(((Label)Field(form, "balanceValueLabel")).Text == "Wallet locked", "Lock does not show a zero balance");
            Check(((Label)Field(form, "availableBalanceLabel")).Text.Contains("coins are still there"), "Locked-wallet text explains that funds were not lost");
            Check(!((Button)Field(form, "sendButton")).Enabled, "A locked wallet cannot start a payment");
            Check(((Button)Field(form, "miningToggleButton")).Enabled, "Stop remains available while the wallet is locked");
            object animation = Field(form, "_bookMiningAnimation");
            Check((bool)animation.GetType().GetProperty("Mining")!.GetValue(animation)!, "Lock preserves mining animation state");
            Check(((Label)Field(form, "miningStatusLineLabel")).Text.Contains("selected mining address"), "Mining destination remains accurate after lock");
            typeof(Form1).GetField("_miningStartedByApp", flags)!.SetValue(form, false);
            typeof(Form1).GetMethod("ClearWallet", flags)!.Invoke(form, [true]);
            Check(!((Button)Field(form, "miningToggleButton")).Enabled, "Starting still requires a wallet or external address");
            Console.WriteLine("No real wallet, settings directory, or node was used.");
        }
        finally { if (Directory.Exists(temporary)) Directory.Delete(temporary, true); }
    }
}
