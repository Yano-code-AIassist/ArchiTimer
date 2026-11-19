using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;

namespace ArchiTimer;

public sealed class MainViewModel : ObservableObject, IDisposable
{
    private readonly JsonDataStore _store;
    private readonly NotificationService _notifications;
    private readonly DispatcherTimer _ticker;
    private string _newTimerName = "設計作業";
    private int _newHours = 1;
    private int _newMinutes;
    private int _newPomodoroFocusMinutes = 25;
    private int _newPomodoroBreakMinutes = 5;
    private TimerMode _selectedMode;
    private bool _alwaysOnTop;
    private bool _isCompact;
    private bool _isTaskbarDisplayEnabled;
    private string _statusMessage = "";

    public ObservableCollection<TimerItem> Timers { get; } = [];
    public ObservableCollection<TimerPreset> Presets { get; } = [];
    public ObservableCollection<HistoryEntry> History { get; } = [];
    public IReadOnlyList<TimerMode> Modes { get; } = Enum.GetValues<TimerMode>();

    public string NewTimerName { get => _newTimerName; set => SetProperty(ref _newTimerName, value); }
    public int NewHours { get => _newHours; set => SetProperty(ref _newHours, Math.Clamp(value, 0, 9999)); }
    public int NewMinutes { get => _newMinutes; set => SetProperty(ref _newMinutes, Math.Clamp(value, 0, 59)); }
    public int NewPomodoroFocusMinutes
    {
        get => _newPomodoroFocusMinutes;
        set
        {
            if (SetProperty(ref _newPomodoroFocusMinutes, Math.Clamp(value, 1, 999))) Save();
        }
    }
    public int NewPomodoroBreakMinutes
    {
        get => _newPomodoroBreakMinutes;
        set
        {
            if (SetProperty(ref _newPomodoroBreakMinutes, Math.Clamp(value, 1, 999))) Save();
        }
    }
    public TimerMode SelectedMode { get => _selectedMode; set => SetProperty(ref _selectedMode, value); }
    public bool AlwaysOnTop { get => _alwaysOnTop; set { if (SetProperty(ref _alwaysOnTop, value)) Save(); } }
    public bool IsCompact { get => _isCompact; set => SetProperty(ref _isCompact, value); }
    public bool IsTaskbarDisplayEnabled
    {
        get => _isTaskbarDisplayEnabled;
        set
        {
            if (SetProperty(ref _isTaskbarDisplayEnabled, value))
                OnPropertyChanged(nameof(TaskbarButtonLabel));
        }
    }
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
    public string RunningSummary => $"実行中 {Timers.Count(x => x.State == TimerState.Running)}件 / 全{Timers.Count}件";
    public string TaskbarButtonLabel => IsTaskbarDisplayEnabled ? "タスクバー表示を終了" : "タスクバー表示";
    public TimerItem? TaskbarTimer => Timers.FirstOrDefault(x => x.State == TimerState.Running)
        ?? Timers.FirstOrDefault(x => x.State == TimerState.Paused);

    public event EventHandler? TaskbarDisplayChanged;

    public ICommand AddTimerCommand { get; }
    public ICommand ToggleTimerCommand { get; }
    public ICommand FinishTimerCommand { get; }
    public ICommand DeleteTimerCommand { get; }
    public ICommand SavePresetCommand { get; }
    public ICommand StartPresetCommand { get; }
    public ICommand DeletePresetCommand { get; }

    public MainViewModel(JsonDataStore? store = null, NotificationService? notifications = null)
    {
        _store = store ?? new JsonDataStore();
        _notifications = notifications ?? new NotificationService();
        AddTimerCommand = new RelayCommand(_ => AddTimer());
        ToggleTimerCommand = new RelayCommand(ToggleTimer);
        FinishTimerCommand = new RelayCommand(FinishTimer);
        DeleteTimerCommand = new RelayCommand(DeleteTimer);
        SavePresetCommand = new RelayCommand(_ => SavePreset());
        StartPresetCommand = new RelayCommand(StartPreset);
        DeletePresetCommand = new RelayCommand(DeletePreset);

        Load();
        _ticker = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(250) };
        _ticker.Tick += Tick;
        _ticker.Start();
        Tick(null, EventArgs.Empty);
    }

    private void AddTimer()
    {
        var name = NormalizeName(NewTimerName);
        var seconds = checked((long)NewHours * 3600 + (long)NewMinutes * 60);
        if (SelectedMode == TimerMode.通常 && seconds <= 0)
        {
            StatusMessage = "通常タイマーは1分以上に設定してください。";
            return;
        }
        Timers.Insert(0, TimerItem.Create(name, SelectedMode, seconds,
            NewPomodoroFocusMinutes * 60L, NewPomodoroBreakMinutes * 60L));
        StatusMessage = $"「{name}」を開始しました。";
        CollectionChanged();
    }

    private void ToggleTimer(object? parameter)
    {
        if (parameter is not TimerItem timer) return;
        timer.Toggle();
        StatusMessage = timer.State == TimerState.Paused
            ? $"「{timer.Name}」を一時停止しました。"
            : $"「{timer.Name}」を実行しています。";
        StateChanged();
    }

    private void FinishTimer(object? parameter)
    {
        if (parameter is not TimerItem timer) return;
        var timerEvent = timer.Finish(DateTimeOffset.UtcNow);
        if (timerEvent is null) return;
        AddHistory(timerEvent);
        StatusMessage = timerEvent.Message;
        StateChanged();
    }

    private void DeleteTimer(object? parameter)
    {
        if (parameter is not TimerItem timer) return;
        Timers.Remove(timer);
        StatusMessage = $"「{timer.Name}」を一覧から削除しました。";
        CollectionChanged();
    }

    private void SavePreset()
    {
        var name = NormalizeName(NewTimerName);
        var seconds = SelectedMode switch
        {
            TimerMode.ポモドーロ => NewPomodoroFocusMinutes * 60L,
            TimerMode.ストップウォッチ => 0,
            _ => checked((long)NewHours * 3600 + (long)NewMinutes * 60)
        };
        if (SelectedMode == TimerMode.通常 && seconds <= 0)
        {
            StatusMessage = "プリセットは1分以上に設定してください。";
            return;
        }
        Presets.Insert(0, new TimerPreset
        {
            Name = name,
            Mode = SelectedMode,
            DurationSeconds = seconds,
            PomodoroFocusSeconds = NewPomodoroFocusMinutes * 60L,
            PomodoroBreakSeconds = NewPomodoroBreakMinutes * 60L
        });
        StatusMessage = $"「{name}」をプリセットに保存しました。";
        Save();
    }

    private void StartPreset(object? parameter)
    {
        if (parameter is not TimerPreset preset) return;
        Timers.Insert(0, TimerItem.Create(preset.Name, preset.Mode, preset.DurationSeconds,
            preset.GetPomodoroFocusSeconds(), preset.GetPomodoroBreakSeconds()));
        StatusMessage = $"プリセット「{preset.Name}」を開始しました。";
        CollectionChanged();
    }

    private void DeletePreset(object? parameter)
    {
        if (parameter is not TimerPreset preset) return;
        Presets.Remove(preset);
        Save();
    }

    public int DeleteHistory(IEnumerable<HistoryEntry> entries)
    {
        var selected = entries.ToHashSet();
        var deleted = 0;
        foreach (var entry in History.Where(selected.Contains).ToArray())
        {
            History.Remove(entry);
            deleted++;
        }

        if (deleted > 0)
        {
            StatusMessage = $"選択した履歴を{deleted}件削除しました。";
            Save();
        }
        return deleted;
    }

    private void Tick(object? sender, EventArgs e)
    {
        var now = DateTimeOffset.UtcNow;
        var changed = false;
        foreach (var timer in Timers.ToArray())
        {
            var timerEvent = timer.Tick(now);
            if (timerEvent is null) continue;
            changed = true;
            AddHistory(timerEvent);
            _notifications.Notify(timerEvent.Message);
            StatusMessage = timerEvent.Message;
        }
        OnPropertyChanged(nameof(RunningSummary));
        TaskbarDisplayChanged?.Invoke(this, EventArgs.Empty);
        if (changed) Save();
    }

    private void AddHistory(TimerEvent timerEvent) => History.Insert(0, new HistoryEntry
    {
        Name = timerEvent.Name,
        Kind = timerEvent.Kind,
        DurationSeconds = timerEvent.DurationSeconds,
        WorkDurationSeconds = timerEvent.WorkDurationSeconds,
        BreakDurationSeconds = timerEvent.BreakDurationSeconds,
        CompletedAtUtc = timerEvent.CompletedAtUtc
    });

    private void Load()
    {
        var data = _store.Load();
        foreach (var timer in data.Timers) { timer.NormalizeAfterLoad(); Timers.Add(timer); }
        foreach (var preset in data.Presets) Presets.Add(preset);
        foreach (var entry in data.History.OrderByDescending(x => x.CompletedAtUtc)) History.Add(entry);
        _alwaysOnTop = data.AlwaysOnTop;
        _newPomodoroFocusMinutes = Math.Clamp(data.PomodoroFocusMinutes, 1, 999);
        _newPomodoroBreakMinutes = Math.Clamp(data.PomodoroBreakMinutes, 1, 999);
        OnPropertyChanged(nameof(AlwaysOnTop));
        OnPropertyChanged(nameof(NewPomodoroFocusMinutes));
        OnPropertyChanged(nameof(NewPomodoroBreakMinutes));
    }

    public void Save() => _store.Save(new AppData
    {
        Timers = Timers.ToList(),
        Presets = Presets.ToList(),
        History = History.ToList(),
        AlwaysOnTop = AlwaysOnTop,
        PomodoroFocusMinutes = NewPomodoroFocusMinutes,
        PomodoroBreakMinutes = NewPomodoroBreakMinutes
    });

    private void CollectionChanged()
    {
        OnPropertyChanged(nameof(RunningSummary));
        TaskbarDisplayChanged?.Invoke(this, EventArgs.Empty);
        Save();
    }

    private void StateChanged()
    {
        OnPropertyChanged(nameof(RunningSummary));
        TaskbarDisplayChanged?.Invoke(this, EventArgs.Empty);
        Save();
    }
    private static string NormalizeName(string value) => string.IsNullOrWhiteSpace(value) ? "名称未設定の作業" : value.Trim();

    public void Dispose()
    {
        _ticker.Stop();
        Save();
        _notifications.Dispose();
    }
}
