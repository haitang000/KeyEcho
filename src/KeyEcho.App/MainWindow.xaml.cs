using System.ComponentModel;
using System.Windows;
using KeyEcho.App.Services;
using Microsoft.Win32;

namespace KeyEcho.App;

public partial class MainWindow : Window
{
    private readonly AppController _controller;
    private bool _allowClose;
    private bool _importing;
    public event Action? HiddenToTray;
    public MainWindow(AppController controller)
    {
        _controller = controller;
        InitializeComponent(); DataContext = controller;
        Height = Math.Min(Height, SystemParameters.WorkArea.Height - 24);
        MinHeight = Math.Min(MinHeight, Height);
    }
    private void Preview_Click(object sender, RoutedEventArgs e) => _controller.PreviewActive();
    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        if (_importing) return;
        var dialog = new OpenFileDialog { Title = "选择 WAV 音效", Filter = "WAV 音效 (*.wav)|*.wav", CheckFileExists = true, Multiselect = false };
        if (dialog.ShowDialog(this) != true) return;
        _importing = true;
        if (sender is System.Windows.Controls.Button button) button.IsEnabled = false;
        try { await _controller.ImportAsync(dialog.FileName); }
        catch (Exception ex) when (AppController.IsAudioFileError(ex))
        {
            MessageBox.Show(this, "无法导入这个音效，请使用有效的 PCM 或 IEEE Float WAV 文件。\n\n" + ex.Message, "导入失败", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        finally { _importing = false; if (sender is System.Windows.Controls.Button importButton) importButton.IsEnabled = true; }
    }
    private void Dismiss_Click(object sender, RoutedEventArgs e) => _controller.DismissNotice();
    private void About_Click(object sender, RoutedEventArgs e) => new AboutWindow { Owner = this }.ShowDialog();
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose) { e.Cancel = true; Hide(); HiddenToTray?.Invoke(); }
        base.OnClosing(e);
    }
    public void CloseForExit() { _allowClose = true; Close(); }
    public void Reveal() { Show(); if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal; Activate(); }
}
