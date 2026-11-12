using Slithy.Diagnostics;

namespace Slithy_Tove;

public partial class Form1
{
    private AppDiagnosticLog? _diagnosticLog;

    private void RecordAppDiagnostic(string message)
    {
        if (IsDisposed || Disposing) return;
        if (_diagnosticLog is null)
        {
            _diagnosticLog = new AppDiagnosticLog(Path.Combine(_settingsStore.LogDirectory, "app-diagnostics"));
            Disposed += (_, _) => _diagnosticLog.Dispose();
        }
        _diagnosticLog.RecordMessage(message);
    }
}
