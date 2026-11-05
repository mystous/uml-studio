using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using UmlStudio.ViewModels;

namespace UmlStudio.Views;

public partial class MainWindow : Window, IDialogService
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainWindowViewModel vm) vm.Dialogs = this;
        };
        Browser.AddHandler(DoubleTappedEvent, OnBrowserDoubleTapped, RoutingStrategies.Bubble, true);
        Browser.AddHandler(PointerPressedEvent, OnBrowserPointerPressed, RoutingStrategies.Tunnel, true);
        Browser.KeyDown += (_, e) =>
        {
            if (DataContext is not MainWindowViewModel vm) return;
            if (e.Key == Key.F2) { vm.RenameNodeCommand.Execute(null); e.Handled = true; }
            else if (e.Key == Key.Enter) { vm.OpenNode(vm.SelectedBrowserNode); e.Handled = true; }
        };
    }

    private void OnBrowserDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        var item = (e.Source as Visual)?.FindAncestorOfType<TreeViewItem>(true);
        if (item?.DataContext is BrowserNode node) { vm.OpenNode(node); e.Handled = true; }
    }

    private void OnBrowserPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(Browser).Properties.IsRightButtonPressed) return;
        var item = (e.Source as Visual)?.FindAncestorOfType<TreeViewItem>(true);
        if (item?.DataContext is BrowserNode node && DataContext is MainWindowViewModel vm)
            vm.SelectedBrowserNode = node;
    }

    // ===== IDialogService =====
    private static readonly FilePickerFileType ProjType = new("UML Studio 프로젝트") { Patterns = new[] { "*.umlproj" } };

    public async Task<string?> OpenProjectAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "프로젝트 열기",
            AllowMultiple = false,
            FileTypeFilter = new[] { ProjType, FilePickerFileTypes.All }
        });
        return files.FirstOrDefault()?.TryGetLocalPath();
    }

    public async Task<string?> SaveFileAsync(string title, string suggestedName, string ext, string filterName)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            SuggestedFileName = suggestedName,
            DefaultExtension = ext,
            ShowOverwritePrompt = true,
            FileTypeChoices = new[] { new FilePickerFileType(filterName) { Patterns = new[] { "*." + ext } } }
        });
        return file?.TryGetLocalPath();
    }

    private Window MakeDialog(string title, Control body, out StackPanel buttons)
    {
        buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
        var header = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#1A4C99")),
            Padding = new Thickness(14, 9),
            Child = new TextBlock { Text = title, FontWeight = FontWeight.SemiBold, Foreground = Brushes.White }
        };
        var root = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        root.Children.Add(header);
        var footer = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#1D2433")),
            BorderBrush = new SolidColorBrush(Color.Parse("#323D57")),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(14, 10),
            Child = buttons
        };
        DockPanel.SetDock(footer, Dock.Bottom);
        root.Children.Add(footer);
        root.Children.Add(new Border { Padding = new Thickness(16, 14), Child = body });
        return new Window
        {
            Title = title,
            Width = 420,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.Parse("#252E42")),
            Content = root
        };
    }

    public async Task<string?> PromptAsync(string title, string label, string initial)
    {
        var box = new TextBox { Text = initial, MinWidth = 360 };
        var body = new StackPanel { Spacing = 6, Children = { new TextBlock { Text = label, Foreground = new SolidColorBrush(Color.Parse("#AEB9CC")) }, box } };
        var dlg = MakeDialog(title, body, out var buttons);
        string? result = null;
        var ok = new Button { Content = "확인", IsDefault = true, MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center, Classes = { "accent" } };
        var cancel = new Button { Content = "취소", IsCancel = true, MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center };
        ok.Click += (_, _) => { result = box.Text; dlg.Close(); };
        cancel.Click += (_, _) => dlg.Close();
        buttons.Children.Add(ok); buttons.Children.Add(cancel);
        dlg.Opened += (_, _) => { box.Focus(); box.SelectAll(); };
        await dlg.ShowDialog(this);
        return result;
    }

    public async Task<bool> ConfirmAsync(string title, string message)
    {
        var body = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.Parse("#E6ECF5")) };
        var dlg = MakeDialog(title, body, out var buttons);
        var result = false;
        var ok = new Button { Content = "예", IsDefault = true, MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center, Classes = { "accent" } };
        var cancel = new Button { Content = "아니요", IsCancel = true, MinWidth = 80, HorizontalContentAlignment = HorizontalAlignment.Center };
        ok.Click += (_, _) => { result = true; dlg.Close(); };
        cancel.Click += (_, _) => dlg.Close();
        buttons.Children.Add(ok); buttons.Children.Add(cancel);
        await dlg.ShowDialog(this);
        return result;
    }

    public void FocusPropertyName()
    {
        PropName.Focus();
        PropName.SelectAll();
    }

    public void FocusSearch()
    {
        BrowserSearch.Focus();
        BrowserSearch.SelectAll();
    }
}
