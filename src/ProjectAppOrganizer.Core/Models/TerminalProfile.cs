namespace ProjectAppOrganizer.Core.Models;

public class TerminalProfile
{
    public string Name { get; set; } = string.Empty;
    public ShellType Shell { get; set; } = ShellType.PowerShell;
    public string WorkingDirectory { get; set; } = string.Empty;
    public string? StartupCommand { get; set; }
}
