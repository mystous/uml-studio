using System.Text.Json.Serialization;

namespace UmlStudio.Models;

public enum RouteStyle { Direct, Orthogonal }

public sealed class UmlProject
{
    public string Version { get; set; } = "2.0";
    public string Name { get; set; } = "새 프로젝트";
    public string Author { get; set; } = "";
    public UmlPackage Root { get; set; } = new() { Name = "모델" };

    /// <summary>Legacy v1 flat diagram list (migrated into Root on load).</summary>
    public List<UmlDiagram>? Diagrams { get; set; }

    public IEnumerable<UmlPackage> AllPackages()
    {
        var stack = new Stack<UmlPackage>();
        stack.Push(Root);
        while (stack.Count > 0)
        {
            var p = stack.Pop();
            yield return p;
            for (int i = p.Packages.Count - 1; i >= 0; i--) stack.Push(p.Packages[i]);
        }
    }

    public IEnumerable<UmlDiagram> AllDiagrams() => AllPackages().SelectMany(p => p.Diagrams);

    public UmlDiagram? FindDiagram(string id) => AllDiagrams().FirstOrDefault(d => d.Id == id);

    public UmlPackage? FindPackage(string id) => AllPackages().FirstOrDefault(p => p.Id == id);

    public UmlPackage? ParentOf(UmlPackage pkg) => AllPackages().FirstOrDefault(p => p.Packages.Contains(pkg));

    public UmlPackage? OwnerOf(UmlDiagram d) => AllPackages().FirstOrDefault(p => p.Diagrams.Contains(d));
}

public sealed class UmlPackage
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "패키지";
    public string Stereotype { get; set; } = "";
    public string Notes { get; set; } = "";
    public List<UmlPackage> Packages { get; set; } = new();
    public List<UmlDiagram> Diagrams { get; set; } = new();
}

public sealed class UmlDiagram
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "다이어그램";
    public DiagramType Type { get; set; } = DiagramType.Class;
    public string Notes { get; set; } = "";
    public string Author { get; set; } = "";
    public double Zoom { get; set; } = 1.0;
    public double PanX { get; set; }
    public double PanY { get; set; }
    public bool ShowFrame { get; set; } = true;
    public List<UmlShape> Shapes { get; set; } = new();
    public List<UmlConnector> Connectors { get; set; } = new();
}

public sealed class UmlShape
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public ShapeKind Kind { get; set; }
    public string Name { get; set; } = "요소";
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 140;
    public double Height { get; set; } = 80;
    public string Stereotype { get; set; } = "";
    public bool IsAbstract { get; set; }
    public string Visibility { get; set; } = "public";
    public string Status { get; set; } = "제안";
    public string Version { get; set; } = "1.0";
    public string Alias { get; set; } = "";
    public List<string> Attributes { get; set; } = new();
    public List<string> Operations { get; set; } = new();
    public string Notes { get; set; } = "";
    /// <summary>Legacy v1 note text.</summary>
    public string? Body { get; set; }
    public string Fill { get; set; } = "";
    public string Stroke { get; set; } = "";
    public string FontColor { get; set; } = "";
    public double LineWidth { get; set; } = 1;
    public double LifelineLength { get; set; } = 420;
    public int Z { get; set; }

    [JsonIgnore] public double Right => X + Width;
    [JsonIgnore] public double Bottom => Y + Height;
    [JsonIgnore] public double CenterX => X + Width / 2;
    [JsonIgnore] public double CenterY => Y + Height / 2;
}

public sealed class UmlConnector
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public ConnectorKind Kind { get; set; } = ConnectorKind.Association;
    public string FromId { get; set; } = "";
    public string ToId { get; set; } = "";
    public string Label { get; set; } = "";
    public string FromMultiplicity { get; set; } = "";
    public string ToMultiplicity { get; set; } = "";
    public string FromRole { get; set; } = "";
    public string ToRole { get; set; } = "";
    public string Stereotype { get; set; } = "";
    public string Guard { get; set; } = "";
    public string Notes { get; set; } = "";
    public RouteStyle Route { get; set; } = RouteStyle.Direct;
    public string Stroke { get; set; } = "";
    public double FromY { get; set; } // sequence message vertical position
    public double ToY { get; set; }
}
