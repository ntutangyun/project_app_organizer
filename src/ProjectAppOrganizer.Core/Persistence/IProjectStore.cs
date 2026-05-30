using ProjectAppOrganizer.Core.Models;

namespace ProjectAppOrganizer.Core.Persistence;

public interface IProjectStore
{
    IList<Project> Load();
    void Save(IEnumerable<Project> projects);
}
