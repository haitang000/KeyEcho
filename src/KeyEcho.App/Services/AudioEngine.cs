using System.Runtime.InteropServices;
using KeyEcho.Core;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace KeyEcho.App.Services;

public sealed class AudioEngine : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private MMDeviceEnumerator? _enumerator;
    private MMDeviceNotificationClient? _notifications;
    private WasapiPlayer? _player;
    private MMDevice? _device;
    private volatile bool _disposed;
    private volatile bool _available;
    private int _rebuildScheduled;
    public VoiceMixer Mixer { get; } = new();
    public bool Available => _available;
    public event Action<string>? StatusChanged;

    public async Task StartAsync()
    {
        await Task.Run(() =>
        {
            _enumerator = new MMDeviceEnumerator();
            _notifications = _enumerator.CreateNotificationClient(false);
            _notifications.DefaultDeviceChanged += (_, args) => { if (args.Flow == DataFlow.Render && args.Role == Role.Multimedia) ScheduleRebuild(); };
            _notifications.DeviceStateChanged += (_, _) => ScheduleRebuild();
            _notifications.DeviceAdded += (_, _) => ScheduleRebuild();
            _notifications.DeviceRemoved += (_, _) => ScheduleRebuild();
        });
        await RebuildAsync();
        if (!_available) ScheduleRebuild();
    }

    private async Task RebuildAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (_disposed) return;
            _available = false;
            Mixer.Clear();
            CloseOutput();
            _device = _enumerator!.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            try { OpenOutput(lowLatency: true); }
            catch (Exception ex) when (ex is COMException or InvalidOperationException or ArgumentException or NotSupportedException)
            {
                _player?.Dispose(); _player = null;
                OpenOutput(lowLatency: false);
            }
            _available = true;
            StatusChanged?.Invoke("正在使用：" + _device.FriendlyName);
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or ArgumentException or NotSupportedException)
        {
            CloseOutput();
            StatusChanged?.Invoke("音频设备暂不可用，连接设备后会自动恢复。");
        }
        finally { _gate.Release(); }
    }

    private void OpenOutput(bool lowLatency)
    {
        var builder = new WasapiPlayerBuilder().WithDevice(_device!).WithLatency(lowLatency ? 20 : 40).WithMmcssThreadPriority("Audio");
        if (lowLatency) builder.WithLowLatency();
        var player = builder.Build();
        _player = player;
        player.PlaybackStopped += (_, args) => { if (args.Exception is not null && ReferenceEquals(_player, player)) ScheduleRebuild(); };
        player.Init(Mixer);
        player.Play();
    }

    private void CloseOutput()
    {
        var old = _player; _player = null;
        old?.Dispose();
        _device?.Dispose(); _device = null;
    }

    private void ScheduleRebuild()
    {
        if (_disposed || Interlocked.Exchange(ref _rebuildScheduled, 1) == 1) return;
        _ = Task.Run(async () =>
        {
            try
            {
                do
                {
                    await Task.Delay(_available ? 250 : 1500, _lifetime.Token);
                    await RebuildAsync();
                } while (!_available && !_disposed);
            }
            catch (OperationCanceledException) { }
            finally { Interlocked.Exchange(ref _rebuildScheduled, 0); }
        });
    }

    public void Dispose()
    {
        _disposed = true; _available = false; _lifetime.Cancel();
        _gate.Wait();
        try
        {
            _notifications?.Dispose();
            CloseOutput();
            _enumerator?.Dispose();
            Mixer.Clear();
        }
        finally { _gate.Release(); }
    }
}
