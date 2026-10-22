using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ProjectAppOrganizer.Core.Files;

/// <summary>
/// Lazily-loaded view of a folder on disk. Folders load their children when first
/// expanded; <see cref="Refresh"/> re-syncs every loaded level against disk in place,
/// keeping existing node instances (and therefore expansion / selection state).
/// </summary>
public partial class FileTreeViewModel : ObservableObject
{
    /// <summary>Folders hidden unless <see cref="ShowHidden"/> is on.</summary>
    public static readonly HashSet<string> NoiseFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".svn", ".vs", ".idea", "node_modules", "bin", "obj",
        "__pycache__", ".venv", "venv", ".mypy_cache", ".pytest_cache", ".superpowers"
    };

    private static readonly StringComparer PathCmp = StringComparer.OrdinalIgnoreCase;

    private HashSet<string> _expanded = new(PathCmp);
    private bool _suppressEvents;

    [ObservableProperty] private bool _showHidden;

    public string RootPath { get; private set; } = string.Empty;
    public ObservableCollection<FileNode> Roots { get; } = new();

    /// <summary>Root-relative paths of every expanded folder (persisted per project).</summary>
    public IReadOnlyCollection<string> ExpandedFolders => _expanded;

    /// <summary>Raised when the user expands or collapses a folder.</summary>
    public event Action? ExpandedFoldersChanged;

    public void SetRoot(string? path, IEnumerable<string>? expanded = null)
    {
        RootPath = path ?? string.Empty;
        _expanded = new HashSet<string>(expanded ?? Array.Empty<string>(), PathCmp);
        _suppressEvents = true;
        try
        {
            Roots.Clear();
            if (Directory.Exists(RootPath))
                Sync(Roots, RootPath, markNew: false);
        }
        finally { _suppressEvents = false; }
    }

    /// <summary>Re-sync every loaded level with disk; nodes that appear are flagged <see cref="FileNode.IsNew"/>.</summary>
    public void Refresh() => Refresh(markNew: true);

    public void ClearNewFlags() => ClearNewFlags(Roots);

    partial void OnShowHiddenChanged(bool value) => Refresh(markNew: false);

    private void Refresh(bool markNew)
    {
        if (!Directory.Exists(RootPath)) { Roots.Clear(); return; }
        Sync(Roots, RootPath, markNew);
    }

    private static void ClearNewFlags(IEnumerable<FileNode> nodes)
    {
        foreach (var n in nodes)
        {
            n.IsNew = false;
            ClearNewFlags(n.Children);
        }
    }

    internal void OnNodeExpandedChanged(FileNode node, bool expanded)
    {
        if (!node.IsDirectory) return;
        if (expanded && !node.ChildrenLoaded)
        {
            node.ChildrenLoaded = true;
            Sync(node.Children, node.FullPath, markNew: false);
        }

        var rel = Path.GetRelativePath(RootPath, node.FullPath);
        var changed = expanded ? _expanded.Add(rel) : _expanded.Remove(rel);
        if (changed && !_suppressEvents) ExpandedFoldersChanged?.Invoke();
    }

    private bool IsVisible(string dir) => ShowHidden || !NoiseFolders.Contains(Path.GetFileName(dir));

    private void Sync(ObservableCollection<FileNode> target, string dir, bool markNew)
    {
        List<(string Path, bool IsDir)> desired;
        try
        {
            desired = Directory.GetDirectories(dir).Where(IsVisible)
                .OrderBy(Path.GetFileName, PathCmp).Select(d => (d, true))
                .Concat(Directory.GetFiles(dir).OrderBy(Path.GetFileName, PathCmp).Select(f => (f, false)))
                .ToList();
        }
        catch (Exception)
        {
            return; // folder vanished or access denied: leave what we have
        }

        var keep = new HashSet<string>(desired.Select(d => d.Path), PathCmp);
        for (var i = target.Count - 1; i >= 0; i--)
            if (!keep.Contains(target[i].FullPath)) target.RemoveAt(i);

        // Both lists share the same ordering, so surviving nodes line up by index.
        for (var i = 0; i < desired.Count; i++)
        {
            var (path, isDir) = desired[i];
            FileNode node;
            if (i < target.Count && PathCmp.Equals(target[i].FullPath, path))
            {
                node = target[i];
            }
            else
            {
                node = new FileNode(this, path, isDir) { IsNew = markNew };
                target.Insert(i, node);
                if (isDir && _expanded.Contains(Path.GetRelativePath(RootPath, path)))
                    node.IsExpanded = true;   // loads children via OnNodeExpandedChanged
            }

            if (node.IsDirectory && node.ChildrenLoaded)
                Sync(node.Children, node.FullPath, markNew);
        }
    }
}
