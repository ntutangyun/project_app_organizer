using ProjectAppOrganizer.Core.Models;

namespace ProjectAppOrganizer.Core.Terminals;

/// <summary>A resolved, OS-ready launch specification for a terminal profile.</summary>
public readonly record struct ShellSpec(string FileName, string Arguments, string WorkingDirectory);

public static class ShellCommand
{
    public static ShellSpec Build(TerminalProfile p, string gitBashPath)
    {
        var cmd = p.StartupCommand;
        return p.Shell switch
        {
            ShellType.Cmd => new ShellSpec(
                "cmd.exe",
                string.IsNullOrWhiteSpace(cmd) ? "" : $"/K \"{cmd}\"",
                p.WorkingDirectory),

            ShellType.PowerShell => new ShellSpec(
                "powershell.exe",
                string.IsNullOrWhiteSpace(cmd) ? "-NoExit" : $"-NoExit -Command \"{cmd}\"",
                p.WorkingDirectory),

            // git-bash.exe exits immediately (and never opens a window) if handed
            // --cd="" — so only pass --cd when a working directory is actually set;
            // otherwise let git bash open at its default home directory.
            ShellType.GitBash => new ShellSpec(
                gitBashPath,
                string.IsNullOrWhiteSpace(p.WorkingDirectory) ? "" : $"--cd=\"{p.WorkingDirectory}\"",
                p.WorkingDirectory),

            _ => throw new ArgumentOutOfRangeException(nameof(p.Shell))
        };
    }
}
