using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using ProjectAppOrganizer.App.Files;
using ProjectAppOrganizer.App.Windows;
using ProjectAppOrganizer.Core.Files;
using ProjectAppOrganizer.Core.Models;
using ProjectAppOrganizer.Core.Persistence;
using ProjectAppOrganizer.Core.Terminals;
using ProjectAppOrganizer.Core.ViewModels;
using ProjectAppOrganizer.Core.Windows;

namespace ProjectAppOrganizer.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly Win32WindowManager _wm;
    private ProjectViewModel? _shown;   // project whose window is currently on the stage
    private readonly FileTreeWatcher _watcher;

    public MainWindow(Win32WindowManager wm, ITerminalLauncher launcher, IProjectStore store)
    {
        InitializeComponent();
        _wm = wm;

        _watcher = new FileTreeWatcher(Dispatcher);
        _vm = new MainViewModel(
            store,
            project =>
            {
                var pvm = new ProjectViewModel(project, wm, launcher,
                    new StageManager(wm, () => _wm.SelfHandle), GetStageRect);
                pvm.FilesStateChanged += () => _vm?.Save();
                return pvm;
            });
        _vm.Load();
        DataContext = _vm;
        _shown = _vm.SelectedProject;
        _vm.PropertyChanged += OnVmPropertyChanged;
        WatchSelectedFolder();

        Loaded += OnLoaded;
        StateChanged += OnStateChanged;
        Closing += OnClosing;
        PreviewMouseWheel += Window_PreviewMouseWheel;

        Themes.ThemeManager.Changed += UpdateThemeToggle;
        UpdateThemeToggle();
    }

    private void ThemeToggle_Click(object sender, RoutedEventArgs e) => Themes.ThemeManager.Toggle();

    /// <summary>The button shows the theme it switches to, like a light switch.</summary>
    private void UpdateThemeToggle()
    {
        var dark = Themes.ThemeManager.Current == Themes.AppTheme.Dark;
        ThemeToggle.Content = dark ? "☀" : "☾";
        ThemeToggle.ToolTip = dark ? "Switch to the warm paper theme" : "Switch to the dark theme";
    }

    private const int WM_EXITSIZEMOVE = 0x0232;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        _wm.SelfHandle = handle;
        Themes.ThemeManager.ApplyTitleBar(this);
        // Re-align the staged window only when the user finishes moving/resizing the
        // organizer. Repositioning a foreign window during the live drag's modal loop
        // breaks the drag.
        System.Windows.Interop.HwndSource.FromHwnd(handle)?.AddHook(WndProc);
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WM_EXITSIZEMOVE)
            ReapplyStage();
        return 0;
    }

    /// <summary>Stage rect in physical screen pixels (DPI-aware).</summary>
    private PixelRect GetStageRect()
    {
        // Two-corner PointToScreen already accounts for DPI and any layout transform,
        // so this stays correct regardless of zoom.
        var topLeft = Stage.PointToScreen(new Point(0, 0));
        var bottomRight = Stage.PointToScreen(new Point(Stage.ActualWidth, Stage.ActualHeight));
        return new PixelRect(
            (int)topLeft.X, (int)topLeft.Y,
            (int)(bottomRight.X - topLeft.X), (int)(bottomRight.Y - topLeft.Y));
    }

    private void ReapplyStage() => _vm.SelectedProject?.ReapplyStage(GetStageRect());

    /// <summary>Detach every docked window across all projects (used on accidental termination).</summary>
    public void DetachAllWindows() => _vm.ReleaseAll();

    // On project switch: hide the outgoing project's staged window and surface the
    // incoming project's last active window (or leave the stage empty if it has none).
    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.SelectedProject)) return;

        _shown?.HideStage();
        _shown = _vm.SelectedProject;
        var next = _shown;
        WatchSelectedFolder();
        // Defer so the (possibly just-shown) stage element has laid out before we measure it.
        Dispatcher.BeginInvoke(new Action(() => next?.ReactivateStage(GetStageRect())),
            System.Windows.Threading.DispatcherPriority.Background);
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
            _vm.SelectedProject?.HideStage();
        else
            _vm.SelectedProject?.ReactivateStage(GetStageRect());
    }

    // Ctrl + mouse wheel zooms the organizer's text (font size), leaving the
    // staged external windows untouched.
    private void Window_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        if ((System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == 0)
            return;

        FontSize = System.Math.Clamp(FontSize + (e.Delta > 0 ? 1 : -1), 9.0, 28.0);
        e.Handled = true;

        // Re-fit the staged window after the (slightly) changed layout settles.
        Dispatcher.BeginInvoke(
            new Action(() => _vm.SelectedProject?.ReapplyStage(GetStageRect())),
            System.Windows.Threading.DispatcherPriority.Background);
    }

    private void AddProject_Click(object sender, RoutedEventArgs e)
    {
        _vm.AddProject("New Project");
        _vm.Save();
    }

    private void AddTodo_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedProject is null) return;
        var item = _vm.SelectedProject.AddTodo();
        _vm.Save();
        FocusTodo(item);
    }

    private void DeleteTodo_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is TodoItem item)
        {
            _vm.SelectedProject?.RemoveTodo(item);
            _vm.Save();
        }
    }

    private void DeleteAllCompleted_Click(object sender, RoutedEventArgs e)
    {
        _vm.SelectedProject?.DeleteAllCompleted();
        _vm.Save();
    }

    private void DeleteProject_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is ProjectViewModel vm)
            DeleteProject(vm);
    }

    private void DeleteProject(ProjectViewModel vm)
    {
        var label = string.IsNullOrWhiteSpace(vm.Project.Name) ? "this project" : $"“{vm.Project.Name}”";
        var choice = Views.ChoiceDialog.Ask(this, "Delete project",
            $"Delete {label}? Its title, description and tasks will be removed. Any windows docked here are detached (left open and independent).",
            "Delete", "Cancel");
        if (choice != 0) return;
        vm.ReleaseAll();   // detach docked windows so they remain open and independent
        vm.ClearStage();   // so the switch handler won't re-hide the just-detached window
        _vm.RemoveProject(vm);
        _vm.Save();
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedProject is null) return;
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Select working folder" };
        if (!string.IsNullOrWhiteSpace(FolderBox.Text) && System.IO.Directory.Exists(FolderBox.Text))
            dlg.InitialDirectory = FolderBox.Text;
        if (dlg.ShowDialog() == true)
        {
            FolderBox.Text = dlg.FolderName;   // writes back to Project.FolderPath via binding
            _vm.Save();
            ReloadSelectedFolder();
        }
    }

    private void FolderBox_LostFocus(object sender, RoutedEventArgs e) => ReloadSelectedFolder();

    private void FolderBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter) ReloadSelectedFolder();
    }

    /// <summary>Folder path edited by hand or via Browse: rebuild the tree and re-point the watcher.</summary>
    private void ReloadSelectedFolder()
    {
        var project = _vm.SelectedProject;
        if (project is null) return;
        if (string.Equals(project.Files.RootPath, project.Project.FolderPath, StringComparison.OrdinalIgnoreCase))
            return;
        project.ReloadFiles();
        WatchSelectedFolder();
    }

    /// <summary>Point the single file watcher at the selected project's working folder.</summary>
    private void WatchSelectedFolder()
    {
        var project = _vm.SelectedProject;
        if (project is null) { _watcher.Stop(); return; }
        _watcher.Watch(project.Project.FolderPath, project.Files.Refresh, project.Files.ClearNewFlags);
    }

    private void NotesToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (IsLoaded) _vm.Save();
    }

    // ---------------- file tree ----------------

    private FileNode? SelectedFile => FileTree.SelectedItem as FileNode;

    private void FileTree_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // Only react to a double-click on a row (not on the empty area), and only for files;
        // folders toggle expansion natively.
        if (FindAncestor<TreeViewItem>(e.OriginalSource as DependencyObject) is null) return;
        if (SelectedFile is { IsDirectory: false } node)
        {
            OpenPath(node.FullPath);
            e.Handled = true;
        }
    }

    private void FileTree_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Enter || SelectedFile is not { } node) return;
        if (node.IsDirectory) node.IsExpanded = !node.IsExpanded;
        else OpenPath(node.FullPath);
        e.Handled = true;
    }

    // Right-click selects the row under the cursor so the context menu acts on it.
    private void FileTree_PreviewMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (FindAncestor<TreeViewItem>(e.OriginalSource as DependencyObject) is { } item)
        {
            item.IsSelected = true;
            item.Focus();
        }
    }

    private void FileOpen_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedFile is { } node) OpenPath(node.FullPath);
    }

    private void FileReveal_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedFile is not { } node) return;
        StartShell("explorer.exe", $"/select,\"{node.FullPath}\"", "Open containing folder");
    }

    private void FileCopyPath_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedFile is not { } node) return;
        try { Clipboard.SetText(node.FullPath); } catch (Exception) { /* clipboard busy */ }
    }

    /// <summary>Open a file or folder with whatever Windows has registered for it.</summary>
    private void OpenPath(string path)
    {
        if (!System.IO.File.Exists(path) && !System.IO.Directory.Exists(path))
        {
            MessageBox.Show(this, "That file no longer exists.", "Open file",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            // No default app registered (or the user cancelled the picker): fall back to "Open with".
            if (ex is System.ComponentModel.Win32Exception)
            {
                StartShell("rundll32.exe", $"shell32.dll,OpenAs_RunDLL {path}", "Open file");
                return;
            }
            MessageBox.Show(this, $"Could not open file:\n{ex.Message}", "Open file",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void StartShell(string exe, string args, string title)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe, args) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not open:\n{ex.Message}", title,
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static T? FindAncestor<T>(DependencyObject? node) where T : DependencyObject
    {
        while (node is not null)
        {
            if (node is T hit) return hit;
            node = node is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
                ? System.Windows.Media.VisualTreeHelper.GetParent(node)
                : LogicalTreeHelper.GetParent(node);
        }
        return null;
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedProject is null) return;
        var path = _vm.SelectedProject.Project.FolderPath;
        if (string.IsNullOrWhiteSpace(path) || !System.IO.Directory.Exists(path))
        {
            MessageBox.Show(this, "The working folder is not set or does not exist.",
                "Open folder", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("explorer.exe", $"\"{path}\"")
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not open folder:\n{ex.Message}", "Open folder",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void OpenTerminal_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedProject is null) return;
        if (sender is Button b && b.ContextMenu is { } menu)
        {
            menu.PlacementTarget = b;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        }
    }

    private async void LaunchShell_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.Tag is string tag &&
            Enum.TryParse<ShellType>(tag, out var shell))
            await LaunchTerminal(shell);
    }

    private Task LaunchTerminal(ShellType shell)
    {
        var project = _vm.SelectedProject;
        if (project is null) return Task.CompletedTask;
        var name = shell switch
        {
            ShellType.GitBash => "bash",
            ShellType.Cmd => "cmd",
            _ => "pwsh"
        };
        return project.OpenTerminalAsync(new TerminalProfile
        {
            Name = name,
            Shell = shell,
            WorkingDirectory = project.Project.FolderPath
        });
    }

    private void AttachWindow_Click(object sender, RoutedEventArgs e)
    {
        var project = _vm.SelectedProject;
        if (project is null) return;
        var picker = new Views.PickerWindow(_wm.EnumerateWindows()) { Owner = this };
        if (picker.ShowDialog() == true && picker.Selected is { } w)
            project.AttachWindow(w);
    }

    private void ActivateTab_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is WindowTab tab)
            _vm.SelectedProject?.Activate(tab);
    }

    private void CloseTab_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is not WindowTab tab) return;
        var project = _vm.SelectedProject;
        if (project is null) return;

        var kind = tab.IsTerminal ? "terminal" : "window";
        var choice = Views.ChoiceDialog.Ask(this, "Remove tab",
            $"Detach the {kind} to keep it open and independent, or close it entirely?",
            "Detach", "Close", "Cancel");
        if (choice == 0) project.DetachTab(tab);
        else if (choice == 1) project.CloseTab(tab);
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        var hasWindows = false;
        foreach (var p in _vm.Projects)
            if (p.Tabs.Count > 0) { hasWindows = true; break; }

        if (hasWindows)
        {
            var choice = Views.ChoiceDialog.Ask(this, "Close organizer",
                "What should happen to the docked terminals and windows?",
                "Detach (keep open)", "Close all", "Cancel");
            if (choice != 0 && choice != 1) { e.Cancel = true; return; }
            if (choice == 0) _vm.ReleaseAll(); else _vm.CloseAll();
        }
        _watcher.Stop();
        _vm.Save();
    }

    private void Todo_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        var tb = (TextBox)sender;
        var project = _vm.SelectedProject;
        if (project is null || tb.DataContext is not TodoItem current) return;

        var list = project.ActiveTodos.Contains(current) ? project.ActiveTodos : project.CompletedTodos;
        var idx = list.IndexOf(current);

        // With Alt held, the real key arrives via SystemKey (e.Key is Key.System).
        var key = e.Key == System.Windows.Input.Key.System ? e.SystemKey : e.Key;
        var alt = (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Alt) != 0;

        // Backspace on an empty task deletes it and moves to the previous task.
        if (key == System.Windows.Input.Key.Back && tb.Text.Length == 0)
        {
            e.Handled = true;
            var prev = idx > 0 ? list[idx - 1] : null;
            project.RemoveTodo(current);
            _vm.Save();
            if (prev is not null) FocusTodo(prev);
            return;
        }

        if (key != System.Windows.Input.Key.Enter) return;

        if (alt)
        {
            // Alt+Enter inserts a line break.
            var caret = tb.CaretIndex;
            tb.Text = tb.Text.Insert(caret, "\r\n");
            tb.CaretIndex = caret + 2;
            e.Handled = true;
            return;
        }

        // Plain Enter: move to the next task (creating one at the end).
        e.Handled = true;
        if (idx >= 0 && idx + 1 < list.Count)
        {
            FocusTodo(list[idx + 1]);
        }
        else
        {
            var item = project.AddTodo();
            _vm.Save();
            FocusTodo(item);
        }
    }

    private void FocusTodo(TodoItem item)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            var tb = FindTodoTextBox(TodoList, item) ?? FindTodoTextBox(CompletedList, item);
            if (tb is null) return;
            tb.Focus();
            tb.CaretIndex = tb.Text.Length;
        }), System.Windows.Threading.DispatcherPriority.Background);
    }

    private static TextBox? FindTodoTextBox(ItemsControl list, object item)
    {
        list.UpdateLayout();
        if (list.ItemContainerGenerator.ContainerFromItem(item) is not DependencyObject container)
            return null;
        return FindDescendant<TextBox>(container);
    }

    private static T? FindDescendant<T>(DependencyObject root) where T : DependencyObject
    {
        var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is T hit) return hit;
            var deeper = FindDescendant<T>(child);
            if (deeper is not null) return deeper;
        }
        return null;
    }
}
