using CommunityToolkit.Mvvm.ComponentModel;

namespace ProjectAppOrganizer.Core.Models;

public partial class TodoItem : ObservableObject
{
    [ObservableProperty] private string _text = string.Empty;
    [ObservableProperty] private bool _done;
}
