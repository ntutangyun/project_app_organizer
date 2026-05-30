namespace ProjectAppOrganizer.Core.Windows;

public interface IWindowManager
{
    IReadOnlyList<WindowInfo> EnumerateWindows();
    bool IsWindow(nint handle);
    void MoveResize(nint handle, PixelRect rect);
    void Minimize(nint handle);
    void Restore(nint handle);
    void BringToFront(nint handle);
    void Close(nint handle);
}
