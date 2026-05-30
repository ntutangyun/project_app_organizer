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
            project => new ProjectViewModel(project, wm, launcher, new StageManager(wm), GetStageRect));
        _vm.Load();
        DataContext = _vm;

        Loaded += (_, _) => _wm.SelfHandle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        LocationChanged += (_, _) => ReapplyStage();
        SizeChanged += (_, _) => ReapplyStage();
        StateChanged += OnStateChanged;
        Closing += (_, _) => _vm.Save();
    }

    /// <summary>Stage rect in physical screen pixels (DPI-aware).</summary>
    private PixelRect GetStageRect()
    {
        var topLeft = Stage.PointToScreen(new Point(0, 0));
        var source = PresentationSource.FromVisual(this);
        var m = source!.CompositionTarget!.TransformToDevice;
        var w = Stage.ActualWidth * m.M11;
        var h = Stage.ActualHeight * m.M22;
        return new PixelRect((int)topLeft.X, (int)topLeft.Y, (int)w, (int)h);
    }

    private void ReapplyStage() => _vm.SelectedProject?.ReapplyStage(GetStageRect());

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
            _vm.SelectedProject?.MinimizeStage();
        else
            ReapplyStage();
    }

    private void AddProject_Click(object sender, RoutedEventArgs e) => _vm.AddProject("New Project");
    private void AddTodo_Click(object sender, RoutedEventArgs e) => _vm.SelectedProject?.AddTodo("New todo");

    private void OpenTerminal_Click(object sender, RoutedEventArgs e)
    {
        var project = _vm.SelectedProject;
        if (project is null) return;
        project.OpenTerminal(new TerminalProfile
        {
            Name = "pwsh",
            Shell = ShellType.PowerShell,
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
