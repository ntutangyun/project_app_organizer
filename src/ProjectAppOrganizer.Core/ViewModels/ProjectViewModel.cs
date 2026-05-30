using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ProjectAppOrganizer.Core.Models;
using ProjectAppOrganizer.Core.Terminals;
using ProjectAppOrganizer.Core.Windows;

namespace ProjectAppOrganizer.Core.ViewModels;

public partial class ProjectViewModel : ObservableObject
{
    private readonly IWindowManager _wm;
    private readonly ITerminalLauncher _launcher;
    private readonly StageManager _stage;
    private readonly Func<PixelRect> _stageRect;

    [ObservableProperty] private WindowTab? _activeTab;

    public ProjectViewModel(
        Project project,
        IWindowManager wm,
        ITerminalLauncher launcher,
        StageManager stage,
        Func<PixelRect> stageRect)
    {
        Project = project;
        _wm = wm;
        _launcher = launcher;
        _stage = stage;
        _stageRect = stageRect;
    }

    public Project Project { get; }
    public ObservableCollection<WindowTab> Tabs { get; } = new();

    public void OpenTerminal(TerminalProfile profile)
    {
        var handle = _launcher.Launch(profile);
        if (handle == 0) return;
        var tab = new WindowTab(handle, profile.Name, TabKind.Terminal);
        Tabs.Add(tab);
        Activate(tab);
    }

    public void AttachWindow(WindowInfo window)
    {
        if (Tabs.Any(t => t.Handle == window.Handle)) return;
        var tab = new WindowTab(window.Handle, window.Title, TabKind.Attached);
        Tabs.Add(tab);
        Activate(tab);
    }

    public void Activate(WindowTab tab)
    {
        foreach (var t in Tabs)
            t.IsActive = ReferenceEquals(t, tab);
        ActiveTab = tab;
        _stage.Stage(tab.Handle, _stageRect());
    }

    public void CloseTab(WindowTab tab)
    {
        Tabs.Remove(tab);

        if (tab.IsTerminal)
        {
            _wm.Close(tab.Handle);
        }
        else
        {
            // Detach only: release ownership and hand the window back, floating.
            _wm.SetOwner(tab.Handle, 0);
            _wm.Restore(tab.Handle);
        }

        if (ReferenceEquals(ActiveTab, tab))
        {
            _stage.Clear();
            ActiveTab = Tabs.LastOrDefault();
            if (ActiveTab is { } next) Activate(next);
        }
    }

    public void AddTodo(string text) => Project.Todos.Add(new TodoItem { Text = text });
    public void RemoveTodo(TodoItem item) => Project.Todos.Remove(item);

    public void ReapplyStage(PixelRect rect) => _stage.Reapply(rect);
    public void ReactivateStage(PixelRect rect) => _stage.Reactivate(rect);
    public void HideStage() => _stage.HideCurrent();

    /// <summary>Un-hide every staged window so none are orphaned when the app closes.</summary>
    public void ReleaseAll()
    {
        foreach (var tab in Tabs)
        {
            _wm.SetOwner(tab.Handle, 0);
            _wm.Show(tab.Handle);
        }
    }
}
