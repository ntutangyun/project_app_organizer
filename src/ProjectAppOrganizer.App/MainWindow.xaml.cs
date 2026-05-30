using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using ProjectAppOrganizer.App.Windows;
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

    public MainWindow(Win32WindowManager wm, ITerminalLauncher launcher, IProjectStore store)
    {
        InitializeComponent();
        _wm = wm;

        _vm = new MainViewModel(
            store,
            project => new ProjectViewModel(project, wm, launcher,
                new StageManager(wm, () => _wm.SelfHandle), GetStageRect));
        _vm.Load();
        DataContext = _vm;
        ProjectList.AddHandler(System.Windows.Controls.Primitives.ButtonBase.ClickEvent,
            new RoutedEventHandler(OnProjectDeleteClick));

        Loaded += OnLoaded;
        StateChanged += OnStateChanged;
        Closing += (_, _) => { _vm.ReleaseAll(); _vm.Save(); };
        PreviewMouseWheel += Window_PreviewMouseWheel;
    }

    private const int WM_EXITSIZEMOVE = 0x0232;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        _wm.SelfHandle = handle;
        Win32Interop.UseDarkTitleBar(handle);
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
        _vm.SelectedProject?.AddTodo("New task");
        _vm.Save();
    }

    private void DeleteTodo_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is TodoItem item)
        {
            _vm.SelectedProject?.RemoveTodo(item);
            _vm.Save();
        }
    }

    // Project delete buttons live inside the ListBox item template, so their clicks
    // bubble up here. RepeatButtons (scrollbar) are not Button, so they're ignored.
    private void OnProjectDeleteClick(object sender, RoutedEventArgs e)
    {
        if (e.Source is Button btn && btn.DataContext is ProjectViewModel vm)
        {
            DeleteProject(vm);
            e.Handled = true;
        }
    }

    private void DeleteProject(ProjectViewModel vm)
    {
        var label = string.IsNullOrWhiteSpace(vm.Project.Name) ? "this project" : $"\"{vm.Project.Name}\"";
        if (MessageBox.Show($"Delete {label}? Its tasks will be removed.", "Delete project",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;
        vm.ReleaseAll();              // un-hide / un-own any staged windows so they aren't orphaned
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

    private void LaunchShell_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.Tag is string tag &&
            Enum.TryParse<ShellType>(tag, out var shell))
            LaunchTerminal(shell);
    }

    private void LaunchTerminal(ShellType shell)
    {
        var project = _vm.SelectedProject;
        if (project is null) return;
        var name = shell switch
        {
            ShellType.GitBash => "bash",
            ShellType.Cmd => "cmd",
            _ => "pwsh"
        };
        project.OpenTerminal(new TerminalProfile
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
        if (((FrameworkElement)sender).Tag is WindowTab tab)
            _vm.SelectedProject?.CloseTab(tab);
    }
}
