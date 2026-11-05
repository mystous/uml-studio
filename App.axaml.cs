using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using UmlStudio.Models;
using UmlStudio.Services;
using UmlStudio.ViewModels;
using UmlStudio.Views;

namespace UmlStudio;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var args = desktop.Args ?? Array.Empty<string>();
            string? Opt(string name) { var i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }

            var vm = new MainWindowViewModel();
            var win = new MainWindow { DataContext = vm };
            desktop.MainWindow = win;

            var file = args.Where((a, i) => !a.StartsWith("--") && (i == 0 || !args[i - 1].StartsWith("--")) && File.Exists(a)).FirstOrDefault();
            if (file != null) vm.OpenPath(file);
            else if (args.Contains("--demo") || Opt("--shot") != null) vm.LoadProject(SampleProject.CreateDemo(), null);

            var shot = Opt("--shot");
            if (shot != null)
            {
                win.Width = 1600; win.Height = 940;
                win.Opened += async (_, _) =>
                {
                    await System.Threading.Tasks.Task.Delay(400);
                    var dIdx = int.TryParse(Opt("--diagram"), out var n) ? n : -1;
                    if (args.Contains("--start")) vm.ShowStartPageCommand.Execute(null);
                    else if (dIdx >= 0)
                    {
                        var ds = vm.Project!.AllDiagrams().ToList();
                        vm.OpenDiagram(ds[Math.Clamp(dIdx, 0, ds.Count - 1)]);
                        await System.Threading.Tasks.Task.Delay(200);
                        vm.ZoomFitCommand.Execute(null);
                        await System.Threading.Tasks.Task.Delay(300);
                        vm.ZoomFitCommand.Execute(null);
                        var sel = Opt("--select");
                        if (sel != null)
                        {
                            var s = vm.CurrentDiagram!.Shapes.FirstOrDefault(x => x.Name == sel);
                            if (s != null) vm.SetSelection(new[] { s }, null);
                        }
                    }
                    var tab = Opt("--ribbon");
                    if (tab != null && int.TryParse(tab, out var ti))
                    {
                        var tc = win.GetVisualDescendantsOfType<TabControl>().FirstOrDefault(t => t.Classes.Contains("ribbon"));
                        if (tc != null) tc.SelectedIndex = ti;
                    }
                    await System.Threading.Tasks.Task.Delay(700);
                    var size = new PixelSize((int)win.Bounds.Width, (int)win.Bounds.Height);
                    using var bmp = new RenderTargetBitmap(size);
                    bmp.Render(win);
                    bmp.Save(shot);
                    desktop.Shutdown();
                };
            }
        }
        base.OnFrameworkInitializationCompleted();
    }
}

internal static class VisualExt
{
    public static System.Collections.Generic.IEnumerable<T> GetVisualDescendantsOfType<T>(this Avalonia.Visual v) where T : Avalonia.Visual
        => Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(v).OfType<T>();
}
