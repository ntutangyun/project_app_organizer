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

            ShellType.GitBash => new ShellSpec(
                gitBashPath,
                $"--cd=\"{p.WorkingDirectory}\"",
                p.WorkingDirectory),

            _ => throw new ArgumentOutOfRangeException(nameof(p.Shell))
        };
    }
}
