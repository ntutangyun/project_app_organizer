using System.Diagnostics;
using System.IO;
using ProjectAppOrganizer.App.Windows;
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

        // cmd/powershell launched directly hand off to the shared default terminal
        // (Windows Terminal), whose window can't be staged per-tab. Hosting them under
        // an explicit conhost forces a dedicated, ownable classic console window.
        var fileName = spec.FileName;
        var arguments = spec.Arguments;
        if (profile.Shell is ShellType.Cmd or ShellType.PowerShell)
        {
            fileName = "conhost.exe";
            arguments = $"{spec.FileName} {spec.Arguments}".Trim();
        }

        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory =
                Directory.Exists(spec.WorkingDirectory) ? spec.WorkingDirectory : Environment.CurrentDirectory,
            UseShellExecute = true
        };

        var process = Process.Start(psi);
        if (process is null) return 0;

        // The visible window is owned by the shell process or a descendant
        // (conhost -> shell, git-bash -> mintty), so poll the process tree (up to ~5s)
        // for a top-level window rather than the unreliable MainWindowHandle.
        for (var i = 0; i < 50; i++)
        {
            var handle = ProcessWindowResolver.FindMainWindow(process.Id);
            if (handle != 0) return handle;
            Thread.Sleep(100);
        }
        return 0;
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
