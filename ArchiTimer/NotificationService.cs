using System.Drawing;
using System.Media;
using Forms = System.Windows.Forms;

namespace ArchiTimer;

public sealed class NotificationService : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon = new()
    {
        Icon = SystemIcons.Application,
        Text = "ArchiTimer",
        Visible = true
    };

    public void Notify(string message)
    {
        SystemSounds.Exclamation.Play();
        _notifyIcon.BalloonTipTitle = "ArchiTimer";
        _notifyIcon.BalloonTipText = message;
        _notifyIcon.BalloonTipIcon = Forms.ToolTipIcon.Info;
        _notifyIcon.ShowBalloonTip(8000);
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
