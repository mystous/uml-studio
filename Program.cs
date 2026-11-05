using System;
using System.IO;
using System.Linq;
using Avalonia;
using UmlStudio.Services;

namespace UmlStudio;

internal sealed class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        // Headless helpers (no window)
        var ws = Array.IndexOf(args, "--write-sample");
        if (ws >= 0 && ws + 1 < args.Length)
        {
            File.WriteAllText(args[ws + 1], ProjectSerializer.Serialize(SampleProject.CreateDemo()));
            Console.WriteLine("sample written: " + args[ws + 1]);
            return 0;
        }
        var ex = Array.IndexOf(args, "--export");
        if (ex >= 0 && ex + 1 < args.Length)
        {
            BuildAvaloniaApp().SetupWithoutStarting();
            var dir = args[ex + 1];
            Directory.CreateDirectory(dir);
            var p = SampleProject.CreateDemo();
            var i = 1;
            foreach (var d in p.AllDiagrams())
            {
                var baseName = Path.Combine(dir, $"{i++:00}-{d.Name}");
                ExportService.ExportPng(d, baseName + ".png");
                ExportService.ExportSvg(d, baseName + ".svg");
                Console.WriteLine("exported " + baseName);
            }
            return 0;
        }
        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
