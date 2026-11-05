using System.Collections.ObjectModel;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UmlStudio.Models;
using UmlStudio.Rendering;
using UmlStudio.Services;

namespace UmlStudio.ViewModels;

public interface IDialogService
{
    Task<string?> OpenProjectAsync();
    Task<string?> SaveFileAsync(string title, string suggestedName, string ext, string filterName);
    Task<string?> PromptAsync(string title, string label, string initial);
    Task<bool> ConfirmAsync(string title, string message);
    void FocusPropertyName();
    void FocusSearch();
}

public partial class MainWindowViewModel : ObservableObject
{
    public IDialogService? Dialogs { get; set; }
    public PropertiesViewModel Properties { get; }
    public RenderState RenderState { get; } = new();
    public event Action? DiagramChanged;
    public event Action? FitRequested;

    private readonly UndoService _undo = new();
    private bool _propEditOpen;

    public MainWindowViewModel()
    {
        Properties = new PropertiesViewModel(this);
        foreach (var r in RecentFiles.Load()) Recent.Add(r);
        BuildToolbox();
        UpdateStatus();
    }

    // ===== project state =====
    [ObservableProperty] private UmlProject? _project;
    [ObservableProperty] private string? _filePath;
    [ObservableProperty] private bool _isDirty;
    [ObservableProperty] private bool _startPageVisible = true;

    public bool HasProject => Project != null;
    public bool NoProject => Project == null;
    public string WindowTitle => Project == null ? "UML Studio" : $"UML Studio — {Project.Name}{(IsDirty ? " *" : "")}";
    public string ProjectName => Project?.Name ?? "";
    public bool ShowDiagram => !StartPageVisible && ActiveTab != null;
    public bool ShowEmptyHint => !StartPageVisible && ActiveTab == null && Project != null;
    public bool ShowStart => StartPageVisible || Project == null;

    partial void OnProjectChanged(UmlProject? value)
    {
        OnPropertyChanged(nameof(HasProject)); OnPropertyChanged(nameof(NoProject));
        OnPropertyChanged(nameof(WindowTitle)); OnPropertyChanged(nameof(ProjectName));
        OnPropertyChanged(nameof(ShowStart)); OnPropertyChanged(nameof(ShowEmptyHint));
    }
    partial void OnIsDirtyChanged(bool value) => OnPropertyChanged(nameof(WindowTitle));
    partial void OnStartPageVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowDiagram)); OnPropertyChanged(nameof(ShowEmptyHint)); OnPropertyChanged(nameof(ShowStart));
    }

    public ObservableCollection<RecentFile> Recent { get; } = new();

    // ===== browser =====
    public ObservableCollection<BrowserNode> BrowserRoots { get; } = new();
    [ObservableProperty] private BrowserNode? _selectedBrowserNode;
    [ObservableProperty] private string _browserFilter = "";
    public UmlPackage? FocusPackage { get; private set; }

    partial void OnBrowserFilterChanged(string value) => RebuildBrowser();

    partial void OnSelectedBrowserNodeChanged(BrowserNode? value)
    {
        if (value == null) return;
        FocusPackage = null;
        switch (value.Kind)
        {
            case BrowserNodeKind.Element when value.Shape != null && value.Diagram != null:
                if (CurrentDiagram == value.Diagram || OpenTabs.Any(t => t.Diagram == value.Diagram))
                {
                    if (CurrentDiagram != value.Diagram) ActivateDiagram(value.Diagram);
                    var shape = value.Diagram.Shapes.FirstOrDefault(s => s.Id == value.Shape.Id);
                    if (shape != null) SetSelection(new[] { shape }, null, fromBrowser: true);
                }
                else
                {
                    RenderState.HighlightShape = value.Shape.Id;
                    ShowTransientProperties(value.Shape);
                }
                break;
            case BrowserNodeKind.Package:
            case BrowserNodeKind.Root:
                FocusPackage = value.Package;
                Properties.Load();
                break;
            case BrowserNodeKind.Diagram:
                Properties.Load();
                break;
        }
        StatusText = value.Kind switch
        {
            BrowserNodeKind.Diagram => $"{value.Name} — 더블클릭하여 열기",
            BrowserNodeKind.Element => $"{value.Detail}: {value.Name}",
            _ => $"패키지: {value.Name}"
        };
    }

    private void ShowTransientProperties(UmlShape s)
    {
        SelectedShapes.Clear();
        SelectedShapes.Add(s);
        Properties.Load();
        SelectedShapes.Clear();
    }

    public void RebuildBrowser()
    {
        var expanded = new HashSet<string>(AllNodes().Where(n => n.IsExpanded).Select(n => n.Key));
        bool first = BrowserRoots.Count == 0;
        var selKey = SelectedBrowserNode?.Key;
        BrowserRoots.Clear();
        if (Project == null) return;
        var filter = BrowserFilter?.Trim() ?? "";
        var root = BuildPackageNode(Project.Root, null, BrowserNodeKind.Root, expanded, first, filter);
        if (root != null) { root.IsExpanded = true; BrowserRoots.Add(root); }
        if (selKey != null)
        {
            var again = AllNodes().FirstOrDefault(n => n.Key == selKey);
            if (again != null) _selectedBrowserNode = again;
            OnPropertyChanged(nameof(SelectedBrowserNode));
        }
        MarkActiveNodes();
    }

    private BrowserNode? BuildPackageNode(UmlPackage pkg, BrowserNode? parent, BrowserNodeKind kind, HashSet<string> expanded, bool first, string filter)
    {
        var node = new BrowserNode
        {
            Kind = kind, Key = "p:" + pkg.Id, Package = pkg, Parent = parent,
            Glyph = kind == BrowserNodeKind.Root ? "root" : "package", Name = pkg.Name,
            Detail = kind == BrowserNodeKind.Root ? "모델" : (string.IsNullOrEmpty(pkg.Stereotype) ? "패키지" : "«" + pkg.Stereotype + "»"),
            IsExpanded = first || expanded.Contains("p:" + pkg.Id) || filter.Length > 0
        };
        bool Match(string s) => filter.Length == 0 || s.Contains(filter, StringComparison.OrdinalIgnoreCase);
        foreach (var sub in pkg.Packages)
        {
            var child = BuildPackageNode(sub, node, BrowserNodeKind.Package, expanded, first, filter);
            if (child != null) node.Children.Add(child);
        }
        foreach (var d in pkg.Diagrams)
        {
            if (!Match(d.Name) && filter.Length > 0 && !d.Shapes.Any(s => Match(s.Name))) continue;
            node.Children.Add(new BrowserNode
            {
                Kind = BrowserNodeKind.Diagram, Key = "d:" + d.Id, Diagram = d, Parent = node,
                Glyph = "diagram:" + d.Type, Name = d.Name, Detail = DiagramCatalog.DisplayName(d.Type) + " 다이어그램"
            });
        }
        // elements: list model elements under their owning package
        var seen = new HashSet<string>();
        foreach (var d in pkg.Diagrams)
            foreach (var s in d.Shapes.Where(s => DiagramCatalog.IsModelElement(s.Kind) && !string.IsNullOrWhiteSpace(s.Name)).OrderBy(s => s.Kind).ThenBy(s => s.Name))
            {
                if (!seen.Add(s.Kind + "|" + s.Name) || !Match(s.Name)) continue;
                var el = new BrowserNode
                {
                    Kind = BrowserNodeKind.Element, Key = "e:" + s.Id, Shape = s, Diagram = d, Parent = node,
                    Glyph = "shape:" + s.Kind, Name = s.Name, Detail = DiagramCatalog.ShapeName(s.Kind),
                    IsExpanded = expanded.Contains("e:" + s.Id)
                };
                if (s.Kind is ShapeKind.Class or ShapeKind.Interface or ShapeKind.Enumeration)
                {
                    foreach (var a in s.Attributes)
                        el.Children.Add(new BrowserNode { Kind = BrowserNodeKind.Element, Key = "a:" + s.Id + a, Shape = s, Diagram = d, Parent = el, Glyph = "feature:attr", Name = a, Detail = "특성" });
                    foreach (var o in s.Operations)
                        el.Children.Add(new BrowserNode { Kind = BrowserNodeKind.Element, Key = "o:" + s.Id + o, Shape = s, Diagram = d, Parent = el, Glyph = "feature:op", Name = o, Detail = "오퍼레이션" });
                }
                node.Children.Add(el);
            }
        if (filter.Length > 0 && node.Children.Count == 0 && !Match(pkg.Name) && kind != BrowserNodeKind.Root) return null;
        return node;
    }

    public IEnumerable<BrowserNode> AllNodes()
    {
        var stack = new Stack<BrowserNode>(BrowserRoots);
        while (stack.Count > 0)
        {
            var n = stack.Pop();
            yield return n;
            foreach (var c in n.Children) stack.Push(c);
        }
    }

    private void MarkActiveNodes()
    {
        foreach (var n in AllNodes().Where(n => n.Kind == BrowserNodeKind.Diagram))
            n.IsActive = n.Diagram == CurrentDiagram;
    }

    public void OpenNode(BrowserNode? node)
    {
        if (node?.Diagram == null) return;
        if (node.Kind == BrowserNodeKind.Diagram) { OpenDiagram(node.Diagram); return; }
        OpenDiagram(node.Diagram);
        var shape = node.Diagram.Shapes.FirstOrDefault(s => s.Id == node.Shape?.Id);
        if (shape != null) SetSelection(new[] { shape }, null, fromBrowser: true);
    }

    // ===== tabs =====
    public ObservableCollection<DiagramTab> OpenTabs { get; } = new();
    [ObservableProperty] private DiagramTab? _activeTab;
    public UmlDiagram? CurrentDiagram => ActiveTab?.Diagram;

    partial void OnActiveTabChanged(DiagramTab? value)
    {
        foreach (var t in OpenTabs) t.IsActive = t == value;
        if (value != null) StartPageVisible = false;
        SelectedShapes.Clear(); SelectedConnector = null;
        RenderState.HighlightShape = null;
        BuildToolbox();
        OnPropertyChanged(nameof(CurrentDiagram)); OnPropertyChanged(nameof(ShowDiagram)); OnPropertyChanged(nameof(ShowEmptyHint));
        OnPropertyChanged(nameof(Zoom)); OnPropertyChanged(nameof(ZoomText));
        MarkActiveNodes();
        SyncSelection();
    }

    public void OpenDiagram(UmlDiagram d)
    {
        var tab = OpenTabs.FirstOrDefault(t => t.Diagram == d);
        if (tab == null) { tab = new DiagramTab(d); OpenTabs.Add(tab); }
        ActiveTab = tab;
        StartPageVisible = false;
        OnPropertyChanged(nameof(ShowDiagram));
        Invalidate();
    }

    private void ActivateDiagram(UmlDiagram d) => OpenDiagram(d);

    [RelayCommand]
    private void ActivateTab(DiagramTab? tab)
    {
        if (tab == null) return;
        OpenDiagram(tab.Diagram);
    }

    [RelayCommand]
    private void CloseTab(DiagramTab? tab)
    {
        tab ??= ActiveTab;
        if (tab == null) return;
        var idx = OpenTabs.IndexOf(tab);
        OpenTabs.Remove(tab);
        if (ActiveTab == tab || ActiveTab == null)
            ActiveTab = OpenTabs.Count == 0 ? null : OpenTabs[Math.Clamp(idx - 1, 0, OpenTabs.Count - 1)];
    }

    [RelayCommand]
    private void ShowStartPage() => StartPageVisible = true;

    [RelayCommand]
    private void HideStartPage()
    {
        if (Project == null) return;
        StartPageVisible = false;
        if (ActiveTab == null && Project.AllDiagrams().FirstOrDefault() is { } d) OpenDiagram(d);
    }

    // ===== toolbox =====
    public ObservableCollection<ToolSection> ToolSections { get; } = new();
    [ObservableProperty] private ToolItem? _currentTool;
    public string ToolboxTitle => CurrentDiagram == null ? "도구상자" : $"도구상자 — {DiagramCatalog.DisplayName(CurrentDiagram.Type)}";

    private void BuildToolbox()
    {
        ToolSections.Clear();
        var common = new ToolSection { Title = "공통" };
        var select = new ToolItem { Mode = ToolMode.Select, Name = "선택", Glyph = "select", Tip = "선택 / 이동 (Esc)" };
        common.Items.Add(select);
        common.Items.Add(new ToolItem { Mode = ToolMode.Shape, Shape = ShapeKind.Note, Name = "노트", Glyph = "shape:Note", Tip = "노트 추가" });
        common.Items.Add(new ToolItem { Mode = ToolMode.Connector, Connector = ConnectorKind.NoteLink, Name = "노트 링크", Glyph = "conn:NoteLink", Tip = "노트를 요소에 연결" });
        ToolSections.Add(common);
        if (CurrentDiagram != null)
        {
            var t = CurrentDiagram.Type;
            var title = DiagramCatalog.DisplayName(t);
            var shapes = new ToolSection { Title = $"{title} — 요소" };
            foreach (var (k, n) in DiagramCatalog.ShapesFor(t))
                shapes.Items.Add(new ToolItem { Mode = ToolMode.Shape, Shape = k, Name = n, Glyph = "shape:" + k, Tip = $"{n} 배치: 캔버스를 클릭" });
            ToolSections.Add(shapes);
            var rels = new ToolSection { Title = $"{title} — 관계" };
            foreach (var (k, n) in DiagramCatalog.ConnectorsFor(t))
                rels.Items.Add(new ToolItem { Mode = ToolMode.Connector, Connector = k, Name = n, Glyph = "conn:" + k, Tip = $"{n}: 시작 요소에서 끝 요소로 드래그" });
            ToolSections.Add(rels);
        }
        CurrentTool = select;
        select.IsActive = true;
        OnPropertyChanged(nameof(ToolboxTitle));
    }

    [RelayCommand]
    private void SelectTool(ToolItem? tool)
    {
        if (tool == null) return;
        foreach (var t in ToolSections.SelectMany(s => s.Items)) t.IsActive = t == tool;
        CurrentTool = tool;
        UpdateStatus();
    }

    public void ResetTool()
    {
        var sel = ToolSections.SelectMany(s => s.Items).FirstOrDefault(t => t.Mode == ToolMode.Select);
        if (sel != null) SelectTool(sel);
    }

    public bool StickyTool { get; set; }

    // ===== selection =====
    public List<UmlShape> SelectedShapes { get; } = new();
    public UmlConnector? SelectedConnector { get; private set; }

    public void SetSelection(IEnumerable<UmlShape> shapes, UmlConnector? connector, bool fromBrowser = false)
    {
        SelectedShapes.Clear();
        SelectedShapes.AddRange(shapes.Distinct());
        SelectedConnector = SelectedShapes.Count == 0 ? connector : null;
        if (!fromBrowser) FocusPackage = null;
        RenderState.HighlightShape = null;
        SyncSelection();
        if (!fromBrowser && SelectedShapes.Count == 1)
        {
            var node = AllNodes().FirstOrDefault(n => n.Kind == BrowserNodeKind.Element && n.Shape?.Id == SelectedShapes[0].Id);
            if (node != null)
            {
                for (var p = node.Parent; p != null; p = p.Parent) p.IsExpanded = true;
                _selectedBrowserNode = node;
                OnPropertyChanged(nameof(SelectedBrowserNode));
            }
        }
    }

    private void SyncSelection()
    {
        RenderState.SelectedShapes.Clear();
        foreach (var s in SelectedShapes) RenderState.SelectedShapes.Add(s.Id);
        RenderState.SelectedConnector = SelectedConnector?.Id;
        _propEditOpen = false;
        Properties.Load();
        UpdateStatus();
        Invalidate();
    }

    // ===== view state =====
    [ObservableProperty] private bool _showGrid = true;
    [ObservableProperty] private bool _snapToGrid = true;
    public double GridSize => 10;

    partial void OnShowGridChanged(bool value) => Invalidate();

    public double Zoom
    {
        get => CurrentDiagram?.Zoom ?? 1.0;
        set
        {
            if (CurrentDiagram == null) return;
            CurrentDiagram.Zoom = Math.Clamp(value, 0.2, 4.0);
            OnPropertyChanged(); OnPropertyChanged(nameof(ZoomText));
            Invalidate();
        }
    }
    public string ZoomText => $"{Zoom * 100:0}%";

    [ObservableProperty] private string _statusText = "준비";
    [ObservableProperty] private string _modeText = "선택";
    [ObservableProperty] private string _mouseText = "X: 0  Y: 0";
    [ObservableProperty] private string _selectionText = "선택 없음";
    [ObservableProperty] private string _diagramTypeText = "";

    public void UpdateStatus()
    {
        ModeText = CurrentTool?.Mode switch
        {
            ToolMode.Shape => $"배치: {CurrentTool.Name}",
            ToolMode.Connector => $"연결: {CurrentTool.Name}",
            _ => "선택"
        };
        SelectionText = SelectedShapes.Count switch
        {
            0 when SelectedConnector != null => "연결선 1개 선택",
            0 => "선택 없음",
            _ => $"{SelectedShapes.Count}개 요소 선택"
        };
        DiagramTypeText = CurrentDiagram == null ? "" : DiagramCatalog.DisplayName(CurrentDiagram.Type) + " 다이어그램";
        if (CurrentTool?.Mode == ToolMode.Shape) StatusText = $"캔버스를 클릭하여 {CurrentTool.Name}을(를) 배치합니다 (Esc: 취소)";
        else if (CurrentTool?.Mode == ToolMode.Connector) StatusText = $"시작 요소에서 끝 요소로 드래그하여 {CurrentTool.Name} 연결선을 만듭니다";
    }

    public void SetMouse(Point p) => MouseText = $"X: {p.X:0}  Y: {p.Y:0}";

    public void Invalidate() => DiagramChanged?.Invoke();

    // ===== undo =====
    private string Snapshot() => ProjectSerializer.SerializeCompact(Project!);

    public void PushUndo()
    {
        if (Project == null) return;
        _undo.Push(Snapshot());
        IsDirty = true;
        UndoCommand.NotifyCanExecuteChanged(); RedoCommand.NotifyCanExecuteChanged();
    }

    public void BeginPropertyEdit()
    {
        if (!_propEditOpen) { PushUndo(); _propEditOpen = true; }
        IsDirty = true;
    }

    public void AfterPropertyEdit(bool tree)
    {
        if (tree)
        {
            foreach (var t in OpenTabs) t.Refresh();
            RebuildBrowser();
        }
        Invalidate();
    }

    public void EndGesture() => _propEditOpen = false;

    private bool CanUndo() => _undo.CanUndo;
    private bool CanRedo() => _undo.CanRedo;

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo() => Restore(_undo.Undo(Snapshot()));

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo() => Restore(_undo.Redo(Snapshot()));

    private void Restore(string? json)
    {
        if (json == null || Project == null) return;
        var tabIds = OpenTabs.Select(t => t.Diagram.Id).ToList();
        var activeId = CurrentDiagram?.Id;
        var selIds = SelectedShapes.Select(s => s.Id).ToHashSet();
        var p = ProjectSerializer.Deserialize(json);
        Project = p;
        OpenTabs.Clear();
        foreach (var id in tabIds)
            if (p.FindDiagram(id) is { } d) OpenTabs.Add(new DiagramTab(d));
        _activeTab = OpenTabs.FirstOrDefault(t => t.Diagram.Id == activeId) ?? OpenTabs.FirstOrDefault();
        OnPropertyChanged(nameof(ActiveTab));
        foreach (var t in OpenTabs) t.IsActive = t == _activeTab;
        OnPropertyChanged(nameof(CurrentDiagram)); OnPropertyChanged(nameof(ShowDiagram)); OnPropertyChanged(nameof(Zoom)); OnPropertyChanged(nameof(ZoomText));
        RebuildBrowser();
        var shapes = CurrentDiagram?.Shapes.Where(s => selIds.Contains(s.Id)).ToList() ?? new();
        SelectedShapes.Clear(); SelectedShapes.AddRange(shapes); SelectedConnector = null;
        SyncSelection();
        IsDirty = true;
        UndoCommand.NotifyCanExecuteChanged(); RedoCommand.NotifyCanExecuteChanged();
        StatusText = "실행 취소/다시 실행 완료";
    }

    // ===== project commands =====
    public void LoadProject(UmlProject p, string? path)
    {
        Project = p;
        FilePath = path;
        OpenTabs.Clear();
        _undo.Clear();
        UndoCommand.NotifyCanExecuteChanged(); RedoCommand.NotifyCanExecuteChanged();
        BrowserRoots.Clear();
        BrowserFilter = "";
        RebuildBrowser();
        var first = p.AllDiagrams().FirstOrDefault();
        StartPageVisible = false;
        if (first != null) OpenDiagram(first); else ActiveTab = null;
        IsDirty = false;
        OnPropertyChanged(nameof(WindowTitle));
        StatusText = path == null ? $"프로젝트 '{p.Name}' 생성됨" : $"열림: {path}";
    }

    [RelayCommand]
    private async Task NewProject(string? template)
    {
        if (!await ConfirmDiscard()) return;
        var name = Dialogs == null ? "새 프로젝트" : await Dialogs.PromptAsync("새 프로젝트 — 모델 빌더", "프로젝트 이름", "새 프로젝트");
        if (string.IsNullOrWhiteSpace(name)) return;
        LoadProject(SampleProject.CreateTemplate(name.Trim(), template == "full"), null);
        IsDirty = true;
    }

    [RelayCommand]
    private async Task OpenDemo()
    {
        if (!await ConfirmDiscard()) return;
        LoadProject(SampleProject.CreateDemo(), null);
    }

    [RelayCommand]
    private async Task OpenProject()
    {
        if (Dialogs == null || !await ConfirmDiscard()) return;
        var path = await Dialogs.OpenProjectAsync();
        if (path != null) OpenPath(path);
    }

    [RelayCommand]
    private async Task OpenRecent(RecentFile? item)
    {
        if (item == null || !await ConfirmDiscard()) return;
        OpenPath(item.Path);
    }

    public void OpenPath(string path)
    {
        try
        {
            var p = ProjectSerializer.Deserialize(File.ReadAllText(path));
            LoadProject(p, path);
            AddRecent(path);
        }
        catch (Exception ex)
        {
            StatusText = "열기 실패: " + ex.Message;
        }
    }

    private void AddRecent(string path)
    {
        Recent.Clear();
        foreach (var r in RecentFiles.Add(path)) Recent.Add(r);
    }

    private async Task<bool> ConfirmDiscard()
    {
        if (Project == null || !IsDirty || Dialogs == null) return true;
        return await Dialogs.ConfirmAsync("저장되지 않은 변경", "현재 프로젝트의 변경 내용이 저장되지 않았습니다. 계속할까요?");
    }

    [RelayCommand]
    private async Task Save()
    {
        if (Project == null) return;
        if (FilePath == null) { await SaveAs(); return; }
        WriteProject(FilePath);
    }

    [RelayCommand]
    private async Task SaveAs()
    {
        if (Project == null || Dialogs == null) return;
        var path = await Dialogs.SaveFileAsync("다른 이름으로 저장", Project.Name + ".umlproj", "umlproj", "UML Studio 프로젝트");
        if (path == null) return;
        WriteProject(path);
    }

    private void WriteProject(string path)
    {
        try
        {
            File.WriteAllText(path, ProjectSerializer.Serialize(Project!));
            FilePath = path;
            IsDirty = false;
            AddRecent(path);
            StatusText = "저장됨: " + path;
        }
        catch (Exception ex) { StatusText = "저장 실패: " + ex.Message; }
    }

    [RelayCommand]
    private async Task CloseProject()
    {
        if (!await ConfirmDiscard()) return;
        Project = null; FilePath = null; OpenTabs.Clear(); ActiveTab = null; BrowserRoots.Clear();
        StartPageVisible = true; IsDirty = false;
        SelectedShapes.Clear(); SelectedConnector = null; SyncSelection();
    }

    [RelayCommand]
    private async Task ExportPng()
    {
        if (CurrentDiagram == null || Dialogs == null) return;
        var path = await Dialogs.SaveFileAsync("PNG로 내보내기", CurrentDiagram.Name + ".png", "png", "PNG 이미지");
        if (path == null) return;
        try { ExportService.ExportPng(CurrentDiagram, path); StatusText = "PNG 내보냄: " + path; }
        catch (Exception ex) { StatusText = "내보내기 실패: " + ex.Message; }
    }

    [RelayCommand]
    private async Task ExportSvg()
    {
        if (CurrentDiagram == null || Dialogs == null) return;
        var path = await Dialogs.SaveFileAsync("SVG로 내보내기", CurrentDiagram.Name + ".svg", "svg", "SVG 벡터");
        if (path == null) return;
        try { ExportService.ExportSvg(CurrentDiagram, path); StatusText = "SVG 내보냄: " + path; }
        catch (Exception ex) { StatusText = "내보내기 실패: " + ex.Message; }
    }

    [RelayCommand]
    private async Task ExportAllPng()
    {
        if (Project == null || Dialogs == null) return;
        var path = await Dialogs.SaveFileAsync("모든 다이어그램 PNG 내보내기 (폴더 선택용 파일명)", Project.Name + ".png", "png", "PNG 이미지");
        if (path == null) return;
        var dir = Path.GetDirectoryName(path)!;
        int n = 0;
        foreach (var d in Project.AllDiagrams())
        {
            var safe = string.Concat(d.Name.Select(ch => Path.GetInvalidFileNameChars().Contains(ch) ? '_' : ch));
            ExportService.ExportPng(d, Path.Combine(dir, safe + ".png")); n++;
        }
        StatusText = $"{n}개 다이어그램을 PNG로 내보냈습니다: {dir}";
    }

    // ===== model structure commands =====
    private UmlPackage TargetPackage(BrowserNode? node)
    {
        node ??= SelectedBrowserNode;
        if (node?.Package != null) return node.Package;
        if (node?.Diagram != null && Project!.OwnerOf(node.Diagram) is { } owner) return owner;
        if (CurrentDiagram != null && Project!.OwnerOf(CurrentDiagram) is { } o2) return o2;
        return Project!.Root.Packages.FirstOrDefault() ?? Project.Root;
    }

    [RelayCommand]
    private async Task AddPackage(BrowserNode? node)
    {
        if (Project == null) return;
        var parent = TargetPackage(node);
        var name = Dialogs == null ? "새 패키지" : await Dialogs.PromptAsync("새 패키지", "패키지 이름", "새 패키지");
        if (string.IsNullOrWhiteSpace(name)) return;
        PushUndo();
        var pkg = new UmlPackage { Name = name.Trim() };
        parent.Packages.Add(pkg);
        RebuildBrowser();
        var n = AllNodes().FirstOrDefault(x => x.Package == pkg);
        if (n != null) { for (var p = n.Parent; p != null; p = p.Parent) p.IsExpanded = true; SelectedBrowserNode = n; }
        StatusText = $"패키지 '{pkg.Name}' 추가됨";
    }

    [RelayCommand]
    private void AddDiagram(DiagramType type)
    {
        if (Project == null) return;
        var pkg = TargetPackage(null);
        PushUndo();
        var count = Project.AllDiagrams().Count(d => d.Type == type) + 1;
        var d = new UmlDiagram { Name = $"{DiagramCatalog.DisplayName(type)} 다이어그램 {count}", Type = type };
        pkg.Diagrams.Add(d);
        RebuildBrowser();
        foreach (var n in AllNodes().Where(n => n.Package == pkg)) n.IsExpanded = true;
        OpenDiagram(d);
        StatusText = $"'{d.Name}' 생성됨 — 도구상자에서 요소를 선택하세요";
    }

    [RelayCommand]
    private async Task RenameNode(BrowserNode? node)
    {
        node ??= SelectedBrowserNode;
        if (node == null || Dialogs == null) return;
        if (node.Kind == BrowserNodeKind.Element && node.Glyph.StartsWith("feature")) return;
        var name = await Dialogs.PromptAsync("이름 바꾸기", "새 이름", node.Name);
        if (string.IsNullOrWhiteSpace(name)) return;
        PushUndo();
        if (node.Package != null) node.Package.Name = name;
        else if (node.Kind == BrowserNodeKind.Diagram && node.Diagram != null) node.Diagram.Name = name;
        else if (node.Shape != null)
        {
            var old = node.Shape.Name;
            foreach (var s in Project!.AllDiagrams().SelectMany(d => d.Shapes).Where(s => s.Name == old && s.Kind == node.Shape.Kind)) s.Name = name;
        }
        if (Project!.Root == node.Package) Project.Name = name;
        foreach (var t in OpenTabs) t.Refresh();
        OnPropertyChanged(nameof(WindowTitle));
        RebuildBrowser();
        Properties.Load();
        Invalidate();
    }

    [RelayCommand]
    private async Task DeleteNode(BrowserNode? node)
    {
        node ??= SelectedBrowserNode;
        if (node == null || Project == null || node.Kind == BrowserNodeKind.Root) return;
        if (Dialogs != null && !await Dialogs.ConfirmAsync("삭제", $"'{node.Name}'을(를) 삭제할까요?")) return;
        PushUndo();
        switch (node.Kind)
        {
            case BrowserNodeKind.Package:
                var parent = Project.ParentOf(node.Package!);
                parent?.Packages.Remove(node.Package!);
                var gone = new HashSet<UmlDiagram>(Project.AllDiagrams());
                foreach (var t in OpenTabs.Where(t => !gone.Contains(t.Diagram)).ToList()) CloseTab(t);
                break;
            case BrowserNodeKind.Diagram:
                Project.OwnerOf(node.Diagram!)?.Diagrams.Remove(node.Diagram!);
                foreach (var t in OpenTabs.Where(t => t.Diagram == node.Diagram).ToList()) CloseTab(t);
                break;
            case BrowserNodeKind.Element when node.Shape != null && node.Diagram != null:
                RemoveShapes(node.Diagram, new[] { node.Shape });
                break;
        }
        FocusPackage = null;
        RebuildBrowser();
        SelectedShapes.Clear(); SelectedConnector = null; SyncSelection();
    }

    [RelayCommand]
    private void OpenNodeCmd(BrowserNode? node) => OpenNode(node ?? SelectedBrowserNode);

    // ===== editing commands =====
    public UmlShape PlaceShape(ShapeKind kind, Point at)
    {
        var d = CurrentDiagram!;
        PushUndo();
        var (w, h) = DiagramCatalog.DefaultSize(kind);
        var (fill, stroke) = DiagramCatalog.DefaultColors(kind);
        var baseName = DiagramCatalog.DefaultName(kind);
        var n = d.Shapes.Count(s => s.Kind == kind) + 1;
        var s = new UmlShape
        {
            Kind = kind,
            Name = string.IsNullOrEmpty(baseName) ? "" : (kind is ShapeKind.Note ? baseName : $"{baseName}{n}"),
            X = Snap(at.X - w / 2), Y = Snap(at.Y - h / 2), Width = w, Height = h,
            Stereotype = DiagramCatalog.DefaultStereotype(kind),
            Z = (d.Shapes.Count == 0 ? 0 : d.Shapes.Max(x => x.Z)) + 1,
            Notes = kind == ShapeKind.Note ? "노트를 입력하세요" : ""
        };
        if (kind is ShapeKind.SystemBoundary or ShapeKind.Package or ShapeKind.Node) s.Z = (d.Shapes.Count == 0 ? 0 : d.Shapes.Min(x => x.Z)) - 1;
        if (kind is ShapeKind.Class) { s.Attributes.Add("- id: Long"); s.Operations.Add("+ operation(): void"); }
        if (kind is ShapeKind.Interface) s.Operations.Add("+ operation(): void");
        if (kind is ShapeKind.Enumeration) { s.Attributes.Add("VALUE_A"); s.Attributes.Add("VALUE_B"); }
        if (kind is ShapeKind.ObjectLifeline or ShapeKind.ActorLifeline)
        {
            s.Y = Math.Max(20, Snap(at.Y - 20));
            if (kind == ShapeKind.ObjectLifeline) s.Name = ":Object" + n;
            var others = d.Shapes.Where(x => DiagramRenderer.IsLifeline(x.Kind)).ToList();
            if (others.Count > 0) { s.Y = others[0].Y + (others[0].Height - s.Height); s.LifelineLength = others[0].LifelineLength + (others[0].Height - s.Height); }
        }
        DiagramRenderer.AutoFit(s, false);
        d.Shapes.Add(s);
        if (!StickyTool) ResetTool();
        RebuildBrowser();
        SetSelection(new[] { s }, null);
        StatusText = $"{DiagramCatalog.ShapeName(kind)} 추가됨";
        return s;
    }

    public UmlConnector? CreateConnector(ConnectorKind kind, UmlShape from, UmlShape to, double y)
    {
        var d = CurrentDiagram!;
        if (from == to && d.Type != DiagramType.Sequence && kind != ConnectorKind.Transition) return null;
        PushUndo();
        var c = new UmlConnector
        {
            Kind = kind, FromId = from.Id, ToId = to.Id,
            Stereotype = DiagramCatalog.DefaultStereotype(kind),
            Route = d.Type is DiagramType.Class or DiagramType.Component ? RouteStyle.Orthogonal : RouteStyle.Direct
        };
        if (d.Type == DiagramType.Sequence)
        {
            c.FromY = c.ToY = Math.Round(Math.Max(y, Math.Max(from.Bottom, to.Bottom) + 20));
            c.Label = kind switch
            {
                ConnectorKind.MessageReturn => "result",
                ConnectorKind.CreateMessage => "create()",
                ConnectorKind.DestroyMessage => "destroy()",
                _ => "message()"
            };
        }
        d.Connectors.Add(c);
        if (!StickyTool) ResetTool();
        SetSelection(Array.Empty<UmlShape>(), c);
        StatusText = $"{DiagramCatalog.ConnectorName(kind)} 연결선 생성: {from.Name} → {to.Name}";
        return c;
    }

    public double Snap(double v) => SnapToGrid ? Math.Round(v / GridSize) * GridSize : Math.Round(v);

    private void RemoveShapes(UmlDiagram d, IEnumerable<UmlShape> shapes)
    {
        var ids = shapes.Select(s => s.Id).ToHashSet();
        d.Shapes.RemoveAll(s => ids.Contains(s.Id));
        d.Connectors.RemoveAll(c => ids.Contains(c.FromId) || ids.Contains(c.ToId));
    }

    [RelayCommand]
    private void DeleteSelected()
    {
        var d = CurrentDiagram;
        if (d == null) return;
        if (SelectedShapes.Count > 0)
        {
            PushUndo();
            var n = SelectedShapes.Count;
            RemoveShapes(d, SelectedShapes.ToList());
            SelectedShapes.Clear();
            RebuildBrowser();
            StatusText = $"{n}개 요소 삭제됨";
        }
        else if (SelectedConnector != null)
        {
            PushUndo();
            d.Connectors.Remove(SelectedConnector);
            SelectedConnector = null;
            StatusText = "연결선 삭제됨";
        }
        SyncSelection();
    }

    [RelayCommand]
    private void SelectAll()
    {
        if (CurrentDiagram == null) return;
        SetSelection(CurrentDiagram.Shapes, null);
    }

    [RelayCommand]
    private void Duplicate()
    {
        var d = CurrentDiagram;
        if (d == null || SelectedShapes.Count == 0) return;
        PushUndo();
        var copies = new List<UmlShape>();
        var maxZ = d.Shapes.Max(s => s.Z);
        foreach (var s in SelectedShapes)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(s, ProjectSerializer.Options);
            var c = System.Text.Json.JsonSerializer.Deserialize<UmlShape>(json, ProjectSerializer.Options)!;
            c.Id = Guid.NewGuid().ToString("N");
            c.X += 30; c.Y += 30; c.Z = ++maxZ;
            if (!string.IsNullOrEmpty(c.Name)) c.Name += " 복사본";
            d.Shapes.Add(c); copies.Add(c);
        }
        RebuildBrowser();
        SetSelection(copies, null);
    }

    [RelayCommand]
    private void BringToFront()
    {
        var d = CurrentDiagram;
        if (d == null || SelectedShapes.Count == 0) return;
        PushUndo();
        var z = d.Shapes.Max(s => s.Z);
        foreach (var s in SelectedShapes) s.Z = ++z;
        Invalidate();
    }

    [RelayCommand]
    private void SendToBack()
    {
        var d = CurrentDiagram;
        if (d == null || SelectedShapes.Count == 0) return;
        PushUndo();
        var z = d.Shapes.Min(s => s.Z);
        foreach (var s in SelectedShapes) s.Z = --z;
        Invalidate();
    }

    [RelayCommand]
    private void Align(string? mode)
    {
        if (SelectedShapes.Count < 2 || mode == null) { StatusText = "정렬하려면 요소를 2개 이상 선택하세요 (Ctrl+클릭 또는 드래그 선택)"; return; }
        PushUndo();
        var l = SelectedShapes;
        var anchor = l[0];
        switch (mode)
        {
            case "left": foreach (var s in l) s.X = anchor.X; break;
            case "center": foreach (var s in l) s.X = anchor.CenterX - s.Width / 2; break;
            case "right": foreach (var s in l) s.X = anchor.Right - s.Width; break;
            case "top": foreach (var s in l) s.Y = anchor.Y; break;
            case "middle": foreach (var s in l) s.Y = anchor.CenterY - s.Height / 2; break;
            case "bottom": foreach (var s in l) s.Y = anchor.Bottom - s.Height; break;
            case "width": foreach (var s in l) s.Width = anchor.Width; break;
            case "height": foreach (var s in l) s.Height = anchor.Height; break;
            case "hspace":
            {
                var o = l.OrderBy(s => s.X).ToList();
                if (o.Count < 3) break;
                var total = o[^1].Right - o[0].X - o.Sum(s => s.Width);
                var gap = total / (o.Count - 1);
                var x = o[0].X;
                foreach (var s in o) { s.X = x; x += s.Width + gap; }
                break;
            }
            case "vspace":
            {
                var o = l.OrderBy(s => s.Y).ToList();
                if (o.Count < 3) break;
                var total = o[^1].Bottom - o[0].Y - o.Sum(s => s.Height);
                var gap = total / (o.Count - 1);
                var y = o[0].Y;
                foreach (var s in o) { s.Y = y; y += s.Height + gap; }
                break;
            }
        }
        Properties.Load();
        Invalidate();
        StatusText = "정렬 완료";
    }

    [RelayCommand]
    private void AutoSize()
    {
        if (CurrentDiagram == null) return;
        PushUndo();
        var targets = SelectedShapes.Count > 0 ? SelectedShapes : CurrentDiagram.Shapes;
        foreach (var s in targets)
        {
            var rh = DiagramRenderer.RequiredHeight(s);
            if (rh > 0) s.Height = Math.Ceiling(rh);
            var rw = DiagramRenderer.RequiredWidth(s);
            if (rw > 0) s.Width = Math.Max(Math.Ceiling(rw / 10) * 10, 120);
        }
        Properties.Load();
        Invalidate();
    }

    [RelayCommand]
    private void SetRoute(string? style)
    {
        var d = CurrentDiagram;
        if (d == null) return;
        PushUndo();
        var r = style == "ortho" ? RouteStyle.Orthogonal : RouteStyle.Direct;
        var targets = SelectedConnector != null ? new List<UmlConnector> { SelectedConnector }
            : SelectedShapes.Count > 0 ? d.Connectors.Where(c => SelectedShapes.Any(s => s.Id == c.FromId || s.Id == c.ToId)).ToList()
            : d.Connectors;
        foreach (var c in targets) c.Route = r;
        Properties.Load();
        Invalidate();
        StatusText = r == RouteStyle.Orthogonal ? "직교 라우팅 적용" : "직선 라우팅 적용";
    }

    [RelayCommand]
    private void EditProperties()
    {
        Dialogs?.FocusPropertyName();
    }

    [RelayCommand]
    private void Find() => Dialogs?.FocusSearch();

    [RelayCommand]
    private void DiagramProperties()
    {
        SetSelection(Array.Empty<UmlShape>(), null);
        Dialogs?.FocusPropertyName();
    }

    [RelayCommand]
    private void ToggleFrame()
    {
        if (CurrentDiagram == null) return;
        CurrentDiagram.ShowFrame = !CurrentDiagram.ShowFrame;
        Invalidate();
    }

    // ===== zoom =====
    [RelayCommand] private void ZoomIn() => Zoom = Math.Round(Zoom * 1.2, 2);
    [RelayCommand] private void ZoomOut() => Zoom = Math.Round(Zoom / 1.2, 2);
    [RelayCommand] private void ZoomReset() { Zoom = 1; if (CurrentDiagram != null) { CurrentDiagram.PanX = 0; CurrentDiagram.PanY = 0; } Invalidate(); }
    [RelayCommand] private void ZoomFit() => FitRequested?.Invoke();
    public void NotifyZoom() { OnPropertyChanged(nameof(Zoom)); OnPropertyChanged(nameof(ZoomText)); }
}
