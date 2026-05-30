using ProjectAppOrganizer.Core.Models;
using ProjectAppOrganizer.Core.Terminals;

namespace ProjectAppOrganizer.Core.Tests;

public class ShellCommandTest
{
    [Fact]
    public void Cmd_WithStartupCommand_UsesSlashK()
    {
        var p = new TerminalProfile { Shell = ShellType.Cmd, WorkingDirectory = @"C:\x", StartupCommand = "dir" };
        var spec = ShellCommand.Build(p, gitBashPath: @"C:\Git\git-bash.exe");

        Assert.Equal("cmd.exe", spec.FileName);
        Assert.Equal(@"C:\x", spec.WorkingDirectory);
        Assert.Equal("/K \"dir\"", spec.Arguments);
    }

    [Fact]
    public void Cmd_WithoutStartupCommand_HasNoArguments()
    {
        var p = new TerminalProfile { Shell = ShellType.Cmd, WorkingDirectory = @"C:\x" };
        var spec = ShellCommand.Build(p, @"C:\Git\git-bash.exe");
        Assert.Equal("", spec.Arguments);
    }

    [Fact]
    public void PowerShell_WithStartupCommand_UsesNoExitCommand()
    {
        var p = new TerminalProfile { Shell = ShellType.PowerShell, WorkingDirectory = @"C:\x", StartupCommand = "ls" };
        var spec = ShellCommand.Build(p, @"C:\Git\git-bash.exe");

        Assert.Equal("powershell.exe", spec.FileName);
        Assert.Equal("-NoExit -Command \"ls\"", spec.Arguments);
    }

    [Fact]
    public void GitBash_UsesGitBashPathAndCdArgument()
    {
        var p = new TerminalProfile { Shell = ShellType.GitBash, WorkingDirectory = @"C:\repo" };
        var spec = ShellCommand.Build(p, @"C:\Git\git-bash.exe");

        Assert.Equal(@"C:\Git\git-bash.exe", spec.FileName);
        Assert.Equal("--cd=\"C:\\repo\"", spec.Arguments);
    }
}
