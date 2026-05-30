using CommunityToolkit.Mvvm.ComponentModel;

namespace ProjectAppOrganizer.Core.ViewModels;

public partial class WindowTab : ObservableObject
{
    [ObservableProperty] private string _title;
    [ObservableProperty] private bool _isActive;

    public WindowTab(nint handle, string title, TabKind kind)
    {
        Handle = handle;
        _title = title;
        Kind = kind;
    }

    public nint Handle { get; }
    public TabKind Kind { get; }
    public bool IsTerminal => Kind == TabKind.Terminal;
}
