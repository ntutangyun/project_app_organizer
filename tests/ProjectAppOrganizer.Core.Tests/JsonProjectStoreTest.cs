using ProjectAppOrganizer.Core.Models;
using ProjectAppOrganizer.Core.Persistence;

namespace ProjectAppOrganizer.Core.Tests;

public class JsonProjectStoreTest : IDisposable
{
    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"pao-test-{Guid.NewGuid():N}.json");

    [Fact]
    public void Load_WhenFileMissing_ReturnsEmptyList()
    {
        var store = new JsonProjectStore(_path);
        Assert.Empty(store.Load());
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsProjects()
    {
        var store = new JsonProjectStore(_path);
        var project = new Project { Id = "p1", Name = "Demo", Description = "d", FolderPath = @"C:\demo" };
        project.Todos.Add(new TodoItem { Text = "first", Done = true });
        project.TerminalProfiles.Add(new TerminalProfile
        {
            Name = "bash", Shell = ShellType.GitBash, WorkingDirectory = @"C:\demo", StartupCommand = "ls"
        });

        store.Save(new[] { project });
        var loaded = store.Load();

        Assert.Single(loaded);
        Assert.Equal("p1", loaded[0].Id);
        Assert.Equal("Demo", loaded[0].Name);
        Assert.Single(loaded[0].Todos);
        Assert.True(loaded[0].Todos[0].Done);
        Assert.Equal(ShellType.GitBash, loaded[0].TerminalProfiles[0].Shell);
        Assert.Equal("ls", loaded[0].TerminalProfiles[0].StartupCommand);
    }

    public void Dispose()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }
}
