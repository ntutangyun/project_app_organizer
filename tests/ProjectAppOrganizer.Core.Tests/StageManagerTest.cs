using ProjectAppOrganizer.Core.Tests.Fakes;
using ProjectAppOrganizer.Core.Windows;

namespace ProjectAppOrganizer.Core.Tests;

public class StageManagerTest
{
    private static readonly PixelRect Stage = new(100, 50, 800, 600);

    [Fact]
    public void Stage_RestoresMovesAndFocusesWindow()
    {
        var wm = new FakeWindowManager();
        var sm = new StageManager(wm);

        sm.Stage(7, Stage);

        Assert.Equal(
            new[] { "Restore:7", "MoveResize:7:100,50,800,600", "BringToFront:7" },
            wm.Calls);
        Assert.Equal((nint)7, sm.Current);
    }

    [Fact]
    public void Stage_MinimizesPreviouslyStagedWindow()
    {
        var wm = new FakeWindowManager();
        var sm = new StageManager(wm);

        sm.Stage(7, Stage);
        wm.Calls.Clear();
        sm.Stage(8, Stage);

        Assert.Equal("Minimize:7", wm.Calls[0]);
        Assert.Contains("MoveResize:8:100,50,800,600", wm.Calls);
        Assert.Equal((nint)8, sm.Current);
    }

    [Fact]
    public void Stage_SameWindowTwice_DoesNotMinimizeIt()
    {
        var wm = new FakeWindowManager();
        var sm = new StageManager(wm);

        sm.Stage(7, Stage);
        wm.Calls.Clear();
        sm.Stage(7, Stage);

        Assert.DoesNotContain("Minimize:7", wm.Calls);
    }

    [Fact]
    public void Reapply_RepositionsCurrentWindowOnly()
    {
        var wm = new FakeWindowManager();
        var sm = new StageManager(wm);
        sm.Stage(7, Stage);
        wm.Calls.Clear();

        sm.Reapply(new PixelRect(0, 0, 1000, 700));

        Assert.Equal(new[] { "MoveResize:7:0,0,1000,700" }, wm.Calls);
    }

    [Fact]
    public void Reapply_WithNothingStaged_DoesNothing()
    {
        var wm = new FakeWindowManager();
        var sm = new StageManager(wm);

        sm.Reapply(Stage);

        Assert.Empty(wm.Calls);
    }

    [Fact]
    public void MinimizeCurrent_MinimizesAndKeepsCurrentForRestore()
    {
        var wm = new FakeWindowManager();
        var sm = new StageManager(wm);
        sm.Stage(7, Stage);
        wm.Calls.Clear();

        sm.MinimizeCurrent();

        Assert.Equal(new[] { "Minimize:7" }, wm.Calls);
    }
}
