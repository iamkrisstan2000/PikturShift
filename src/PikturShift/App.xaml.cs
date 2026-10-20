using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using PikturShift.Core.Settings;
using PikturShift.Theming;
using PikturShift.ViewModels;

namespace PikturShift;

public partial class App : Application
{
    private SettingsStore? _store;
    private AppSettings? _settings;

    public static string CrashLogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PikturShift",
        "crash.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

        // Headless smoke test used by the build pipeline (no UI, works in CI).
        if (e.Args.Any(a => a.Equals("--selftest", StringComparison.OrdinalIgnoreCase)))
        {
            int code = RunSelfTest();
            Shutdown(code);
            return;
        }

        _store = new SettingsStore();
        SettingsStore.MigrateLegacyFile();
        _settings = _store.Load();
        ThemeManager.Initialize(_settings.Theme, _settings.Accent);

        var viewModel = new MainViewModel(_store, _settings);
        var window = new MainWindow(viewModel);
        MainWindow = window;

        // "Send to" / drag-onto-the-exe / file associations: queue whatever was passed on the command line.
        var startupFiles = e.Args.Where(a => !a.StartsWith('-') && File.Exists(a)).ToArray();
        if (startupFiles.Length > 0)
        {
            window.ContentRendered += (_, _) => viewModel.AddPaths(startupFiles);
        }

        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        ThemeManager.Shutdown();
        base.OnExit(e);
    }

    private static int RunSelfTest()
    {
        var log = new StringBuilder();
        try
        {
            var store = new SettingsStore(Path.Combine(Path.GetTempPath(), "pc_selftest", "settings.json"));
            var settings = new AppSettings();
            store.Save(settings);
            log.AppendLine("settings: ok");

            ThemeManager.Initialize("Dark", "matcha");
            log.AppendLine($"theme dark accent: {ThemeManager.AccentColor}");
            log.AppendLine($"system accent: {ThemeManager.SystemAccentHex}");

            ThemeManager.Apply(theme: "Light");
            log.AppendLine($"theme light accent: {ThemeManager.AccentColor}");

            ThemeManager.Apply(accent: "mono");
            log.AppendLine($"mono on light: {ThemeManager.AccentColor}");

            ThemeManager.Apply(theme: "Dark");
            log.AppendLine($"mono on dark: {ThemeManager.AccentColor}");

            // Translucency is only real when a system backdrop exists; report both states.
            ThemeManager.Apply(theme: "Dark");
            ThemeManager.EnableTranslucency(true);
            log.AppendLine($"translucency dark rail: {AlphaOf("RailBgBrush")} content: {AlphaOf("ContentBgBrush")} caption: {AlphaOf("CaptionBgBrush")}");

            ThemeManager.Apply(theme: "Light");
            log.AppendLine($"translucency light rail: {AlphaOf("RailBgBrush")} content: {AlphaOf("ContentBgBrush")} caption: {AlphaOf("CaptionBgBrush")}");

            ThemeManager.EnableTranslucency(false);
            ThemeManager.Apply(theme: "Dark");
            log.AppendLine($"translucency off rail: {AlphaOf("RailBgBrush")}");

            var temp = Path.Combine(Path.GetTempPath(), "pc_selftest");
            Directory.CreateDirectory(temp);
            var sample = Path.Combine(temp, "sample.png");
            using (var img = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(64, 48))
            {
                using (var fs = File.Create(sample))
                {
                    img.Save(fs, new SixLabors.ImageSharp.Formats.Png.PngEncoder());
                }
            }

            var result = Core.Imaging.ImageConverter.Convert(
                sample,
                Path.Combine(temp, "sample.webp"),
                new Core.Imaging.ConversionOptions { Format = Core.Formats.OutputFormat.WebP, Quality = 90 },
                overwrite: true);
            log.AppendLine($"convert: {result.Success} {result.Width}x{result.Height} {result.OutputBytes} bytes");

            File.WriteAllText(Path.Combine(temp, "selftest.log"), log.ToString());
            return result.Success ? 0 : 2;
        }
        catch (Exception ex)
        {
            log.AppendLine("FATAL " + ex);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.Combine(Path.GetTempPath(), "pc_selftest"))!);
                File.WriteAllText(Path.Combine(Path.GetTempPath(), "pc_selftest", "selftest.log"), log.ToString());
            }
            catch
            {
                // ignore
            }

            return 1;
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogCrash(e.Exception);
        e.Handled = true;
        MessageBox.Show(
            "Something went wrong, but your queue is safe.\n\n" + e.Exception.Message,
            "PikturShift",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            LogCrash(ex);
        }
    }

    private static void LogCrash(Exception ex)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CrashLogPath)!);
            File.AppendAllText(CrashLogPath, $"[{DateTime.Now:u}] {ex}\n\n");
        }
        catch
        {
            // never throw from the crash logger
        }
    }

    /// <summary>Alpha channel of a theme brush, used by --selftest.</summary>
    private static string AlphaOf(string key) =>
        Application.Current?.Resources[key] is SolidColorBrush brush
            ? brush.Color.A.ToString()
            : "n/a";
}
