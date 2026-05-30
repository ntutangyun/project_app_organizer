using ProjectAppOrganizer.Core.Tests.Fakes;
using ProjectAppOrganizer.Core.Windows;

namespace ProjectAppOrganizer.Core.Tests;

public class StageManagerTest
{
    private static readonly PixelRect Stage = new(100, 50, 800, 600);

    [Fact]
    public void Stage_ShowsMovesAndFocusesWindow()
    {
        var wm = new FakeWindowManager();
        var sm = new StageManager(wm);

        sm.Stage(7, Stage);

        Assert.Equal(
            new[] { "Show:7", "MoveResize:7:100,50,800,600", "BringToFront:7" },
            wm.Calls);
        Assert.Equal((nint)7, sm.Current);
    }

    [Fact]
    public void Stage_HidesPreviouslyStagedWindow()
    {
        var wm = new FakeWindowManager();
        var sm = new StageManager(wm);

        sm.Stage(7, Stage);
        wm.Calls.Clear();
        sm.Stage(8, Stage);

        Assert.Equal("Hide:7", wm.Calls[0]);
        Assert.Contains("Show:8", wm.Calls);
        Assert.Contains("MoveResize:8:100,50,800,600", wm.Calls);
        Assert.Equal((nint)8, sm.Current);
    }

    [Fact]
    public void Stage_SameWindowTwice_DoesNotHideIt()
    {
        var wm = new FakeWindowManager();
        var sm = new StageManager(wm);

        sm.Stage(7, Stage);
        wm.Calls.Clear();
        sm.Stage(7, Stage);

        Assert.DoesNotContain("Hide:7", wm.Calls);
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
    public void Reactivate_ShowsMovesAndFocusesCurrent()
    {
        var wm = new FakeWindowManager();
        var sm = new StageManager(wm);
        sm.Stage(7, Stage);
        wm.Calls.Clear();

        sm.Reactivate(new PixelRect(0, 0, 1000, 700));

        Assert.Equal(
            new[] { "Show:7", "MoveResize:7:0,0,1000,700", "BringToFront:7" },
            wm.Calls);
    }

    [Fact]
    public void Stage_WithOwnerProvider_OwnsWindowToOrganizer()
    {
        var wm = new FakeWindowManager();
        var sm = new StageManager(wm, () => 999);

        sm.Stage(7, Stage);

        Assert.Contains("SetOwner:7:999", wm.Calls);
    }

    [Fact]
    public void HideCurrent_HidesCurrentWindow()
    {
        var wm = new FakeWindowManager();
        var sm = new StageManager(wm);
        sm.Stage(7, Stage);
        wm.Calls.Clear();

        sm.HideCurrent();

        Assert.Equal(new[] { "Hide:7" }, wm.Calls);
    }
}
