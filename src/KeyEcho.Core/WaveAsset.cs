using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace KeyEcho.Core;

public static class WaveAsset
{
    public const int MaximumSeconds = 30;
    public const long MaximumFileBytes = 32 * 1024 * 1024;

    public static float[] Load(string path)
    {
        if (new FileInfo(path).Length > MaximumFileBytes) throw new InvalidDataException("请选择小于 32 MB 的 WAV 文件。");
        using var reader = new WaveFileReader(path);
        if (reader.TotalTime.TotalSeconds > MaximumSeconds) throw new InvalidDataException("音效长度不能超过 30 秒。");
        if (reader.WaveFormat.Channels is < 1 or > 2) throw new InvalidDataException("请选择单声道或双声道 WAV 文件。");
        if (reader.WaveFormat.SampleRate is < 8000 or > 192000) throw new InvalidDataException("不支持该 WAV 采样率。");
        ISampleProvider samples = reader.ToSampleProvider();
        if (samples.WaveFormat.Channels == 1) samples = new MonoToStereoSampleProvider(samples);
        if (samples.WaveFormat.SampleRate != 48000) samples = new WdlResamplingSampleProvider(samples, 48000);
        int expectedSamples = (int)Math.Ceiling(reader.TotalTime.TotalSeconds * 48000 * 2);
        var result = new List<float>(Math.Clamp(expectedSamples, 2, MaximumSeconds * 48000 * 2));
        var buffer = new float[8192];
        int read;
        while ((read = samples.Read(buffer.AsSpan())) > 0)
        {
            if (result.Count + read > MaximumSeconds * 48000 * 2 + 8192) throw new InvalidDataException("音效长度不能超过 30 秒。");
            for (int i = 0; i < read; i++) result.Add(float.IsFinite(buffer[i]) ? Math.Clamp(buffer[i], -1, 1) : 0);
        }
        if (result.Count < 2) throw new InvalidDataException("WAV 文件中没有可播放的声音。");
        return result.ToArray();
    }
}
