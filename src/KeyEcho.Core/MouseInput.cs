namespace KeyEcho.Core;

[Flags]
public enum MouseButton
{
    None = 0, Left = 1, Right = 2, Middle = 4, Back = 8, Forward = 16
}

public enum SoundSource { Keyboard, Mouse }
public sealed record MousePreset(string Id, string Name, string Description);

/// <summary>Detects press edges from button state only; coordinates and wheel data never enter this path.</summary>
public sealed class MouseButtonStateTracker
{
    private MouseButton _previous;
    public MouseButton Update(MouseButton buttons)
    {
        buttons &= MouseButton.Left | MouseButton.Right | MouseButton.Middle | MouseButton.Back | MouseButton.Forward;
        var fresh = buttons & ~_previous;
        _previous = buttons;
        return fresh;
    }
    public void Reset(MouseButton current = MouseButton.None) => _previous = current;
}
