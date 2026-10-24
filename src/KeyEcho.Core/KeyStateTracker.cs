namespace KeyEcho.Core;

/// <summary>Tracks physical keys, without translating or retaining typed text.</summary>
public sealed class KeyStateTracker
{
    private readonly HashSet<PhysicalKey> _pressed = [];

    public bool Process(nint device, ushort scanCode, ushort flags, ushort virtualKey)
    {
        if (virtualKey == 0xFF) return false; // KEYBOARD_OVERRUN_MAKE_CODE / fake keys
        bool released = (flags & 1) != 0;
        var key = new PhysicalKey(device, scanCode == 0 ? virtualKey : scanCode, (ushort)(flags & 6));
        // Pause has no corresponding break event on standard PC keyboards.
        if (virtualKey == 0x13) return !released;
        if (released) { _pressed.Remove(key); return false; }
        return _pressed.Add(key);
    }

    public void RemoveDevice(nint device) => _pressed.RemoveWhere(key => key.Device == device);
    public void Reset() => _pressed.Clear();
    private readonly record struct PhysicalKey(nint Device, ushort ScanCode, ushort Extended);
}
