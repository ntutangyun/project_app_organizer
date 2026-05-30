using System.Text.Json;
using System.Text.Json.Serialization;
using ProjectAppOrganizer.Core.Models;

namespace ProjectAppOrganizer.Core.Persistence;

public class JsonProjectStore : IProjectStore
{
    private readonly string _path;
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public JsonProjectStore(string path) => _path = path;

    public static string DefaultPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ProjectAppOrganizer", "projects.json");

    public IList<Project> Load()
    {
        if (!File.Exists(_path)) return new List<Project>();
        var json = File.ReadAllText(_path);
        return JsonSerializer.Deserialize<List<Project>>(json, Options) ?? new List<Project>();
    }

    public void Save(IEnumerable<Project> projects)
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(projects.ToList(), Options);
        File.WriteAllText(_path, json);
    }
}
