using System.Text.Json;
using System.Text.Json.Serialization;
using UmlStudio.Models;

namespace UmlStudio.Services;

public static class ProjectSerializer
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly JsonSerializerOptions Compact = new(Options) { WriteIndented = false };

    public static string Serialize(UmlProject project) => JsonSerializer.Serialize(project, Options);

    public static string SerializeCompact(UmlProject project) => JsonSerializer.Serialize(project, Compact);

    public static UmlProject Deserialize(string json)
    {
        var p = JsonSerializer.Deserialize<UmlProject>(json, Options) ?? new UmlProject();
        Migrate(p);
        return p;
    }

    public static void Migrate(UmlProject p)
    {
        p.Root ??= new UmlPackage { Name = "모델" };
        if (p.Diagrams is { Count: > 0 })
        {
            var pkg = new UmlPackage { Name = "가져온 다이어그램" };
            pkg.Diagrams.AddRange(p.Diagrams);
            p.Root.Packages.Add(pkg);
        }
        p.Diagrams = null;
        foreach (var d in p.AllDiagrams())
        {
            foreach (var s in d.Shapes)
            {
                if (!string.IsNullOrEmpty(s.Body) && string.IsNullOrEmpty(s.Notes)) s.Notes = s.Body;
                s.Body = null;
                s.Attributes ??= new();
                s.Operations ??= new();
                if (s.LifelineLength <= 0) s.LifelineLength = 420;
                if (s.LineWidth <= 0) s.LineWidth = 1;
            }
        }
        p.Version = "2.0";
    }
}
