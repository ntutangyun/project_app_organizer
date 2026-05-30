namespace ProjectAppOrganizer.Core.Windows;

public static class WindowFilter
{
    public static bool ShouldList(WindowInfo w, nint selfHandle)
    {
        if (w.Handle == selfHandle) return false;
        if (!w.IsVisible) return false;
        if (w.IsToolWindow) return false;
        if (string.IsNullOrWhiteSpace(w.Title)) return false;
        return true;
    }
}
