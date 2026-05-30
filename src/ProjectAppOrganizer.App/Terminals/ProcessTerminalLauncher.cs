using System.Diagnostics;
using System.IO;
using ProjectAppOrganizer.Core.Models;
using ProjectAppOrganizer.Core.Terminals;

namespace ProjectAppOrganizer.App.Terminals;

public class ProcessTerminalLauncher : ITerminalLauncher
{
    private readonly string _gitBashPath;

    public ProcessTerminalLauncher(string? gitBashPath = null)
        => _gitBashPath = gitBashPath ?? DetectGitBash();

    public nint Launch(TerminalProfile profile)
    {
        var spec = ShellCommand.Build(profile, _gitBashPath);
        var psi = new ProcessStartInfo
        {
            FileName = spec.FileName,
            Arguments = spec.Arguments,
            WorkingDirectory =
                Directory.Exists(spec.WorkingDirectory) ? spec.WorkingDirectory : Environment.CurrentDirectory,
            UseShellExecute = true
        };

        var process = Process.Start(psi);
        if (process is null) return 0;

        // Poll up to ~3s for the main window handle to appear.
        for (var i = 0; i < 30; i++)
        {
            process.Refresh();
            if (process.MainWindowHandle != 0) return process.MainWindowHandle;
            Thread.Sleep(100);
        }
        return process.MainWindowHandle;
    }

    private static string DetectGitBash()
    {
        string[] candidates =
        {
            @"C:\Program Files\Git\git-bash.exe",
            @"C:\Program Files (x86)\Git\git-bash.exe"
        };
        return candidates.FirstOrDefault(File.Exists) ?? "git-bash.exe";
    }
}
