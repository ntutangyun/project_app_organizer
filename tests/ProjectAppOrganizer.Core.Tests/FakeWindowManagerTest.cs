using ProjectAppOrganizer.Core.Tests.Fakes;
using ProjectAppOrganizer.Core.Windows;

namespace ProjectAppOrganizer.Core.Tests;

public class FakeWindowManagerTest
{
    [Fact]
    public void RecordsCallsInOrder()
    {
        var wm = new FakeWindowManager();
        wm.Restore(5);
        wm.MoveResize(5, new PixelRect(1, 2, 3, 4));
        wm.BringToFront(5);

        Assert.Equal(new[] { "Restore:5", "MoveResize:5:1,2,3,4", "BringToFront:5" }, wm.Calls);
    }
}
