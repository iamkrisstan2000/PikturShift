using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using PikturShift.Interop;
using PikturShift.Theming;
using PikturShift.ViewModels;

namespace PikturShift;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();

        RestoreWindowPlacement();
        Loaded += OnLoaded;
        StateChanged += OnStateChanged;
        Closing += OnClosing;
        ThemeManager.ThemeChanged += (_, _) => ApplyNativeChrome();
    }

    /// <summary>Keeps the drop overlay clear of the navigation pane as it animates.</summary>
    private void OnNavPaneSizeChanged(object sender, SizeChangedEventArgs e) =>
        DragOverlay.Margin = new Thickness(NavPane.ActualWidth + 8, 8, 8, 8);

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        ThemeManager.RefreshSystemSync();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // A backdrop only exists on Windows 11 22H2+; if it is missing the theme stays opaque,
        // so translucent alpha never blends against black.
        try
        {
            var handle = new WindowInteropHelper(this).Handle;
            bool mica = NativeWindow.TryEnableBackdrop(handle);

            // Translucent surfaces need a transparent composition background, otherwise WPF
            // composites them over black - which is what showed up as a dark wedge in the
            // rounded corner of the content surface.
            NativeWindow.SetCompositionBackground(
                handle, mica ? Colors.Transparent : ThemeManager.Palette.Window);
            ThemeManager.EnableTranslucency(mica);
        }
        catch
        {
            ThemeManager.EnableTranslucency(false);
        }

        ApplyNativeChrome();
        UpdateMaximizeGlyph();
        Activate();
    }

    private void OnStateChanged(object? sender, EventArgs e) => UpdateMaximizeGlyph();

    private void UpdateMaximizeGlyph()
    {
        MaxRestoreButton.Content = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
        MaxRestoreButton.ToolTip = WindowState == WindowState.Maximized ? "Restore" : "Maximize";
        RootBorder.BorderThickness = WindowState == WindowState.Maximized ? new Thickness(0) : new Thickness(1);
    }

    private void ApplyNativeChrome()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        NativeWindow.ApplyRoundedCorners(handle);
        NativeWindow.SetImmersiveDarkMode(handle, ThemeManager.IsDark);

        // Keep the composition background in step with the backdrop: transparent while Mica is
        // doing the painting, the plain window colour when translucency is off.
        NativeWindow.SetCompositionBackground(
            handle, ThemeManager.UseTranslucency ? Colors.Transparent : ThemeManager.Palette.Window);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ApplyNativeChrome();
    }

    // ------------------------------------------------------------ placement

    private void RestoreWindowPlacement()
    {
        var settings = _viewModel.ExportPlacement();
        if (settings is null)
        {
            return;
        }

        var (left, top, width, height, maximized) = settings.Value;
        Width = width;
        Height = height;

        if (!double.IsNaN(left) && !double.IsNaN(top) && IsOnScreen(left, top, width, height))
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = left;
            Top = top;
        }

        if (maximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    private static bool IsOnScreen(double left, double top, double width, double height)
    {
        var virtualLeft = SystemParameters.VirtualScreenLeft;
        var virtualTop = SystemParameters.VirtualScreenTop;
        var virtualRight = virtualLeft + SystemParameters.VirtualScreenWidth;
        var virtualBottom = virtualTop + SystemParameters.VirtualScreenHeight;

        return left >= virtualLeft - 40
            && top >= virtualTop - 40
            && left + width <= virtualRight + 40
            && top + height <= virtualBottom + 40;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        var bounds = WindowState == WindowState.Normal
            ? new Rect(Left, Top, Width, Height)
            : RestoreBounds;

        _viewModel.SaveWindowState(bounds.Left, bounds.Top, bounds.Width, bounds.Height,
            WindowState == WindowState.Maximized);
        _viewModel.SaveOnExit();
    }

    // -------------------------------------------------------------- caption

    private void OnMinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximizeClick(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private const string ProjectUrl = "https://github.com/iamkrisstan2000/PikturShift";

    /// <summary>Opens the project page in the default browser (used by the About icon).</summary>
    private void OnOpenProjectPage(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(ProjectUrl) { UseShellExecute = true });
        }
        catch
        {
            // No default browser / blocked by policy - never break the window over a link.
            _viewModel.ShowNotice("Couldn't open the browser.");
        }
    }

    // ------------------------------------------------------------- dropzone

    private void OnDropZoneClick(object sender, MouseButtonEventArgs e) =>
        _viewModel.BrowseFilesCommand.Execute(null);

    private void OnFolderBoxClick(object sender, MouseButtonEventArgs e) =>
        _viewModel.BrowseFolderCommand.Execute(null);

    private void OnFillSwatchClick(object sender, RoutedEventArgs e)
    {
        _viewModel.IsFillPaletteOpen = !_viewModel.IsFillPaletteOpen;
    }

    private void OnFillColorPick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string hex })
        {
            _viewModel.FillColor = hex;
            _viewModel.IsFillPaletteOpen = false;
        }
    }

    private void OnResetSettingsClick(object sender, RoutedEventArgs e)
    {
        var result = MessageBox.Show(
            this,
            "Restore theme, accent and conversion settings to their defaults?",
            "Reset settings",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.OK)
        {
            _viewModel.ResetSettingsCommand.Execute(null);
        }
    }

    private void OnJobDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.SelectedJob is { } job)
        {
            _viewModel.OpenJobFolderCommand.Execute(job);
        }
    }

    // ---------------------------------------------------------- drag & drop

    protected override void OnDragEnter(DragEventArgs e) => HandleDragOver(e);

    protected override void OnDragOver(DragEventArgs e) => HandleDragOver(e);

    private void HandleDragOver(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.Copy;
        if (_viewModel.IsSettingsPage)
        {
            _viewModel.IsSettingsPage = false;
        }

        DragOverlay.Visibility = Visibility.Visible;
        DropDashHover.Opacity = 1;
        e.Handled = true;
    }

    protected override void OnDragLeave(DragEventArgs e)
    {
        ResetDragVisuals();
        base.OnDragLeave(e);
    }

    protected override void OnDrop(DragEventArgs e)
    {
        ResetDragVisuals();
        if (e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            _viewModel.AddPaths(paths, fromDrop: true);
        }

        base.OnDrop(e);
    }

    private void ResetDragVisuals()
    {
        DragOverlay.Visibility = Visibility.Collapsed;
        DropDashHover.Opacity = 0;
    }
}
