using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using ProjectAppOrganizer.Core.Files;
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

        foreach (var todo in Project.Todos) Track(todo);
        Project.Todos.CollectionChanged += OnTodosCollectionChanged;

        Files.ShowHidden = Project.ShowHiddenFiles;
        Files.SetRoot(Project.FolderPath, Project.ExpandedFolders);
        Files.ExpandedFoldersChanged += () =>
        {
            Project.ExpandedFolders = Files.ExpandedFolders.ToList();
            FilesStateChanged?.Invoke();
        };
        Files.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(FileTreeViewModel.ShowHidden)) return;
            Project.ShowHiddenFiles = Files.ShowHidden;
            FilesStateChanged?.Invoke();
        };
    }

    public Project Project { get; }
    public ObservableCollection<WindowTab> Tabs { get; } = new();

    // Todos split for display: active (pending) above, completed below.
    public ObservableCollection<TodoItem> ActiveTodos { get; } = new();
    public ObservableCollection<TodoItem> CompletedTodos { get; } = new();

    // ---------------- files ----------------

    /// <summary>Tree of the working folder, kept in sync by the view's watcher.</summary>
    public FileTreeViewModel Files { get; } = new();

    /// <summary>Raised when persisted file-tree state (expansion, show-hidden) changes.</summary>
    public event Action? FilesStateChanged;

    /// <summary>Re-read the tree after <see cref="Project.FolderPath"/> changed.</summary>
    public void ReloadFiles() => Files.SetRoot(Project.FolderPath, Project.ExpandedFolders);

    // ---------------- terminals / windows ----------------

    /// <summary>
    /// Launch a terminal without blocking the caller's thread. The launch + window
    /// resolution (which can poll for up to several seconds) runs on a background
    /// thread; the tab is created and staged back on the captured (UI) context.
    /// </summary>
    public async Task OpenTerminalAsync(TerminalProfile profile)
    {
        var handle = await Task.Run(() => _launcher.Launch(profile)).ConfigureAwait(true);
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

    /// <summary>Close the underlying window/terminal and remove its tab.</summary>
    public void CloseTab(WindowTab tab) => RemoveTab(tab, close: true);

    /// <summary>Detach the window (leave it open and independent) and remove its tab.</summary>
    public void DetachTab(WindowTab tab) => RemoveTab(tab, close: false);

    private void RemoveTab(WindowTab tab, bool close)
    {
        Tabs.Remove(tab);

        if (close)
        {
            _wm.Close(tab.Handle);
        }
        else
        {
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

    /// <summary>Detach every window (clear ownership, leave them floating).</summary>
    public void ReleaseAll()
    {
        foreach (var tab in Tabs)
        {
            _wm.SetOwner(tab.Handle, 0);
            _wm.Show(tab.Handle);
        }
    }

    /// <summary>Close every window/terminal.</summary>
    public void CloseAll()
    {
        foreach (var tab in Tabs)
            _wm.Close(tab.Handle);
    }

    // ---------------- todos ----------------

    public TodoItem AddTodo(string text = "")
    {
        var item = new TodoItem { Text = text };
        Project.Todos.Add(item);
        return item;
    }

    public void RemoveTodo(TodoItem item) => Project.Todos.Remove(item);

    public void DeleteAllCompleted()
    {
        foreach (var done in CompletedTodos.ToList())
            Project.Todos.Remove(done);
    }

    private void OnTodosCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            foreach (var t in ActiveTodos.Concat(CompletedTodos).ToList())
                t.PropertyChanged -= OnTodoChanged;
            ActiveTodos.Clear();
            CompletedTodos.Clear();
            foreach (var t in Project.Todos) Track(t);
            return;
        }

        if (e.OldItems is not null)
            foreach (TodoItem t in e.OldItems) Untrack(t);
        if (e.NewItems is not null)
            foreach (TodoItem t in e.NewItems) Track(t);
    }

    private void Track(TodoItem t)
    {
        t.PropertyChanged += OnTodoChanged;
        (t.Done ? CompletedTodos : ActiveTodos).Add(t);
    }

    private void Untrack(TodoItem t)
    {
        t.PropertyChanged -= OnTodoChanged;
        ActiveTodos.Remove(t);
        CompletedTodos.Remove(t);
    }

    private void OnTodoChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TodoItem.Done) || sender is not TodoItem t) return;
        if (t.Done)
        {
            if (ActiveTodos.Remove(t)) CompletedTodos.Add(t);
        }
        else
        {
            if (CompletedTodos.Remove(t)) ActiveTodos.Add(t);
        }
    }

    // ---------------- stage helpers (called by the view) ----------------

    public void ReapplyStage(PixelRect rect) => _stage.Reapply(rect);
    public void ReactivateStage(PixelRect rect) => _stage.Reactivate(rect);
    public void HideStage() => _stage.HideCurrent();
    public void ClearStage() => _stage.Clear();
}
