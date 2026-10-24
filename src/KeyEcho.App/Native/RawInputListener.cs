using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;
using KeyEcho.Core;

namespace KeyEcho.App.Native;

public sealed class RawInputListener : IDisposable
{
    private readonly KeyboardEventRouter _keys = new();
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Thread _thread;
    private HwndSource? _source;
    private Dispatcher? _dispatcher;
    private bool _sessionRegistered;
    private nint _hook;
    private readonly KeyboardHookProc _hookProc;
    private DispatcherTimer? _fallbackTimer;
    private long _lastToggleTimestamp = long.MinValue / 2;
    public event Action<ushort, ushort>? KeyPressed;
    public event Action? ToggleRequested;
    public event Action? StateReset;
    public bool HotkeyRegistered { get; private set; }
    public bool ShortcutFallbackAvailable => _hook != 0;

    public RawInputListener()
    {
        _hookProc = KeyboardHook;
        _thread = new Thread(Run) { Name = "KeyEcho keyboard input", IsBackground = true };
        _thread.SetApartmentState(ApartmentState.STA);
    }

    public Task StartAsync() { _thread.Start(); return _ready.Task; }

    private void Run()
    {
        try
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            _source = new HwndSource(new HwndSourceParameters("KeyEcho.Input") { WindowStyle = 0, Width = 0, Height = 0 });
            _source.AddHook(WindowProc);
            RawInputDevice[] registrations =
            [
                new() { UsagePage = 1, Usage = 6, Flags = 0x100 | 0x2000, Target = _source.Handle }
            ];
            if (!RegisterRawInputDevices(registrations, 1, (uint)Marshal.SizeOf<RawInputDevice>())) throw new Win32Exception(Marshal.GetLastWin32Error());
            _fallbackTimer = new DispatcherTimer(DispatcherPriority.Send) { Interval = TimeSpan.FromMilliseconds(6) };
            _fallbackTimer.Tick += (_, _) =>
            {
                _keys.FlushFallbacks(Environment.TickCount64, EmitKeyboard);
                if (!_keys.HasPending) _fallbackTimer.Stop();
            };
            _hook = SetWindowsHookEx(13, _hookProc, GetModuleHandle(null), 0);
            HotkeyRegistered = RegisterHotKey(_source.Handle, 1, 0x4000 | 1 | 2, 0x7B); // Ctrl+Alt+F12, no-repeat
            _sessionRegistered = WTSRegisterSessionNotification(_source.Handle, 0);
            _ready.SetResult();
            Dispatcher.Run();
        }
        catch (Exception ex) { _ready.TrySetException(ex); }
        finally
        {
            if (_source is not null)
            {
                _fallbackTimer?.Stop();
                if (_hook != 0) { UnhookWindowsHookEx(_hook); _hook = 0; }
                UnregisterHotKey(_source.Handle, 1);
                if (_sessionRegistered) WTSUnRegisterSessionNotification(_source.Handle);
                RawInputDevice[] remove = [new() { UsagePage = 1, Usage = 6, Flags = 1 }];
                RegisterRawInputDevices(remove, 1, (uint)Marshal.SizeOf<RawInputDevice>());
                _source.Dispose();
            }
        }
    }

    private nint WindowProc(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        switch (message)
        {
            case 0x00FF: // WM_INPUT -- leave unhandled so DefWindowProc can perform cleanup
                uint size = (uint)Marshal.SizeOf<RawInput>();
                if (GetRawInputData(lParam, 0x10000003, out var input, ref size, (uint)Marshal.SizeOf<RawInputHeader>()) == uint.MaxValue) break;
                if (input.Header.Type == 1)
                {
                    var key = input.Keyboard;
                    var stroke = new KeyboardStroke(key.MakeCode, key.Flags, key.VirtualKey, ExpandTimestamp(unchecked((uint)GetMessageTime())));
                    if (_keys.OnRaw(input.Header.Device, stroke)) EmitKeyboard(stroke);
                    if (!_keys.HasPending) _fallbackTimer?.Stop();
                }
                break;
            case 0x0312:
                _lastToggleTimestamp = ExpandTimestamp(unchecked((uint)GetMessageTime()));
                ToggleRequested?.Invoke(); handled = true; break;
            case 0x00FE: // WM_INPUT_DEVICE_CHANGE, GIDC_REMOVAL
                if (wParam == 2) { _keys.RemoveDevice(lParam); StateReset?.Invoke(); }
                break;
            case 0x02B1: // WM_WTSSESSION_CHANGE
            case 0x0218: // WM_POWERBROADCAST
                _keys.Reset(); StateReset?.Invoke();
                break;
        }
        return 0;
    }

    private nint KeyboardHook(int code, nint wParam, nint lParam)
    {
        if (code >= 0 && wParam is 0x0100 or 0x0101 or 0x0104 or 0x0105)
        {
            var key = Marshal.PtrToStructure<LowLevelKeyboard>(lParam);
            ushort flags = (ushort)(((key.Flags & 1) != 0 ? 2 : 0) | ((key.Flags & 0x80) != 0 ? 1 : 0));
            _keys.OnHook(new KeyboardStroke((ushort)key.ScanCode, flags, (ushort)key.VirtualKey, ExpandTimestamp(key.Time)));
            if (_keys.HasPending) _fallbackTimer?.Start();
        }
        // Never consume input: shortcuts keep their original action in other apps.
        return CallNextHookEx(_hook, code, wParam, lParam);
    }

    private void EmitKeyboard(KeyboardStroke stroke)
    {
        // The owned pause hotkey gets exactly one explicit sound after its state change.
        bool toggle = HotkeyRegistered && stroke.VirtualKey == 0x7B &&
            (Math.Abs(stroke.Timestamp - _lastToggleTimestamp) <= 16 || ((GetAsyncKeyState(0x11) & 0x8000) != 0 && (GetAsyncKeyState(0x12) & 0x8000) != 0));
        if (!toggle) KeyPressed?.Invoke(stroke.ScanCode, stroke.Flags);
    }

    private static long ExpandTimestamp(uint timestamp)
    {
        long now = Environment.TickCount64;
        return now + unchecked((int)(timestamp - (uint)now));
    }

    public void Dispose()
    {
        _dispatcher?.BeginInvokeShutdown(DispatcherPriority.Send);
        if (_thread.IsAlive) _thread.Join(TimeSpan.FromSeconds(3));
    }

    [StructLayout(LayoutKind.Sequential)] private struct RawInputDevice { public ushort UsagePage, Usage; public uint Flags; public nint Target; }
    [StructLayout(LayoutKind.Sequential)] private struct RawInputHeader { public uint Type, Size; public nint Device, WParam; }
    [StructLayout(LayoutKind.Sequential)] private struct LowLevelKeyboard { public uint VirtualKey, ScanCode, Flags, Time; public nint ExtraInformation; }
    [StructLayout(LayoutKind.Sequential)] private struct RawKeyboard { public ushort MakeCode, Flags, Reserved, VirtualKey; public uint Message, ExtraInformation; }
    [StructLayout(LayoutKind.Sequential)] private struct RawInput { public RawInputHeader Header; public RawKeyboard Keyboard; }
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool RegisterRawInputDevices(RawInputDevice[] devices, uint count, uint size);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint GetRawInputData(nint input, uint command, out RawInput data, ref uint size, uint headerSize);
    [DllImport("user32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool UnregisterHotKey(nint hwnd, int id);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    private delegate nint KeyboardHookProc(int code, nint wParam, nint lParam);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern nint SetWindowsHookEx(int id, KeyboardHookProc callback, nint module, uint threadId);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] private static extern nint CallNextHookEx(nint hook, int code, nint wParam, nint lParam);
    [DllImport("user32.dll")] private static extern int GetMessageTime();
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandle(string? moduleName);
    [DllImport("wtsapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool WTSRegisterSessionNotification(nint hwnd, int flags);
    [DllImport("wtsapi32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool WTSUnRegisterSessionNotification(nint hwnd);
}
