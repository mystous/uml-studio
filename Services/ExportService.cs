using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using UmlStudio.Models;
using UmlStudio.Rendering;

namespace UmlStudio.Services;

public static class ExportService
{
    private static Rect Area(UmlDiagram d, bool frame)
    {
        var b = DiagramRenderer.ContentBounds(d);
        b = frame ? b.Inflate(new Thickness(24, 44, 24, 24)) : b;
        return b.Inflate(24);
    }

    public static void ExportPng(UmlDiagram d, string path, double scale = 2.0, bool frame = true)
    {
        var area = Area(d, frame);
        var w = Math.Max(1, (int)Math.Ceiling(area.Width * scale));
        var h = Math.Max(1, (int)Math.Ceiling(area.Height * scale));
        using var bmp = new RenderTargetBitmap(new PixelSize(w, h), new Vector(96 * scale, 96 * scale));
        using (var ctx = bmp.CreateDrawingContext(true))
        {
            ctx.FillRectangle(Brushes.White, new Rect(0, 0, area.Width, area.Height));
            using (ctx.PushTransform(Matrix.CreateTranslation(-area.X, -area.Y)))
                DiagramRenderer.Render(new AvaloniaSurface(ctx), d, null, frame);
        }
        bmp.Save(path);
    }

    public static string ToSvg(UmlDiagram d, bool frame = true)
    {
        var area = Area(d, frame);
        var svg = new SvgSurface();
        DiagramRenderer.Render(svg, d, null, frame);
        return svg.ToSvg(area.Width, area.Height, -area.X, -area.Y);
    }

    public static void ExportSvg(UmlDiagram d, string path, bool frame = true) =>
        File.WriteAllText(path, ToSvg(d, frame));
}
