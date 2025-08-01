using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using DynamicData;
using Oracle.Helpers;
using Oracle.Models;
using Oracle.Services;

namespace Oracle.Views.Modals;

public partial class SearchModal : UserControl
{
    private MotelySearchService _searchService;
    private readonly ObservableCollection<Oracle.Views.Modals.SearchResultViewModel> _results = new();
    private string? _configPath;
    private bool _isRunning;
    private TextBox? _consoleOutput;
    private Border? _emptyState;
    private CancellationTokenSource? _searchCts;
    private DateTime _searchStartTime;
    

    public SearchModal()
    {
        InitializeComponent();
        
        // Get service from DI using helper
        _searchService = ServiceHelper.GetService<MotelySearchService>() ?? new MotelySearchService();
        
        // Find console output control
        _consoleOutput = this.FindControl<TextBox>("ConsoleOutput");
        _emptyState = this.FindControl<Border>("EmptyState");
        
        // Set default values for controls
        InitializeDefaults();
    }
    
    private void InitializeDefaults()
    {
        // Set default values based on Motely Program.cs defaults
        var threadsUpDown = this.FindControl<NumericUpDown>("ThreadsUpDown");
        if (threadsUpDown != null) threadsUpDown.Value = Environment.ProcessorCount;
        
        var batchSizeUpDown = this.FindControl<NumericUpDown>("BatchSizeUpDown");
        if (batchSizeUpDown != null) batchSizeUpDown.Value = 4;
        
        var startBatchUpDown = this.FindControl<NumericUpDown>("StartBatchUpDown");
        if (startBatchUpDown != null) startBatchUpDown.Value = 0;
        
        var endBatchUpDown = this.FindControl<NumericUpDown>("EndBatchUpDown");
        if (endBatchUpDown != null) endBatchUpDown.Value = 999999; // Search entire seed space
        
        var cutoffUpDown = this.FindControl<NumericUpDown>("CutoffUpDown");
        if (cutoffUpDown != null) cutoffUpDown.Value = 0;
        
        var debugCheckBox = this.FindControl<CheckBox>("DebugCheckBox");
        if (debugCheckBox != null) debugCheckBox.IsChecked = false;
    }
    
    // Methods to set state from widget
    public void SetSearchService(MotelySearchService service)
    {
        _searchService = service;
    }
    
    /// <summary>
    /// Stops the search if it's running - called during application shutdown
    /// </summary>
    public void StopSearch()
    {
        if (_isRunning)
        {
            _searchService.StopSearch();
            _isRunning = false;
        }
    }
    
    public void SetConfigPath(string? configPath)
    {
        _configPath = configPath;
        var configPathTextBox = this.FindControl<TextBlock>("ConfigPathTextBox");
        var startButton = this.FindControl<Button>("StartButton");
        
        if (configPathTextBox != null && !string.IsNullOrEmpty(configPath))
        {
            configPathTextBox.Text = Path.GetFileName(configPath);
            if (startButton != null) startButton.IsEnabled = true;
            
            // Update console
            AppendConsoleOutput($"> Config loaded from widget: {Path.GetFileName(configPath)}");
            AppendConsoleOutput($"> Ready to start cooking!");
        }
        else
        {
            if (configPathTextBox != null) configPathTextBox.Text = "No filter loaded";
            if (startButton != null) startButton.IsEnabled = false;
        }
    }
    
    public void SetResults(List<Oracle.Models.SearchResult> results)
    {
        _results.Clear();
        int index = 1;
        foreach (var result in results)
        {
            _results.Add(new Oracle.Views.Modals.SearchResultViewModel
            {
                Index = index++,
                Seed = result.Seed,
                Score = result.Score,
                Details = result.Details,
                ScoreBreakdown = result.ScoreBreakdown
            });
        }
        
        var resultsGrid = this.FindControl<DataGrid>("ResultsGrid");
        var summaryText = this.FindControl<TextBlock>("SummaryText");
        var exportButton = this.FindControl<Button>("ExportButton");
        
        // Toggle empty state vs results grid
        if (_emptyState != null && resultsGrid != null)
        {
            resultsGrid.ItemsSource = _results;
            resultsGrid.IsVisible = _results.Count > 0;
            _emptyState.IsVisible = _results.Count == 0;
        }
        
        
        if (summaryText != null)
        {
            summaryText.Text = $"{_results.Count} seeds found";
        }
        
        if (exportButton != null)
        {
            exportButton.IsEnabled = _results.Count > 0;
        }
    }
    
    public void SetSearchState(bool isRunning, int foundCount)
    {
        _isRunning = isRunning;
        
        var statusText = this.FindControl<TextBlock>("StatusText");
        if (statusText != null)
        {
            statusText.Text = isRunning ? $"Searching... Found {foundCount} seeds" : $"Found {foundCount} seeds";
        }
        
        var foundCountText = this.FindControl<TextBlock>("FoundCountText");
        if (foundCountText != null)
        {
            foundCountText.Text = $"Found: {foundCount}";
        }
    }
    
    private void OnMinimizeClick(object? sender, RoutedEventArgs e)
    {
        // Close the modal and show the widget again
        var mainWindow = TopLevel.GetTopLevel(this) as Window;
        
        // The MainWindow content is a Grid, need to find BalatroMainMenu within it
        Views.BalatroMainMenu? mainMenu = null;
        if (mainWindow?.Content is Grid grid)
        {
            foreach (var child in grid.Children)
            {
                if (child is Views.BalatroMainMenu menu)
                {
                    mainMenu = menu;
                    break;
                }
            }
        }
        
        if (mainMenu != null)
        {
            // Hide the modal
            mainMenu.HideModalContent();
            
            // Show the widget again
            var searchWidget = mainMenu.FindControl<Components.SearchWidget>("SearchWidget");
            if (searchWidget != null)
            {
                searchWidget.IsVisible = true;
            }
        }
    }

    private void InitializeComponent()
    {
        Avalonia.Markup.Xaml.AvaloniaXamlLoader.Load(this);
    }
    
    private void OnClearClick(object? sender, RoutedEventArgs e)
    {
        _results.Clear();
        SetResults(new List<Oracle.Models.SearchResult>());
    }
    
    private void AppendConsoleOutput(string text)
    {
        if (_consoleOutput != null)
        {
            _consoleOutput.Text += text + Environment.NewLine;
            
            // Auto-scroll to bottom
            _consoleOutput.CaretIndex = _consoleOutput.Text.Length;
        }
    }
    
    private async void OnLoadConfigClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;
            
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Select Filter Config",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Ouija Config Files")
                    {
                        Patterns = new[] { "*.ouija.json", "*.json" }
                    }
                }
            });
            
            if (files.Count > 0)
            {
                var file = files[0];
                _configPath = file.Path.LocalPath;
                
                // Update UI
                var configPathTextBox = this.FindControl<TextBlock>("ConfigPathTextBox");
                if (configPathTextBox != null)
                {
                    configPathTextBox.Text = Path.GetFileName(_configPath);
                }
                
                // Enable start button
                var startButton = this.FindControl<Button>("StartButton");
                if (startButton != null)
                {
                    startButton.IsEnabled = true;
                }
                
                // Update console
                AppendConsoleOutput($"> Loaded config: {Path.GetFileName(_configPath)}");
                AppendConsoleOutput($"> Ready to start cooking!");
            }
        }
        catch (Exception ex)
        {
            AppendConsoleOutput($"> Error loading config: {ex.Message}");
        }
    }
    
    private async void OnExportClick(object? sender, RoutedEventArgs e)
    {
        if (_results.Count == 0) return;
        
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            var storageProvider = topLevel?.StorageProvider;
            
            if (storageProvider == null) return;

            var saveOptions = new FilePickerSaveOptions
            {
                Title = "Export Search Results",
                DefaultExtension = "csv",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("CSV Files") 
                    { 
                        Patterns = new[] { "*.csv" }
                    }
                },
                SuggestedFileName = $"search-results-{DateTime.Now:yyyyMMdd-HHmmss}.csv"
            };

            var file = await storageProvider.SaveFilePickerAsync(saveOptions);
            if (file != null)
            {
                // Export results to CSV
                var csv = "Seed,Score,Details\n";
                foreach (var result in _results)
                {
                    csv += $"{result.Seed},{result.Score},\"{result.Details}\"\n";
                }
                
                await File.WriteAllTextAsync(file.Path.LocalPath, csv);
                
                var statusText = this.FindControl<TextBlock>("StatusText");
                if (statusText != null)
                {
                    statusText.Text = $"Exported {_results.Count} results to {file.Name}";
                }
            }
        }
        catch (Exception ex)
        {
            Oracle.Helpers.DebugLogger.Log("SearchModal", $"Error exporting results: {ex.Message}");
        }
    }
    
    private async void OnStartClick(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_configPath))
        {
            var statusText = this.FindControl<TextBlock>("StatusText");
            if (statusText != null)
            {
                statusText.Text = "No config loaded. Please load a filter configuration first.";
            }
            return;
        }

        try
        {
            // Get values from UI controls
            var threadsUpDown = this.FindControl<NumericUpDown>("ThreadsUpDown");
            var batchSizeUpDown = this.FindControl<NumericUpDown>("BatchSizeUpDown");
            var startBatchUpDown = this.FindControl<NumericUpDown>("StartBatchUpDown");
            var endBatchUpDown = this.FindControl<NumericUpDown>("EndBatchUpDown");
            var cutoffUpDown = this.FindControl<NumericUpDown>("CutoffUpDown");
            var debugCheckBox = this.FindControl<CheckBox>("DebugCheckBox");
            var specificSeedTextBox = this.FindControl<TextBox>("SpecificSeedTextBox");
            var keywordTextBox = this.FindControl<TextBox>("KeywordTextBox");
            
            var startButton = this.FindControl<Button>("StartButton");
            var stopButton = this.FindControl<Button>("StopButton");
            
            // Build search criteria from UI controls
            var criteria = new Oracle.Models.SearchCriteria
            {
                ConfigPath = _configPath,
                ThreadCount = (int)(threadsUpDown?.Value ?? 4),
                BatchSize = (int)(batchSizeUpDown?.Value ?? 4),
                MinScore = (int)(cutoffUpDown?.Value ?? 0),
                EnableDebugOutput = debugCheckBox?.IsChecked ?? false
            };
            
            // Set debug logging based on checkbox
            Oracle.Helpers.DebugLogger.SetDebugEnabled(criteria.EnableDebugOutput);
            
            // Calculate MaxSeeds from batch range if specified
            var startBatch = (int)(startBatchUpDown?.Value ?? 0);
            var endBatch = (int)(endBatchUpDown?.Value ?? 1000);
            criteria.MaxSeeds = (endBatch - startBatch) * (long)Math.Pow(36, criteria.BatchSize);

            // Update UI state
            _isRunning = true;
            if (startButton != null) startButton.IsEnabled = false;
            if (stopButton != null) stopButton.IsEnabled = true;
            
            var statusText = this.FindControl<TextBlock>("StatusText");
            if (statusText != null)
            {
                statusText.Text = $"Starting search with {criteria.ThreadCount} threads...";
            }

            // Clear previous results
            _results.Clear();
            _searchStartTime = DateTime.UtcNow;
            
            // Hide empty state and show grid
            var resultsGrid = this.FindControl<DataGrid>("ResultsGrid");
            if (_emptyState != null && resultsGrid != null)
            {
                _emptyState.IsVisible = false;
                resultsGrid.IsVisible = true;
                resultsGrid.ItemsSource = _results;
            }
            
            // Create progress handler
            var progress = new Progress<SearchProgress>(OnSearchProgress);
            
            // Create cancellation token
            _searchCts = new CancellationTokenSource();
            
            // Start the search with progress reporting
            await _searchService.StartSearchAsync(criteria, progress, _searchCts.Token);
        }
        catch (Exception ex)
        {
            Oracle.Helpers.DebugLogger.Log("SearchModal", $"Error starting search: {ex.Message}");
            
            // Reset UI state on error
            _isRunning = false;
            var startButton = this.FindControl<Button>("StartButton");
            var stopButton = this.FindControl<Button>("StopButton");
            if (startButton != null) startButton.IsEnabled = true;
            if (stopButton != null) stopButton.IsEnabled = false;
        }
    }
    
    private void OnStopClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            // Cancel via token first
            _searchCts?.Cancel();
            
            // Then stop the service
            _searchService.StopSearch();
            
            // Update UI state
            _isRunning = false;
            var startButton = this.FindControl<Button>("StartButton");
            var stopButton = this.FindControl<Button>("StopButton");
            if (startButton != null) startButton.IsEnabled = true;
            if (stopButton != null) stopButton.IsEnabled = false;
            
            var statusText = this.FindControl<TextBlock>("StatusText");
            if (statusText != null)
            {
                statusText.Text = "Search stopped";
            }
        }
        catch (Exception ex)
        {
            Oracle.Helpers.DebugLogger.Log("SearchModal", $"Error stopping search: {ex.Message}");
        }
    }
    
    private void OnSearchProgress(SearchProgress progress)
    {
        // Run UI updates on UI thread
        Dispatcher.UIThread.InvokeAsync(() =>
        {
            // Update status text
            var statusText = this.FindControl<TextBlock>("StatusText");
            if (statusText != null)
            {
                statusText.Text = progress.Message;
            }
            
            // Update speed and count displays
            var seedsSearchedText = this.FindControl<TextBlock>("SeedsSearchedText");
            var foundCountText = this.FindControl<TextBlock>("FoundCountText");
            var speedText = this.FindControl<TextBlock>("SpeedText");
            
            if (seedsSearchedText != null)
                seedsSearchedText.Text = progress.SeedsSearched.ToString("N0");
            if (foundCountText != null)
                foundCountText.Text = progress.ResultsFound.ToString();
            if (speedText != null)
                speedText.Text = $"{progress.SeedsPerSecond:F0}/s";
            
            // Add new result if any
            if (progress.NewResult != null)
            {
                _results.Add(new Oracle.Views.Modals.SearchResultViewModel
                {
                    Index = _results.Count + 1,
                    Seed = progress.NewResult.Seed,
                    Score = progress.NewResult.Score,
                    Details = progress.NewResult.Details ?? "",
                    ScoreBreakdown = progress.NewResult.ScoreBreakdown ?? ""
                });
                
                // Update summary
                var summaryText = this.FindControl<TextBlock>("SummaryText");
                if (summaryText != null)
                {
                    summaryText.Text = $"{_results.Count} results";
                }
                
                // Enable export button
                var exportButton = this.FindControl<Button>("ExportButton");
                if (exportButton != null && _results.Count > 0)
                {
                    exportButton.IsEnabled = true;
                }
                
                // Log to console
                AppendConsoleOutput($"> Found: {progress.NewResult.Seed} (Score: {progress.NewResult.Score})");
            }
            
            // Handle completion
            if (progress.IsComplete)
            {
                _isRunning = false;
                var startButton = this.FindControl<Button>("StartButton");
                var stopButton = this.FindControl<Button>("StopButton");
                if (startButton != null) startButton.IsEnabled = true;
                if (stopButton != null) stopButton.IsEnabled = false;
                
                // Show final stats
                var elapsed = DateTime.UtcNow - _searchStartTime;
                AppendConsoleOutput($"> Search completed in {elapsed:hh\\:mm\\:ss}");
                AppendConsoleOutput($"> Total seeds searched: {progress.SeedsSearched:N0}");
                AppendConsoleOutput($"> Results found: {_results.Count}");
            }
            
            // Handle errors
            if (progress.HasError)
            {
                AppendConsoleOutput($"> ERROR: {progress.Message}");
            }
        });
    }
}

public class SearchResultViewModel : INotifyPropertyChanged
{
    private int _index;
    private string _seed = "";
    private int _score;
    private string _details = "";
    private string _scoreBreakdown = "";
    
    public int Index
    {
        get => _index;
        set
        {
            _index = value;
            OnPropertyChanged();
        }
    }
    
    public string Seed 
    { 
        get => _seed; 
        set
        {
            _seed = value;
            OnPropertyChanged();
        }
    }
    
    public int Score 
    { 
        get => _score; 
        set
        {
            _score = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ScoreFormatted));
            OnPropertyChanged(nameof(ScoreTooltip));
        }
    }
    
    public string ScoreFormatted => Score.ToString("N0");
    
    public string Details 
    { 
        get => _details; 
        set
        {
            _details = value;
            OnPropertyChanged();
        }
    }
    
    public string ScoreBreakdown
    {
        get => _scoreBreakdown;
        set
        {
            _scoreBreakdown = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ScoreTooltip));
        }
    }
    
    /// <summary>
    /// Tooltip showing score breakdown
    /// </summary>
    public string ScoreTooltip
    {
        get
        {
            if (string.IsNullOrEmpty(ScoreBreakdown) || ScoreBreakdown == "[]")
                return $"Total Score: {ScoreFormatted}";
                
            try
            {
                var scores = System.Text.Json.JsonSerializer.Deserialize<int[]>(ScoreBreakdown);
                if (scores == null || scores.Length == 0)
                    return $"Total Score: {ScoreFormatted}";
                    
                var breakdown = string.Join(" + ", scores);
                return $"Score Breakdown: {breakdown} = {ScoreFormatted}";
            }
            catch
            {
                return $"Total Score: {ScoreFormatted}";
            }
        }
    }
    
    // Copy command
    private ICommand? _copyCommand;
    public ICommand CopyCommand => _copyCommand ??= new RelayCommand<string>(CopySeed);
    
    // View command
    private ICommand? _viewCommand;
    public ICommand ViewCommand => _viewCommand ??= new RelayCommand<SearchResultViewModel>(ViewDetails);
    
    private async void CopySeed(string? seed)
    {
        if (string.IsNullOrEmpty(seed)) return;
        
        try
        {
            if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                var clipboard = desktop.MainWindow?.Clipboard;
                if (clipboard != null)
                {
                    await clipboard.SetTextAsync(seed);
                    Oracle.Helpers.DebugLogger.Log("SearchModal", $"Copied seed {seed} to clipboard");
                }
            }
        }
        catch (Exception ex)
        {
            Oracle.Helpers.DebugLogger.Log("SearchModal", $"Error copying to clipboard: {ex.Message}");
        }
    }
    
    private void ViewDetails(SearchResultViewModel? result)
    {
        if (result == null) return;
        
        // Show seed details in a simple message box for now
        var detailsText = $"Seed: {result.Seed}\nScore: {result.Score:N0}\nDetails: {result.Details}";
        
        // Create a simple detail dialog
        var detailWindow = new Window
        {
            Title = $"Seed Details - {result.Seed}",
            Width = 400,
            Height = 300,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new ScrollViewer
            {
                Content = new TextBlock
                {
                    Text = detailsText,
                    Margin = new Thickness(10),
                    TextWrapping = TextWrapping.Wrap
                }
            }
        };
        
        if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            var parentWindow = desktop.MainWindow;
            if (parentWindow != null)
            {
                detailWindow.ShowDialog(parentWindow);
            }
        }
    }
    
    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Predicate<T?>? _canExecute;
    
    public RelayCommand(Action<T?> execute, Predicate<T?>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }
    
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }
    
    public bool CanExecute(object? parameter)
    {
        return _canExecute?.Invoke((T?)parameter) ?? true;
    }
    
    public void Execute(object? parameter)
    {
        _execute((T?)parameter);
    }
}