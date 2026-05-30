using ProjectAppOrganizer.Core.Windows;

namespace ProjectAppOrganizer.Core.Tests.Fakes;

public class FakeWindowManager : IWindowManager
{
    public List<WindowInfo> Windows { get; } = new();
    public List<string> Calls { get; } = new();
    public HashSet<nint> Existing { get; } = new();

    public IReadOnlyList<WindowInfo> EnumerateWindows() => Windows;

    public bool IsWindow(nint handle) => Existing.Contains(handle) || Windows.Any(w => w.Handle == handle);

    public void MoveResize(nint handle, PixelRect rect)
        => Calls.Add($"MoveResize:{handle}:{rect.X},{rect.Y},{rect.Width},{rect.Height}");

    public void Minimize(nint handle) => Calls.Add($"Minimize:{handle}");
    public void Restore(nint handle) => Calls.Add($"Restore:{handle}");
    public void BringToFront(nint handle) => Calls.Add($"BringToFront:{handle}");
}
