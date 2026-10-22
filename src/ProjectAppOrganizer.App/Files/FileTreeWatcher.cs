using System.IO;
using System.Windows.Threading;

namespace ProjectAppOrganizer.App.Files;

/// <summary>
/// Watches a folder recursively and, after a short debounce, asks the tree to refresh
/// on the UI thread. A second timer clears the "new" highlight a few seconds later.
/// </summary>
public sealed class FileTreeWatcher : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _debounce;
    private readonly DispatcherTimer _clearNew;
    private FileSystemWatcher? _fsw;
    private Action? _refresh;
    private Action? _clearNewFlags;

    public FileTreeWatcher(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        _clearNew = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = TimeSpan.FromSeconds(5)
        };
        _clearNew.Tick += (_, _) =>
        {
            _clearNew.Stop();
            _clearNewFlags?.Invoke();
        };
        _debounce = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(350)
        };
        _debounce.Tick += (_, _) =>
        {
            _debounce.Stop();
            _refresh?.Invoke();
            _clearNew.Stop();
            _clearNew.Start();
        };
    }

    /// <summary>Switch to a new folder (or null to stop). Triggers one immediate refresh.</summary>
    public void Watch(string? path, Action refresh, Action clearNewFlags)
    {
        Stop();
        _refresh = refresh;
        _clearNewFlags = clearNewFlags;
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;

        try
        {
            _fsw = new FileSystemWatcher(path)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName,
                InternalBufferSize = 64 * 1024
            };
            _fsw.Created += OnChange;
            _fsw.Deleted += OnChange;
            _fsw.Renamed += OnChange;
            _fsw.Error += (_, _) => Bump();   // buffer overflow: just resync
            _fsw.EnableRaisingEvents = true;
        }
        catch (Exception)
        {
            _fsw?.Dispose();
            _fsw = null;   // unwatchable (network path, permissions): tree still shows a snapshot
        }
        Bump();
    }

    public void Stop()
    {
        if (_fsw is not null)
        {
            _fsw.EnableRaisingEvents = false;
            _fsw.Dispose();
            _fsw = null;
        }
        _debounce.Stop();
        _clearNew.Stop();
    }

    private void OnChange(object sender, FileSystemEventArgs e) => Bump();

    private void Bump()
    {
        // FileSystemWatcher raises on a pool thread; timers belong to the UI dispatcher.
        _dispatcher.BeginInvoke(new Action(() =>
        {
            _debounce.Stop();
            _debounce.Start();
        }));
    }

    public void Dispose() => Stop();
}
