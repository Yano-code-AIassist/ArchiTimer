using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;

namespace ArchiTimer;

/// <summary>
/// タスクバーのアプリアイコンとタイトルへ、優先タイマーの時間を表示します。
/// 表示中はWindowsタスクバーの自動非表示を一時的に解除します。
/// </summary>
public sealed class TaskbarDisplayService : IDisposable
{
    private const uint AbmGetState = 0x00000004;
    private const uint AbmSetState = 0x0000000A;
    private const int AbsAutoHide = 0x1;

    private readonly Window _window;
    private readonly ImageSource? _originalIcon;
    private readonly TaskbarItemInfo _taskbarItem = new();
    private bool _enabled;
    private bool _disabledAutoHide;
    private string? _lastSignature;

    public TaskbarDisplayService(Window window)
    {
        _window = window;
        _originalIcon = window.Icon;
    }

    public void Enable()
    {
        if (_enabled) return;

        _enabled = true;
        _window.TaskbarItemInfo = _taskbarItem;
        KeepTaskbarVisible();
    }

    public void Update(TimerItem? timer)
    {
        if (!_enabled) return;

        KeepTaskbarVisible();
        var signature = timer is null
            ? "none"
            : $"{timer.Id}:{timer.Mode}:{timer.State}:{timer.RemainingSeconds}:{timer.Name}";
        if (signature == _lastSignature) return;
        _lastSignature = signature;

        if (timer is null)
        {
            _window.Title = "ArchiTimer — タイマー待機中";
            _window.Icon = _originalIcon;
            _taskbarItem.Description = "ArchiTimer — タイマー待機中";
            _taskbarItem.ProgressState = TaskbarItemProgressState.None;
            return;
        }

        var kind = timer.Mode == TimerMode.ストップウォッチ ? "経過" : "残り";
        var state = timer.State == TimerState.Paused ? "（一時停止）" : "";
        var title = $"{kind} {timer.DisplayTime} {state}— {timer.Name} — ArchiTimer";
        var timeIcon = CreateTimeIcon(timer);
        _window.Title = title;
        _window.Icon = timeIcon;
        _taskbarItem.Description = title;
        UpdateProgress(timer);
    }

    public void Disable()
    {
        if (!_enabled) return;

        RestoreTaskbarAutoHide();
        _enabled = false;
        _lastSignature = null;
        _window.Title = "ArchiTimer";
        _window.Icon = _originalIcon;
        _window.TaskbarItemInfo = null;
    }

    private void UpdateProgress(TimerItem timer)
    {
        if (timer.State == TimerState.Paused)
        {
            _taskbarItem.ProgressState = TaskbarItemProgressState.Paused;
        }
        else if (timer.Mode == TimerMode.ストップウォッチ)
        {
            _taskbarItem.ProgressState = TaskbarItemProgressState.Indeterminate;
        }
        else
        {
            _taskbarItem.ProgressState = TaskbarItemProgressState.Normal;
            var duration = Math.Max(1, timer.OriginalDurationSeconds);
            _taskbarItem.ProgressValue = Math.Clamp(1d - timer.RemainingSeconds / (double)duration, 0d, 1d);
        }
    }

    private static ImageSource CreateTimeIcon(TimerItem timer)
    {
        const int size = 64;
        var seconds = Math.Max(0, timer.RemainingSeconds);
        var totalMinutes = seconds / 60;
        var top = totalMinutes.ToString(CultureInfo.InvariantCulture);
        var bottom = (seconds % 60).ToString("00", CultureInfo.InvariantCulture);
        var accent = timer.Mode == TimerMode.ストップウォッチ
            ? System.Windows.Media.Color.FromRgb(90, 200, 240)
            : System.Windows.Media.Color.FromRgb(244, 178, 74);

        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawRoundedRectangle(new SolidColorBrush(System.Windows.Media.Color.FromRgb(23, 26, 29)), null,
                new Rect(0, 0, size, size), 10, 10);
            var topFontSize = top.Length > 5 ? 12 : top.Length > 3 ? 17 : 24;
            DrawCenteredText(drawing, top, topFontSize, 3, System.Windows.Media.Brushes.White);
            DrawCenteredText(drawing, bottom, 24, 31, new SolidColorBrush(accent));
        }

        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static void DrawCenteredText(DrawingContext drawing, string text, double fontSize, double y,
        System.Windows.Media.Brush brush)
    {
        var formatted = new FormattedText(text, CultureInfo.InvariantCulture, System.Windows.FlowDirection.LeftToRight,
            new Typeface("Segoe UI Semibold"), fontSize, brush, 1d);
        drawing.DrawText(formatted, new System.Windows.Point((64 - formatted.Width) / 2, y));
    }

    private void KeepTaskbarVisible()
    {
        var data = CreateAppBarData();
        var state = unchecked((int)SHAppBarMessage(AbmGetState, ref data).ToUInt64());
        if ((state & AbsAutoHide) == 0) return;

        data.LParam = new IntPtr(state & ~AbsAutoHide);
        SHAppBarMessage(AbmSetState, ref data);
        _disabledAutoHide = true;
    }

    private void RestoreTaskbarAutoHide()
    {
        if (!_disabledAutoHide) return;

        var data = CreateAppBarData();
        var state = unchecked((int)SHAppBarMessage(AbmGetState, ref data).ToUInt64());
        data.LParam = new IntPtr(state | AbsAutoHide);
        SHAppBarMessage(AbmSetState, ref data);
        _disabledAutoHide = false;
    }

    private static AppBarData CreateAppBarData() => new()
    {
        CbSize = Marshal.SizeOf<AppBarData>()
    };

    public void Dispose() => Disable();

    [DllImport("shell32.dll")]
    private static extern UIntPtr SHAppBarMessage(uint message, ref AppBarData data);

    [StructLayout(LayoutKind.Sequential)]
    private struct AppBarData
    {
        public int CbSize;
        public IntPtr HWnd;
        public uint CallbackMessage;
        public uint Edge;
        public NativeRect Rect;
        public IntPtr LParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
