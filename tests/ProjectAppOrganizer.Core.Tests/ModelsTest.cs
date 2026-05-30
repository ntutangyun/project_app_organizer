using ProjectAppOrganizer.Core.Models;

namespace ProjectAppOrganizer.Core.Tests;

public class ModelsTest
{
    [Fact]
    public void NewProject_HasIdAndEmptyCollections()
    {
        var p = new Project { Name = "Demo" };

        Assert.False(string.IsNullOrWhiteSpace(p.Id));
        Assert.Equal("Demo", p.Name);
        Assert.Empty(p.Todos);
        Assert.Empty(p.TerminalProfiles);
    }

    [Fact]
    public void TodoItem_TogglingDone_RaisesPropertyChanged()
    {
        var todo = new TodoItem { Text = "Task" };
        var raised = false;
        todo.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(TodoItem.Done)) raised = true; };

        todo.Done = true;

        Assert.True(todo.Done);
        Assert.True(raised);
    }
}
