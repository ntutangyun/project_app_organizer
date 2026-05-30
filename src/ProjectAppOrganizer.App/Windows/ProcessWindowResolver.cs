using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ProjectAppOrganizer.App.Windows;

/// <summary>
/// Resolves the top-level window belonging to a launched process or any of its
/// descendants. A shell's visible window is often owned by a child process
/// (conhost hosting cmd/powershell, git-bash spawning mintty) rather than the
/// process we started, so <see cref="System.Diagnostics.Process.MainWindowHandle"/>
/// is unreliable.
/// </summary>
internal static class ProcessWindowResolver
{
    public static nint FindMainWindow(int rootPid)
    {
        var pids = DescendantPids((uint)rootPid);
        nint found = 0;

        Win32Interop.EnumWindows((hWnd, _) =>
        {
            if (!Win32Interop.IsWindowVisible(hWnd)) return true;
            Win32Interop.GetWindowThreadProcessId(hWnd, out var pid);
            if (!pids.Contains(pid)) return true;
            if (Win32Interop.GetWindowTextLength(hWnd) == 0) return true;
            found = hWnd;
            return false; // stop enumeration
        }, 0);

        return found;
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
