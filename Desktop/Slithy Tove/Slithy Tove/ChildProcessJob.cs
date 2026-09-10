//======================================================================
//                        Slithy Tove
//======================================================================


//===============================================
//               Child Process Cleanup
//===============================================
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Slithy_Tove;

// Ties helper processes to this app so they do not get left behind.
internal static class ChildProcessJob
{
    private static readonly nint JobHandle = CreateKillOnCloseJob();

    public static void Assign(Process process)
    {
        if (!AssignProcessToJobObject(JobHandle, process.Handle))
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "Could not attach the Slithy helper process to the desktop application.");
        }
    }

    private static nint CreateKillOnCloseJob()
    {
        nint handle = CreateJobObject(nint.Zero, null);
        if (handle == nint.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        JOBOBJECT_EXTENDED_LIMIT_INFORMATION information = new();
        information.BasicLimitInformation.LimitFlags =
            JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;
        int length = Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
        nint buffer = Marshal.AllocHGlobal(length);
        try
        {
            Marshal.StructureToPtr(information, buffer, false);
            if (!SetInformationJobObject(
                handle,
                JobObjectExtendedLimitInformation,
                buffer,
                (uint)length))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }
        catch
        {
            CloseHandle(handle);
            throw;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
        return handle;
    }

    internal static bool RunSelfTest()
    {
        // Use a disposable sleeping process, never a wallet or an existing node.
        nint testJob = CreateKillOnCloseJob();
        Process? child = null;
        try
        {
            ProcessStartInfo info = new()
            {
                FileName = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"),
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (string argument in new[] { "-NoProfile", "-NonInteractive", "-WindowStyle", "Hidden", "-Command", "Start-Sleep -Seconds 60" })
                info.ArgumentList.Add(argument);
            child = Process.Start(info) ?? throw new InvalidOperationException("Test process did not start.");
            if (!AssignProcessToJobObject(testJob, child.Handle)) throw new Win32Exception(Marshal.GetLastWin32Error());
            if (!CloseHandle(testJob)) throw new Win32Exception(Marshal.GetLastWin32Error());
            testJob = nint.Zero;
            bool exited = child.WaitForExit(5000);
            Console.WriteLine(exited ? "Child process cleanup passed." : "Child process remained running.");
            return exited;
        }
        finally
        {
            if (testJob != nint.Zero) CloseHandle(testJob);
            if (child is { HasExited: false }) { child.Kill(entireProcessTree: true); child.WaitForExit(); }
            child?.Dispose();
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(nint handle);

    private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000;
    private const int JobObjectExtendedLimitInformation = 9;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint CreateJobObject(nint jobAttributes, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetInformationJobObject(
        nint job,
        int informationClass,
        nint information,
        uint informationLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AssignProcessToJobObject(nint job, nint process);

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public nuint MinimumWorkingSetSize;
        public nuint MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public nuint Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public nuint ProcessMemoryLimit;
        public nuint JobMemoryLimit;
        public nuint PeakProcessMemoryUsed;
        public nuint PeakJobMemoryUsed;
    }
}

