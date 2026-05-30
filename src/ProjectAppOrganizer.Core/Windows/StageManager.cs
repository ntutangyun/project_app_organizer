namespace ProjectAppOrganizer.Core.Windows;

/// <summary>
/// Drives the Docker/stage model: positions the active window into the stage
/// rectangle, minimizes the previously staged window, and re-aligns the active
/// window when the stage moves/resizes.
/// </summary>
public class StageManager
{
    private readonly IWindowManager _wm;

    public StageManager(IWindowManager wm) => _wm = wm;

    public nint? Current { get; private set; }

    public void Stage(nint handle, PixelRect stage)
    {
        if (Current is { } prev && prev != handle)
            _wm.Minimize(prev);

        _wm.Restore(handle);
        _wm.MoveResize(handle, stage);
        _wm.BringToFront(handle);
        Current = handle;
    }

    public void Reapply(PixelRect stage)
    {
        if (Current is { } h)
            _wm.MoveResize(h, stage);
    }

    public void MinimizeCurrent()
    {
        if (Current is { } h)
            _wm.Minimize(h);
    }

    public void Clear() => Current = null;
}
