using Avalonia;
using Avalonia.Media;
using UmlStudio.Models;

namespace UmlStudio.Rendering;

public sealed class RenderState
{
    public HashSet<string> SelectedShapes { get; } = new();
    public string? SelectedConnector { get; set; }
    public string? HoverShape { get; set; }
    public string? HighlightShape { get; set; }
    public bool Handles { get; set; } = true;
}

/// <summary>UML renderer shared by canvas, PNG and SVG export.</summary>
public static class DiagramRenderer
{
    public static readonly Color Accent = Color.Parse("#2F6FD0");
    public static readonly Color TextColor = Color.Parse("#1C1C1C");
    public static readonly Color LineColor = Color.Parse("#2A2F38");
    public static readonly Color Shadow = Color.FromArgb(38, 0, 0, 0);
    public const double LineH = 15;
    public const double ActivationW = 10;

    public static bool IsContainer(ShapeKind k) => k is ShapeKind.SystemBoundary or ShapeKind.Package or ShapeKind.Node;
    public static bool IsLifeline(ShapeKind k) => k is ShapeKind.ObjectLifeline or ShapeKind.ActorLifeline;

    // ---------- colors ----------
    public static Color ParseOr(string? hex, Color fallback)
    {
        if (string.IsNullOrWhiteSpace(hex)) return fallback;
        try { return Color.Parse(hex); } catch { return fallback; }
    }
    public static Color FillOf(UmlShape s) => ParseOr(s.Fill, Color.Parse(DiagramCatalog.DefaultColors(s.Kind).Fill));
    public static Color StrokeOf(UmlShape s) => ParseOr(s.Stroke, Color.Parse(DiagramCatalog.DefaultColors(s.Kind).Stroke));
    public static Color TextOf(UmlShape s) => ParseOr(s.FontColor, TextColor);
    public static Color Lerp(Color a, Color b, double t) => Color.FromArgb(
        (byte)(a.A + (b.A - a.A) * t), (byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));
    private static Color Light(Color c) => Lerp(c, Colors.White, 0.65);
    private static Color Dark(Color c, double t = 0.18) => Lerp(c, Colors.Black, t);

    // ---------- layout helpers ----------
    public static string Fit(string text, double size, double maxW, bool bold = false, bool italic = false)
    {
        if (string.IsNullOrEmpty(text) || TextMetrics.Measure(text, size, bold, italic).Width <= maxW) return text;
        for (int n = text.Length - 1; n > 0; n--)
        {
            var t = text[..n] + "…";
            if (TextMetrics.Measure(t, size, bold, italic).Width <= maxW) return t;
        }
        return "…";
    }

    private static void TextC(IDiagramSurface g, string text, double cx, double y, double size, Color c, bool bold = false, bool italic = false, double maxW = 10000, bool underline = false)
    {
        if (string.IsNullOrEmpty(text)) return;
        text = Fit(text, size, maxW, bold, italic);
        var w = TextMetrics.Measure(text, size, bold, italic).Width;
        g.Text(text, new Point(cx - w / 2, y), size, c, bold, italic, underline);
    }

    private static void TextL(IDiagramSurface g, string text, double x, double y, double size, Color c, double maxW, bool bold = false, bool italic = false, bool underline = false)
    {
        if (string.IsNullOrEmpty(text)) return;
        g.Text(Fit(text, size, maxW, bold, italic), new Point(x, y), size, c, bold, italic, underline);
    }

    public static double HeaderHeight(UmlShape s) => 8 + (string.IsNullOrEmpty(s.Stereotype) ? 0 : 14) + 18 + 6;

    /// <summary>Minimum height that fits the classifier compartments.</summary>
    public static double RequiredHeight(UmlShape s)
    {
        switch (s.Kind)
        {
            case ShapeKind.Class:
            case ShapeKind.Interface:
            case ShapeKind.Enumeration:
            {
                var h = HeaderHeight(s);
                h += Math.Max(1, s.Attributes.Count) * LineH + 8;
                if (s.Kind != ShapeKind.Enumeration || s.Operations.Count > 0)
                    h += Math.Max(1, s.Operations.Count) * LineH + 8;
                return h;
            }
            case ShapeKind.State:
                return 30 + (s.Attributes.Count > 0 ? s.Attributes.Count * LineH + 10 : 10);
            case ShapeKind.Component:
                return HeaderHeight(s) + 14 + (s.Attributes.Count > 0 ? s.Attributes.Count * LineH + 8 : 0);
            default:
                return 0;
        }
    }

    public static double RequiredWidth(UmlShape s)
    {
        if (!DiagramCatalog.HasFeatures(s.Kind)) return 0;
        double w = TextMetrics.Measure(s.Name, 13, true).Width + 30;
        foreach (var a in s.Attributes.Concat(s.Operations))
            w = Math.Max(w, TextMetrics.Measure(a, 11).Width + 22);
        return Math.Min(w, 420);
    }

    public static void AutoFit(UmlShape s, bool growWidth = true)
    {
        var rh = RequiredHeight(s);
        if (rh > 0 && s.Height < rh) s.Height = Math.Ceiling(rh);
        if (rh > 0 && s.Kind is ShapeKind.Class or ShapeKind.Interface or ShapeKind.Enumeration) s.Height = Math.Ceiling(Math.Max(rh, Math.Min(s.Height, rh + 60)));
        if (growWidth)
        {
            var rw = RequiredWidth(s);
            if (rw > s.Width) s.Width = Math.Ceiling(rw / 10) * 10;
        }
    }

    public static Rect Bounds(UmlShape s)
    {
        var r = new Rect(s.X, s.Y, Math.Max(1, s.Width), Math.Max(1, s.Height));
        if (IsLifeline(s.Kind)) r = r.Union(new Rect(s.X, s.Y, s.Width, s.Height + s.LifelineLength));
        if (s.Kind == ShapeKind.Actor) r = r.Union(new Rect(s.CenterX - 50, s.Bottom, 100, 18));
        return r;
    }

    public static Rect ContentBounds(UmlDiagram d)
    {
        if (d.Shapes.Count == 0) return new Rect(0, 0, 800, 600);
        var r = Bounds(d.Shapes[0]);
        foreach (var s in d.Shapes.Skip(1)) r = r.Union(Bounds(s));
        var map = d.Shapes.ToDictionary(x => x.Id);
        foreach (var c in d.Connectors)
        {
            var p = ConnectorPath(d, c, map);
            foreach (var pt in p) r = r.Union(new Rect(pt.X - 40, pt.Y - 20, 80, 40));
        }
        return r;
    }

    // ---------- main entry ----------
    public static void Render(IDiagramSurface g, UmlDiagram d, RenderState? st = null, bool frame = false)
    {
        var map = d.Shapes.ToDictionary(s => s.Id);
        if (frame) DrawFrame(g, d);
        var activations = d.Type == DiagramType.Sequence ? ComputeActivations(d, map) : new();

        foreach (var s in d.Shapes.Where(s => IsContainer(s.Kind)).OrderBy(s => s.Z))
            DrawShape(g, s, st, activations);
        foreach (var s in d.Shapes.Where(s => !IsContainer(s.Kind)).OrderBy(s => s.Z))
            DrawShape(g, s, st, activations);

        var seqNo = d.Type == DiagramType.Sequence
            ? d.Connectors.Where(IsMessage).OrderBy(c => c.FromY).Select((c, i) => (c.Id, i + 1)).ToDictionary(x => x.Id, x => x.Item2)
            : new Dictionary<string, int>();
        foreach (var c in d.Connectors)
        {
            if (!map.ContainsKey(c.FromId) || !map.ContainsKey(c.ToId)) continue;
            DrawConnector(g, d, c, map, activations, st?.SelectedConnector == c.Id, seqNo.TryGetValue(c.Id, out var n) ? n : 0);
        }

        if (st != null && st.Handles)
        {
            foreach (var s in d.Shapes.Where(s => st.SelectedShapes.Contains(s.Id)))
                DrawHandles(g, s, st.SelectedShapes.Count == 1);
        }
    }

    public static bool IsMessage(UmlConnector c) => c.Kind is ConnectorKind.MessageSync or ConnectorKind.MessageAsync
        or ConnectorKind.MessageReturn or ConnectorKind.CreateMessage or ConnectorKind.DestroyMessage;

    private static void DrawFrame(IDiagramSurface g, UmlDiagram d)
    {
        var b = ContentBounds(d).Inflate(new Thickness(24, 44, 24, 24));
        var col = Color.Parse("#5A6372");
        g.Rect(b, null, col, 1);
        var title = $"{DiagramCatalog.DiagramKeyword(d.Type)} {d.Name}";
        var w = TextMetrics.Measure(title, 12, true).Width + 18;
        var pts = new[] { b.TopLeft, new Point(b.X + w, b.Y), new Point(b.X + w, b.Y + 14), new Point(b.X + w - 8, b.Y + 22), new Point(b.X, b.Y + 22) };
        g.Polygon(pts, Color.Parse("#EEF1F5"), col, 1);
        g.Text(title, new Point(b.X + 6, b.Y + 4), 12, TextColor, true);
    }

    // ---------- shapes ----------
    private static void DrawShape(IDiagramSurface g, UmlShape s, RenderState? st, Dictionary<string, List<(double, double)>> act)
    {
        var fill = FillOf(s);
        var stroke = StrokeOf(s);
        var tc = TextOf(s);
        var lw = Math.Max(0.5, s.LineWidth);
        bool hover = st?.HoverShape == s.Id && !(st?.SelectedShapes.Contains(s.Id) ?? false);
        bool hi = st?.HighlightShape == s.Id;
        if (hi) { stroke = Accent; lw = 2.2; }
        var r = new Rect(s.X, s.Y, s.Width, s.Height);
        var shadow = new Rect(s.X + 3, s.Y + 3, s.Width, s.Height);

        switch (s.Kind)
        {
            case ShapeKind.Class:
            case ShapeKind.Interface:
            case ShapeKind.Enumeration:
                DrawClassifier(g, s, r, fill, stroke, tc, lw);
                break;

            case ShapeKind.Package:
            {
                var name = s.Name;
                var tabW = Math.Min(s.Width * 0.5, Math.Max(60, TextMetrics.Measure(name, 11, true).Width + 16));
                var tab = new Rect(s.X, s.Y, tabW, 20);
                var body = new Rect(s.X, s.Y + 19, s.Width, Math.Max(10, s.Height - 19));
                g.Rect(new Rect(body.X + 3, body.Y + 3, body.Width, body.Height), Shadow, null);
                g.Rect(tab, Light(fill), stroke, lw, 0, false, fill);
                g.Rect(body, Light(fill), stroke, lw, 0, false, fill);
                double y = body.Y + 8;
                if (!string.IsNullOrEmpty(s.Stereotype)) { TextC(g, "«" + s.Stereotype + "»", body.Center.X, y, 10, tc, false, false, s.Width - 10); y += 14; }
                TextC(g, name, body.Center.X, y, 13, tc, true, false, s.Width - 12);
                break;
            }

            case ShapeKind.Note:
            {
                const double fold = 12;
                var pts = new[] { r.TopLeft, new Point(r.Right - fold, r.Y), new Point(r.Right, r.Y + fold), r.BottomRight, r.BottomLeft };
                g.Polygon(pts.Select(p => p + new Point(3, 3)).ToArray(), Shadow, null);
                g.Polygon(pts, fill, stroke, lw, Lerp(fill, Color.Parse("#F5EDB5"), 0.6));
                g.Polygon(new[] { new Point(r.Right - fold, r.Y), new Point(r.Right - fold, r.Y + fold), new Point(r.Right, r.Y + fold) }, Dark(fill, 0.08), stroke, lw);
                var text = string.IsNullOrWhiteSpace(s.Notes) ? s.Name : s.Notes;
                double y = r.Y + 7;
                foreach (var line in TextMetrics.Wrap(text, 11, s.Width - 22))
                {
                    if (y + LineH > r.Bottom - 2) break;
                    g.Text(line, new Point(r.X + 8, y), 11, tc);
                    y += LineH;
                }
                break;
            }

            case ShapeKind.ObjectLifeline:
            case ShapeKind.ActorLifeline:
                DrawLifeline(g, s, fill, stroke, tc, lw, act);
                break;

            case ShapeKind.Component:
            {
                g.Rect(shadow, Shadow, null, 0, 2);
                g.Rect(r, Light(fill), stroke, lw, 2, false, fill);
                // UML2 component glyph
                var gx = r.Right - 22; var gy = r.Y + 7;
                g.Rect(new Rect(gx + 3, gy, 13, 16), Colors.White, stroke, 1);
                g.Rect(new Rect(gx, gy + 3, 7, 4), Colors.White, stroke, 1);
                g.Rect(new Rect(gx, gy + 9, 7, 4), Colors.White, stroke, 1);
                double y = r.Y + 8;
                if (!string.IsNullOrEmpty(s.Stereotype)) { TextC(g, "«" + s.Stereotype + "»", r.Center.X, y, 10, tc, false, false, s.Width - 40); y += 14; }
                TextC(g, s.Name, r.Center.X, y, 13, tc, true, false, s.Width - 40);
                y += 22;
                if (s.Attributes.Count > 0)
                {
                    g.Line(new Point(r.X, y), new Point(r.Right, y), stroke, lw);
                    y += 4;
                    foreach (var a in s.Attributes) { if (y + LineH > r.Bottom) break; TextL(g, a, r.X + 8, y, 11, tc, s.Width - 14); y += LineH; }
                }
                break;
            }

            case ShapeKind.Artifact:
            {
                g.Rect(shadow, Shadow, null);
                g.Rect(r, Light(fill), stroke, lw, 0, false, fill);
                var gx = r.Right - 20; var gy = r.Y + 6;
                g.Polygon(new[] { new Point(gx, gy), new Point(gx + 9, gy), new Point(gx + 13, gy + 4), new Point(gx + 13, gy + 16), new Point(gx, gy + 16) }, Colors.White, stroke, 1);
                double y = r.Y + 10;
                if (!string.IsNullOrEmpty(s.Stereotype)) { TextC(g, "«" + s.Stereotype + "»", r.Center.X, y, 10, tc, false, false, s.Width - 40); y += 14; }
                TextC(g, s.Name, r.Center.X, y, 12, tc, true, false, s.Width - 40);
                break;
            }

            case ShapeKind.Node:
            {
                const double d3 = 12;
                var front = new Rect(s.X, s.Y + d3, s.Width - d3, s.Height - d3);
                g.Rect(new Rect(front.X + 3, front.Y + 3, front.Width, front.Height), Shadow, null);
                g.Polygon(new[] { new Point(s.X, s.Y + d3), new Point(s.X + d3, s.Y), new Point(s.Right, s.Y), new Point(s.Right - d3, s.Y + d3) }, Light(fill), stroke, lw);
                g.Polygon(new[] { new Point(s.Right - d3, s.Y + d3), new Point(s.Right, s.Y), new Point(s.Right, s.Bottom - d3), new Point(s.Right - d3, s.Bottom) }, Dark(fill, 0.12), stroke, lw);
                g.Rect(front, Light(fill), stroke, lw, 0, false, fill);
                double y = front.Y + 8;
                if (!string.IsNullOrEmpty(s.Stereotype)) { TextC(g, "«" + s.Stereotype + "»", front.Center.X, y, 10, tc, false, false, front.Width - 10); y += 14; }
                TextC(g, s.Name, front.Center.X, y, 13, tc, true, false, front.Width - 10);
                break;
            }

            case ShapeKind.Port:
                g.Rect(r, fill, stroke, lw);
                if (!string.IsNullOrEmpty(s.Name)) g.Text(Fit(s.Name, 10, 120), new Point(s.Right + 3, s.Y - 13), 10, tc);
                break;

            case ShapeKind.State:
            {
                g.Rect(shadow, Shadow, null, 0, 12);
                g.Rect(r, Light(fill), stroke, lw, 12, false, fill);
                TextC(g, s.Name, r.Center.X, r.Y + 7, 13, tc, true, false, s.Width - 12);
                if (s.Attributes.Count > 0)
                {
                    var y = r.Y + 28;
                    g.Line(new Point(r.X, y), new Point(r.Right, y), stroke, lw);
                    y += 4;
                    foreach (var a in s.Attributes) { if (y + LineH > r.Bottom) break; TextL(g, a, r.X + 9, y, 11, tc, s.Width - 16); y += LineH; }
                }
                break;
            }

            case ShapeKind.InitialState:
            case ShapeKind.InitialNode:
            {
                var c = new Rect(s.CenterX - 10, s.CenterY - 10, 20, 20);
                g.Ellipse(new Rect(c.X + 2, c.Y + 2, 20, 20), Shadow, null);
                g.Ellipse(c, Color.Parse("#2B2B2B"), hi ? Accent : null, 2);
                break;
            }

            case ShapeKind.FinalState:
            case ShapeKind.FinalNode:
                g.Ellipse(new Rect(s.CenterX - 12, s.CenterY - 12, 24, 24), Colors.White, hi ? Accent : Color.Parse("#2B2B2B"), 1.6);
                g.Ellipse(new Rect(s.CenterX - 7.5, s.CenterY - 7.5, 15, 15), Color.Parse("#2B2B2B"), null);
                break;

            case ShapeKind.Choice:
            case ShapeKind.Decision:
            case ShapeKind.Merge:
            {
                var pts = new[] { new Point(s.CenterX, s.Y), new Point(s.Right, s.CenterY), new Point(s.CenterX, s.Bottom), new Point(s.X, s.CenterY) };
                g.Polygon(pts.Select(p => p + new Point(2, 2)).ToArray(), Shadow, null);
                g.Polygon(pts, Light(fill), stroke, lw, fill);
                if (!string.IsNullOrEmpty(s.Name)) g.Text(Fit(s.Name, 11, 160), new Point(s.Right + 4, s.Y - 12), 11, tc);
                break;
            }

            case ShapeKind.Fork:
            case ShapeKind.Join:
                g.Rect(new Rect(s.X + 2, s.Y + 2, s.Width, s.Height), Shadow, null, 0, 2);
                g.Rect(r, Color.Parse("#2B2B2B"), hi ? Accent : null, 2, 2);
                break;

            case ShapeKind.Actor:
                DrawStickman(g, s.CenterX, s.Y, s.Width, s.Height - 4, stroke, fill, lw);
                TextC(g, s.Name, s.CenterX, s.Bottom + 1, 12, tc, true, false, 160);
                if (!string.IsNullOrEmpty(s.Stereotype)) TextC(g, "«" + s.Stereotype + "»", s.CenterX, s.Y - 15, 10, tc);
                break;

            case ShapeKind.UseCase:
            {
                g.Ellipse(shadow, Shadow, null);
                g.Ellipse(r, Light(fill), stroke, lw, false, fill);
                var lines = TextMetrics.Wrap(s.Name, 12, s.Width * 0.78, true);
                double total = lines.Count * 16 + (string.IsNullOrEmpty(s.Stereotype) ? 0 : 14);
                double y = s.CenterY - total / 2;
                if (!string.IsNullOrEmpty(s.Stereotype)) { TextC(g, "«" + s.Stereotype + "»", s.CenterX, y, 10, tc); y += 14; }
                foreach (var l in lines) { TextC(g, l, s.CenterX, y, 12, tc, true); y += 16; }
                break;
            }

            case ShapeKind.SystemBoundary:
                g.Rect(r, fill, stroke, lw * 1.2, 0);
                TextC(g, s.Name, r.Center.X, r.Y + 8, 13, tc, true, false, s.Width - 10);
                if (!string.IsNullOrEmpty(s.Stereotype)) TextC(g, "«" + s.Stereotype + "»", r.Center.X, r.Y + 26, 10, tc);
                break;

            case ShapeKind.Action:
            {
                g.Rect(shadow, Shadow, null, 0, 14);
                g.Rect(r, Light(fill), stroke, lw, 14, false, fill);
                var lines = TextMetrics.Wrap(s.Name, 12, s.Width - 20);
                double y = s.CenterY - lines.Count * 16 / 2.0;
                foreach (var l in lines) { TextC(g, l, s.CenterX, y, 12, tc); y += 16; }
                break;
            }

            default:
                g.Rect(r, fill, stroke, lw);
                TextC(g, s.Name, r.Center.X, r.Y + 6, 12, tc, true);
                break;
        }

        if (hover)
        {
            var b = IsLifeline(s.Kind) ? r : Bounds(s);
            g.Rect(b.Inflate(2), null, Color.FromArgb(150, Accent.R, Accent.G, Accent.B), 1, 2);
        }
    }

    private static void DrawClassifier(IDiagramSurface g, UmlShape s, Rect r, Color fill, Color stroke, Color tc, double lw)
    {
        g.Rect(new Rect(r.X + 3, r.Y + 3, r.Width, r.Height), Shadow, null);
        var hh = HeaderHeight(s);
        // header gradient + body
        g.Rect(r, Lerp(fill, Colors.White, 0.35), null, 0, 0, false, fill);
        g.Rect(new Rect(r.X, r.Y, r.Width, hh), Light(fill), null, 0, 0, false, fill);
        g.Rect(r, null, stroke, lw);
        double y = r.Y + 8;
        if (!string.IsNullOrEmpty(s.Stereotype))
        {
            TextC(g, "«" + s.Stereotype + "»", r.Center.X, y, 10, tc, false, false, r.Width - 10);
            y += 14;
        }
        bool italic = s.IsAbstract || s.Kind == ShapeKind.Interface;
        TextC(g, s.Name, r.Center.X, y, 13, tc, true, italic, r.Width - 12);
        y = r.Y + hh;
        g.Line(new Point(r.X, y), new Point(r.Right, y), stroke, lw);
        y += 4;
        bool showOps = s.Kind != ShapeKind.Enumeration || s.Operations.Count > 0;
        double attrEnd = showOps ? r.Y + hh + Math.Max(1, s.Attributes.Count) * LineH + 8 : r.Bottom;
        foreach (var a in s.Attributes)
        {
            if (y + LineH > Math.Min(attrEnd, r.Bottom) + 1) break;
            TextL(g, a, r.X + 8, y, 11, tc, r.Width - 14, false, false, a.Contains("{static}"));
            y += LineH;
        }
        if (!showOps) return;
        y = attrEnd;
        if (y < r.Bottom - 2)
        {
            g.Line(new Point(r.X, y), new Point(r.Right, y), stroke, lw);
            y += 4;
            foreach (var o in s.Operations)
            {
                if (y + LineH > r.Bottom + 1) break;
                TextL(g, o, r.X + 8, y, 11, tc, r.Width - 14, false, o.Contains("{abstract}"));
                y += LineH;
            }
        }
    }

    private static void DrawStickman(IDiagramSurface g, double cx, double top, double w, double h, Color stroke, Color fill, double lw)
    {
        var head = Math.Min(w * 0.42, h * 0.24);
        var t = Math.Max(1.4, lw * 1.4);
        g.Ellipse(new Rect(cx - head / 2, top, head, head), Color.Parse("#FFFFFF"), stroke, t, false, Color.Parse("#E8EEF6"));
        var neck = top + head;
        var hip = top + h * 0.64;
        g.Line(new Point(cx, neck), new Point(cx, hip), stroke, t);
        g.Line(new Point(cx - w * 0.42, neck + h * 0.14), new Point(cx + w * 0.42, neck + h * 0.14), stroke, t);
        g.Line(new Point(cx, hip), new Point(cx - w * 0.34, top + h), stroke, t);
        g.Line(new Point(cx, hip), new Point(cx + w * 0.34, top + h), stroke, t);
    }

    private static void DrawLifeline(IDiagramSurface g, UmlShape s, Color fill, Color stroke, Color tc, double lw, Dictionary<string, List<(double, double)>> act)
    {
        var r = new Rect(s.X, s.Y, s.Width, s.Height);
        var cx = s.CenterX;
        g.Line(new Point(cx, s.Bottom), new Point(cx, s.Bottom + s.LifelineLength), Color.Parse("#6B7380"), 1, true);
        if (s.Kind == ShapeKind.ObjectLifeline)
        {
            g.Rect(new Rect(r.X + 3, r.Y + 3, r.Width, r.Height), Shadow, null);
            g.Rect(r, Light(fill), stroke, lw, 0, false, fill);
            double y = r.Y + (string.IsNullOrEmpty(s.Stereotype) ? (r.Height - 17) / 2 : (r.Height - 31) / 2);
            if (!string.IsNullOrEmpty(s.Stereotype)) { TextC(g, "«" + s.Stereotype + "»", cx, y, 10, tc, false, false, s.Width - 8); y += 14; }
            TextC(g, s.Name, cx, y, 12, tc, true, false, s.Width - 10);
        }
        else
        {
            DrawStickman(g, cx, s.Y, Math.Min(36, s.Width * 0.45), s.Height - 22, stroke, fill, lw);
            TextC(g, s.Name, cx, s.Bottom - 17, 12, tc, true, false, s.Width + 30);
        }
        if (act.TryGetValue(s.Id, out var bars))
            foreach (var (y1, y2) in bars)
            {
                var br = new Rect(cx - ActivationW / 2, y1, ActivationW, Math.Max(8, y2 - y1));
                g.Rect(new Rect(br.X + 2, br.Y + 2, br.Width, br.Height), Shadow, null);
                g.Rect(br, Colors.White, stroke, 1, 0, false, Light(fill));
            }
    }

    private static void DrawHandles(IDiagramSurface g, UmlShape s, bool single)
    {
        var r = new Rect(s.X, s.Y, s.Width, s.Height);
        g.Rect(r.Inflate(2), null, Accent, 1, 0, true);
        foreach (var p in HandlePoints(r))
            g.Rect(new Rect(p.X - 3.5, p.Y - 3.5, 7, 7), single ? Accent : Colors.White, single ? Colors.White : Accent, 1);
    }

    public static Point[] HandlePoints(Rect r) => new[]
    {
        r.TopLeft, new Point(r.Center.X, r.Y), r.TopRight, new Point(r.Right, r.Center.Y),
        r.BottomRight, new Point(r.Center.X, r.Bottom), r.BottomLeft, new Point(r.X, r.Center.Y)
    };

    // ---------- sequence activations ----------
    public static Dictionary<string, List<(double, double)>> ComputeActivations(UmlDiagram d, Dictionary<string, UmlShape> map)
    {
        var res = new Dictionary<string, List<(double, double)>>();
        var msgs = d.Connectors.Where(IsMessage).OrderBy(c => c.FromY).ToList();
        foreach (var m in msgs)
        {
            if (m.Kind != ConnectorKind.MessageSync || m.FromId == m.ToId) continue;
            if (!map.TryGetValue(m.ToId, out var target) || !IsLifeline(target.Kind)) continue;
            var ret = msgs.FirstOrDefault(r => r.Kind == ConnectorKind.MessageReturn && r.FromId == m.ToId && r.FromY > m.FromY);
            var end = ret?.FromY ?? m.FromY + 30;
            if (!res.TryGetValue(target.Id, out var list)) res[target.Id] = list = new();
            list.Add((m.FromY - 4, end + 4));
            // caller activation (if caller is not already active)
            if (map.TryGetValue(m.FromId, out var caller) && IsLifeline(caller.Kind))
            {
                if (!res.TryGetValue(caller.Id, out var cl)) res[caller.Id] = cl = new();
                if (!cl.Any(b => b.Item1 <= m.FromY && b.Item2 >= m.FromY))
                    cl.Add((m.FromY - 10, end + 12));
            }
        }
        return res;
    }

    private static bool ActiveAt(Dictionary<string, List<(double, double)>> act, string id, double y) =>
        act.TryGetValue(id, out var l) && l.Any(b => b.Item1 <= y + 0.5 && b.Item2 >= y - 0.5);

    // ---------- connectors ----------
    public static List<Point> ConnectorPath(UmlDiagram d, UmlConnector c, Dictionary<string, UmlShape> map,
        Dictionary<string, List<(double, double)>>? act = null)
    {
        if (!map.TryGetValue(c.FromId, out var a) || !map.TryGetValue(c.ToId, out var b)) return new();
        if (d.Type == DiagramType.Sequence && IsLifeline(a.Kind) && IsLifeline(b.Kind))
        {
            var y = c.FromY > 0 ? c.FromY : Math.Max(a.Bottom, b.Bottom) + 40;
            if (a == b)
            {
                var x = a.CenterX + ActivationW / 2;
                return new() { new(x, y), new(x + 42, y), new(x + 42, y + 22), new(x, y + 22) };
            }
            double off(UmlShape s) => act != null && ActiveAt(act, s.Id, y) ? ActivationW / 2 : 0;
            var dir = Math.Sign(b.CenterX - a.CenterX);
            var x1 = a.CenterX + dir * off(a);
            var x2 = b.CenterX - dir * off(b);
            if (c.Kind == ConnectorKind.CreateMessage)
                x2 = dir > 0 ? b.X : b.Right;
            return new() { new(x1, y), new(x2, y) };
        }
        if (a == b)
        {
            var p0 = new Point(a.Right - Math.Min(30, a.Width / 3), a.Y);
            return new() { p0, new(p0.X, a.Y - 26), new(a.Right + 26, a.Y - 26), new(a.Right + 26, a.Y + Math.Min(24, a.Height / 2)), new(a.Right, a.Y + Math.Min(24, a.Height / 2)) };
        }
        var ra = HitRect(a); var rb = HitRect(b);
        if (c.Route == RouteStyle.Orthogonal)
            return Orthogonal(ra, rb);
        var p1 = Clip(a, rb.Center);
        var p2 = Clip(b, ra.Center);
        return new() { p1, p2 };
    }

    private static Rect HitRect(UmlShape s) => new(s.X, s.Y, s.Width, s.Height);

    private static List<Point> Orthogonal(Rect a, Rect b)
    {
        double ox1 = Math.Max(a.X, b.X), ox2 = Math.Min(a.Right, b.Right);
        double oy1 = Math.Max(a.Y, b.Y), oy2 = Math.Min(a.Bottom, b.Bottom);
        if (ox2 - ox1 > 10)
        {
            var x = (ox1 + ox2) / 2;
            return b.Center.Y > a.Center.Y ? new() { new(x, a.Bottom), new(x, b.Y) } : new() { new(x, a.Y), new(x, b.Bottom) };
        }
        if (oy2 - oy1 > 10)
        {
            var y = (oy1 + oy2) / 2;
            return b.Center.X > a.Center.X ? new() { new(a.Right, y), new(b.X, y) } : new() { new(a.X, y), new(b.Right, y) };
        }
        var dx = b.Center.X - a.Center.X; var dy = b.Center.Y - a.Center.Y;
        if (Math.Abs(dx) >= Math.Abs(dy))
        {
            var sx = dx > 0 ? a.Right : a.X; var ex = dx > 0 ? b.X : b.Right;
            var mx = (sx + ex) / 2;
            return new() { new(sx, a.Center.Y), new(mx, a.Center.Y), new(mx, b.Center.Y), new(ex, b.Center.Y) };
        }
        else
        {
            var sy = dy > 0 ? a.Bottom : a.Y; var ey = dy > 0 ? b.Y : b.Bottom;
            var my = (sy + ey) / 2;
            return new() { new(a.Center.X, sy), new(a.Center.X, my), new(b.Center.X, my), new(b.Center.X, ey) };
        }
    }

    public static Point Clip(UmlShape s, Point toward)
    {
        var cx = s.CenterX; var cy = s.CenterY;
        var dx = toward.X - cx; var dy = toward.Y - cy;
        if (Math.Abs(dx) < 1e-6 && Math.Abs(dy) < 1e-6) return new Point(cx, cy);
        double hw = s.Width / 2, hh = s.Height / 2;
        double t;
        switch (s.Kind)
        {
            case ShapeKind.UseCase:
            case ShapeKind.InitialNode or ShapeKind.InitialState or ShapeKind.FinalNode or ShapeKind.FinalState:
            {
                if (s.Kind is not ShapeKind.UseCase) { hw = Math.Min(hw, 12); hh = Math.Min(hh, 12); }
                t = 1 / Math.Sqrt(dx * dx / (hw * hw) + dy * dy / (hh * hh));
                break;
            }
            case ShapeKind.Decision or ShapeKind.Merge or ShapeKind.Choice:
                t = 1 / (Math.Abs(dx) / hw + Math.Abs(dy) / hh);
                break;
            default:
            {
                t = double.PositiveInfinity;
                if (dx != 0) t = Math.Min(t, hw / Math.Abs(dx));
                if (dy != 0) t = Math.Min(t, hh / Math.Abs(dy));
                break;
            }
        }
        return new Point(cx + dx * t, cy + dy * t);
    }

    public static bool IsDashed(ConnectorKind k) => k is ConnectorKind.Dependency or ConnectorKind.Realization
        or ConnectorKind.MessageReturn or ConnectorKind.Include or ConnectorKind.Extend or ConnectorKind.CreateMessage
        or ConnectorKind.NoteLink;

    private static void DrawConnector(IDiagramSurface g, UmlDiagram d, UmlConnector c, Dictionary<string, UmlShape> map,
        Dictionary<string, List<(double, double)>> act, bool selected, int seq)
    {
        var pts = ConnectorPath(d, c, map, act);
        if (pts.Count < 2) return;
        var col = selected ? Accent : ParseOr(c.Stroke, LineColor);
        var lw = selected ? 2 : 1.15;
        bool dashed = IsDashed(c.Kind);

        // shorten line for heads so the stroke doesn't poke through
        var line = pts.ToList();
        var end = line[^1]; var prev = line[^2];
        var start = line[0]; var next = line[1];
        double headCut = c.Kind switch
        {
            ConnectorKind.Inheritance or ConnectorKind.Realization => 13,
            ConnectorKind.Aggregation or ConnectorKind.Composition => 0,
            _ => 0
        };
        if (headCut > 0) line[^1] = Toward(end, prev, headCut);
        double tailCut = c.Kind is ConnectorKind.Aggregation or ConnectorKind.Composition ? 0 : 0;
        _ = tailCut;
        g.Polyline(line, col, lw, dashed);

        switch (c.Kind)
        {
            case ConnectorKind.Inheritance:
            case ConnectorKind.Realization:
                g.Polygon(Triangle(prev, end, 13, 8), Colors.White, col, lw);
                break;
            case ConnectorKind.Aggregation:
                g.Polygon(DiamondHead(prev, end, 16, 6), Colors.White, col, lw);
                break;
            case ConnectorKind.Composition:
                g.Polygon(DiamondHead(prev, end, 16, 6), col, col, lw);
                break;
            case ConnectorKind.MessageSync:
            case ConnectorKind.DestroyMessage:
                g.Polygon(Triangle(prev, end, 10, 5), col, col, 1);
                if (c.Kind == ConnectorKind.DestroyMessage)
                {
                    g.Line(end + new Point(-8, -8), end + new Point(8, 8), col, 2);
                    g.Line(end + new Point(-8, 8), end + new Point(8, -8), col, 2);
                }
                break;
            case ConnectorKind.Association:
            case ConnectorKind.NoteLink:
                break;
            default:
                OpenArrow(g, prev, end, col, lw);
                break;
        }

        // label
        var parts = new List<string>();
        var stereo = string.IsNullOrEmpty(c.Stereotype) ? "" : "«" + c.Stereotype + "»";
        var label = c.Label;
        if (!string.IsNullOrEmpty(c.Guard)) label = (label + " [" + c.Guard + "]").Trim();
        if (seq > 0 && !string.IsNullOrEmpty(label)) label = seq + ": " + label;
        var (mid, horiz) = MidPoint(pts);
        if (IsMessage(c) && d.Type == DiagramType.Sequence)
        {
            var y = pts[0].Y;
            double cx = c.FromId == c.ToId ? pts[0].X + 48 : (pts[0].X + pts[^1].X) / 2;
            var txt = string.IsNullOrEmpty(stereo) ? label : stereo + " " + label;
            if (c.FromId == c.ToId)
                g.Text(txt, new Point(cx, y - 2), 11, TextColor);
            else
                TextC(g, txt, cx, y - 17, 11, selected ? Accent : TextColor);
        }
        else
        {
            var lines = new List<string>();
            if (stereo.Length > 0) lines.Add(stereo);
            if (!string.IsNullOrEmpty(label)) lines.Add(label);
            if (lines.Count > 0)
            {
                double w = lines.Max(l => TextMetrics.Measure(l, 11).Width) + 8;
                double h = lines.Count * 14 + 2;
                var box = horiz ? new Rect(mid.X - w / 2, mid.Y - h - 3, w, h) : new Rect(mid.X + 6, mid.Y - h / 2, w, h);
                g.Rect(box, Color.FromArgb(225, 255, 255, 255), null);
                double ly = box.Y + 1;
                foreach (var l in lines) { TextC(g, l, box.Center.X, ly, 11, selected ? Accent : TextColor, false, l == stereo); ly += 14; }
            }
            EndLabel(g, pts[0], pts[1], c.FromMultiplicity, c.FromRole);
            EndLabel(g, pts[^1], pts[^2], c.ToMultiplicity, c.ToRole);
        }

        if (selected)
        {
            foreach (var p in new[] { pts[0], pts[^1] })
                g.Rect(new Rect(p.X - 3.5, p.Y - 3.5, 7, 7), Accent, Colors.White, 1);
        }
    }

    private static void EndLabel(IDiagramSurface g, Point end, Point toward, string mult, string role)
    {
        if (string.IsNullOrEmpty(mult) && string.IsNullOrEmpty(role)) return;
        var v = Unit(toward - end);
        var n = new Point(-v.Y, v.X);
        void Put(string t, double side, Color col)
        {
            var sz = TextMetrics.Measure(t, 11);
            double ev = Math.Abs(v.X) * sz.Width / 2 + Math.Abs(v.Y) * sz.Height / 2;
            double en = Math.Abs(n.X) * sz.Width / 2 + Math.Abs(n.Y) * sz.Height / 2;
            var p = end + v * (12 + ev) + n * (side * (5 + en));
            g.Text(t, new Point(p.X - sz.Width / 2, p.Y - sz.Height / 2), 11, col);
        }
        if (!string.IsNullOrEmpty(mult)) Put(mult, 1, TextColor);
        if (!string.IsNullOrEmpty(role)) Put(role, -1, Color.Parse("#40546E"));
    }

    public static (Point, bool) MidPoint(List<Point> pts)
    {
        double total = 0;
        for (int i = 1; i < pts.Count; i++) total += Len(pts[i] - pts[i - 1]);
        double half = total / 2, acc = 0;
        for (int i = 1; i < pts.Count; i++)
        {
            var l = Len(pts[i] - pts[i - 1]);
            if (acc + l >= half)
            {
                var t = l == 0 ? 0 : (half - acc) / l;
                var p = pts[i - 1] + (pts[i] - pts[i - 1]) * t;
                var seg = pts[i] - pts[i - 1];
                return (p, Math.Abs(seg.X) >= Math.Abs(seg.Y));
            }
            acc += l;
        }
        return (pts[0], true);
    }

    private static double Len(Point v) => Math.Sqrt(v.X * v.X + v.Y * v.Y);
    private static Point Unit(Point v) { var l = Len(v); return l < 1e-9 ? new Point(1, 0) : new Point(v.X / l, v.Y / l); }
    private static Point Toward(Point from, Point to, double dist) => from + Unit(to - from) * dist;

    private static Point[] Triangle(Point from, Point tip, double len, double half)
    {
        var u = Unit(tip - from); var n = new Point(-u.Y, u.X);
        var b = tip - u * len;
        return new[] { tip, b + n * half, b - n * half };
    }

    private static Point[] DiamondHead(Point from, Point tip, double len, double half)
    {
        var u = Unit(tip - from); var n = new Point(-u.Y, u.X);
        var m = tip - u * (len / 2);
        return new[] { tip, m + n * half, tip - u * len, m - n * half };
    }

    private static void OpenArrow(IDiagramSurface g, Point from, Point tip, Color col, double lw)
    {
        var u = Unit(tip - from); var n = new Point(-u.Y, u.X);
        var b = tip - u * 11;
        g.Polyline(new[] { b + n * 5.5, tip, b - n * 5.5 }, col, lw + 0.2);
    }

    public static double DistToPath(Point p, IReadOnlyList<Point> pts)
    {
        double best = double.MaxValue;
        for (int i = 1; i < pts.Count; i++) best = Math.Min(best, DistToSegment(p, pts[i - 1], pts[i]));
        return best;
    }

    public static double DistToSegment(Point p, Point a, Point b)
    {
        var dx = b.X - a.X; var dy = b.Y - a.Y;
        if (dx == 0 && dy == 0) return Len(p - a);
        var t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / (dx * dx + dy * dy), 0, 1);
        return Len(p - new Point(a.X + t * dx, a.Y + t * dy));
    }
}
