using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using PikturShift.Common;
using PikturShift.Core.Formats;
using PikturShift.Core.Imaging;
using PikturShift.Core.Settings;
using PikturShift.Theming;

namespace PikturShift.ViewModels;

public sealed record FormatChoice(OutputFormat Value, string Name, string Hint);

public sealed class MainViewModel : ObservableObject
{
    // Two previews at a time: enough to keep the list filling smoothly, low enough that the
    // peak decode cost stays flat no matter how many hundred files were dropped at once.
    private static readonly SemaphoreSlim PreviewGate = new(2, 2);

    private readonly SettingsStore _store;
    private readonly AppSettings _settings;
    private readonly DispatcherTimer _noticeTimer;
    private readonly DispatcherTimer _saveTimer;

    private JobViewModel? _selectedJob;
    private FormatChoice _selectedFormat;
    private double _quality;
    private string _fillColor;
    private bool _chooseFolder;
    private string _outputFolder;
    private bool _overwrite;
    private bool _keepExif;
    private bool _openAfterConvert;
    private bool _isConverting;
    private bool _isSettingsPage;
    private bool _navExpanded;
    private string _statusText = string.Empty;
    private string _statusTooltip = string.Empty;
    private bool _statusIsError;
    private double _overallProgress;
    private string _folderError = string.Empty;
    private string _notice = string.Empty;
    private bool _isFillPaletteOpen;
    private DateTime? _lastRunAt;
    private CancellationTokenSource? _cts;

    public MainViewModel(SettingsStore store, AppSettings settings)
    {
        _store = store;
        _settings = settings;

        Formats = new FormatChoice[]
        {
            new FormatChoice(OutputFormat.Jpg, "JPG", FormatInfo.Hint(OutputFormat.Jpg)),
            new FormatChoice(OutputFormat.Png, "PNG", FormatInfo.Hint(OutputFormat.Png)),
            new FormatChoice(OutputFormat.WebP, "WebP", FormatInfo.Hint(OutputFormat.WebP)),
            new FormatChoice(OutputFormat.Bmp, "BMP", FormatInfo.Hint(OutputFormat.Bmp)),
            new FormatChoice(OutputFormat.Tiff, "TIFF", FormatInfo.Hint(OutputFormat.Tiff))
        };

        _selectedFormat = Formats.FirstOrDefault(f => f.Name.Equals(settings.Format, StringComparison.OrdinalIgnoreCase)) ?? Formats[0];
        _quality = Math.Clamp(settings.Quality, 1, 100);
        _fillColor = NormalizeHex(settings.FillColor) ?? "#FFFFFF";
        _chooseFolder = settings.ChooseFolder;
        _outputFolder = settings.OutputFolder ?? string.Empty;
        _overwrite = settings.Overwrite;
        _keepExif = settings.KeepExif;
        _openAfterConvert = settings.OpenFolderAfterConvert;
        _navExpanded = settings.NavExpanded;
        _lastRunAt = null;

        AccentSwatches = new ObservableCollection<AccentSwatchViewModel>(
            AccentCatalog.Options.Select(o => new AccentSwatchViewModel
            {
                Id = o.Id,
                IsSystem = o.IsSystem,
                IsMono = o.IsMono
            }));

        BrowseFilesCommand = new RelayCommand(BrowseFiles);
        BrowseFolderCommand = new RelayCommand(BrowseFolder);
        ConvertCommand = new RelayCommand(async () => await ConvertAsync(), () => CanConvert);
        CancelCommand = new RelayCommand(Cancel, () => IsConverting);
        ClearCommand = new RelayCommand(ClearQueue, () => HasJobs && !IsConverting);
        RemoveJobCommand = new RelayCommand(p => RemoveJob(p as JobViewModel), _ => !IsConverting);
        RemoveSelectedCommand = new RelayCommand(() => RemoveJob(SelectedJob), () => SelectedJob is not null && !IsConverting);
        OpenFolderCommand = new RelayCommand(OpenFolder, () => HasJobs || _lastRunAt is not null);
        SetThemeCommand = new RelayCommand(p => SetTheme(p as string));
        SetAccentCommand = new RelayCommand(p => SetAccent(p));
        SetPageCommand = new RelayCommand(p =>
        {
            IsSettingsPage = string.Equals(p as string, "settings", StringComparison.OrdinalIgnoreCase);

            // A rail pick doubles as the destination choice and the "I'm done with the menu"
            // signal, so the pane folds back to its 48 px rail - WinUI NavigationView default.
            NavExpanded = false;
        });
        ToggleNavCommand = new RelayCommand(() => NavExpanded = !NavExpanded);
        ResetSettingsCommand = new RelayCommand(ResetSettings);
        OpenJobFolderCommand = new RelayCommand(p => RevealJob(p as JobViewModel));

        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
        _saveTimer.Tick += (_, _) =>
        {
            _saveTimer.Stop();
            _store.Save(_settings);
        };

        _noticeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _noticeTimer.Tick += (_, _) =>
        {
            _noticeTimer.Stop();
            Notice = string.Empty;
        };

        ThemeManager.ThemeChanged += OnThemeChanged;
        RefreshAppearance();
        UpdateStatus();
    }

    // ------------------------------------------------------------- surface

    public ObservableCollection<JobViewModel> Jobs { get; } = new();
    public IReadOnlyList<FormatChoice> Formats { get; }
    public ObservableCollection<AccentSwatchViewModel> AccentSwatches { get; }

    public RelayCommand BrowseFilesCommand { get; }
    public RelayCommand BrowseFolderCommand { get; }
    public RelayCommand ConvertCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand ClearCommand { get; }
    public RelayCommand RemoveJobCommand { get; }
    public RelayCommand RemoveSelectedCommand { get; }
    public RelayCommand OpenFolderCommand { get; }
    public RelayCommand SetThemeCommand { get; }
    public RelayCommand SetAccentCommand { get; }
    public RelayCommand SetPageCommand { get; }
    public RelayCommand ToggleNavCommand { get; }
    public RelayCommand ResetSettingsCommand { get; }
    public RelayCommand OpenJobFolderCommand { get; }

    public string AppVersion
    {
        get
        {
            var version = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0, 0);
            return $"PikturShift {version.Major}.{version.Minor}{(version.Build > 0 ? "." + version.Build : string.Empty)}";
        }
    }

    public JobViewModel? SelectedJob
    {
        get => _selectedJob;
        set
        {
            if (SetProperty(ref _selectedJob, value))
            {
                RemoveSelectedCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool HasJobs => Jobs.Count > 0;
    public bool IsEmpty => Jobs.Count == 0;

    public bool IsConverting
    {
        get => _isConverting;
        private set
        {
            if (SetProperty(ref _isConverting, value))
            {
                OnPropertiesChanged(nameof(IsIdle), nameof(CanConvert));
                ConvertCommand.RaiseCanExecuteChanged();
                CancelCommand.RaiseCanExecuteChanged();
                ClearCommand.RaiseCanExecuteChanged();
                RemoveJobCommand.RaiseCanExecuteChanged();
                RemoveSelectedCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool IsIdle => !IsConverting;
    public double OverallProgress
    {
        get => _overallProgress;
        private set => SetProperty(ref _overallProgress, value);
    }

    public bool CanConvert => HasJobs && !IsConverting;

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string StatusTooltip
    {
        get => _statusTooltip;
        private set => SetProperty(ref _statusTooltip, value);
    }

    public bool StatusIsError
    {
        get => _statusIsError;
        private set => SetProperty(ref _statusIsError, value);
    }

    public string Notice
    {
        get => _notice;
        private set
        {
            if (SetProperty(ref _notice, value))
            {
                OnPropertyChanged(nameof(HasNotice));
            }
        }
    }

    public bool HasNotice => !string.IsNullOrEmpty(Notice);

    public bool IsSettingsPage
    {
        get => _isSettingsPage;
        set
        {
            if (SetProperty(ref _isSettingsPage, value))
            {
                OnPropertiesChanged(nameof(IsConverterPage));
            }
        }
    }

    public bool IsConverterPage => !IsSettingsPage;

    public bool NavExpanded
    {
        get => _navExpanded;
        set
        {
            if (SetProperty(ref _navExpanded, value))
            {
                _settings.NavExpanded = value;
                Persist();
            }
        }
    }

    // -------------------------------------------------------------- theme

    public AppTheme Theme => ThemeManager.Theme;
    public AccentId Accent => ThemeManager.Accent;
    public bool IsDark => ThemeManager.IsDark;
    public string AccentDisplayName => ThemeManager.AccentDisplayName;
    /// <summary>Live accent as a hex string, used by the fill palette's accent swatch.</summary>
    public string AccentSwatchHex => ColorUtil.ToHex(ThemeManager.AccentColor);

    private void OnThemeChanged(object? sender, EventArgs e) => RefreshAppearance();

    private void RefreshAppearance()
    {
        foreach (var swatch in AccentSwatches)
        {
            swatch.Refresh(ThemeManager.IsDark, ThemeManager.SystemAccentColor, ThemeManager.Accent);
        }

        OnPropertiesChanged(nameof(Theme), nameof(Accent), nameof(IsDark), nameof(AccentDisplayName),
            nameof(AccentSwatchHex));
    }

    private void SetTheme(string? key)
    {
        if (string.IsNullOrEmpty(key) || !Enum.TryParse<AppTheme>(key, true, out var theme) || theme == ThemeManager.Theme)
        {
            return;
        }

        ThemeManager.Apply(theme: theme.ToString());
        _settings.Theme = ThemeManager.Theme.ToString();
        Persist();
        RefreshAppearance();
    }

    private void SetAccent(object? parameter)
    {
        AccentId id = parameter switch
        {
            AccentId a => a,
            string s when Enum.TryParse<AccentId>(s, true, out var parsed) => parsed,
            AccentSwatchViewModel swatch => swatch.Id,
            _ => ThemeManager.Accent
        };

        if (id == ThemeManager.Accent)
        {
            return;
        }

        ThemeManager.Apply(accent: AccentCatalog.Find(id).Key);
        _settings.Accent = AccentCatalog.Find(id).Key;
        Persist();
        RefreshAppearance();
    }

    // ------------------------------------------------------------ formats

    public FormatChoice SelectedFormat
    {
        get => _selectedFormat;
        set
        {
            if (value is null || !SetProperty(ref _selectedFormat, value))
            {
                return;
            }

            _settings.Format = value.Name;
            Persist();
            OnPropertiesChanged(nameof(FormatHint), nameof(ShowQuality), nameof(ShowFill));
            foreach (var job in Jobs)
            {
                job.CurrentTargetLabel = value.Name;
                job.Refresh();
            }

            UpdateStatus();
        }
    }

    public string FormatHint => _selectedFormat.Hint;
    public bool ShowQuality => FormatInfo.SupportsQuality(_selectedFormat.Value);
    public bool ShowFill => !FormatInfo.SupportsAlpha(_selectedFormat.Value);
    public double Quality
    {
        get => _quality;
        set
        {
            var clamped = Math.Clamp(Math.Round(value), 1, 100);
            if (SetProperty(ref _quality, clamped))
            {
                _settings.Quality = (int)clamped;
                Persist();
                OnPropertyChanged(nameof(QualityText));
            }
        }
    }

    public string QualityText => $"{_quality:0} %";

    public string FillColor
    {
        get => _fillColor;
        set
        {
            var normalized = NormalizeHex(value);
            if (normalized is null || !SetProperty(ref _fillColor, normalized))
            {
                return;
            }

            _settings.FillColor = normalized;
            Persist();
            OnPropertyChanged(nameof(FillColorBrush));
        }
    }

    public bool IsFillPaletteOpen
    {
        get => _isFillPaletteOpen;
        set => SetProperty(ref _isFillPaletteOpen, value);
    }

    public Brush FillColorBrush
    {
        get
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(_fillColor));
            brush.Freeze();
            return brush;
        }
    }


    // --------------------------------------------------------------- save

    public bool ChooseFolder
    {
        get => _chooseFolder;
        set
        {
            if (!SetProperty(ref _chooseFolder, value))
            {
                return;
            }

            _settings.ChooseFolder = value;
            Persist();
            OnPropertiesChanged(nameof(SameFolder));
            if (value && string.IsNullOrWhiteSpace(OutputFolder))
            {
                Application.Current?.Dispatcher.BeginInvoke(new Action(BrowseFolder));
            }
        }
    }

    public bool SameFolder
    {
        get => !_chooseFolder;
        set
        {
            if (!value)
            {
                return;
            }

            ChooseFolder = false;
        }
    }

    public string OutputFolder
    {
        get => _outputFolder;
        set
        {
            if (SetProperty(ref _outputFolder, value))
            {
                _settings.OutputFolder = value;
                Persist();
                OnPropertiesChanged(nameof(OutputFolderDisplay));
                FolderError = string.Empty;
            }
        }
    }

    public string OutputFolderDisplay => string.IsNullOrWhiteSpace(_outputFolder)
        ? "Choose a destination folder\u2026"
        : _outputFolder;

    public string FolderError
    {
        get => _folderError;
        private set
        {
            if (SetProperty(ref _folderError, value))
            {
                OnPropertyChanged(nameof(HasFolderError));
            }
        }
    }

    public bool HasFolderError => !string.IsNullOrEmpty(FolderError);

    public bool Overwrite
    {
        get => _overwrite;
        set
        {
            if (SetProperty(ref _overwrite, value))
            {
                _settings.Overwrite = value;
                Persist();
            }
        }
    }

    public bool KeepExif
    {
        get => _keepExif;
        set
        {
            if (SetProperty(ref _keepExif, value))
            {
                _settings.KeepExif = value;
                Persist();
            }
        }
    }

    public bool OpenAfterConvert
    {
        get => _openAfterConvert;
        set
        {
            if (SetProperty(ref _openAfterConvert, value))
            {
                _settings.OpenFolderAfterConvert = value;
                Persist();
            }
        }
    }

    // --------------------------------------------------------------- queue

    public void AddPaths(IEnumerable<string> paths, bool fromDrop = false)
    {
        if (IsConverting)
        {
            ShowNotice("Wait for the current conversion to finish before adding files.");
            return;
        }

        if (IsSettingsPage)
        {
            IsSettingsPage = false;
        }

        var candidates = new List<string>();
        foreach (var path in paths)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    candidates.AddRange(Directory
                        .EnumerateFiles(path, "*.*", SearchOption.TopDirectoryOnly)
                        .Where(FormatInfo.IsSupportedInput));
                }
                else if (File.Exists(path) && FormatInfo.IsSupportedInput(path))
                {
                    candidates.Add(path);
                }
            }
            catch
            {
                // Unreadable path: skip silently, counted below only if it was a file.
            }
        }

        int skipped = 0;
        int added = 0;
        var existing = new HashSet<string>(Jobs.Select(j => j.SourcePath), StringComparer.OrdinalIgnoreCase);

        foreach (var path in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!existing.Add(path))
            {
                continue;
            }

            var job = new ConversionJob(path) { Status = "queued" };
            var vm = new JobViewModel(job) { CurrentTargetLabel = _selectedFormat.Name };
            Jobs.Add(vm);
            added++;
            _ = LoadPreviewAsync(vm);
        }

        if (fromDrop)
        {
            skipped = paths.Count(p => File.Exists(p) && !FormatInfo.IsSupportedInput(p));
        }

        if (added == 0 && skipped == 0 && fromDrop)
        {
            ShowNotice("Nothing new to add \u2014 those images are already queued.");
            return;
        }

        if (skipped > 0)
        {
            ShowNotice($"{skipped} file{(skipped == 1 ? " was" : "s were")} skipped (unsupported format).");
        }

        if (added > 0)
        {
            _lastRunAt = null;
        }

        RaiseQueueChanged();
        UpdateStatus();
    }

    private async Task LoadPreviewAsync(JobViewModel job)
    {
        // Snapshot UI-thread state before hopping to the pool (the queue itself is not thread safe).
        int queueSize = Jobs.Count;
        await PreviewGate.WaitAsync().ConfigureAwait(false);
        try
        {
            var path = job.SourcePath;
            var probe = await Task.Run(() => ImageConverter.Probe(path)).ConfigureAwait(false);

            // WIC scales while decoding, so even big files are cheap here. Anything past
            // 120 MP (scans, panoramas) skips the icon rather than reserving a pixel buffer.
            bool wantThumb = probe.Success
                             && (long)probe.Width * probe.Height <= 120_000_000
                             && queueSize <= 500;

            BitmapImage? thumb = null;
            if (wantThumb)
            {
                thumb = await Task.Run(() => ThumbnailLoader.Load(path, 96)).ConfigureAwait(false);
            }

            Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                job.ApplyProbe(probe);
                job.AttachThumbnail(thumb);
                UpdateStatus();
            }));
        }
        catch
        {
            // previews are decoration only
        }
        finally
        {
            PreviewGate.Release();
        }
    }

    private void RaiseQueueChanged()
    {
        OnPropertiesChanged(nameof(HasJobs), nameof(IsEmpty), nameof(CanConvert));
        ConvertCommand.RaiseCanExecuteChanged();
        ClearCommand.RaiseCanExecuteChanged();
        OpenFolderCommand.RaiseCanExecuteChanged();
    }

    private void RemoveJob(JobViewModel? job)
    {
        if (job is null || IsConverting)
        {
            return;
        }

        Jobs.Remove(job);
        if (ReferenceEquals(SelectedJob, job))
        {
            SelectedJob = null;
        }

        RaiseQueueChanged();
        UpdateStatus();
    }

    private void ClearQueue()
    {
        if (IsConverting)
        {
            return;
        }

        Jobs.Clear();
        SelectedJob = null;
        _lastRunAt = null;
        RaiseQueueChanged();
        UpdateStatus();

        // Dropping the queue drops every thumbnail too - release the pages immediately.
        MemoryTrim.AfterHeavyWork();
    }

    // ---------------------------------------------------------- conversion

    private ConversionOptions BuildOptions() => new()
    {
        Format = _selectedFormat.Value,
        Quality = (int)_quality,
        FillColor = _fillColor,
        KeepExif = _keepExif
    };

    private async Task ConvertAsync()
    {
        if (!CanConvert)
        {
            return;
        }

        FolderError = string.Empty;
        if (_chooseFolder)
        {
            if (string.IsNullOrWhiteSpace(_outputFolder))
            {
                FolderError = "Pick a destination folder first.";
                return;
            }

            try
            {
                Directory.CreateDirectory(_outputFolder);
            }
            catch (Exception ex)
            {
                FolderError = $"That folder can\u2019t be used: {ex.Message}";
                return;
            }
        }

        var jobs = Jobs.ToList();
        foreach (var job in jobs)
        {
            job.Model.Status = "queued";
            job.Model.Progress = 0;
            job.Model.Error = null;
            job.Model.OutputPath = null;
            job.Model.OutputBytes = 0;
            job.CurrentTargetLabel = _selectedFormat.Name;
            job.Refresh();
        }

        IsConverting = true;
        OverallProgress = 0;
        Notice = string.Empty;
        _cts = new CancellationTokenSource();
        var started = Stopwatch.StartNew();
        StatusIsError = false;
        StatusText = $"Converting 0 of {jobs.Count}\u2026";

        var byModel = jobs.ToDictionary(j => j.Model);
        var dispatcher = Application.Current?.Dispatcher;
        int completed = 0;

        try
        {
            await ConversionBatch.RunAsync(
                jobs.Select(j => j.Model).ToList(),
                BuildOptions(),
                _chooseFolder ? _outputFolder : null,
                _overwrite,
                model =>
                {
                    dispatcher?.BeginInvoke(new Action(() =>
                    {
                        if (byModel.TryGetValue(model, out var vm))
                        {
                            vm.Refresh();
                        }

                        completed = jobs.Count(j => j.Model.IsDone);
                        OverallProgress = jobs.Count == 0 ? 0 : Math.Round(jobs.Sum(j => j.Model.Progress) / (double)jobs.Count);
                        int active = Math.Min(completed + 1, jobs.Count);
                        StatusText = $"Converting {active} of {jobs.Count}\u2026";
                    }));
                },
                ConversionBatch.DefaultParallelism,
                _cts.Token);
        }
        catch (OperationCanceledException)
        {
            // handled below
        }
        catch (Exception ex)
        {
            ShowNotice("Conversion stopped: " + ex.Message);
        }
        finally
        {
            started.Stop();
            _lastRunAt = DateTime.Now;
            IsConverting = false;
            OverallProgress = 100;
            _cts?.Dispose();
            _cts = null;

            foreach (var job in jobs)
            {
                job.Refresh();
            }

            var done = jobs.Count(j => j.IsDone);
            var failed = jobs.Count(j => j.IsFailed);
            var cancelled = jobs.Count(j => j.IsCancelled);

            if (cancelled > 0)
            {
                StatusText = $"Cancelled \u2014 {done} of {jobs.Count} converted";
                StatusIsError = true;
            }
            else if (failed > 0)
            {
                StatusText = $"{done} converted \u00b7 {failed} failed \u00b7 {started.Elapsed.TotalSeconds:0.0} s";
                StatusTooltip = string.Join(Environment.NewLine, jobs
                    .Where(j => j.IsFailed)
                    .Take(6)
                    .Select(j => $"{j.FileName}: {j.ErrorText}"));
                StatusIsError = true;
            }
            else if (done > 0)
            {
                StatusText = $"Done \u00b7 {done} image{(done == 1 ? string.Empty : "s")} converted in {started.Elapsed.TotalSeconds:0.0} s";
                StatusIsError = false;
            }
            else
            {
                UpdateStatus();
            }

            RaiseQueueChanged();

            // Conversion grows the GC heap; hand those pages back now rather than letting
            // them sit in the working set until the app closes.
            MemoryTrim.AfterHeavyWork();

            if (_openAfterConvert && done > 0)
            {
                OpenFolder();
            }
        }
    }

    private void Cancel()
    {
        _cts?.Cancel();
    }

    private void OpenFolder()
    {
        string? folder = null;
        if (_chooseFolder && Directory.Exists(_outputFolder))
        {
            folder = _outputFolder;
        }
        else
        {
            var lastOutput = Jobs.Select(j => j.OutputPath).LastOrDefault(p => !string.IsNullOrEmpty(p) && File.Exists(p));
            if (!string.IsNullOrEmpty(lastOutput))
            {
                folder = Path.GetDirectoryName(lastOutput);
            }
            else if (Jobs.Count > 0)
            {
                folder = Path.GetDirectoryName(Jobs[0].SourcePath);
            }
        }

        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ShowNotice("Could not open the folder: " + ex.Message);
        }
    }

    private void RevealJob(JobViewModel? job)
    {
        if (job is null)
        {
            return;
        }

        var path = job.OutputPath;
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
                return;
            }
            catch
            {
                // fall through to folder
            }
        }

        var folder = Path.GetDirectoryName(job.SourcePath);
        if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
        }
    }

    // ----------------------------------------------------------- dialogs

    private void BrowseFiles()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Add images",
            Multiselect = true,
            CheckFileExists = true,
            Filter = "Images|*.jpg;*.jpeg;*.jpe;*.jfif;*.png;*.webp;*.bmp;*.dib;*.gif;*.tif;*.tiff|All files|*.*"
        };

        if (dialog.ShowDialog() == true)
        {
            AddPaths(dialog.FileNames);
        }
    }

    private void BrowseFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Choose where converted images go",
            Multiselect = false
        };

        if (!string.IsNullOrWhiteSpace(_outputFolder) && Directory.Exists(_outputFolder))
        {
            dialog.InitialDirectory = _outputFolder;
        }

        if (dialog.ShowDialog() == true)
        {
            OutputFolder = dialog.FolderName;
            ChooseFolder = true;
        }
    }

    // ---------------------------------------------------------- status

    private void UpdateStatus()
    {
        if (IsConverting)
        {
            return;
        }

        if (Jobs.Count == 0)
        {
            StatusText = "Add images to get started";
            StatusIsError = false;
            StatusTooltip = string.Empty;
            return;
        }

        if (_lastRunAt is not null && Jobs.All(j => j.IsDone || j.IsFailed || j.IsCancelled))
        {
            return; // keep the run summary visible
        }

        long total = Jobs.Sum(j => j.Model.SizeBytes);
        StatusText = Jobs.Count == 1
            ? $"1 image \u00b7 {JobViewModel.FormatBytes(total)} ready"
            : $"{Jobs.Count} images \u00b7 {JobViewModel.FormatBytes(total)} ready";
        StatusIsError = false;
        StatusTooltip = string.Empty;
    }

    /// <summary>Shows a transient message; called from the window for shell-level failures.</summary>
    public void ShowNotice(string text)
    {
        Notice = text;
        _noticeTimer.Stop();
        _noticeTimer.Start();
    }

    // -------------------------------------------------------- settings

    private void ResetSettings()
    {
        var defaults = new AppSettings();
        _settings.Theme = defaults.Theme;
        _settings.Accent = defaults.Accent;
        _settings.Format = defaults.Format;
        _settings.Quality = defaults.Quality;
        _settings.FillColor = defaults.FillColor;
        _settings.Overwrite = defaults.Overwrite;
        _settings.KeepExif = defaults.KeepExif;
        _settings.OpenFolderAfterConvert = defaults.OpenFolderAfterConvert;
        _settings.ChooseFolder = defaults.ChooseFolder;
        _settings.OutputFolder = defaults.OutputFolder;
        _settings.NavExpanded = defaults.NavExpanded;

        ThemeManager.Apply(defaults.Theme, defaults.Accent);
        Persist();

        _selectedFormat = Formats[0];
        _quality = defaults.Quality;
        _fillColor = defaults.FillColor;
        _chooseFolder = defaults.ChooseFolder;
        _outputFolder = defaults.OutputFolder;
        _overwrite = defaults.Overwrite;
        _keepExif = defaults.KeepExif;
        _openAfterConvert = defaults.OpenFolderAfterConvert;
        _navExpanded = defaults.NavExpanded;

        RefreshAppearance();
        OnPropertiesChanged(
            nameof(SelectedFormat), nameof(FormatHint), nameof(ShowQuality), nameof(ShowFill),
            nameof(Quality), nameof(QualityText), nameof(FillColor), nameof(FillColorBrush),
            nameof(ChooseFolder), nameof(SameFolder), nameof(OutputFolder), nameof(OutputFolderDisplay),
            nameof(Overwrite), nameof(KeepExif), nameof(OpenAfterConvert),
            nameof(NavExpanded));

        foreach (var job in Jobs)
        {
            job.CurrentTargetLabel = _selectedFormat.Name;
            job.Refresh();
        }

        ShowNotice("Settings restored to defaults.");
        UpdateStatus();
    }

    /// <summary>Writes settings after a short idle period so dragging a slider doesn't hammer the disk.</summary>
    private void Persist()
    {
        _settings.Format = _selectedFormat.Name;
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    public void SaveWindowState(double left, double top, double width, double height, bool maximized)
    {
        if (double.IsNaN(left) || double.IsNaN(top) || width < 400 || height < 300)
        {
            return;
        }

        _settings.WindowLeft = left;
        _settings.WindowTop = top;
        _settings.WindowWidth = width;
        _settings.WindowHeight = height;
        _settings.Maximized = maximized;
        _store.Save(_settings);
    }

    public void SaveOnExit()
    {
        _saveTimer.Stop();
        _store.Save(_settings);
    }

    /// <summary>Saved window bounds, or null when the app is running for the first time.</summary>
    public (double Left, double Top, double Width, double Height, bool Maximized)? ExportPlacement()
    {
        if (_settings.WindowLeft is null || _settings.WindowTop is null)
        {
            return null;
        }

        return (_settings.WindowLeft.Value, _settings.WindowTop.Value,
            _settings.WindowWidth, _settings.WindowHeight, _settings.Maximized);
    }

    private static string? NormalizeHex(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = value.Trim();
        if (!text.StartsWith('#'))
        {
            text = "#" + text;
        }

        if (text.Length == 7 && text.Skip(1).All(Uri.IsHexDigit))
        {
            return text.ToUpperInvariant();
        }

        if (text.Length == 4 && text.Skip(1).All(Uri.IsHexDigit))
        {
            var r = text[1];
            var g = text[2];
            var b = text[3];
            return $"#{r}{r}{g}{g}{b}{b}".ToUpperInvariant();
        }

        return null;
    }
}
