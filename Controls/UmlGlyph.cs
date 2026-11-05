using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using UmlStudio.Models;
using UmlStudio.Rendering;

namespace UmlStudio.Controls;

/// <summary>Small vector icons for UML elements, connectors and diagrams (toolbox, browser, tabs).</summary>
public sealed class UmlGlyph : Control
{
    public static readonly StyledProperty<string?> GlyphProperty =
        AvaloniaProperty.Register<UmlGlyph, string?>(nameof(Glyph));

    static UmlGlyph() => AffectsRender<UmlGlyph>(GlyphProperty);

    public UmlGlyph() { Width = 16; Height = 16; }

    public string? Glyph { get => GetValue(GlyphProperty); set => SetValue(GlyphProperty, value); }

    private static Color C(string h) => Color.Parse(h);

    public override void Render(DrawingContext ctx)
    {
        var key = Glyph;
        if (string.IsNullOrEmpty(key)) return;
        var s = Math.Min(Bounds.Width, Bounds.Height) / 16.0;
        using var _ = ctx.PushTransform(Matrix.CreateScale(s, s));
        var g = new AvaloniaSurface(ctx);
        var ink = C("#D3DCEA");

        if (key == "select")
        {
            g.Polygon(new Point[] { new(4, 1), new(4, 14), new(7, 11), new(9.5, 15.5), new(11.5, 14.5), new(9, 10), new(13, 10) }, Colors.White, C("#1E2A3A"), 1);
            return;
        }
        if (key == "root")
        {
            g.Rect(new Rect(1.5, 2.5, 13, 11), C("#3C7DD9"), C("#1E4E91"), 1, 2, false, C("#2A63B5"));
            g.Rect(new Rect(4, 5, 3.5, 3.5), Colors.White, null);
            g.Rect(new Rect(8.5, 8, 3.5, 3.5), Colors.White, null);
            g.Line(new Point(7.5, 6.7), new Point(10, 8), Colors.White, 1);
            return;
        }
        if (key == "package")
        {
            g.Rect(new Rect(1.5, 2.5, 6, 3), C("#F0C77A"), C("#9A6A2F"), 1);
            g.Rect(new Rect(1.5, 5, 13, 9), C("#F6D893"), C("#9A6A2F"), 1, 0, false, C("#E8B558"));
            return;
        }
        if (key.StartsWith("feature:"))
        {
            if (key.EndsWith("attr"))
            {
                g.Rect(new Rect(3, 5, 10, 6), C("#3A9AD9"), C("#1F5F8B"), 1, 1);
            }
            else
            {
                g.Polygon(new Point[] { new(8, 3), new(13, 8), new(8, 13), new(3, 8) }, C("#C45ED1"), C("#7A2D85"), 1);
            }
            return;
        }
        if (key.StartsWith("diagram:"))
        {
            Enum.TryParse<DiagramType>(key[8..], out var dt);
            g.Rect(new Rect(1.5, 1.5, 13, 13), Colors.White, C("#5D6B80"), 1, 1);
            g.Rect(new Rect(1.5, 1.5, 13, 3), C("#7C8CA6"), null);
            switch (dt)
            {
                case DiagramType.Class:
                    g.Rect(new Rect(3.5, 6, 4, 4), C("#FFE29A"), C("#8C6D1F"), 0.8);
                    g.Rect(new Rect(9, 9, 4, 4), C("#FFE29A"), C("#8C6D1F"), 0.8);
                    g.Line(new Point(7.5, 8), new Point(9, 10.5), ink, 0.8);
                    break;
                case DiagramType.Sequence:
                    g.Rect(new Rect(3, 5.5, 4, 2.5), C("#BFD5F2"), C("#3E5F8A"), 0.7);
                    g.Rect(new Rect(9, 5.5, 4, 2.5), C("#BFD5F2"), C("#3E5F8A"), 0.7);
                    g.Line(new Point(5, 8), new Point(5, 14), ink, 0.6, true);
                    g.Line(new Point(11, 8), new Point(11, 14), ink, 0.6, true);
                    g.Line(new Point(5, 10.5), new Point(11, 10.5), C("#C0392B"), 0.9);
                    break;
                case DiagramType.Component:
                    g.Rect(new Rect(4, 6, 8, 6.5), C("#BFD8F5"), C("#2F5D8F"), 0.8);
                    g.Rect(new Rect(3, 7.5, 2.5, 1.3), Colors.White, C("#2F5D8F"), 0.6);
                    g.Rect(new Rect(3, 10, 2.5, 1.3), Colors.White, C("#2F5D8F"), 0.6);
                    break;
                case DiagramType.State:
                    g.Rect(new Rect(3, 6, 10, 6), C("#C6E1F7"), C("#2E6A99"), 0.8, 2.5);
                    break;
                case DiagramType.UseCase:
                    g.Ellipse(new Rect(3, 6.5, 10, 6), C("#CFEABF"), C("#4A7D2E"), 0.8);
                    break;
                case DiagramType.Activity:
                    g.Ellipse(new Rect(3, 5.8, 3, 3), ink, null);
                    g.Rect(new Rect(6.5, 9, 7, 4), C("#C9EBD7"), C("#2F7A55"), 0.8, 2);
                    break;
            }
            return;
        }
        if (key.StartsWith("conn:"))
        {
            Enum.TryParse<ConnectorKind>(key[5..], out var ck);
            var a = new Point(2, 14); var b = new Point(14, 2);
            bool dashed = DiagramRenderer.IsDashed(ck);
            switch (ck)
            {
                case ConnectorKind.Inheritance:
                case ConnectorKind.Realization:
                    g.Line(a, new Point(10.2, 5.8), ink, 1.1, dashed);
                    g.Polygon(new Point[] { b, new(8.5, 4.2), new(11.8, 7.5) }, Colors.White, ink, 1);
                    break;
                case ConnectorKind.Aggregation:
                case ConnectorKind.Composition:
                    g.Line(a, new Point(9, 7), ink, 1.1);
                    g.Polygon(new Point[] { b, new(13.3, 6.8), new(9, 7), new(9.2, 2.7) }, ck == ConnectorKind.Composition ? ink : Colors.White, ink, 1);
                    break;
                case ConnectorKind.Association:
                case ConnectorKind.NoteLink:
                    g.Line(a, b, ink, 1.2, dashed);
                    break;
                case ConnectorKind.MessageSync:
                case ConnectorKind.MessageAsync:
                case ConnectorKind.MessageReturn:
                case ConnectorKind.CreateMessage:
                case ConnectorKind.DestroyMessage:
                {
                    var y = 8.0;
                    bool back = ck == ConnectorKind.MessageReturn;
                    var p1 = new Point(back ? 14 : 2, y); var p2 = new Point(back ? 2 : 14, y);
                    g.Line(p1, p2, ink, 1.1, dashed);
                    var dir = back ? 1 : -1;
                    if (ck is ConnectorKind.MessageSync or ConnectorKind.DestroyMessage)
                        g.Polygon(new Point[] { p2, new(p2.X + dir * 5, y - 3), new(p2.X + dir * 5, y + 3) }, ink, ink, 0.5);
                    else
                        g.Polyline(new Point[] { new(p2.X + dir * 5, y - 3.5), p2, new(p2.X + dir * 5, y + 3.5) }, ink, 1.1);
                    if (ck == ConnectorKind.DestroyMessage)
                    {
                        g.Line(new Point(11, 11), new Point(15, 15), C("#C0392B"), 1.3);
                        g.Line(new Point(11, 15), new Point(15, 11), C("#C0392B"), 1.3);
                    }
                    break;
                }
                default:
                    g.Line(a, b, ink, 1.1, dashed);
                    g.Polyline(new Point[] { new(8.5, 2.6), b, new(13.4, 7.5) }, ink, 1.2);
                    break;
            }
            return;
        }
        if (key.StartsWith("shape:"))
        {
            Enum.TryParse<ShapeKind>(key[6..], out var k);
            var (fh, sh) = DiagramCatalog.DefaultColors(k);
            var f = DiagramRenderer.Lerp(C(fh), C(sh), 0.25); var st = C(sh);
            switch (k)
            {
                case ShapeKind.Class:
                case ShapeKind.Interface:
                case ShapeKind.Enumeration:
                    g.Rect(new Rect(1.5, 2, 13, 12), DiagramRenderer.Lerp(C(fh), Colors.White, 0.3), st, 1);
                    g.Rect(new Rect(1.5, 2, 13, 4), f, st, 1);
                    g.Line(new Point(1.5, 10), new Point(14.5, 10), st, 0.8);
                    if (k == ShapeKind.Interface) g.Ellipse(new Rect(10, 0.5, 5, 5), Colors.White, st, 1);
                    break;
                case ShapeKind.Package:
                    g.Rect(new Rect(1.5, 2.5, 6, 3), f, st, 1);
                    g.Rect(new Rect(1.5, 5, 13, 9), f, st, 1);
                    break;
                case ShapeKind.Note:
                    g.Polygon(new Point[] { new(2, 1.5), new(10.5, 1.5), new(14, 5), new(14, 14.5), new(2, 14.5) }, C("#FFF6B8"), C("#9C9555"), 1);
                    g.Polyline(new Point[] { new(10.5, 1.5), new(10.5, 5), new(14, 5) }, C("#9C9555"), 1);
                    break;
                case ShapeKind.Actor:
                case ShapeKind.ActorLifeline:
                    g.Ellipse(new Rect(5.5, 0.8, 5, 5), Colors.White, ink, 1.1);
                    g.Line(new Point(8, 5.8), new Point(8, 10.5), ink, 1.1);
                    g.Line(new Point(3.5, 7.5), new Point(12.5, 7.5), ink, 1.1);
                    g.Line(new Point(8, 10.5), new Point(4.5, 15), ink, 1.1);
                    g.Line(new Point(8, 10.5), new Point(11.5, 15), ink, 1.1);
                    break;
                case ShapeKind.ObjectLifeline:
                    g.Rect(new Rect(2, 1.5, 12, 5), f, st, 1);
                    g.Line(new Point(8, 6.5), new Point(8, 15), ink, 1, true);
                    break;
                case ShapeKind.Component:
                    g.Rect(new Rect(3.5, 2, 11, 12), f, st, 1);
                    g.Rect(new Rect(1.5, 4.5, 4, 2.2), Colors.White, st, 0.9);
                    g.Rect(new Rect(1.5, 9, 4, 2.2), Colors.White, st, 0.9);
                    break;
                case ShapeKind.Port:
                    g.Rect(new Rect(4.5, 4.5, 7, 7), Colors.White, st, 1.2);
                    break;
                case ShapeKind.Artifact:
                    g.Polygon(new Point[] { new(3, 1.5), new(10, 1.5), new(13, 4.5), new(13, 14.5), new(3, 14.5) }, Colors.White, st, 1);
                    g.Polyline(new Point[] { new(10, 1.5), new(10, 4.5), new(13, 4.5) }, st, 1);
                    break;
                case ShapeKind.Node:
                    g.Polygon(new Point[] { new(1.5, 5), new(5, 1.5), new(14.5, 1.5), new(11, 5) }, C("#F2F5F8"), st, 1);
                    g.Polygon(new Point[] { new(11, 5), new(14.5, 1.5), new(14.5, 11), new(11, 14.5) }, C("#B5C0CC"), st, 1);
                    g.Rect(new Rect(1.5, 5, 9.5, 9.5), f, st, 1);
                    break;
                case ShapeKind.State:
                    g.Rect(new Rect(1.5, 3.5, 13, 9), f, st, 1, 3.5);
                    break;
                case ShapeKind.Action:
                    g.Rect(new Rect(1.5, 4, 13, 8), f, st, 1, 4);
                    break;
                case ShapeKind.InitialNode:
                case ShapeKind.InitialState:
                    g.Ellipse(new Rect(3.5, 3.5, 9, 9), ink, null);
                    break;
                case ShapeKind.FinalNode:
                case ShapeKind.FinalState:
                    g.Ellipse(new Rect(2.5, 2.5, 11, 11), Colors.White, ink, 1.2);
                    g.Ellipse(new Rect(5, 5, 6, 6), ink, null);
                    break;
                case ShapeKind.Choice:
                case ShapeKind.Decision:
                case ShapeKind.Merge:
                    g.Polygon(new Point[] { new(8, 1.5), new(14.5, 8), new(8, 14.5), new(1.5, 8) }, C("#FFE6BF"), C("#A0661E"), 1);
                    break;
                case ShapeKind.Fork:
                case ShapeKind.Join:
                    g.Rect(new Rect(1.5, 6.5, 13, 3), ink, null, 0, 1);
                    break;
                case ShapeKind.UseCase:
                    g.Ellipse(new Rect(1, 4, 14, 8.5), f, st, 1);
                    break;
                case ShapeKind.SystemBoundary:
                    g.Rect(new Rect(1.5, 1.5, 13, 13), C("#F4F6FA"), st, 1);
                    g.Line(new Point(1.5, 4.5), new Point(14.5, 4.5), st, 0.7);
                    break;
                default:
                    g.Rect(new Rect(2, 2, 12, 12), f, st, 1);
                    break;
            }
        }
    }
}
