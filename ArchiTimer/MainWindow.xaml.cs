using System.ComponentModel;
using System.Windows;

namespace ArchiTimer;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
        SetBinding(TopmostProperty, nameof(MainViewModel.AlwaysOnTop));
    }

    private void CompactButton_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.IsCompact = !_viewModel.IsCompact;
        if (_viewModel.IsCompact)
        {
            WindowState = WindowState.Normal;
            MinWidth = 320;
            MinHeight = 160;
            Width = 360;
            Height = 210;
            ResizeMode = ResizeMode.CanResizeWithGrip;
            Topmost = true;
            var area = SystemParameters.WorkArea;
            Left = area.Right - Width - 12;
            Top = area.Bottom - Height - 12;
        }
        else
        {
            MinWidth = 760;
            MinHeight = 520;
            Width = 1040;
            Height = 720;
            Topmost = _viewModel.AlwaysOnTop;
            Left = (SystemParameters.WorkArea.Width - Width) / 2 + SystemParameters.WorkArea.Left;
            Top = (SystemParameters.WorkArea.Height - Height) / 2 + SystemParameters.WorkArea.Top;
        }
    }

    private void Window_Closing(object? sender, CancelEventArgs e) => _viewModel.Dispose();
}
