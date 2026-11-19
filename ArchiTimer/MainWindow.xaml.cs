using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ArchiTimer;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly TaskbarDisplayService _taskbarDisplay;
    private HistoryEntry? _historyDragAnchor;
    private HistoryEntry? _historyDragCurrent;
    private bool _isHistoryDragging;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;
        SetBinding(TopmostProperty, nameof(MainViewModel.AlwaysOnTop));
        _taskbarDisplay = new TaskbarDisplayService(this);
        _viewModel.TaskbarDisplayChanged += ViewModel_TaskbarDisplayChanged;
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

    private void TaskbarButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.IsTaskbarDisplayEnabled)
        {
            _taskbarDisplay.Disable();
            _viewModel.IsTaskbarDisplayEnabled = false;
            _viewModel.StatusMessage = "タスクバー表示を終了しました。";
            return;
        }

        _taskbarDisplay.Enable();
        _viewModel.IsTaskbarDisplayEnabled = true;
        _taskbarDisplay.Update(_viewModel.TaskbarTimer);
        _viewModel.StatusMessage = _viewModel.TaskbarTimer is null
            ? "タスクバー表示を開始しました。タイマーを開始すると時間が表示されます。"
            : "タスクバーに時間を表示しています。";
    }

    private void ViewModel_TaskbarDisplayChanged(object? sender, EventArgs e)
    {
        if (_viewModel.IsTaskbarDisplayEnabled)
            _taskbarDisplay.Update(_viewModel.TaskbarTimer);
    }

    private void SelectAllHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        HistoryGrid.SelectAll();
        _viewModel.StatusMessage = HistoryGrid.SelectedItems.Count == 0
            ? "選択できる履歴がありません。"
            : $"履歴を{HistoryGrid.SelectedItems.Count}件選択しました。";
    }

    private void ClearHistorySelectionButton_Click(object sender, RoutedEventArgs e)
    {
        HistoryGrid.UnselectAll();
        _viewModel.StatusMessage = "履歴の選択を解除しました。";
    }

    private void DeleteSelectedHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = HistoryGrid.SelectedItems.OfType<HistoryEntry>().ToArray();
        if (selected.Length == 0)
        {
            _viewModel.StatusMessage = "削除する履歴を選択してください。";
            return;
        }

        var result = System.Windows.MessageBox.Show(this,
            $"選択した履歴を{selected.Length}件削除します。\nこの操作は元に戻せません。",
            "履歴削除の確認", MessageBoxButton.YesNo, MessageBoxImage.Warning,
            MessageBoxResult.No);
        if (result != MessageBoxResult.Yes) return;

        _viewModel.DeleteHistory(selected);
        HistoryGrid.UnselectAll();
    }

    private void HistoryGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DeleteSelectedHistoryButton is not null)
            DeleteSelectedHistoryButton.IsEnabled = HistoryGrid.SelectedItems.Count > 0;
    }

    private void HistoryGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.None) return;
        var source = e.OriginalSource as DependencyObject;
        var row = source is null ? null : ItemsControl.ContainerFromElement(HistoryGrid, source) as DataGridRow;
        if (row?.Item is not HistoryEntry entry) return;

        _historyDragAnchor = entry;
        _historyDragCurrent = entry;
        _isHistoryDragging = true;
        SelectHistoryRange(entry, entry);
        HistoryGrid.CaptureMouse();
        e.Handled = true;
    }

    private void HistoryGrid_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isHistoryDragging) return;
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            EndHistoryDrag();
            return;
        }

        var source = HistoryGrid.InputHitTest(e.GetPosition(HistoryGrid)) as DependencyObject;
        var row = source is null ? null : ItemsControl.ContainerFromElement(HistoryGrid, source) as DataGridRow;
        if (row?.Item is not HistoryEntry current || current == _historyDragCurrent
            || _historyDragAnchor is null) return;

        _historyDragCurrent = current;
        SelectHistoryRange(_historyDragAnchor, current);
    }

    private void HistoryGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) => EndHistoryDrag();

    private void HistoryGrid_LostMouseCapture(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _isHistoryDragging = false;
        _historyDragAnchor = null;
        _historyDragCurrent = null;
    }

    private void SelectHistoryRange(HistoryEntry first, HistoryEntry last)
    {
        var firstIndex = _viewModel.History.IndexOf(first);
        var lastIndex = _viewModel.History.IndexOf(last);
        if (firstIndex < 0 || lastIndex < 0) return;

        var start = Math.Min(firstIndex, lastIndex);
        var end = Math.Max(firstIndex, lastIndex);
        HistoryGrid.SelectedItems.Clear();
        for (var index = start; index <= end; index++)
            HistoryGrid.SelectedItems.Add(_viewModel.History[index]);
    }

    private void EndHistoryDrag()
    {
        if (!_isHistoryDragging) return;
        _isHistoryDragging = false;
        _historyDragAnchor = null;
        _historyDragCurrent = null;
        if (HistoryGrid.IsMouseCaptured) HistoryGrid.ReleaseMouseCapture();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        _viewModel.TaskbarDisplayChanged -= ViewModel_TaskbarDisplayChanged;
        _taskbarDisplay.Dispose();
        _viewModel.Dispose();
    }
}
