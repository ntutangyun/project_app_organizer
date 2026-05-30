namespace ProjectAppOrganizer.Core.Windows;

public record WindowInfo(
    nint Handle,
    string Title,
    string ProcessName,
    bool IsVisible,
    bool IsToolWindow);
