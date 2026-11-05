using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Media;

namespace UmlStudio.Rendering;

public sealed class SvgSurface : IDiagramSurface
{
    private readonly StringBuilder _body = new();
    private readonly StringBuilder _defs = new();
    private int _grad;

    private static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    private static string Hex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";
    private static string Op(Color c) => c.A == 255 ? "" : $" {{0}}-opacity=\"{F(c.A / 255.0)}\"";

    private string Fill(Color? c, Color? c2)
    {
        if (c is null) return "fill=\"none\"";
        if (c2 is { } g && g != c.Value)
        {
            var id = "g" + (++_grad);
            _defs.Append($"<linearGradient id=\"{id}\" x1=\"0\" y1=\"0\" x2=\"0\" y2=\"1\"><stop offset=\"0\" stop-color=\"{Hex(c.Value)}\"/><stop offset=\"1\" stop-color=\"{Hex(g)}\"/></linearGradient>");
            return $"fill=\"url(#{id})\"";
        }
        return $"fill=\"{Hex(c.Value)}\"" + string.Format(Op(c.Value), "fill");
    }

    private static string Stroke(Color? c, double t, bool dashed)
    {
        if (c is null || t <= 0) return "stroke=\"none\"";
        var s = $"stroke=\"{Hex(c.Value)}\" stroke-width=\"{F(t)}\"" + string.Format(Op(c.Value), "stroke");
        if (dashed) s += " stroke-dasharray=\"5,3\"";
        return s;
    }

    public void Rect(Rect r, Color? fill, Color? stroke, double thickness = 1, double radius = 0, bool dashed = false, Color? fill2 = null) =>
        _body.Append($"<rect x=\"{F(r.X)}\" y=\"{F(r.Y)}\" width=\"{F(r.Width)}\" height=\"{F(r.Height)}\" rx=\"{F(radius)}\" {Fill(fill, fill2)} {Stroke(stroke, thickness, dashed)}/>\n");

    public void Ellipse(Rect r, Color? fill, Color? stroke, double thickness = 1, bool dashed = false, Color? fill2 = null) =>
        _body.Append($"<ellipse cx=\"{F(r.Center.X)}\" cy=\"{F(r.Center.Y)}\" rx=\"{F(r.Width / 2)}\" ry=\"{F(r.Height / 2)}\" {Fill(fill, fill2)} {Stroke(stroke, thickness, dashed)}/>\n");

    public void Line(Point a, Point b, Color color, double thickness = 1, bool dashed = false) =>
        _body.Append($"<line x1=\"{F(a.X)}\" y1=\"{F(a.Y)}\" x2=\"{F(b.X)}\" y2=\"{F(b.Y)}\" {Stroke(color, thickness, dashed)}/>\n");

    private static string Pts(IReadOnlyList<Point> pts) => string.Join(" ", pts.Select(p => F(p.X) + "," + F(p.Y)));

    public void Polyline(IReadOnlyList<Point> pts, Color color, double thickness = 1, bool dashed = false) =>
        _body.Append($"<polyline points=\"{Pts(pts)}\" fill=\"none\" {Stroke(color, thickness, dashed)}/>\n");

    public void Polygon(IReadOnlyList<Point> pts, Color? fill, Color? stroke, double thickness = 1, Color? fill2 = null) =>
        _body.Append($"<polygon points=\"{Pts(pts)}\" {Fill(fill, fill2)} {Stroke(stroke, thickness, false)}/>\n");

    public void Text(string text, Point topLeft, double size, Color color, bool bold = false, bool italic = false, bool underline = false)
    {
        if (string.IsNullOrEmpty(text)) return;
        var esc = System.Security.SecurityElement.Escape(text);
        var baseline = topLeft.Y + size * 1.0;
        var attrs = $"font-size=\"{F(size)}\" fill=\"{Hex(color)}\"";
        if (bold) attrs += " font-weight=\"bold\"";
        if (italic) attrs += " font-style=\"italic\"";
        if (underline) attrs += " text-decoration=\"underline\"";
        _body.Append($"<text x=\"{F(topLeft.X)}\" y=\"{F(baseline)}\" {attrs} xml:space=\"preserve\">{esc}</text>\n");
    }

    public string ToSvg(double width, double height, double offsetX, double offsetY)
    {
        var sb = new StringBuilder();
        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{F(width)}\" height=\"{F(height)}\" viewBox=\"0 0 {F(width)} {F(height)}\" font-family=\"{TextMetrics.CssFamily}\">\n");
        sb.Append("<defs>").Append(_defs).Append("</defs>\n");
        sb.Append($"<rect width=\"100%\" height=\"100%\" fill=\"#FFFFFF\"/>\n");
        sb.Append($"<g transform=\"translate({F(offsetX)},{F(offsetY)})\">\n");
        sb.Append(_body);
        sb.Append("</g>\n</svg>\n");
        return sb.ToString();
    }
}
