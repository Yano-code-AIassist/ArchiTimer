using ArchiTimer;

var timer = TimerItem.Create("ストップウォッチ動作確認", TimerMode.ストップウォッチ, 0);
Ensure(timer.RemainingSeconds == 0, "開始時刻が0秒ではありません。" );
Ensure(timer.State == TimerState.Running, "作成直後に実行状態ではありません。" );

var now = DateTimeOffset.UtcNow;
timer.CountUpStartedAtUtc = now.AddSeconds(-3.2);
timer.Tick(now);
Ensure(timer.RemainingSeconds == 3, "経過時間が正しく積算されません。" );

timer.Toggle();
var pausedAt = timer.RemainingSeconds;
timer.Tick(now.AddHours(1));
Ensure(timer.RemainingSeconds == pausedAt, "一時停止中に時間が進みました。" );

timer.Toggle();
var resumedAt = timer.CountUpStartedAtUtc ?? throw new InvalidOperationException("再開時刻がありません。" );
timer.Tick(resumedAt.AddSeconds(2.2));
Ensure(timer.RemainingSeconds == pausedAt + 2, "再開後に続きから計測されません。" );

var completed = timer.FinishStopwatch(resumedAt.AddSeconds(2.2));
Ensure(completed is not null && completed.DurationSeconds == timer.RemainingSeconds, "完了時間を履歴用データへ渡せません。" );
Ensure(timer.State == TimerState.Completed, "作業完了状態になりません。" );

Console.WriteLine("ストップウォッチ動作確認: 成功");

var pomodoro = TimerItem.Create("ポモドーロ設定確認", TimerMode.ポモドーロ, 0, 2 * 60, 1 * 60);
Ensure(pomodoro.RemainingSeconds == 2 * 60, "設定した集中時間で開始されません。" );
var focusCompleted = pomodoro.Tick(pomodoro.EndAtUtc!.Value);
Ensure(focusCompleted is not null, "集中時間の完了イベントが作成されません。" );
Ensure(pomodoro.Phase == PomodoroPhase.Break && pomodoro.RemainingSeconds == 60,
    "設定した休憩時間へ切り替わりません。" );
var breakCompleted = pomodoro.Tick(pomodoro.EndAtUtc!.Value);
Ensure(breakCompleted is not null, "休憩時間の完了イベントが作成されません。" );
Ensure(pomodoro.Phase == PomodoroPhase.Focus && pomodoro.RemainingSeconds == 2 * 60,
    "設定した集中時間へ戻りません。" );
pomodoro.State = TimerState.Paused;
pomodoro.EndAtUtc = null;
pomodoro.RemainingSeconds = 90;
var pomodoroCompleted = pomodoro.Finish(DateTimeOffset.UtcNow)
    ?? throw new InvalidOperationException("ポモドーロを完了できません。" );
Ensure(pomodoroCompleted.DurationSeconds == 210,
    "ポモドーロの合計時間が正しく集計されません。" );
Ensure(pomodoroCompleted.WorkDurationSeconds == 150 && pomodoroCompleted.BreakDurationSeconds == 60,
    "ポモドーロの作業時間と休憩時間が正しく分離されません。" );
Ensure(pomodoro.State == TimerState.Completed && !pomodoro.ShowFinish,
    "ポモドーロが作業完了状態になりません。" );

var normalTimer = TimerItem.Create("通常タイマー完了確認", TimerMode.通常, 10 * 60);
Ensure(normalTimer.ShowFinish, "通常タイマーに作業完了操作が表示されません。" );
normalTimer.State = TimerState.Paused;
normalTimer.EndAtUtc = null;
normalTimer.RemainingSeconds = 7 * 60;
var normalCompleted = normalTimer.Finish(DateTimeOffset.UtcNow)
    ?? throw new InvalidOperationException("通常タイマーを完了できません。" );
Ensure(normalCompleted.DurationSeconds == 3 * 60,
    "通常タイマーの途中完了時間が正しく記録されません。" );

var preset = new TimerPreset
{
    Mode = TimerMode.ポモドーロ,
    PomodoroFocusSeconds = 40 * 60,
    PomodoroBreakSeconds = 10 * 60
};
Ensure(preset.Description.Contains("40分集中／10分休憩"), "プリセットに設定時間が表示されません。" );
var defaultData = System.Text.Json.JsonSerializer.Deserialize<AppData>("{}")!;
Ensure(defaultData.PomodoroFocusMinutes == 25 && defaultData.PomodoroBreakMinutes == 5,
    "既存データ向けのポモドーロ初期値が保たれていません。" );
var history = new HistoryEntry
{
    Kind = "ポモドーロ",
    WorkDurationSeconds = 150,
    BreakDurationSeconds = 60
};
Ensure(history.WorkDurationLabel == "00:02:30" && history.BreakDurationLabel == "00:01:00",
    "履歴の作業時間と休憩時間を表示できません。" );
var legacyHistory = new HistoryEntry { Kind = "ポモドーロ・集中", DurationSeconds = 25 * 60 };
Ensure(legacyHistory.WorkDurationLabel == "00:25:00" && legacyHistory.BreakDurationLabel == "00:00:00",
    "既存のポモドーロ履歴から時間内訳を復元できません。" );

Console.WriteLine("ポモドーロ時間設定確認: 成功");

static void Ensure(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
