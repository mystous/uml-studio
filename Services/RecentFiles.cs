using System.Text.Json;

namespace UmlStudio.Services;

public sealed record RecentFile(string Path, DateTime OpenedAt)
{
    public string FileName => System.IO.Path.GetFileNameWithoutExtension(Path);
    public string Folder => System.IO.Path.GetDirectoryName(Path) ?? "";
    public string When => OpenedAt.ToString("yyyy-MM-dd HH:mm");
}

public static class RecentFiles
{
    private static string StorePath
    {
        get
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UMLStudio");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "recent.json");
        }
    }

    public static List<RecentFile> Load()
    {
        try
        {
            if (!File.Exists(StorePath)) return new();
            var list = JsonSerializer.Deserialize<List<RecentFile>>(File.ReadAllText(StorePath)) ?? new();
            return list.Where(r => File.Exists(r.Path)).Take(12).ToList();
        }
        catch { return new(); }
    }

    public static List<RecentFile> Add(string path)
    {
        var list = Load();
        list.RemoveAll(r => string.Equals(r.Path, path, StringComparison.OrdinalIgnoreCase));
        list.Insert(0, new RecentFile(path, DateTime.Now));
        try { File.WriteAllText(StorePath, JsonSerializer.Serialize(list.Take(12).ToList())); } catch { }
        return list;
    }
}
