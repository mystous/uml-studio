using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using UmlStudio.Models;
using UmlStudio.Rendering;
using UmlStudio.ViewModels;

namespace UmlStudio.Controls;

/// <summary>Custom-drawn diagram view: zoom/pan, rubber-band, multi-select, move, resize, connect.</summary>
public sealed class DiagramCanvas : Control
{
    public static readonly StyledProperty<MainWindowViewModel?> ViewModelProperty =
        AvaloniaProperty.Register<DiagramCanvas, MainWindowViewModel?>(nameof(ViewModel));

    public MainWindowViewModel? ViewModel { get => GetValue(ViewModelProperty); set => SetValue(ViewModelProperty, value); }

    private enum Drag { None, Pan, Move, Resize, Band, Connect, Message }
    private Drag _drag;
    private Point _pressScreen, _pressWorld, _curWorld;
    private Point _panStart;
    private bool _moved, _spaceDown;
    private Dictionary<UmlShape, Point> _origPos = new();
    private UmlShape? _resizeShape;
    private int _handle;
    private Rect _origRect;
    private UmlShape? _connectFrom, _connectTarget;
    private UmlConnector? _message;
    private double _origMsgY;

    private static readonly Color Paper = Color.Parse("#FFFFFF");
    private static readonly Color GridMinor = Color.Parse("#EEF1F6");
    private static readonly Color GridMajor = Color.Parse("#DDE3EC");

    public DiagramCanvas()
    {
        ClipToBounds = true;
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.Arrow);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ViewModelProperty)
        {
            if (change.OldValue is MainWindowViewModel o) { o.DiagramChanged -= InvalidateVisual; o.FitRequested -= FitToContent; }
            if (change.NewValue is MainWindowViewModel n) { n.DiagramChanged += InvalidateVisual; n.FitRequested += FitToContent; }
            InvalidateVisual();
        }
    }

    private UmlDiagram? D => ViewModel?.CurrentDiagram;
    private double Zoom => D?.Zoom ?? 1;
    private Point Pan => D == null ? default : new Point(D.PanX, D.PanY);
    private Point ToWorld(Point screen) => new((screen.X - Pan.X) / Zoom, (screen.Y - Pan.Y) / Zoom);

    // ===================== rendering =====================
    public override void Render(DrawingContext ctx)
    {
        var b = new Rect(Bounds.Size);
        ctx.FillRectangle(new SolidColorBrush(Paper), b);
        var d = D;
        var vm = ViewModel;
        if (d == null || vm == null) return;
        var z = Zoom;

        if (vm.ShowGrid) DrawGrid(ctx, b, z);

        using (ctx.PushTransform(Matrix.CreateScale(z, z) * Matrix.CreateTranslation(Pan.X, Pan.Y)))
        {
            var g = new AvaloniaSurface(ctx);
            DiagramRenderer.Render(g, d, vm.RenderState, d.ShowFrame && d.Shapes.Count > 0);

            if (_drag == Drag.Band)
            {
                var r = new Rect(_pressWorld, _curWorld).Normalize();
                g.Rect(r, Color.FromArgb(40, 47, 111, 208), DiagramRenderer.Accent, 1 / z, 0, true);
            }
            if (_drag == Drag.Connect && _connectFrom != null)
            {
                var start = d.Type == DiagramType.Sequence ? new Point(_connectFrom.CenterX, _pressWorld.Y) : _connectFrom.Bounds().Center;
                var end = _curWorld;
                if (_connectTarget != null)
                {
                    g.Rect(_connectTarget.Bounds().Inflate(3), null, Color.Parse("#2E9E5B"), 2 / z, 3);
                    if (d.Type == DiagramType.Sequence) end = new Point(_connectTarget.CenterX, _pressWorld.Y);
                }
                g.Line(start, end, DiagramRenderer.Accent, 1.5 / z, true);
                g.Ellipse(new Rect(end.X - 3, end.Y - 3, 6, 6), DiagramRenderer.Accent, null);
            }
            if (vm.CurrentTool?.Mode == ToolMode.Shape && _drag == Drag.None && IsPointerOver)
            {
                var (w, h) = DiagramCatalog.DefaultSize(vm.CurrentTool.Shape);
                var r = new Rect(vm.Snap(_curWorld.X - w / 2), vm.Snap(_curWorld.Y - h / 2), w, h);
                g.Rect(r, Color.FromArgb(28, 47, 111, 208), DiagramRenderer.Accent, 1 / z, 2, true);
            }
        }

        if (d.Shapes.Count == 0)
        {
            var t1 = TextMetrics.Format("빈 다이어그램", 18, Color.Parse("#9AA6B8"), true, false);
            var t2 = TextMetrics.Format("왼쪽 도구상자에서 요소를 선택한 뒤 캔버스를 클릭하여 배치하세요", 12, Color.Parse("#9AA6B8"), false, false);
            ctx.DrawText(t1, new Point((b.Width - t1.Width) / 2, b.Height / 2 - 30));
            ctx.DrawText(t2, new Point((b.Width - t2.Width) / 2, b.Height / 2));
        }
    }

    private void DrawGrid(DrawingContext ctx, Rect b, double z)
    {
        var step = 10 * z;
        var minor = new Pen(new SolidColorBrush(GridMinor), 1);
        var major = new Pen(new SolidColorBrush(GridMajor), 1);
        var ox = Pan.X % (step * 5); var oy = Pan.Y % (step * 5);
        if (step >= 6)
        {
            var mx = Pan.X % step; var my = Pan.Y % step;
            for (double x = mx; x < b.Width; x += step) ctx.DrawLine(minor, new Point(Math.Round(x) + 0.5, 0), new Point(Math.Round(x) + 0.5, b.Height));
            for (double y = my; y < b.Height; y += step) ctx.DrawLine(minor, new Point(0, Math.Round(y) + 0.5), new Point(b.Width, Math.Round(y) + 0.5));
        }
        for (double x = ox; x < b.Width; x += step * 5) ctx.DrawLine(major, new Point(Math.Round(x) + 0.5, 0), new Point(Math.Round(x) + 0.5, b.Height));
        for (double y = oy; y < b.Height; y += step * 5) ctx.DrawLine(major, new Point(0, Math.Round(y) + 0.5), new Point(b.Width, Math.Round(y) + 0.5));
    }

    // ===================== hit testing =====================
    private UmlShape? HitShape(Point p, UmlShape? except = null)
    {
        var d = D;
        if (d == null) return null;
        var tol = 4 / Zoom;
        foreach (var s in d.Shapes.Where(s => !DiagramRenderer.IsContainer(s.Kind)).OrderByDescending(s => s.Z))
        {
            if (s == except) continue;
            if (Hit(s, p, tol)) return s;
        }
        foreach (var s in d.Shapes.Where(s => DiagramRenderer.IsContainer(s.Kind)).OrderByDescending(s => s.Z))
        {
            if (s == except) continue;
            if (Hit(s, p, tol)) return s;
        }
        return null;
    }

    private static bool Hit(UmlShape s, Point p, double tol)
    {
        var r = new Rect(s.X, s.Y, s.Width, s.Height).Inflate(tol);
        if (r.Contains(p)) return true;
        if (DiagramRenderer.IsLifeline(s.Kind))
            return Math.Abs(p.X - s.CenterX) <= 7 + tol && p.Y >= s.Bottom && p.Y <= s.Bottom + s.LifelineLength;
        if (s.Kind == ShapeKind.Actor) return new Rect(s.CenterX - 40, s.Bottom, 80, 18).Contains(p);
        return false;
    }

    private UmlConnector? HitConnector(Point p)
    {
        var d = D;
        if (d == null) return null;
        var map = d.Shapes.ToDictionary(s => s.Id);
        var act = d.Type == DiagramType.Sequence ? DiagramRenderer.ComputeActivations(d, map) : null;
        UmlConnector? best = null; double bestD = 6 / Zoom;
        foreach (var c in d.Connectors)
        {
            var path = DiagramRenderer.ConnectorPath(d, c, map, act);
            if (path.Count < 2) continue;
            var dist = DiagramRenderer.DistToPath(p, path);
            if (dist < bestD) { bestD = dist; best = c; }
        }
        return best;
    }

    private int HitHandle(Point p)
    {
        var vm = ViewModel;
        if (vm == null || vm.SelectedShapes.Count != 1) return -1;
        var s = vm.SelectedShapes[0];
        if (s.Kind is ShapeKind.InitialNode or ShapeKind.InitialState or ShapeKind.FinalNode or ShapeKind.FinalState) return -1;
        var pts = DiagramRenderer.HandlePoints(new Rect(s.X, s.Y, s.Width, s.Height));
        var tol = 6 / Zoom;
        for (int i = 0; i < pts.Length; i++)
            if (Math.Abs(pts[i].X - p.X) <= tol && Math.Abs(pts[i].Y - p.Y) <= tol) return i;
        return -1;
    }

    private static readonly StandardCursorType[] HandleCursors =
    {
        StandardCursorType.TopLeftCorner, StandardCursorType.TopSide, StandardCursorType.TopRightCorner, StandardCursorType.RightSide,
        StandardCursorType.BottomRightCorner, StandardCursorType.BottomSide, StandardCursorType.BottomLeftCorner, StandardCursorType.LeftSide
    };

    // ===================== input =====================
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var vm = ViewModel; var d = D;
        if (vm == null || d == null) return;
        Focus();
        var props = e.GetCurrentPoint(this).Properties;
        _pressScreen = e.GetPosition(this);
        _pressWorld = _curWorld = ToWorld(_pressScreen);
        _moved = false;
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        if (props.IsMiddleButtonPressed || (props.IsLeftButtonPressed && _spaceDown))
        {
            _drag = Drag.Pan; _panStart = Pan;
            Cursor = new Cursor(StandardCursorType.SizeAll);
            e.Pointer.Capture(this); e.Handled = true;
            return;
        }

        if (props.IsRightButtonPressed)
        {
            var hs = HitShape(_pressWorld);
            if (hs != null) { if (!vm.SelectedShapes.Contains(hs)) vm.SetSelection(new[] { hs }, null); }
            else if (HitConnector(_pressWorld) is { } hc) vm.SetSelection(Array.Empty<UmlShape>(), hc);
            else vm.SetSelection(Array.Empty<UmlShape>(), null);
            BuildContextMenu().Open(this);
            e.Handled = true;
            return;
        }
        if (!props.IsLeftButtonPressed) return;

        var tool = vm.CurrentTool;
        if (tool?.Mode == ToolMode.Shape)
        {
            vm.PlaceShape(tool.Shape, _pressWorld);
            e.Handled = true;
            return;
        }
        if (tool?.Mode == ToolMode.Connector)
        {
            var from = HitShape(_pressWorld);
            if (from != null)
            {
                _drag = Drag.Connect; _connectFrom = from; _connectTarget = null;
                e.Pointer.Capture(this);
            }
            else vm.StatusText = "연결선은 요소 위에서 시작해야 합니다";
            e.Handled = true;
            return;
        }

        // select tool
        var h = HitHandle(_pressWorld);
        if (h >= 0)
        {
            _drag = Drag.Resize; _handle = h; _resizeShape = vm.SelectedShapes[0];
            _origRect = new Rect(_resizeShape.X, _resizeShape.Y, _resizeShape.Width, _resizeShape.Height);
            e.Pointer.Capture(this); e.Handled = true;
            return;
        }
        var shape = HitShape(_pressWorld);
        if (shape != null)
        {
            if (e.ClickCount >= 2) { vm.SetSelection(new[] { shape }, null); vm.EditPropertiesCommand.Execute(null); e.Handled = true; return; }
            if (ctrl)
            {
                var list = vm.SelectedShapes.ToList();
                if (!list.Remove(shape)) list.Add(shape);
                vm.SetSelection(list, null);
                if (!list.Contains(shape)) { e.Handled = true; return; }
            }
            else if (!vm.SelectedShapes.Contains(shape)) vm.SetSelection(new[] { shape }, null);
            _drag = Drag.Move;
            _origPos = vm.SelectedShapes.ToDictionary(s => s, s => new Point(s.X, s.Y));
            // moving a container drags its contents
            foreach (var c in vm.SelectedShapes.Where(s => DiagramRenderer.IsContainer(s.Kind)).ToList())
                foreach (var inner in d.Shapes.Where(s => s != c && !_origPos.ContainsKey(s) && new Rect(c.X, c.Y, c.Width, c.Height).Contains(new Rect(s.X, s.Y, s.Width, s.Height))))
                    _origPos[inner] = new Point(inner.X, inner.Y);
            e.Pointer.Capture(this); e.Handled = true;
            return;
        }
        var conn = HitConnector(_pressWorld);
        if (conn != null)
        {
            vm.SetSelection(Array.Empty<UmlShape>(), conn);
            if (e.ClickCount >= 2) vm.EditPropertiesCommand.Execute(null);
            if (d.Type == DiagramType.Sequence && DiagramRenderer.IsMessage(conn))
            {
                _drag = Drag.Message; _message = conn; _origMsgY = conn.FromY;
                e.Pointer.Capture(this);
            }
            e.Handled = true;
            return;
        }
        if (!ctrl) vm.SetSelection(Array.Empty<UmlShape>(), null);
        _drag = Drag.Band;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var vm = ViewModel; var d = D;
        if (vm == null || d == null) return;
        var screen = e.GetPosition(this);
        _curWorld = ToWorld(screen);
        vm.SetMouse(_curWorld);

        switch (_drag)
        {
            case Drag.Pan:
                d.PanX = _panStart.X + (screen.X - _pressScreen.X);
                d.PanY = _panStart.Y + (screen.Y - _pressScreen.Y);
                InvalidateVisual();
                return;
            case Drag.Move:
            {
                var dx = _curWorld.X - _pressWorld.X; var dy = _curWorld.Y - _pressWorld.Y;
                if (!_moved && Math.Abs(dx) < 2 / Zoom && Math.Abs(dy) < 2 / Zoom) return;
                if (!_moved) { vm.PushUndo(); _moved = true; }
                var primary = vm.SelectedShapes.FirstOrDefault() ?? _origPos.Keys.First();
                var o = _origPos[primary];
                var nx = vm.Snap(o.X + dx); var ny = vm.Snap(o.Y + dy);
                var sdx = nx - o.X; var sdy = ny - o.Y;
                bool lifelinesOnly = _origPos.Keys.All(s => DiagramRenderer.IsLifeline(s.Kind));
                foreach (var (s, p) in _origPos)
                {
                    s.X = p.X + sdx;
                    if (!lifelinesOnly) s.Y = p.Y + sdy;
                }
                vm.Properties.RefreshGeometry();
                vm.StatusText = $"이동: {primary.Name}  ({primary.X:0}, {primary.Y:0})";
                InvalidateVisual();
                return;
            }
            case Drag.Resize when _resizeShape != null:
            {
                if (!_moved) { vm.PushUndo(); _moved = true; }
                var r = _origRect; var p = new Point(vm.Snap(_curWorld.X), vm.Snap(_curWorld.Y));
                double l = r.X, t = r.Y, rr = r.Right, bb = r.Bottom;
                if (_handle is 0 or 6 or 7) l = Math.Min(p.X, rr - 16);
                if (_handle is 2 or 3 or 4) rr = Math.Max(p.X, l + 16);
                if (_handle is 0 or 1 or 2) t = Math.Min(p.Y, bb - 8);
                if (_handle is 4 or 5 or 6) bb = Math.Max(p.Y, t + 8);
                _resizeShape.X = l; _resizeShape.Y = t; _resizeShape.Width = rr - l; _resizeShape.Height = bb - t;
                vm.Properties.RefreshGeometry();
                vm.StatusText = $"크기: {_resizeShape.Width:0} × {_resizeShape.Height:0}";
                InvalidateVisual();
                return;
            }
            case Drag.Band:
                InvalidateVisual();
                return;
            case Drag.Connect:
                _connectTarget = HitShape(_curWorld);
                if (_connectTarget == _connectFrom && d.Type != DiagramType.Sequence && vm.CurrentTool?.Connector != ConnectorKind.Transition) _connectTarget = null;
                InvalidateVisual();
                return;
            case Drag.Message when _message != null:
            {
                var ny = Math.Round(_origMsgY + (_curWorld.Y - _pressWorld.Y));
                if (!_moved && Math.Abs(ny - _origMsgY) < 2) return;
                if (!_moved) { vm.PushUndo(); _moved = true; }
                _message.FromY = _message.ToY = vm.SnapToGrid ? Math.Round(ny / 5) * 5 : ny;
                vm.StatusText = $"메시지 위치 Y={_message.FromY:0}";
                InvalidateVisual();
                return;
            }
        }

        // hover feedback
        if (vm.CurrentTool?.Mode == ToolMode.Shape) { Cursor = new Cursor(StandardCursorType.Cross); InvalidateVisual(); return; }
        if (vm.CurrentTool?.Mode == ToolMode.Connector) Cursor = new Cursor(StandardCursorType.Hand);
        else
        {
            var h = HitHandle(_curWorld);
            Cursor = new Cursor(h >= 0 ? HandleCursors[h] : _spaceDown ? StandardCursorType.SizeAll : StandardCursorType.Arrow);
        }
        var hover = HitShape(_curWorld)?.Id;
        if (hover != vm.RenderState.HoverShape) { vm.RenderState.HoverShape = hover; InvalidateVisual(); }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var vm = ViewModel; var d = D;
        if (vm == null || d == null) { _drag = Drag.None; return; }
        switch (_drag)
        {
            case Drag.Band:
            {
                var r = new Rect(_pressWorld, _curWorld).Normalize();
                if (r.Width > 3 || r.Height > 3)
                {
                    var hits = d.Shapes.Where(s => r.Contains(new Rect(s.X, s.Y, s.Width, s.Height)) || (r.Intersects(new Rect(s.X, s.Y, s.Width, s.Height)) && !DiagramRenderer.IsContainer(s.Kind) && r.Width * r.Height > 0 && IsMostlyInside(r, s)));
                    var keep = e.KeyModifiers.HasFlag(KeyModifiers.Control) ? vm.SelectedShapes.ToList() : new List<UmlShape>();
                    vm.SetSelection(keep.Concat(hits), null);
                }
                break;
            }
            case Drag.Connect when _connectFrom != null:
            {
                var target = HitShape(_curWorld);
                if (target != null && vm.CurrentTool?.Mode == ToolMode.Connector)
                    vm.CreateConnector(vm.CurrentTool.Connector, _connectFrom, target, _pressWorld.Y);
                break;
            }
            case Drag.Move or Drag.Resize when _moved:
                vm.Properties.RefreshGeometry();
                vm.StatusText = "준비";
                break;
        }
        _drag = Drag.None; _connectFrom = null; _connectTarget = null; _message = null; _resizeShape = null;
        vm.EndGesture();
        Cursor = new Cursor(StandardCursorType.Arrow);
        e.Pointer.Capture(null);
        InvalidateVisual();
    }

    private static bool IsMostlyInside(Rect band, UmlShape s)
    {
        var sr = new Rect(s.X, s.Y, s.Width, s.Height);
        var i = band.Intersect(sr);
        return i.Width * i.Height >= sr.Width * sr.Height * 0.6;
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        if (ViewModel != null && ViewModel.RenderState.HoverShape != null) { ViewModel.RenderState.HoverShape = null; }
        InvalidateVisual();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var vm = ViewModel; var d = D;
        if (vm == null || d == null) return;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            var screen = e.GetPosition(this);
            var world = ToWorld(screen);
            var nz = Math.Clamp(Math.Round(d.Zoom * (e.Delta.Y > 0 ? 1.1 : 1 / 1.1), 3), 0.2, 4);
            d.Zoom = nz;
            d.PanX = screen.X - world.X * nz;
            d.PanY = screen.Y - world.Y * nz;
            vm.NotifyZoom();
        }
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) d.PanX += e.Delta.Y * 50;
        else { d.PanY += e.Delta.Y * 50; d.PanX += e.Delta.X * 50; }
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        var vm = ViewModel;
        if (vm == null) return;
        switch (e.Key)
        {
            case Key.Space: _spaceDown = true; Cursor = new Cursor(StandardCursorType.SizeAll); e.Handled = true; break;
            case Key.Escape:
                vm.ResetTool();
                vm.SetSelection(Array.Empty<UmlShape>(), null);
                _drag = Drag.None;
                e.Handled = true; break;
            case Key.F2: vm.EditPropertiesCommand.Execute(null); e.Handled = true; break;
            case Key.Left or Key.Right or Key.Up or Key.Down when vm.SelectedShapes.Count > 0:
            {
                var step = e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? 1 : vm.GridSize;
                vm.PushUndo();
                foreach (var s in vm.SelectedShapes)
                {
                    if (e.Key == Key.Left) s.X -= step;
                    if (e.Key == Key.Right) s.X += step;
                    if (e.Key == Key.Up) s.Y -= step;
                    if (e.Key == Key.Down) s.Y += step;
                }
                vm.Properties.RefreshGeometry();
                InvalidateVisual();
                e.Handled = true;
                break;
            }
        }
    }

    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (e.Key == Key.Space) { _spaceDown = false; Cursor = new Cursor(StandardCursorType.Arrow); }
    }

    public void FitToContent()
    {
        var d = D; var vm = ViewModel;
        if (d == null || vm == null || Bounds.Width < 10) return;
        var c = DiagramRenderer.ContentBounds(d).Inflate(40);
        var z = Math.Clamp(Math.Min(Bounds.Width / c.Width, Bounds.Height / c.Height), 0.2, 1.5);
        d.Zoom = Math.Round(z, 3);
        d.PanX = (Bounds.Width - c.Width * z) / 2 - c.X * z;
        d.PanY = (Bounds.Height - c.Height * z) / 2 - c.Y * z;
        vm.NotifyZoom();
        InvalidateVisual();
    }

    // ===================== context menu =====================
    private static object? Icon(string key)
    {
        if (Application.Current?.TryFindResource("Ic." + key, out var g) == true && g is Geometry geo)
            return new PathIcon { Data = geo, Width = 14, Height = 14 };
        return null;
    }

    private static MenuItem Item(string header, System.Windows.Input.ICommand cmd, object? param = null, string? icon = null, string? gesture = null)
    {
        var mi = new MenuItem { Header = header, Command = cmd, CommandParameter = param, Icon = icon == null ? null : Icon(icon) };
        if (gesture != null) mi.InputGesture = KeyGesture.Parse(gesture);
        return mi;
    }

    private ContextMenu BuildContextMenu()
    {
        var vm = ViewModel!;
        var items = new List<Control>();
        if (vm.SelectedShapes.Count > 0)
        {
            items.Add(Item("속성 편집", vm.EditPropertiesCommand, null, "TableEdit", "F2"));
            items.Add(new Separator());
            items.Add(Item("맨 앞으로 가져오기", vm.BringToFrontCommand, null, "ArrangeBringToFront"));
            items.Add(Item("맨 뒤로 보내기", vm.SendToBackCommand, null, "ArrangeSendToBack"));
            items.Add(Item("복제", vm.DuplicateCommand, null, "ContentCopy", "Ctrl+D"));
            items.Add(Item("크기 자동 맞춤", vm.AutoSizeCommand, null, "Resize"));
            if (vm.SelectedShapes.Count > 1)
            {
                var align = new MenuItem { Header = "정렬", Icon = Icon("AlignHorizontalLeft") };
                align.Items.Add(Item("왼쪽 맞춤", vm.AlignCommand, "left", "AlignHorizontalLeft"));
                align.Items.Add(Item("가운데 맞춤", vm.AlignCommand, "center", "AlignHorizontalCenter"));
                align.Items.Add(Item("오른쪽 맞춤", vm.AlignCommand, "right", "AlignHorizontalRight"));
                align.Items.Add(Item("위쪽 맞춤", vm.AlignCommand, "top", "AlignVerticalTop"));
                align.Items.Add(Item("중간 맞춤", vm.AlignCommand, "middle", "AlignVerticalCenter"));
                align.Items.Add(Item("아래쪽 맞춤", vm.AlignCommand, "bottom", "AlignVerticalBottom"));
                items.Add(align);
            }
            items.Add(new Separator());
            items.Add(Item("연결선 직교 라우팅", vm.SetRouteCommand, "ortho", "VectorPolyline"));
            items.Add(Item("연결선 직선 라우팅", vm.SetRouteCommand, "direct", "VectorLine"));
            items.Add(new Separator());
            items.Add(Item("다이어그램에서 삭제", vm.DeleteSelectedCommand, null, "DeleteOutline", "Delete"));
        }
        else if (vm.SelectedConnector != null)
        {
            items.Add(Item("연결선 속성", vm.EditPropertiesCommand, null, "TableEdit", "F2"));
            items.Add(Item("직교 라우팅", vm.SetRouteCommand, "ortho", "VectorPolyline"));
            items.Add(Item("직선 라우팅", vm.SetRouteCommand, "direct", "VectorLine"));
            var rev = new MenuItem { Header = "방향 반전", Icon = Icon("Sort") };
            rev.Click += (_, _) =>
            {
                var c = vm.SelectedConnector; if (c == null) return;
                vm.PushUndo();
                (c.FromId, c.ToId) = (c.ToId, c.FromId);
                (c.FromMultiplicity, c.ToMultiplicity) = (c.ToMultiplicity, c.FromMultiplicity);
                (c.FromRole, c.ToRole) = (c.ToRole, c.FromRole);
                vm.Properties.Load(); InvalidateVisual();
            };
            items.Add(rev);
            items.Add(new Separator());
            items.Add(Item("연결선 삭제", vm.DeleteSelectedCommand, null, "DeleteOutline", "Delete"));
        }
        else
        {
            items.Add(Item("다이어그램 속성", vm.DiagramPropertiesCommand, null, "TableEdit"));
            items.Add(Item("모두 선택", vm.SelectAllCommand, null, "SelectAll", "Ctrl+A"));
            items.Add(new Separator());
            items.Add(Item("전체 보기", vm.ZoomFitCommand, null, "FitToScreenOutline"));
            items.Add(Item("100% 보기", vm.ZoomResetCommand, null, "MagnifyScan"));
            var grid = new MenuItem { Header = vm.ShowGrid ? "격자 숨기기" : "격자 표시", Icon = Icon("Grid") };
            grid.Click += (_, _) => vm.ShowGrid = !vm.ShowGrid;
            items.Add(grid);
            items.Add(Item(D?.ShowFrame == true ? "다이어그램 프레임 숨기기" : "다이어그램 프레임 표시", vm.ToggleFrameCommand, null, "CheckboxMultipleBlankOutline"));
            items.Add(new Separator());
            items.Add(Item("PNG로 내보내기...", vm.ExportPngCommand, null, "FileImageOutline"));
            items.Add(Item("SVG로 내보내기...", vm.ExportSvgCommand, null, "Svg"));
        }
        var menu = new ContextMenu();
        foreach (var i in items) menu.Items.Add(i);
        return menu;
    }
}

internal static class ShapeExt
{
    public static Rect Bounds(this UmlShape s) => new(s.X, s.Y, s.Width, s.Height);
}
