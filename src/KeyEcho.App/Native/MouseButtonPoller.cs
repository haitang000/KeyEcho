using System.ComponentModel;
using System.Runtime.InteropServices;
using KeyEcho.Core;
using Microsoft.Win32.SafeHandles;

namespace KeyEcho.App.Native;

/// <summary>Observes only five button states. Never registers raw mouse input or a mouse hook.</summary>
public sealed class MouseButtonPoller : IDisposable
{
    public const int IntervalMilliseconds = 4;
    private readonly ManualResetEvent _stop = new(false);
    private readonly AutoResetEvent _stateChanged = new(false);
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Func<MouseButton> _readButtons;
    private readonly Thread _thread;
    private int _enabled;
    private int _disposed;
    private long _samples;
    public event Action<MouseButton>? ButtonPressed;
    public event Action<Exception>? Failed;
    public long SamplesTaken => Interlocked.Read(ref _samples);

    public MouseButtonPoller() : this(ReadNativeButtons) { }
    internal MouseButtonPoller(Func<MouseButton> readButtons)
    {
        _readButtons = readButtons;
        _thread = new Thread(Run) { IsBackground = true, Name = "KeyEcho mouse button sampler" };
    }
    public Task StartAsync() { _thread.Start(); return _ready.Task; }
    public void SetEnabled(bool enabled)
    {
        if (Volatile.Read(ref _disposed) != 0) return;
        int value = enabled ? 1 : 0;
        if (Interlocked.Exchange(ref _enabled, value) != value) _stateChanged.Set();
    }
    public void Reset() { if (Volatile.Read(ref _disposed) == 0) _stateChanged.Set(); }

    private void Run()
    {
        try
        {
            using var timer = CreateTimer();
            WaitHandle[] activeWait = [_stop, _stateChanged, timer];
            WaitHandle[] inactiveWait = [_stop, _stateChanged];
            var tracker = new MouseButtonStateTracker();
            bool armed = false;
            _ready.TrySetResult();
            while (!_stop.WaitOne(0))
            {
                if (Volatile.Read(ref _enabled) == 0)
                {
                    if (armed) { CancelWaitableTimer(timer.SafeWaitHandle); armed = false; }
                    if (WaitHandle.WaitAny(inactiveWait) == 0) break;
                    continue;
                }
                if (!armed)
                {
                    tracker.Reset(_readButtons()); // no phantom click if a button was held on resume
                    long dueTime = -IntervalMilliseconds * 10_000L;
                    if (!SetWaitableTimer(timer.SafeWaitHandle, ref dueTime, IntervalMilliseconds, 0, 0, false)) throw new Win32Exception(Marshal.GetLastWin32Error());
                    armed = true;
                }
                int signaled = WaitHandle.WaitAny(activeWait);
                if (signaled == 0) break;
                if (signaled == 1) { CancelWaitableTimer(timer.SafeWaitHandle); armed = false; continue; }
                if (Volatile.Read(ref _enabled) == 0) continue;
                var pressed = tracker.Update(_readButtons());
                Interlocked.Increment(ref _samples);
                for (int i = 0; i < 5; i++)
                {
                    var button = (MouseButton)(1 << i);
                    if ((pressed & button) != 0) ButtonPressed?.Invoke(button);
                }
            }
            CancelWaitableTimer(timer.SafeWaitHandle);
        }
        catch (Exception ex)
        {
            if (!_ready.TrySetException(ex) && Volatile.Read(ref _disposed) == 0) Failed?.Invoke(ex);
        }
    }

    private static NativeTimer CreateTimer()
    {
        const uint synchronizeAndModify = 0x00100002;
        var handle = CreateWaitableTimerEx(0, null, 2, synchronizeAndModify);
        if (handle.IsInvalid) { handle.Dispose(); handle = CreateWaitableTimerEx(0, null, 0, synchronizeAndModify); }
        if (handle.IsInvalid) { int error = Marshal.GetLastWin32Error(); handle.Dispose(); throw new Win32Exception(error); }
        return new NativeTimer(handle);
    }
    private static MouseButton ReadNativeButtons()
    {
        var buttons = MouseButton.None;
        if (GetAsyncKeyState(1) < 0) buttons |= MouseButton.Left;
        if (GetAsyncKeyState(2) < 0) buttons |= MouseButton.Right;
        if (GetAsyncKeyState(4) < 0) buttons |= MouseButton.Middle;
        if (GetAsyncKeyState(5) < 0) buttons |= MouseButton.Back;
        if (GetAsyncKeyState(6) < 0) buttons |= MouseButton.Forward;
        return buttons;
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _stop.Set();
        if (!_thread.IsAlive || _thread.Join(TimeSpan.FromSeconds(3))) { _stop.Dispose(); _stateChanged.Dispose(); }
    }
    private sealed class NativeTimer : WaitHandle
    {
        public NativeTimer(SafeWaitHandle handle) => SafeWaitHandle = handle;
    }
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeWaitHandle CreateWaitableTimerEx(nint attributes, string? name, uint flags, uint access);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetWaitableTimer(SafeWaitHandle timer, ref long dueTime, int period, nint completion, nint argument, [MarshalAs(UnmanagedType.Bool)] bool resume);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CancelWaitableTimer(SafeWaitHandle timer);
}
