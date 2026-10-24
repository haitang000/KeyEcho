namespace KeyEcho.Core;

/// <summary>Real click variations, with a separate recording for the middle button.</summary>
public sealed class MouseSoundBank
{
    private readonly float[][] _clicks;
    private readonly float[] _middle;
    private readonly object _gate = new();
    private int _last = -1;

    public MouseSoundBank(float[][] clicks, float[] middle)
    {
        if (clicks.Length < 2 || clicks.Any(clip => clip.Length == 0) || middle.Length == 0)
            throw new ArgumentException("鼠标音效需要至少两段点击录音和一段中键录音。");
        _clicks = clicks;
        _middle = middle;
    }

    public static MouseSoundBank Load(string directory, string preset)
    {
        if (preset is not ("crisp" or "soft")) throw new ArgumentOutOfRangeException(nameof(preset));
        var clicks = Enumerable.Range(1, 4).Select(index => WaveAsset.Load(Path.Combine(directory,
            $"mouse-{preset}{(index == 1 ? "" : "-" + index)}.wav"))).ToArray();
        return new(clicks, WaveAsset.Load(Path.Combine(directory, $"mouse-{preset}-middle.wav")));
    }

    public float[] ForButton(MouseButton button)
    {
        if (button == MouseButton.Middle) return _middle;
        if (button is not (MouseButton.Left or MouseButton.Right or MouseButton.Back or MouseButton.Forward))
            throw new ArgumentOutOfRangeException(nameof(button));
        lock (_gate)
        {
            // Skip the previous take, including between different primary buttons.
            int next = Random.Shared.Next(_last < 0 ? _clicks.Length : _clicks.Length - 1);
            if (_last >= 0 && next >= _last) next++;
            _last = next;
            return _clicks[next];
        }
    }
}
