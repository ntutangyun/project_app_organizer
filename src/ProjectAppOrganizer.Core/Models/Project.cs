using System.Collections.ObjectModel;

namespace ProjectAppOrganizer.Core.Models;

public class Project
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string FolderPath { get; set; } = string.Empty;
    public ObservableCollection<TodoItem> Todos { get; set; } = new();
    public ObservableCollection<TerminalProfile> TerminalProfiles { get; set; } = new();

    // File-tree panel state (root-relative folder paths that are expanded).
    public List<string> ExpandedFolders { get; set; } = new();
    public bool ShowHiddenFiles { get; set; }

    // Whether the collapsible "notes & tasks" section is open.
    public bool NotesExpanded { get; set; }
}
