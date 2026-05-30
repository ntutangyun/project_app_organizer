using ProjectAppOrganizer.Core.Models;
using ProjectAppOrganizer.Core.Persistence;
using ProjectAppOrganizer.Core.Tests.Fakes;
using ProjectAppOrganizer.Core.Terminals;
using ProjectAppOrganizer.Core.ViewModels;
using ProjectAppOrganizer.Core.Windows;

namespace ProjectAppOrganizer.Core.Tests;

public class MainViewModelTest
{
    private sealed class InMemoryStore : IProjectStore
    {
        public List<Project> Saved = new();
        public IList<Project> Seed = new List<Project>();
        public IList<Project> Load() => Seed;
        public void Save(IEnumerable<Project> projects) => Saved = projects.ToList();
    }

    private sealed class NullLauncher : ITerminalLauncher
    {
        public nint Launch(TerminalProfile profile) => 1;
    }

    private static MainViewModel Make(InMemoryStore store)
    {
        var wm = new FakeWindowManager();
        return new MainViewModel(
            store,
            project => new ProjectViewModel(
                project, wm, new NullLauncher(), new StageManager(wm),
                () => new PixelRect(0, 0, 10, 10)));
    }

    [Fact]
    public void Load_PopulatesProjectsFromStore()
    {
        var store = new InMemoryStore { Seed = new List<Project> { new() { Name = "A" }, new() { Name = "B" } } };
        var vm = Make(store);

        vm.Load();

        Assert.Equal(2, vm.Projects.Count);
        Assert.Equal("A", vm.SelectedProject!.Project.Name);
    }

    [Fact]
    public void AddProject_AddsSelectsAndIsAvailableImmediately()
    {
        var vm = Make(new InMemoryStore());

        vm.AddProject("New");

        Assert.Single(vm.Projects);
        Assert.Equal("New", vm.SelectedProject!.Project.Name);
    }

    [Fact]
    public void RemoveProject_RemovesAndReselects()
    {
        var vm = Make(new InMemoryStore());
        vm.AddProject("A");
        vm.AddProject("B");
        var a = vm.Projects[0];

        vm.RemoveProject(a);

        Assert.Single(vm.Projects);
        Assert.Equal("B", vm.SelectedProject!.Project.Name);
    }

    [Fact]
    public void Save_WritesUnderlyingProjectsToStore()
    {
        var store = new InMemoryStore();
        var vm = Make(store);
        vm.AddProject("A");

        vm.Save();

        Assert.Single(store.Saved);
        Assert.Equal("A", store.Saved[0].Name);
    }
}
