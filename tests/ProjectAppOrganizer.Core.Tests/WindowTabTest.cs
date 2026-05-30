using ProjectAppOrganizer.Core.ViewModels;

namespace ProjectAppOrganizer.Core.Tests;

public class WindowTabTest
{
    [Fact]
    public void Terminal_TabIsOwnedTrue()
    {
        var tab = new WindowTab(handle: 12, title: "git bash", TabKind.Terminal);
        Assert.Equal(TabKind.Terminal, tab.Kind);
        Assert.True(tab.IsTerminal);
        Assert.Equal((nint)12, tab.Handle);
    }

    [Fact]
    public void Attached_TabIsTerminalFalse()
    {
        var tab = new WindowTab(handle: 13, title: "VS Code", TabKind.Attached);
        Assert.False(tab.IsTerminal);
    }
}
