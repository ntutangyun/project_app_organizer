using ProjectAppOrganizer.Core.Models;
using ProjectAppOrganizer.Core.Persistence;

namespace ProjectAppOrganizer.Core.Tests;

public class ProjectPersistenceFieldsTest : IDisposable
{
    private readonly string _path =
        Path.Combine(Path.GetTempPath(), $"pao-fields-{Guid.NewGuid():N}.json");

    public void Dispose() { try { File.Delete(_path); } catch { } }

    [Fact]
    public void Save_ThenLoad_RoundTripsFileTreeAndNotesState()
    {
        var store = new JsonProjectStore(_path);
        var project = new Project
        {
            Name = "Demo",
            ExpandedFolders = { "src", @"src\sub" },
            ShowHiddenFiles = true,
            NotesExpanded = true
        };

        store.Save(new[] { project });
        var loaded = store.Load()[0];

        Assert.Equal(new[] { "src", @"src\sub" }, loaded.ExpandedFolders);
        Assert.True(loaded.ShowHiddenFiles);
        Assert.True(loaded.NotesExpanded);
    }

    [Fact]
    public void Load_OldFileWithoutNewFields_UsesDefaults()
    {
        File.WriteAllText(_path,
            "[{\"Id\":\"p1\",\"Name\":\"Old\",\"Description\":\"\",\"FolderPath\":\"\",\"Todos\":[],\"TerminalProfiles\":[]}]");
        var loaded = new JsonProjectStore(_path).Load()[0];

        Assert.Empty(loaded.ExpandedFolders);
        Assert.False(loaded.ShowHiddenFiles);
        Assert.False(loaded.NotesExpanded);
    }
}
