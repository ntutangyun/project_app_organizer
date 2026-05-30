using System.Text;
using ProjectAppOrganizer.Core.Windows;

namespace ProjectAppOrganizer.App.Windows;

public class Win32WindowManager : IWindowManager
{
    public nint SelfHandle { get; set; }

    public IReadOnlyList<WindowInfo> EnumerateWindows()
    {
        var result = new List<WindowInfo>();

        Win32Interop.EnumWindows((hWnd, _) =>
        {
            var len = Win32Interop.GetWindowTextLength(hWnd);
            var sb = new StringBuilder(len + 1);
            Win32Interop.GetWindowText(hWnd, sb, sb.Capacity);
            var title = sb.ToString();

            var exStyle = Win32Interop.GetWindowLong(hWnd, Win32Interop.GWL_EXSTYLE);
            var isTool = ((long)(uint)exStyle & Win32Interop.WS_EX_TOOLWINDOW) != 0;

            Win32Interop.GetWindowThreadProcessId(hWnd, out var pid);
            var procName = SafeProcessName(pid);

            var info = new WindowInfo(hWnd, title, procName, Win32Interop.IsWindowVisible(hWnd), isTool);
            if (WindowFilter.ShouldList(info, SelfHandle))
                result.Add(info);
            return true;
        }, 0);

        return result;
    }

    public bool IsWindow(nint handle) => Win32Interop.IsWindow(handle);

    public void MoveResize(nint handle, PixelRect rect) =>
        Win32Interop.SetWindowPos(handle, Win32Interop.HWND_TOP,
            rect.X, rect.Y, rect.Width, rect.Height,
            Win32Interop.SWP_SHOWWINDOW | Win32Interop.SWP_NOACTIVATE);

    public void Show(nint handle) => Win32Interop.ShowWindow(handle, Win32Interop.SW_SHOWNA);

    public void Hide(nint handle) => Win32Interop.ShowWindow(handle, Win32Interop.SW_HIDE);

    public void Restore(nint handle) => Win32Interop.ShowWindow(handle, Win32Interop.SW_RESTORE);

    public void BringToFront(nint handle) => Win32Interop.SetForegroundWindow(handle);

    public void Close(nint handle) =>
        Win32Interop.PostMessage(handle, Win32Interop.WM_CLOSE, 0, 0);

    public void SetOwner(nint handle, nint owner) =>
        Win32Interop.SetWindowLongPtr(handle, Win32Interop.GWLP_HWNDPARENT, owner);

    private static string SafeProcessName(uint pid)
    {
        try { return System.Diagnostics.Process.GetProcessById((int)pid).ProcessName; }
        catch { return string.Empty; }
    }
}
