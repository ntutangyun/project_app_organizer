using ProjectAppOrganizer.Core.Windows;

namespace ProjectAppOrganizer.Core.Tests;

public class WindowFilterTest
{
    private static WindowInfo Win(nint handle, string title, bool visible = true, bool tool = false)
        => new(handle, title, "proc.exe", visible, tool);

    [Fact]
    public void ShouldList_TrueForNormalVisibleTitledWindow()
        => Assert.True(WindowFilter.ShouldList(Win(10, "Editor"), selfHandle: 99));

    [Fact]
    public void ShouldList_FalseForHiddenTitlelessOrToolWindows()
    {
        Assert.False(WindowFilter.ShouldList(Win(10, "X", visible: false), 99));
        Assert.False(WindowFilter.ShouldList(Win(10, "  "), 99));
        Assert.False(WindowFilter.ShouldList(Win(10, "X", tool: true), 99));
    }

    [Fact]
    public void ShouldList_FalseForOwnWindow()
        => Assert.False(WindowFilter.ShouldList(Win(99, "Organizer"), selfHandle: 99));
}
