using System.Drawing;
using System.Windows;
using System.Windows.Threading;
using KeyEcho.App.Services;
using Forms = System.Windows.Forms;

namespace KeyEcho.App;

public partial class App : System.Windows.Application
{
    private Mutex? _mutex;
    private bool _ownsMutex;
    private EventWaitHandle? _showEvent;
    private RegisteredWaitHandle? _showWait;
    private EventWaitHandle? _exitEvent;
    private RegisteredWaitHandle? _exitWait;
    private AppController? _controller;
    private MainWindow? _window;
    private Forms.NotifyIcon? _tray;
    private Forms.ToolStripMenuItem? _toggleMenu;
    private bool _exiting;
    private bool _trayHintShown;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\KeyEcho.Show");
        _exitEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\KeyEcho.Exit");
        if (e.Args.Contains("--exit")) { _exitEvent.Set(); Shutdown(); return; }
        _mutex = new Mutex(true, @"Local\KeyEcho.App", out _ownsMutex);
        if (!_ownsMutex) { _showEvent.Set(); Shutdown(); return; }
        _controller = new AppController(Dispatcher);
        _window = new MainWindow(_controller);
        MainWindow = _window;
        _window.HiddenToTray += ShowTrayHint;
        _showWait = ThreadPool.RegisterWaitForSingleObject(_showEvent, (_, _) => Dispatcher.BeginInvoke(() => _window?.Reveal()), null, Timeout.Infinite, false);
        _exitWait = ThreadPool.RegisterWaitForSingleObject(_exitEvent, (_, _) => Dispatcher.BeginInvoke(ExitApplication), null, Timeout.Infinite, false);
        CreateTray();
        _controller.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(AppController.Enabled)) UpdateTray(); };
        SessionEnding += (_, _) => ExitApplication();
        if (!e.Args.Contains("--background")) _window.Show();
        try { await _controller.InitializeAsync(); UpdateTray(); }
        catch (Exception ex)
        {
            _window.Reveal();
            MessageBox.Show(_window, "KeyEcho 无法完成初始化。\n请确认音效文件完整，或重新安装应用。\n\n" + ex.Message, "启动失败", MessageBoxButton.OK, MessageBoxImage.Error);
            ExitApplication();
        }
    }

    private void CreateTray()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("打开 KeyEcho", null, (_, _) => Dispatcher.BeginInvoke(() => _window?.Reveal()));
        _toggleMenu = new Forms.ToolStripMenuItem("暂停音效", null, (_, _) => Dispatcher.BeginInvoke(() => { if (_controller?.Ready == true) _controller.Enabled = !_controller.Enabled; }));
        menu.Items.Add(_toggleMenu);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出", null, (_, _) => Dispatcher.BeginInvoke(ExitApplication));
        using var resource = GetResourceStream(new Uri("pack://application:,,,/Assets/KeyEcho.ico"))!.Stream;
        _tray = new Forms.NotifyIcon { Icon = new Icon(resource), Text = "KeyEcho · 正在启动", ContextMenuStrip = menu, Visible = true };
        _tray.DoubleClick += (_, _) => Dispatcher.BeginInvoke(() => _window?.Reveal());
    }

    private void UpdateTray()
    {
        if (_tray is null || _controller is null || _toggleMenu is null) return;
        _tray.Text = _controller.Enabled ? "KeyEcho · 键鼠音效已开启" : "KeyEcho · 键鼠音效已暂停";
        _toggleMenu.Text = _controller.Enabled ? "暂停音效" : "恢复音效";
        _toggleMenu.Enabled = _controller.Ready;
        _toggleMenu.Checked = _controller.Enabled;
    }

    private void ShowTrayHint()
    {
        if (_trayHintShown || _tray is null) return;
        _trayHintShown = true;
        _tray.ShowBalloonTip(2500, "KeyEcho 正在托盘运行", "双击托盘图标打开窗口，右键可暂停或退出。", Forms.ToolTipIcon.Info);
    }

    private void ExitApplication()
    {
        if (_exiting) return;
        _exiting = true;
        _showWait?.Unregister(null);
        _exitWait?.Unregister(null);
        _controller?.Dispose();
        _tray?.Dispose();
        _window?.CloseForExit();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _showWait?.Unregister(null); _showEvent?.Dispose();
        _exitWait?.Unregister(null); _exitEvent?.Dispose();
        _tray?.Dispose();
        if (_ownsMutex) _mutex?.ReleaseMutex();
        _mutex?.Dispose(); base.OnExit(e);
    }
}
