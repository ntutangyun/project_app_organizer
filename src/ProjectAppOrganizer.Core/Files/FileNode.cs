using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ProjectAppOrganizer.Core.Files;

/// <summary>One entry (file or folder) in the project file tree.</summary>
public partial class FileNode : ObservableObject
{
    private readonly FileTreeViewModel _tree;

    [ObservableProperty] private bool _isExpanded;
    [ObservableProperty] private bool _isNew;

    internal FileNode(FileTreeViewModel tree, string fullPath, bool isDirectory)
    {
        _tree = tree;
        FullPath = fullPath;
        Name = Path.GetFileName(fullPath);
        IsDirectory = isDirectory;
    }

    public string FullPath { get; }
    public string Name { get; }
    public bool IsDirectory { get; }
    public string Extension => IsDirectory ? string.Empty : Path.GetExtension(Name).ToLowerInvariant();

    /// <summary>Children, populated lazily the first time the folder is expanded.</summary>
    public ObservableCollection<FileNode> Children { get; } = new();

    internal bool ChildrenLoaded { get; set; }

    partial void OnIsExpandedChanged(bool value) => _tree.OnNodeExpandedChanged(this, value);
}
