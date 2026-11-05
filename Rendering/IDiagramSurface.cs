using Avalonia;
using Avalonia.Media;

namespace UmlStudio.Rendering;

public enum TextAlign { Left, Center, Right }

/// <summary>Abstract drawing surface so the same renderer drives screen, PNG and SVG output.</summary>
public interface IDiagramSurface
{
    void Rect(Rect r, Color? fill, Color? stroke, double thickness = 1, double radius = 0, bool dashed = false, Color? fill2 = null);
    void Ellipse(Rect r, Color? fill, Color? stroke, double thickness = 1, bool dashed = false, Color? fill2 = null);
    void Line(Point a, Point b, Color color, double thickness = 1, bool dashed = false);
    void Polyline(IReadOnlyList<Point> pts, Color color, double thickness = 1, bool dashed = false);
    void Polygon(IReadOnlyList<Point> pts, Color? fill, Color? stroke, double thickness = 1, Color? fill2 = null);
    void Text(string text, Point topLeft, double size, Color color, bool bold = false, bool italic = false, bool underline = false);
}

public static class TextMetrics
{
    public static readonly FontFamily Family = new("Malgun Gothic, Segoe UI, Noto Sans CJK KR, NanumGothic, Inter, sans-serif");
    public const string CssFamily = "'Malgun Gothic','Segoe UI','Noto Sans CJK KR','NanumGothic',sans-serif";

    public static Typeface Face(bool bold, bool italic) =>
        new(Family, italic ? FontStyle.Italic : FontStyle.Normal, bold ? FontWeight.Bold : FontWeight.Normal);

    public static FormattedText Format(string text, double size, Color color, bool bold, bool italic) =>
        new(text, System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
            Face(bold, italic), size, new SolidColorBrush(color));

    private static readonly Dictionary<(string, double, bool, bool), Size> Cache = new();

    public static Size Measure(string text, double size, bool bold = false, bool italic = false)
    {
        if (string.IsNullOrEmpty(text)) return new Size(0, size * 1.3);
        var key = (text, size, bold, italic);
        if (Cache.TryGetValue(key, out var s)) return s;
        var ft = Format(text, size, Colors.Black, bold, italic);
        s = new Size(ft.WidthIncludingTrailingWhitespace, ft.Height);
        if (Cache.Count > 5000) Cache.Clear();
        Cache[key] = s;
        return s;
    }

    public static List<string> Wrap(string text, double size, double maxWidth, bool bold = false)
    {
        var lines = new List<string>();
        foreach (var para in (text ?? "").Replace("\r", "").Split('\n'))
        {
            if (para.Length == 0) { lines.Add(""); continue; }
            var cur = "";
            foreach (var ch in SplitTokens(para))
            {
                var trial = cur + ch;
                if (cur.Length > 0 && Measure(trial, size, bold).Width > maxWidth)
                {
                    lines.Add(cur.TrimEnd());
                    cur = ch.TrimStart();
                }
                else cur = trial;
            }
            lines.Add(cur);
        }
        return lines;
    }

    // Split into words (latin) but allow breaking between Hangul / CJK characters.
    private static IEnumerable<string> SplitTokens(string s)
    {
        var buf = "";
        foreach (var c in s)
        {
            if (c == ' ') { buf += c; yield return buf; buf = ""; }
            else if (c >= 0x1100) { if (buf.Length > 0) { yield return buf; buf = ""; } yield return c.ToString(); }
            else buf += c;
        }
        if (buf.Length > 0) yield return buf;
    }
}
