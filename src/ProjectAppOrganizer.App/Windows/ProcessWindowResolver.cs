using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ProjectAppOrganizer.App.Windows;

/// <summary>
/// Resolves the top-level window of a launched shell. The visible window is usually
/// owned by a child process (conhost hosting cmd/powershell, git-bash spawning
/// mintty) rather than the process we started, so we match by process tree — with a
/// process-name fallback (e.g. "mintty") — and ignore windows that existed before launch.
/// </summary>
internal static class ProcessWindowResolver
{
    public static HashSet<nint> SnapshotVisibleWindows()
    {
        var set = new HashSet<nint>();
        Win32Interop.EnumWindows((h, _) =>
        {
            if (Win32Interop.IsWindowVisible(h)) set.Add(h);
            return true;
        }, 0);
        return set;
    }

    /// <summary>
    /// Find a newly-appeared, visible, titled window owned by <paramref name="rootPid"/>
    /// or a descendant, or (fallback) by a process named <paramref name="hintProcessName"/>.
    /// </summary>
    public static nint FindNewWindow(int rootPid, HashSet<nint> preexisting, string? hintProcessName)
    {
        var pids = DescendantPids((uint)rootPid);
        nint found = 0;

        Win32Interop.EnumWindows((hWnd, _) =>
        {
            if (preexisting.Contains(hWnd)) return true;
            if (!Win32Interop.IsWindowVisible(hWnd)) return true;
            if (Win32Interop.GetWindowTextLength(hWnd) == 0) return true;

            Win32Interop.GetWindowThreadProcessId(hWnd, out var pid);
            var match = pids.Contains(pid);
            if (!match && hintProcessName is not null)
                match = string.Equals(SafeProcessName(pid), hintProcessName, StringComparison.OrdinalIgnoreCase);
            if (!match) return true;

            found = hWnd;
            return false; // stop enumeration
        }, 0);

        return found;
    }

    private static string SafeProcessName(uint pid)
    {
        try { return System.Diagnostics.Process.GetProcessById((int)pid).ProcessName; }
        catch { return string.Empty; }
    }

    private static HashSet<uint> DescendantPids(uint rootPid)
    {
        var childrenByParent = BuildChildMap();

        var result = new HashSet<uint>();
        var queue = new Queue<uint>();
        queue.Enqueue(rootPid);
        while (queue.Count > 0)
        {
            var pid = queue.Dequeue();
            if (!result.Add(pid)) continue; // guard against pid-reuse cycles
            if (childrenByParent.TryGetValue(pid, out var kids))
                foreach (var kid in kids) queue.Enqueue(kid);
        }
        return result;
    }

    private static Dictionary<uint, List<uint>> BuildChildMap()
    {
        var map = new Dictionary<uint, List<uint>>();
        var snapshot = Win32Interop.CreateToolhelp32Snapshot(Win32Interop.TH32CS_SNAPPROCESS, 0);
        if (snapshot == Win32Interop.INVALID_HANDLE_VALUE) return map;

        try
        {
            var entry = new Win32Interop.PROCESSENTRY32
            {
                dwSize = (uint)Marshal.SizeOf<Win32Interop.PROCESSENTRY32>()
            };
            if (Win32Interop.Process32First(snapshot, ref entry))
            {
                do
                {
                    if (!map.TryGetValue(entry.th32ParentProcessID, out var list))
                        map[entry.th32ParentProcessID] = list = new List<uint>();
                    list.Add(entry.th32ProcessID);
                } while (Win32Interop.Process32Next(snapshot, ref entry));
            }
        }
        finally
        {
            Win32Interop.CloseHandle(snapshot);
        }
        return map;
    }
}
