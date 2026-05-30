namespace ProjectAppOrganizer.Core.Windows;

/// <summary>
/// Drives the Docker/stage model: shows the active window in the stage rectangle
/// and hides the previously staged one (no taskbar minimize animation), so only
/// the active tab's window is visible. Also re-aligns the active window when the
/// stage moves/resizes.
/// </summary>
public class StageManager
{
    private readonly IWindowManager _wm;
    private readonly Func<nint>? _owner;

    public StageManager(IWindowManager wm, Func<nint>? owner = null)
    {
        _wm = wm;
        _owner = owner;
    }

    public nint? Current { get; private set; }

    /// <summary>Make <paramref name="handle"/> the visible staged window.</summary>
    public void Stage(nint handle, PixelRect stage)
    {
        if (Current is { } prev && prev != handle)
            _wm.Hide(prev);

        _wm.Show(handle);
        _wm.MoveResize(handle, stage);
        // Own the window to the organizer so the OS keeps it above the organizer
        // (the user can't lose it behind the window) and it follows minimize.
        if (_owner is not null)
        {
            var owner = _owner();
            if (owner != 0) _wm.SetOwner(handle, owner);
        }
        _wm.BringToFront(handle);
        Current = handle;
    }

    /// <summary>Reposition the active window (e.g. after the organizer is moved/resized).</summary>
    public void Reapply(PixelRect stage)
    {
        if (Current is { } h)
            _wm.MoveResize(h, stage);
    }

    /// <summary>Re-show and focus the active window (e.g. after the organizer is restored).</summary>
    public void Reactivate(PixelRect stage)
    {
        if (Current is { } h)
        {
            _wm.Show(h);
            _wm.MoveResize(h, stage);
            _wm.BringToFront(h);
        }
    }

    /// <summary>Hide the active window (e.g. when the organizer is minimized).</summary>
    public void HideCurrent()
    {
        if (Current is { } h)
            _wm.Hide(h);
    }

    public void Clear() => Current = null;
}
