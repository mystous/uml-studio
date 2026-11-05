using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace UmlStudio.Rendering;

public sealed class AvaloniaSurface : IDiagramSurface
{
    private readonly DrawingContext _ctx;
    private static readonly Dictionary<(uint, double, bool), Pen> PenCache = new();
    private static readonly Dictionary<uint, ISolidColorBrush> BrushCache = new();

    public AvaloniaSurface(DrawingContext ctx) => _ctx = ctx;

    private static IBrush? B(Color? c, Color? c2, Rect r)
    {
        if (c is null) return null;
        if (c2 is { } g && g != c.Value)
        {
            return new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(c.Value, 0), new GradientStop(g, 1) }
            };
        }
        var key = c.Value.ToUInt32();
        if (!BrushCache.TryGetValue(key, out var b))
            BrushCache[key] = b = new ImmutableSolidColorBrush(c.Value);
        return b;
    }

    private static IPen? P(Color? c, double t, bool dashed)
    {
        if (c is null || t <= 0) return null;
        var key = (c.Value.ToUInt32(), t, dashed);
        if (!PenCache.TryGetValue(key, out var p))
        {
            p = new Pen(new ImmutableSolidColorBrush(c.Value), t)
            {
                DashStyle = dashed ? new DashStyle(new double[] { 5, 3 }, 0) : null,
                LineJoin = PenLineJoin.Miter
            };
            if (PenCache.Count > 500) PenCache.Clear();
            PenCache[key] = p;
        }
        return p;
    }

    public void Rect(Rect r, Color? fill, Color? stroke, double thickness = 1, double radius = 0, bool dashed = false, Color? fill2 = null)
    {
        if (radius > 0)
            _ctx.DrawRectangle(B(fill, fill2, r), P(stroke, thickness, dashed), r, radius, radius);
        else
            _ctx.DrawRectangle(B(fill, fill2, r), P(stroke, thickness, dashed), r);
    }

    public void Ellipse(Rect r, Color? fill, Color? stroke, double thickness = 1, bool dashed = false, Color? fill2 = null) =>
        _ctx.DrawEllipse(B(fill, fill2, r), P(stroke, thickness, dashed), r);

    public void Line(Point a, Point b, Color color, double thickness = 1, bool dashed = false) =>
        _ctx.DrawLine(P(color, thickness, dashed)!, a, b);

    public void Polyline(IReadOnlyList<Point> pts, Color color, double thickness = 1, bool dashed = false)
    {
        if (pts.Count < 2) return;
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(pts[0], false);
            for (int i = 1; i < pts.Count; i++) g.LineTo(pts[i]);
            g.EndFigure(false);
        }
        _ctx.DrawGeometry(null, P(color, thickness, dashed), geo);
    }

    public void Polygon(IReadOnlyList<Point> pts, Color? fill, Color? stroke, double thickness = 1, Color? fill2 = null)
    {
        if (pts.Count < 3) return;
        var geo = new StreamGeometry();
        using (var g = geo.Open())
        {
            g.BeginFigure(pts[0], true);
            for (int i = 1; i < pts.Count; i++) g.LineTo(pts[i]);
            g.EndFigure(true);
        }
        _ctx.DrawGeometry(B(fill, fill2, geo.Bounds), P(stroke, thickness, false), geo);
    }

    public void Text(string text, Point topLeft, double size, Color color, bool bold = false, bool italic = false, bool underline = false)
    {
        if (string.IsNullOrEmpty(text)) return;
        var ft = TextMetrics.Format(text, size, color, bold, italic);
        if (underline) ft.SetTextDecorations(TextDecorations.Underline);
        _ctx.DrawText(ft, topLeft);
    }
}
