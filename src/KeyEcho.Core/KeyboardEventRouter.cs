namespace KeyEcho.Core;

public readonly record struct KeyboardStroke(ushort ScanCode, ushort Flags, ushort VirtualKey, long Timestamp);

/// <summary>Raw Input keeps device identity; a low-level hook fills shortcut events that never arrive raw.</summary>
public sealed class KeyboardEventRouter
{
    public const int FallbackDelayMilliseconds = 12;
    private const int MatchWindowMilliseconds = 16;
    private const int RetentionMilliseconds = 250;
    private readonly KeyStateTracker _raw = new();
    private readonly KeyStateTracker _hook = new();
    private readonly List<Pending> _pending = [];
    public bool HasPending => _pending.Count != 0;

    public void OnHook(KeyboardStroke stroke)
    {
        if (!_hook.Process(0, stroke.ScanCode, stroke.Flags, stroke.VirtualKey)) return;
        _pending.RemoveAll(pending => stroke.Timestamp - pending.Stroke.Timestamp > RetentionMilliseconds);
        if (_pending.Count >= 128) _pending.RemoveAt(0);
        _pending.Add(new Pending(stroke));
    }

    public bool OnRaw(nint device, KeyboardStroke stroke)
    {
        if (!_raw.Process(device, stroke.ScanCode, stroke.Flags, stroke.VirtualKey)) return false;
        int match = -1;
        long distance = long.MaxValue;
        for (int i = 0; i < _pending.Count; i++)
        {
            var candidate = _pending[i];
            if (Identity(candidate.Stroke) != Identity(stroke)) continue;
            long difference = Math.Abs(candidate.Stroke.Timestamp - stroke.Timestamp);
            if (difference <= MatchWindowMilliseconds && difference < distance) { match = i; distance = difference; }
        }
        if (match < 0) return true;
        bool alreadyPlayed = _pending[match].Played;
        _pending.RemoveAt(match);
        return !alreadyPlayed;
    }

    public void FlushFallbacks(long now, Action<KeyboardStroke> emit)
    {
        _pending.RemoveAll(pending => now - pending.Stroke.Timestamp > RetentionMilliseconds);
        foreach (var pending in _pending)
        {
            if (!pending.Played && now - pending.Stroke.Timestamp >= FallbackDelayMilliseconds)
            {
                pending.Played = true;
                emit(pending.Stroke);
            }
        }
    }

    public void RemoveDevice(nint device) { _raw.RemoveDevice(device); _hook.Reset(); _pending.Clear(); }
    public void Reset() { _raw.Reset(); _hook.Reset(); _pending.Clear(); }
    private static int Identity(KeyboardStroke stroke) => (stroke.ScanCode == 0 ? stroke.VirtualKey : stroke.ScanCode) | ((stroke.Flags & 2) != 0 ? 0xE000 : 0);
    private sealed class Pending(KeyboardStroke stroke)
    {
        public KeyboardStroke Stroke { get; } = stroke;
        public bool Played { get; set; }
    }
}
