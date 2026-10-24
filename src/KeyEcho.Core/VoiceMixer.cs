using NAudio.Wave;

namespace KeyEcho.Core;

/// <summary>Always-running, bounded polyphonic float mixer. No file I/O on the audio thread.</summary>
public sealed class VoiceMixer(int capacity = 32) : ISampleProvider
{
    private readonly object _gate = new();
    private readonly List<Voice> _voices = new(capacity > 0 ? capacity : throw new ArgumentOutOfRangeException(nameof(capacity)));
    private float _volume = 0.35f;
    private float _mouseVolume = 0.35f;
    private bool _enabled = true;
    private bool _mouseEnabled = true;
    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);
    public int ActiveVoices { get { lock (_gate) return _voices.Count; } }
    public float Volume { get { lock (_gate) return _volume; } set { lock (_gate) _volume = Math.Clamp(value, 0, 1); } }
    public float MouseVolume { get { lock (_gate) return _mouseVolume; } set { lock (_gate) _mouseVolume = Math.Clamp(value, 0, 1); } }

    public void SetEnabled(bool enabled)
    {
        lock (_gate) { _enabled = enabled; if (!enabled) _voices.Clear(); }
    }

    public void SetMouseEnabled(bool enabled)
    {
        lock (_gate)
        {
            _mouseEnabled = enabled;
            if (!enabled) _voices.RemoveAll(voice => voice.Source == SoundSource.Mouse);
        }
    }

    public bool Play(float[] samples, bool preview = false, SoundSource source = SoundSource.Keyboard)
    {
        if (samples.Length == 0) return false;
        lock (_gate)
        {
            if ((!_enabled || (source == SoundSource.Mouse && !_mouseEnabled)) && !preview) return false;
            if (_voices.Count == capacity) _voices.RemoveAt(0);
            _voices.Add(new Voice(samples, source));
            return true;
        }
    }

    public void Clear() { lock (_gate) _voices.Clear(); }

    public int Read(float[] buffer, int offset, int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (offset > buffer.Length - count) throw new ArgumentException("Invalid output buffer range.");
        return Read(buffer.AsSpan(offset, count));
    }

    public int Read(Span<float> buffer)
    {
        int count = buffer.Length;
        buffer.Clear();
        lock (_gate)
        {
            for (int v = _voices.Count - 1; v >= 0; v--)
            {
                var voice = _voices[v];
                int available = Math.Min(count, voice.Samples.Length - voice.Position);
                float volume = voice.Source == SoundSource.Mouse ? _mouseVolume : _volume;
                for (int i = 0; i < available; i++) buffer[i] += voice.Samples[voice.Position + i] * volume;
                voice.Position += available;
                if (voice.Position >= voice.Samples.Length) _voices.RemoveAt(v);
            }
            for (int i = 0; i < count; i++)
            {
                float sample = buffer[i];
                // Transparent at ordinary levels, smoothly compresses overlapping transients.
                float magnitude = Math.Abs(sample);
                buffer[i] = magnitude <= 0.8f ? sample : MathF.CopySign(0.8f + 0.18f * MathF.Tanh((magnitude - 0.8f) / 0.18f), sample);
            }
        }
        return count; // continuous silence keeps the device warm between key presses
    }

    private sealed class Voice(float[] samples, SoundSource source)
    {
        public readonly float[] Samples = samples;
        public readonly SoundSource Source = source;
        public int Position;
    }
}
