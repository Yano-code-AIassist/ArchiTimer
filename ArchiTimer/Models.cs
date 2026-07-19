using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ArchiTimer;

public enum TimerMode
{
    通常,
    ポモドーロ,
    ストップウォッチ
}

public enum TimerState
{
    Running,
    Paused,
    Completed
}

public enum PomodoroPhase
{
    Focus,
    Break
}

public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class TimerItem : ObservableObject
{
    private string _name = "設計作業";
    private TimerMode _mode;
    private TimerState _state;
    private PomodoroPhase _phase;
    private long _remainingSeconds;
    private DateTimeOffset? _endAtUtc;
    private DateTimeOffset? _countUpStartedAtUtc;
    private long _accumulatedSeconds;
    private int _completedRounds;

    public Guid Id { get; set; } = Guid.NewGuid();
    public long OriginalDurationSeconds { get; set; }

    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public TimerMode Mode { get => _mode; set { if (SetProperty(ref _mode, value)) RefreshLabels(); } }
    public TimerState State { get => _state; set { if (SetProperty(ref _state, value)) RefreshLabels(); } }
    public PomodoroPhase Phase { get => _phase; set { if (SetProperty(ref _phase, value)) RefreshLabels(); } }
    public int CompletedRounds { get => _completedRounds; set { if (SetProperty(ref _completedRounds, value)) RefreshLabels(); } }
    public DateTimeOffset? EndAtUtc { get => _endAtUtc; set => SetProperty(ref _endAtUtc, value); }
    public DateTimeOffset? CountUpStartedAtUtc { get => _countUpStartedAtUtc; set => SetProperty(ref _countUpStartedAtUtc, value); }
    public long AccumulatedSeconds { get => _accumulatedSeconds; set => SetProperty(ref _accumulatedSeconds, Math.Max(0, value)); }

    public long RemainingSeconds
    {
        get => _remainingSeconds;
        set
        {
            if (SetProperty(ref _remainingSeconds, Math.Max(0, value)))
            {
                OnPropertyChanged(nameof(DisplayTime));
            }
        }
    }

    public string DisplayTime
    {
        get
        {
            var span = TimeSpan.FromSeconds(RemainingSeconds);
            var totalHours = (long)span.TotalHours;
            return $"{totalHours:00}:{span.Minutes:00}:{span.Seconds:00}";
        }
    }

    public string ModeLabel => Mode switch
    {
        TimerMode.通常 => "通常",
        TimerMode.ストップウォッチ => "0秒から計測",
        _ => Phase == PomodoroPhase.Focus ? "集中" : "休憩"
    };

    public string StateLabel => State switch
    {
        TimerState.Running when Mode == TimerMode.ストップウォッチ => "計測中",
        TimerState.Running when Mode == TimerMode.ポモドーロ => $"ポモドーロ {CompletedRounds + 1} セット目",
        TimerState.Running => EndAtUtc is null ? "実行中" : $"終了予定 {EndAtUtc.Value.ToLocalTime():M/d HH:mm}",
        TimerState.Paused => "一時停止中",
        _ => "完了"
    };

    public string ToggleLabel => State switch
    {
        TimerState.Running => "一時停止",
        TimerState.Paused => "再開",
        _ => "再スタート"
    };

    public bool ShowAddTime => Mode != TimerMode.ストップウォッチ;
    public bool ShowFinish => Mode == TimerMode.ストップウォッチ && State != TimerState.Completed;

    public static TimerItem Create(string name, TimerMode mode, long durationSeconds)
    {
        var seconds = mode switch
        {
            TimerMode.ポモドーロ => 25 * 60,
            TimerMode.ストップウォッチ => 0,
            _ => Math.Max(60, durationSeconds)
        };
        var now = DateTimeOffset.UtcNow;
        var item = new TimerItem
        {
            Name = name,
            Mode = mode,
            Phase = PomodoroPhase.Focus,
            OriginalDurationSeconds = seconds,
            RemainingSeconds = seconds,
            State = TimerState.Running,
            EndAtUtc = mode == TimerMode.ストップウォッチ ? null : now.AddSeconds(seconds),
            CountUpStartedAtUtc = mode == TimerMode.ストップウォッチ ? now : null,
            AccumulatedSeconds = 0
        };
        item.RefreshLabels();
        return item;
    }

    public void Toggle()
    {
        if (Mode == TimerMode.ストップウォッチ)
        {
            ToggleStopwatch();
            return;
        }

        switch (State)
        {
            case TimerState.Running:
                RemainingSeconds = SecondsUntilEnd();
                EndAtUtc = null;
                State = TimerState.Paused;
                break;
            case TimerState.Paused:
                EndAtUtc = DateTimeOffset.UtcNow.AddSeconds(RemainingSeconds);
                State = TimerState.Running;
                break;
            case TimerState.Completed:
                Phase = PomodoroPhase.Focus;
                RemainingSeconds = OriginalDurationSeconds;
                EndAtUtc = DateTimeOffset.UtcNow.AddSeconds(RemainingSeconds);
                State = TimerState.Running;
                break;
        }
        RefreshLabels();
    }

    public void AddTime(TimeSpan amount)
    {
        if (Mode == TimerMode.ストップウォッチ) return;
        var seconds = (long)amount.TotalSeconds;
        RemainingSeconds += seconds;
        if (State == TimerState.Running && EndAtUtc is not null) EndAtUtc = EndAtUtc.Value.AddSeconds(seconds);
        if (State == TimerState.Completed) State = TimerState.Paused;
        RefreshLabels();
    }

    public TimerEvent? Tick(DateTimeOffset nowUtc)
    {
        if (State != TimerState.Running) return null;

        if (Mode == TimerMode.ストップウォッチ)
        {
            CountUpStartedAtUtc ??= nowUtc;
            RemainingSeconds = AccumulatedSeconds + Math.Max(0, (long)Math.Floor((nowUtc - CountUpStartedAtUtc.Value).TotalSeconds));
            return null;
        }

        if (EndAtUtc is null) return null;

        RemainingSeconds = Math.Max(0, (long)Math.Ceiling((EndAtUtc.Value - nowUtc).TotalSeconds));
        OnPropertyChanged(nameof(StateLabel));
        if (RemainingSeconds > 0) return null;

        var completedKind = Mode == TimerMode.通常
            ? "通常タイマー"
            : Phase == PomodoroPhase.Focus ? "ポモドーロ・集中" : "ポモドーロ・休憩";
        var completedDuration = OriginalDurationSeconds;
        var message = $"「{Name}」が終了しました。";

        if (Mode == TimerMode.ポモドーロ)
        {
            if (Phase == PomodoroPhase.Focus)
            {
                CompletedRounds++;
                Phase = PomodoroPhase.Break;
                OriginalDurationSeconds = 5 * 60;
                message = $"「{Name}」の集中時間が終了しました。5分休憩です。";
            }
            else
            {
                Phase = PomodoroPhase.Focus;
                OriginalDurationSeconds = 25 * 60;
                message = $"「{Name}」の休憩が終了しました。集中を再開します。";
            }

            RemainingSeconds = OriginalDurationSeconds;
            EndAtUtc = nowUtc.AddSeconds(RemainingSeconds);
        }
        else
        {
            State = TimerState.Completed;
            EndAtUtc = null;
        }

        RefreshLabels();
        return new TimerEvent(Name, completedKind, completedDuration, nowUtc, message);
    }

    public void NormalizeAfterLoad()
    {
        if (Mode == TimerMode.ストップウォッチ)
        {
            if (AccumulatedSeconds == 0 && RemainingSeconds > 0) AccumulatedSeconds = RemainingSeconds;
            if (State == TimerState.Running) CountUpStartedAtUtc ??= DateTimeOffset.UtcNow;
            RefreshLabels();
            return;
        }
        if (State == TimerState.Running && EndAtUtc is null)
            EndAtUtc = DateTimeOffset.UtcNow.AddSeconds(RemainingSeconds);
        RefreshLabels();
    }

    private long SecondsUntilEnd() => EndAtUtc is null
        ? RemainingSeconds
        : Math.Max(0, (long)Math.Ceiling((EndAtUtc.Value - DateTimeOffset.UtcNow).TotalSeconds));

    public TimerEvent? FinishStopwatch(DateTimeOffset nowUtc)
    {
        if (Mode != TimerMode.ストップウォッチ || State == TimerState.Completed) return null;
        UpdateStopwatch(nowUtc);
        AccumulatedSeconds = RemainingSeconds;
        CountUpStartedAtUtc = null;
        State = TimerState.Completed;
        RefreshLabels();
        return new TimerEvent(Name, "ストップウォッチ", RemainingSeconds, nowUtc, $"「{Name}」の計測を完了しました。");
    }

    private void ToggleStopwatch()
    {
        var now = DateTimeOffset.UtcNow;
        switch (State)
        {
            case TimerState.Running:
                UpdateStopwatch(now);
                AccumulatedSeconds = RemainingSeconds;
                CountUpStartedAtUtc = null;
                State = TimerState.Paused;
                break;
            case TimerState.Paused:
                CountUpStartedAtUtc = now;
                State = TimerState.Running;
                break;
            case TimerState.Completed:
                RemainingSeconds = 0;
                AccumulatedSeconds = 0;
                CountUpStartedAtUtc = now;
                State = TimerState.Running;
                break;
        }
        RefreshLabels();
    }

    private void UpdateStopwatch(DateTimeOffset nowUtc)
    {
        if (State == TimerState.Running && CountUpStartedAtUtc is not null)
            RemainingSeconds = AccumulatedSeconds + Math.Max(0, (long)Math.Floor((nowUtc - CountUpStartedAtUtc.Value).TotalSeconds));
    }

    private void RefreshLabels()
    {
        OnPropertyChanged(nameof(ModeLabel));
        OnPropertyChanged(nameof(StateLabel));
        OnPropertyChanged(nameof(ToggleLabel));
        OnPropertyChanged(nameof(ShowAddTime));
        OnPropertyChanged(nameof(ShowFinish));
    }
}

public sealed record TimerEvent(string Name, string Kind, long DurationSeconds, DateTimeOffset CompletedAtUtc, string Message);

public sealed class TimerPreset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "設計作業";
    public TimerMode Mode { get; set; }
    public long DurationSeconds { get; set; }
    public string Description => Mode switch
    {
        TimerMode.ポモドーロ => "ポモドーロ 25分集中／5分休憩",
        TimerMode.ストップウォッチ => "0秒から作業時間を計測",
        _ => $"通常タイマー {Format(DurationSeconds)}"
    };

    private static string Format(long seconds)
    {
        var span = TimeSpan.FromSeconds(seconds);
        return $"{(long)span.TotalHours:00}:{span.Minutes:00}:{span.Seconds:00}";
    }
}

public sealed class HistoryEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "";
    public long DurationSeconds { get; set; }
    public DateTimeOffset CompletedAtUtc { get; set; }
    public string CompletedAtLocal => CompletedAtUtc.ToLocalTime().ToString("yyyy/MM/dd HH:mm");
    public string DurationLabel
    {
        get
        {
            var span = TimeSpan.FromSeconds(DurationSeconds);
            return $"{(long)span.TotalHours:00}:{span.Minutes:00}:{span.Seconds:00}";
        }
    }
}

public sealed class AppData
{
    public List<TimerItem> Timers { get; set; } = [];
    public List<TimerPreset> Presets { get; set; } = [];
    public List<HistoryEntry> History { get; set; } = [];
    public bool AlwaysOnTop { get; set; }
}
