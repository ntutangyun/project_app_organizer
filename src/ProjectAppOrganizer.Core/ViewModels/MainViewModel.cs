using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ProjectAppOrganizer.Core.Models;
using ProjectAppOrganizer.Core.Persistence;

namespace ProjectAppOrganizer.Core.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IProjectStore _store;
    private readonly Func<Project, ProjectViewModel> _factory;

    [ObservableProperty] private ProjectViewModel? _selectedProject;

    public MainViewModel(IProjectStore store, Func<Project, ProjectViewModel> factory)
    {
        _store = store;
        _factory = factory;
    }

    public ObservableCollection<ProjectViewModel> Projects { get; } = new();

    public void Load()
    {
        Projects.Clear();
        foreach (var p in _store.Load())
            Projects.Add(_factory(p));
        SelectedProject = Projects.FirstOrDefault();
    }

    public void AddProject(string name)
    {
        var vm = _factory(new Project { Name = name });
        Projects.Add(vm);
        SelectedProject = vm;
    }

    public void RemoveProject(ProjectViewModel vm)
    {
        var index = Projects.IndexOf(vm);
        Projects.Remove(vm);
        if (ReferenceEquals(SelectedProject, vm))
            SelectedProject = Projects.ElementAtOrDefault(Math.Max(0, index - 1)) ?? Projects.FirstOrDefault();
    }

    public void Save() => _store.Save(Projects.Select(p => p.Project));

    public void ReleaseAll()
    {
        foreach (var project in Projects)
            project.ReleaseAll();
    }
}
