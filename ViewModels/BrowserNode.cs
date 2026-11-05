using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using UmlStudio.Models;

namespace UmlStudio.ViewModels;

public enum BrowserNodeKind { Root, Package, Diagram, Element }

public partial class BrowserNode : ObservableObject
{
    public BrowserNodeKind Kind { get; init; }
    public string Key { get; init; } = "";
    public string Glyph { get; init; } = "";
    public UmlPackage? Package { get; init; }
    public UmlDiagram? Diagram { get; init; }
    public UmlShape? Shape { get; init; }
    public BrowserNode? Parent { get; init; }

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _detail = "";
    [ObservableProperty] private bool _isExpanded;
    [ObservableProperty] private bool _isActive;

    public ObservableCollection<BrowserNode> Children { get; } = new();

    public bool IsDiagram => Kind == BrowserNodeKind.Diagram;
    public bool IsPackageLike => Kind is BrowserNodeKind.Package or BrowserNodeKind.Root;
}

public partial class DiagramTab : ObservableObject
{
    public DiagramTab(UmlDiagram d) { Diagram = d; _title = d.Name; }
    public UmlDiagram Diagram { get; set; }
    [ObservableProperty] private string _title;
    [ObservableProperty] private bool _isActive;
    public string Glyph => "diagram:" + Diagram.Type;
    public void Refresh() => Title = Diagram.Name;
}

public enum ToolMode { Select, Shape, Connector }

public partial class ToolItem : ObservableObject
{
    public ToolMode Mode { get; init; }
    public ShapeKind Shape { get; init; }
    public ConnectorKind Connector { get; init; }
    public string Name { get; init; } = "";
    public string Glyph { get; init; } = "";
    public string Tip { get; init; } = "";
    [ObservableProperty] private bool _isActive;
}

public sealed class ToolSection
{
    public string Title { get; init; } = "";
    public ObservableCollection<ToolItem> Items { get; } = new();
}

public partial class FeatureRow : ObservableObject
{
    private readonly Action _changed;
    public FeatureRow(string text, Action changed) { _text = text; _changed = changed; }
    [ObservableProperty] private string _text;
    partial void OnTextChanged(string value) => _changed();
}

public sealed class ConnectorChoice
{
    public ConnectorKind Value { get; init; }
    public string Name { get; init; } = "";
    public override string ToString() => Name;
}
