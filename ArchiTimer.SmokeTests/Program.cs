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

static void Ensure(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
