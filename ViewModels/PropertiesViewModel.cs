using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UmlStudio.Models;
using UmlStudio.Rendering;

namespace UmlStudio.ViewModels;

/// <summary>Properties window: reflects the current selection and writes edits back live.</summary>
public partial class PropertiesViewModel : ObservableObject
{
    private readonly MainWindowViewModel _main;
    private bool _loading;

    public PropertiesViewModel(MainWindowViewModel main) => _main = main;

    public UmlShape? Shape { get; private set; }
    public UmlConnector? Connector { get; private set; }
    public UmlDiagram? Diagram { get; private set; }
    public UmlPackage? Package { get; private set; }

    [ObservableProperty] private string _title = "선택 없음";
    [ObservableProperty] private string _typeName = "";
    [ObservableProperty] private string _glyph = "";
    [ObservableProperty] private bool _hasSelection;
    [ObservableProperty] private bool _isShape;
    [ObservableProperty] private bool _isConnector;
    [ObservableProperty] private bool _isDiagram;
    [ObservableProperty] private bool _isPackage;
    [ObservableProperty] private bool _hasFeatures;
    [ObservableProperty] private bool _hasOperations;
    [ObservableProperty] private bool _isMulti;
    [ObservableProperty] private bool _showEmptyHint = true;
    [ObservableProperty] private bool _canEditNotes;
    [ObservableProperty] private int _selectedTab;

    // general
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _stereotype = "";
    [ObservableProperty] private string _alias = "";
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private string _version = "";
    [ObservableProperty] private string _visibility = "public";
    [ObservableProperty] private bool _isAbstract;
    [ObservableProperty] private string _notes = "";
    [ObservableProperty] private string _author = "";

    // layout / appearance
    [ObservableProperty] private double _x;
    [ObservableProperty] private double _y;
    [ObservableProperty] private double _width;
    [ObservableProperty] private double _height;
    [ObservableProperty] private string _fill = "";
    [ObservableProperty] private string _stroke = "";
    [ObservableProperty] private string _fontColor = "";
    [ObservableProperty] private double _lineWidth = 1;

    // connector
    [ObservableProperty] private string _label = "";
    [ObservableProperty] private string _guard = "";
    [ObservableProperty] private string _fromMultiplicity = "";
    [ObservableProperty] private string _toMultiplicity = "";
    [ObservableProperty] private string _fromRole = "";
    [ObservableProperty] private string _toRole = "";
    [ObservableProperty] private bool _orthogonal;
    [ObservableProperty] private string _sourceName = "";
    [ObservableProperty] private string _targetName = "";
    [ObservableProperty] private ConnectorChoice? _connectorKind;
    public ObservableCollection<ConnectorChoice> ConnectorKinds { get; } = new();

    public ObservableCollection<FeatureRow> Attributes { get; } = new();
    public ObservableCollection<FeatureRow> Operations { get; } = new();

    public string[] StatusOptions { get; } = { "제안", "승인", "구현됨", "검증됨", "폐기" };
    public string[] VisibilityOptions { get; } = { "public", "protected", "private", "package" };
    public string[] Palette { get; } =
    {
        "#FFF7D6", "#FCEBD2", "#FDE2E1", "#EDE4F7", "#DDEBFA", "#E4F1FB", "#E6F4EA", "#E8F5E0",
        "#F1F3F5", "#FFFFFF", "#FFE7A3", "#C9DDF7", "#C8E6C9", "#F8BBD0", "#D1C4E9", "#B0BEC5"
    };

    public void Load()
    {
        _loading = true;
        try
        {
            var shapes = _main.SelectedShapes;
            Shape = shapes.Count == 1 ? shapes[0] : null;
            Connector = shapes.Count == 0 ? _main.SelectedConnector : null;
            Package = shapes.Count == 0 && Connector == null ? _main.FocusPackage : null;
            Diagram = shapes.Count == 0 && Connector == null && Package == null ? _main.CurrentDiagram : null;
            IsMulti = shapes.Count > 1;
            IsShape = Shape != null;
            IsConnector = Connector != null;
            IsDiagram = Diagram != null;
            IsPackage = Package != null;
            HasSelection = IsShape || IsConnector || IsDiagram || IsPackage;
            ShowEmptyHint = !HasSelection && !IsMulti;
            HasFeatures = Shape != null && DiagramCatalog.HasFeatures(Shape.Kind);
            HasOperations = Shape != null && Shape.Kind is ShapeKind.Class or ShapeKind.Interface or ShapeKind.Enumeration;
            CanEditNotes = HasSelection;

            Attributes.Clear(); Operations.Clear();
            if (Shape != null)
            {
                var s = Shape;
                Title = string.IsNullOrEmpty(s.Name) ? DiagramCatalog.ShapeName(s.Kind) : s.Name;
                TypeName = DiagramCatalog.ShapeName(s.Kind);
                Glyph = "shape:" + s.Kind;
                Name = s.Name; Stereotype = s.Stereotype; Alias = s.Alias; Status = s.Status; Version = s.Version;
                Visibility = s.Visibility; IsAbstract = s.IsAbstract; Notes = s.Notes;
                X = s.X; Y = s.Y; Width = s.Width; Height = s.Height;
                Fill = string.IsNullOrEmpty(s.Fill) ? DiagramCatalog.DefaultColors(s.Kind).Fill : s.Fill;
                Stroke = string.IsNullOrEmpty(s.Stroke) ? DiagramCatalog.DefaultColors(s.Kind).Stroke : s.Stroke;
                FontColor = string.IsNullOrEmpty(s.FontColor) ? "#1C1C1C" : s.FontColor;
                LineWidth = s.LineWidth;
                foreach (var a in s.Attributes) Attributes.Add(new FeatureRow(a, SyncFeatures));
                foreach (var o in s.Operations) Operations.Add(new FeatureRow(o, SyncFeatures));
            }
            else if (Connector != null)
            {
                var c = Connector;
                var d = _main.CurrentDiagram!;
                Title = DiagramCatalog.ConnectorName(c.Kind) + (string.IsNullOrEmpty(c.Label) ? "" : " · " + c.Label);
                TypeName = DiagramCatalog.ConnectorName(c.Kind) + " 연결선";
                Glyph = "conn:" + c.Kind;
                Label = c.Label; Stereotype = c.Stereotype; Guard = c.Guard; Notes = c.Notes;
                FromMultiplicity = c.FromMultiplicity; ToMultiplicity = c.ToMultiplicity;
                FromRole = c.FromRole; ToRole = c.ToRole; Orthogonal = c.Route == RouteStyle.Orthogonal;
                SourceName = d.Shapes.FirstOrDefault(s => s.Id == c.FromId)?.Name ?? "";
                TargetName = d.Shapes.FirstOrDefault(s => s.Id == c.ToId)?.Name ?? "";
                ConnectorKinds.Clear();
                foreach (var (k, n) in DiagramCatalog.ConnectorsFor(d.Type).Append((UmlStudio.Models.ConnectorKind.NoteLink, "노트 링크")))
                    ConnectorKinds.Add(new ConnectorChoice { Value = k, Name = n });
                if (ConnectorKinds.All(x => x.Value != c.Kind))
                    ConnectorKinds.Add(new ConnectorChoice { Value = c.Kind, Name = DiagramCatalog.ConnectorName(c.Kind) });
                ConnectorKind = ConnectorKinds.First(x => x.Value == c.Kind);
            }
            else if (Diagram != null)
            {
                Title = Diagram.Name;
                TypeName = DiagramCatalog.DisplayName(Diagram.Type) + " 다이어그램";
                Glyph = "diagram:" + Diagram.Type;
                Name = Diagram.Name; Notes = Diagram.Notes; Author = Diagram.Author;
                Stereotype = "";
            }
            else if (Package != null)
            {
                Title = Package.Name;
                TypeName = "패키지";
                Glyph = "package";
                Name = Package.Name; Stereotype = Package.Stereotype; Notes = Package.Notes;
            }
            else
            {
                Title = IsMulti ? $"{shapes.Count}개 요소 선택됨" : "선택 없음";
                TypeName = IsMulti ? "다중 선택" : "";
                Glyph = "";
                Notes = "";
            }
            if (IsMulti) { Title = $"{shapes.Count}개 요소 선택됨"; TypeName = "다중 선택 — 정렬은 [레이아웃] 탭"; }
            if (!HasFeatures && SelectedTab == 2) SelectedTab = 0;
            if (!IsShape && SelectedTab == 1) SelectedTab = 0;
        }
        finally { _loading = false; }
    }

    /// <summary>Refresh only geometry fields after a canvas drag/resize.</summary>
    public void RefreshGeometry()
    {
        if (Shape == null) return;
        _loading = true;
        X = Shape.X; Y = Shape.Y; Width = Shape.Width; Height = Shape.Height;
        _loading = false;
    }

    private void Edit(Action apply, bool tree = false)
    {
        if (_loading) return;
        _main.BeginPropertyEdit();
        apply();
        _main.AfterPropertyEdit(tree);
    }

    partial void OnNameChanged(string value) => Edit(() =>
    {
        if (Shape != null) Shape.Name = value;
        else if (Diagram != null) Diagram.Name = value;
        else if (Package != null) Package.Name = value;
        Title = value;
    }, true);

    partial void OnStereotypeChanged(string value) => Edit(() =>
    {
        if (Shape != null) { Shape.Stereotype = value; DiagramRenderer.AutoFit(Shape, false); }
        else if (Connector != null) Connector.Stereotype = value;
        else if (Package != null) Package.Stereotype = value;
    });

    partial void OnAliasChanged(string value) => Edit(() => { if (Shape != null) Shape.Alias = value; });
    partial void OnStatusChanged(string value) => Edit(() => { if (Shape != null && value != null) Shape.Status = value; });
    partial void OnVersionChanged(string value) => Edit(() => { if (Shape != null) Shape.Version = value; });
    partial void OnVisibilityChanged(string value) => Edit(() => { if (Shape != null && value != null) Shape.Visibility = value; });
    partial void OnIsAbstractChanged(bool value) => Edit(() => { if (Shape != null) Shape.IsAbstract = value; });
    partial void OnAuthorChanged(string value) => Edit(() => { if (Diagram != null) Diagram.Author = value; });

    partial void OnNotesChanged(string value) => Edit(() =>
    {
        if (Shape != null) Shape.Notes = value;
        else if (Connector != null) Connector.Notes = value;
        else if (Diagram != null) Diagram.Notes = value;
        else if (Package != null) Package.Notes = value;
    });

    partial void OnXChanged(double value) => Edit(() => { if (Shape != null) Shape.X = value; });
    partial void OnYChanged(double value) => Edit(() => { if (Shape != null) Shape.Y = value; });
    partial void OnWidthChanged(double value) => Edit(() => { if (Shape != null && value >= 8) Shape.Width = value; });
    partial void OnHeightChanged(double value) => Edit(() => { if (Shape != null && value >= 4) Shape.Height = value; });
    partial void OnFillChanged(string value) => Edit(() => { if (Shape != null) Shape.Fill = value; });
    partial void OnStrokeChanged(string value) => Edit(() => { if (Shape != null) Shape.Stroke = value; });
    partial void OnFontColorChanged(string value) => Edit(() => { if (Shape != null) Shape.FontColor = value; });
    partial void OnLineWidthChanged(double value) => Edit(() => { if (Shape != null && value > 0) Shape.LineWidth = value; });

    partial void OnLabelChanged(string value) => Edit(() => { if (Connector != null) Connector.Label = value; });
    partial void OnGuardChanged(string value) => Edit(() => { if (Connector != null) Connector.Guard = value; });
    partial void OnFromMultiplicityChanged(string value) => Edit(() => { if (Connector != null) Connector.FromMultiplicity = value; });
    partial void OnToMultiplicityChanged(string value) => Edit(() => { if (Connector != null) Connector.ToMultiplicity = value; });
    partial void OnFromRoleChanged(string value) => Edit(() => { if (Connector != null) Connector.FromRole = value; });
    partial void OnToRoleChanged(string value) => Edit(() => { if (Connector != null) Connector.ToRole = value; });
    partial void OnOrthogonalChanged(bool value) => Edit(() => { if (Connector != null) Connector.Route = value ? RouteStyle.Orthogonal : RouteStyle.Direct; });
    partial void OnConnectorKindChanged(ConnectorChoice? value) => Edit(() =>
    {
        if (Connector != null && value != null)
        {
            Connector.Kind = value.Value;
            if (string.IsNullOrEmpty(Connector.Stereotype)) Connector.Stereotype = DiagramCatalog.DefaultStereotype(value.Value);
        }
    });

    private void SyncFeatures()
    {
        Edit(() =>
        {
            if (Shape == null) return;
            Shape.Attributes = Attributes.Select(a => a.Text).ToList();
            Shape.Operations = Operations.Select(a => a.Text).ToList();
            DiagramRenderer.AutoFit(Shape);
        });
        _loading = true; Height = Shape?.Height ?? Height; Width = Shape?.Width ?? Width; _loading = false;
    }

    [RelayCommand]
    private void AddAttribute()
    {
        if (Shape == null) return;
        var text = Shape.Kind switch
        {
            ShapeKind.Enumeration => "LITERAL",
            ShapeKind.State => "do / 활동",
            ShapeKind.Component => "provided: IService",
            _ => "- attribute: String"
        };
        Attributes.Add(new FeatureRow(text, SyncFeatures));
        SyncFeatures();
    }

    [RelayCommand]
    private void AddOperation()
    {
        if (Shape == null) return;
        Operations.Add(new FeatureRow("+ operation(): void", SyncFeatures));
        SyncFeatures();
    }

    [RelayCommand]
    private void RemoveFeature(FeatureRow? row)
    {
        if (row == null) return;
        if (!Attributes.Remove(row)) Operations.Remove(row);
        SyncFeatures();
    }

    [RelayCommand]
    private void MoveFeatureUp(FeatureRow? row)
    {
        if (row == null) return;
        var list = Attributes.Contains(row) ? Attributes : Operations;
        var i = list.IndexOf(row);
        if (i > 0) { list.Move(i, i - 1); SyncFeatures(); }
    }

    [RelayCommand]
    private void SetFill(string? color) { if (color != null && Shape != null) Fill = color; }

    [RelayCommand]
    private void ResetAppearance()
    {
        if (Shape == null) return;
        Edit(() => { Shape.Fill = ""; Shape.Stroke = ""; Shape.FontColor = ""; Shape.LineWidth = 1; });
        Load();
    }
}
