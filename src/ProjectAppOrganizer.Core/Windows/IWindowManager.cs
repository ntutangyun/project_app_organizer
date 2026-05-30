namespace ProjectAppOrganizer.Core.Windows;

public interface IWindowManager
{
    IReadOnlyList<WindowInfo> EnumerateWindows();
    bool IsWindow(nint handle);
    void MoveResize(nint handle, PixelRect rect);
    void Show(nint handle);
    void Hide(nint handle);
    void Restore(nint handle);
    void BringToFront(nint handle);
    void Close(nint handle);
}
