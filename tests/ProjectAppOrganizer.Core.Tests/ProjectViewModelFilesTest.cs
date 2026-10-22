using ProjectAppOrganizer.Core.Models;
using ProjectAppOrganizer.Core.Tests.Fakes;
using ProjectAppOrganizer.Core.Terminals;
using ProjectAppOrganizer.Core.ViewModels;
using ProjectAppOrganizer.Core.Windows;

namespace ProjectAppOrganizer.Core.Tests;

public class ProjectViewModelFilesTest : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), $"pao-pvm-{Guid.NewGuid():N}");

    public ProjectViewModelFilesTest() => Directory.CreateDirectory(_root);
    public void Dispose() { try { Directory.Delete(_root, true); } catch { } }

    private sealed class NullLauncher : ITerminalLauncher
    {
        public nint Launch(TerminalProfile profile) => 1;
    }

    private static ProjectViewModel Make(Project project)
    {
        var wm = new FakeWindowManager();
        return new ProjectViewModel(project, wm, new NullLauncher(), new StageManager(wm),
            () => new PixelRect(0, 0, 1, 1));
    }

    [Fact]
    public void Constructor_LoadsFilesFromFolderPath_WithSavedState()
    {
        Directory.CreateDirectory(Path.Combine(_root, "src"));
        Directory.CreateDirectory(Path.Combine(_root, ".git"));
        var project = new Project { FolderPath = _root, ExpandedFolders = { "src" }, ShowHiddenFiles = true };

        var vm = Make(project);

        Assert.True(vm.Files.ShowHidden);
        Assert.Contains(vm.Files.Roots, n => n.Name == ".git");
        Assert.True(vm.Files.Roots.Single(n => n.Name == "src").IsExpanded);
    }

    [Fact]
    public void TogglingExpansion_WritesBackToProject()
    {
        Directory.CreateDirectory(Path.Combine(_root, "src"));
        var project = new Project { FolderPath = _root };
        var vm = Make(project);

        vm.Files.Roots.Single(n => n.Name == "src").IsExpanded = true;
        Assert.Equal(new[] { "src" }, project.ExpandedFolders);

        vm.Files.ShowHidden = true;
        Assert.True(project.ShowHiddenFiles);
    }

    [Fact]
    public void ReloadFiles_PicksUpChangedFolderPath()
    {
        var project = new Project { FolderPath = "" };
        var vm = Make(project);
        Assert.Empty(vm.Files.Roots);

        Directory.CreateDirectory(Path.Combine(_root, "src"));
        project.FolderPath = _root;
        vm.ReloadFiles();

        Assert.Single(vm.Files.Roots);
    }
}
