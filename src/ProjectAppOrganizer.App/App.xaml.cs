using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using ProjectAppOrganizer.App.Terminals;
using ProjectAppOrganizer.App.Windows;
using ProjectAppOrganizer.Core.Persistence;
using ProjectAppOrganizer.Core.Terminals;
using ProjectAppOrganizer.Core.ViewModels;
using ProjectAppOrganizer.Core.Windows;

namespace ProjectAppOrganizer.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var sc = new ServiceCollection();
        sc.AddSingleton<Win32WindowManager>();
        sc.AddSingleton<IWindowManager>(sp => sp.GetRequiredService<Win32WindowManager>());
        sc.AddSingleton<ITerminalLauncher>(_ => new ProcessTerminalLauncher());
        sc.AddSingleton<IProjectStore>(_ => new JsonProjectStore(JsonProjectStore.DefaultPath));
        sc.AddSingleton<MainWindow>();
        Services = sc.BuildServiceProvider();

        // Re-attach any windows a previous crashed/killed session left owned.
        Services.GetRequiredService<Win32WindowManager>().RecoverOrphans();

        var window = Services.GetRequiredService<MainWindow>();

        // On any accidental termination, detach docked windows so they're never orphaned.
        DispatcherUnhandledException += (_, _) => window.DetachAllWindows();
        AppDomain.CurrentDomain.UnhandledException += (_, _) => window.DetachAllWindows();
        AppDomain.CurrentDomain.ProcessExit += (_, _) => window.DetachAllWindows();

        window.Show();
    }
}
