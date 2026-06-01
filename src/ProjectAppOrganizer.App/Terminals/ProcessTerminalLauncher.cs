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
        // A blank OR non-existent working directory makes git-bash exit instantly
        // (--cd="" / --cd to a missing path opens no window). Normalize a missing
        // path to blank so ShellCommand omits --cd and the shell opens at home.
        if (!string.IsNullOrWhiteSpace(profile.WorkingDirectory) && !Directory.Exists(profile.WorkingDirectory))
            profile = new TerminalProfile
            {
                Name = profile.Name,
                Shell = profile.Shell,
                WorkingDirectory = string.Empty,
                StartupCommand = profile.StartupCommand
            };

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

        // Snapshot existing windows so we only match the one this launch creates.
        var preexisting = ProcessWindowResolver.SnapshotVisibleWindows();

        var process = Process.Start(psi);
        if (process is null) return 0;

        // The visible window is owned by the shell process or a descendant
        // (conhost -> shell, git-bash -> mintty). Poll the process tree for a newly
        // appeared top-level window; git bash gets a "mintty" name fallback and a
        // longer window because its launcher exits early and the window appears late.
        var hint = profile.Shell == ShellType.GitBash ? "mintty" : null;
        var attempts = profile.Shell == ShellType.GitBash ? 100 : 50;
        for (var i = 0; i < attempts; i++)
        {
            var handle = ProcessWindowResolver.FindNewWindow(process.Id, preexisting, hint);
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
