using ProjectAppOrganizer.Core.Files;

namespace ProjectAppOrganizer.Core.Tests;

public class FileTreeViewModelTest : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), $"pao-tree-{Guid.NewGuid():N}");

    public FileTreeViewModelTest() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    private void Touch(string relative)
    {
        var full = Path.Combine(_root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, "x");
    }

    private static FileNode Find(IEnumerable<FileNode> nodes, string name) => nodes.Single(n => n.Name == name);

    [Fact]
    public void Load_ListsFoldersFirstThenFiles_SortedCaseInsensitively()
    {
        Touch("zeta.txt");
        Touch("Alpha.md");
        Directory.CreateDirectory(Path.Combine(_root, "src"));
        Directory.CreateDirectory(Path.Combine(_root, "Docs"));

        var tree = new FileTreeViewModel();
        tree.SetRoot(_root);

        Assert.Equal(new[] { "Docs", "src", "Alpha.md", "zeta.txt" }, tree.Roots.Select(n => n.Name));
        Assert.True(tree.Roots[0].IsDirectory);
        Assert.False(tree.Roots[2].IsDirectory);
        Assert.Equal(Path.Combine(_root, "Alpha.md"), tree.Roots[2].FullPath);
    }

    [Fact]
    public void Load_HidesNoiseFolders_UntilShowHiddenIsOn()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".git"));
        Directory.CreateDirectory(Path.Combine(_root, "node_modules"));
        Directory.CreateDirectory(Path.Combine(_root, "bin"));
        Directory.CreateDirectory(Path.Combine(_root, "obj"));
        Directory.CreateDirectory(Path.Combine(_root, "src"));

        var tree = new FileTreeViewModel();
        tree.SetRoot(_root);
        Assert.Equal(new[] { "src" }, tree.Roots.Select(n => n.Name));

        tree.ShowHidden = true;
        Assert.Equal(new[] { ".git", "bin", "node_modules", "obj", "src" }, tree.Roots.Select(n => n.Name));
    }

    [Fact]
    public void SetRoot_MissingFolder_YieldsEmptyTree()
    {
        var tree = new FileTreeViewModel();
        tree.SetRoot(Path.Combine(_root, "nope"));
        Assert.Empty(tree.Roots);

        tree.SetRoot("");
        Assert.Empty(tree.Roots);
    }

    [Fact]
    public void Folder_LoadsChildrenWhenExpanded()
    {
        Touch("src/a.cs");
        Touch("src/b.cs");

        var tree = new FileTreeViewModel();
        tree.SetRoot(_root);
        var src = Find(tree.Roots, "src");
        Assert.Empty(src.Children);

        src.IsExpanded = true;

        Assert.Equal(new[] { "a.cs", "b.cs" }, src.Children.Select(n => n.Name));
    }

    [Fact]
    public void Refresh_AddsNewFile_InSortedPosition_AndMarksItNew()
    {
        Touch("a.txt");
        Touch("c.txt");
        var tree = new FileTreeViewModel();
        tree.SetRoot(_root);
        Assert.All(tree.Roots, n => Assert.False(n.IsNew));

        Touch("b.txt");
        tree.Refresh();

        Assert.Equal(new[] { "a.txt", "b.txt", "c.txt" }, tree.Roots.Select(n => n.Name));
        Assert.True(Find(tree.Roots, "b.txt").IsNew);
        Assert.False(Find(tree.Roots, "a.txt").IsNew);
    }

    [Fact]
    public void Refresh_RemovesDeletedFile_AndKeepsExistingNodeInstances()
    {
        Touch("a.txt");
        Touch("b.txt");
        var tree = new FileTreeViewModel();
        tree.SetRoot(_root);
        var a = Find(tree.Roots, "a.txt");

        File.Delete(Path.Combine(_root, "b.txt"));
        tree.Refresh();

        Assert.Equal(new[] { "a.txt" }, tree.Roots.Select(n => n.Name));
        Assert.Same(a, tree.Roots[0]);
    }

    [Fact]
    public void Refresh_SyncsInsideExpandedFolders_AndKeepsThemExpanded()
    {
        Touch("src/a.cs");
        var tree = new FileTreeViewModel();
        tree.SetRoot(_root);
        var src = Find(tree.Roots, "src");
        src.IsExpanded = true;

        Touch("src/b.cs");
        tree.Refresh();

        Assert.Same(src, Find(tree.Roots, "src"));
        Assert.True(src.IsExpanded);
        Assert.Equal(new[] { "a.cs", "b.cs" }, src.Children.Select(n => n.Name));
        Assert.True(Find(src.Children, "b.cs").IsNew);
    }

    [Fact]
    public void ClearNewFlags_ResetsEveryNode()
    {
        Touch("src/a.cs");
        var tree = new FileTreeViewModel();
        tree.SetRoot(_root);
        Find(tree.Roots, "src").IsExpanded = true;
        Touch("src/b.cs");
        Touch("c.txt");
        tree.Refresh();

        tree.ClearNewFlags();

        Assert.False(Find(tree.Roots, "c.txt").IsNew);
        Assert.False(Find(Find(tree.Roots, "src").Children, "b.cs").IsNew);
    }

    [Fact]
    public void ExpandedFolders_TracksRelativePathsOfExpandedNodes()
    {
        Touch("src/sub/a.cs");
        var tree = new FileTreeViewModel();
        tree.SetRoot(_root);
        var src = Find(tree.Roots, "src");
        src.IsExpanded = true;
        var sub = Find(src.Children, "sub");
        sub.IsExpanded = true;

        Assert.Equal(new[] { "src", Path.Combine("src", "sub") }, tree.ExpandedFolders.OrderBy(p => p));

        src.IsExpanded = false;
        Assert.DoesNotContain("src", tree.ExpandedFolders);
    }

    [Fact]
    public void SetRoot_RestoresPreviouslyExpandedFolders()
    {
        Touch("src/sub/a.cs");
        Touch("other/b.cs");
        var tree = new FileTreeViewModel();

        tree.SetRoot(_root, expanded: new[] { "src", Path.Combine("src", "sub") });

        var src = Find(tree.Roots, "src");
        Assert.True(src.IsExpanded);
        var sub = Find(src.Children, "sub");
        Assert.True(sub.IsExpanded);
        Assert.Equal(new[] { "a.cs" }, sub.Children.Select(n => n.Name));
        Assert.False(Find(tree.Roots, "other").IsExpanded);
    }

    [Fact]
    public void ExpandedFoldersChanged_FiresWhenNodeToggles()
    {
        Directory.CreateDirectory(Path.Combine(_root, "src"));
        var tree = new FileTreeViewModel();
        tree.SetRoot(_root);
        var fired = 0;
        tree.ExpandedFoldersChanged += () => fired++;

        Find(tree.Roots, "src").IsExpanded = true;

        Assert.Equal(1, fired);
    }
}
