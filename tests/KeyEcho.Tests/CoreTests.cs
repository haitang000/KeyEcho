using KeyEcho.Core;
using NAudio.Wave;

namespace KeyEcho.Tests;

public sealed class KeyStateTests
{
    [Fact] public void HoldOnlyTriggersOnceAndReleaseRearms()
    {
        var keys = new KeyStateTracker();
        Assert.True(keys.Process(1, 30, 0, 65));
        for (int i = 0; i < 100; i++) Assert.False(keys.Process(1, 30, 0, 65));
        Assert.False(keys.Process(1, 30, 1, 65));
        Assert.True(keys.Process(1, 30, 0, 65));
    }
    [Fact] public void ChordsKeyboardsAndExtendedKeysAreIndependent()
    {
        var keys = new KeyStateTracker();
        Assert.True(keys.Process(1, 29, 0, 17)); // left Ctrl
        Assert.True(keys.Process(1, 29, 2, 17)); // right Ctrl
        Assert.True(keys.Process(1, 30, 0, 65));
        Assert.True(keys.Process(2, 30, 0, 65));
        keys.RemoveDevice(1);
        Assert.True(keys.Process(1, 30, 0, 65));
        Assert.False(keys.Process(2, 30, 0, 65));
        keys.Reset(); Assert.True(keys.Process(2, 30, 0, 65));
    }
    [Fact] public void PauseWithoutBreakDoesNotRemainStuck()
    {
        var keys = new KeyStateTracker();
        Assert.True(keys.Process(1, 69, 4, 19)); Assert.True(keys.Process(1, 69, 4, 19));
        Assert.False(keys.Process(1, 0, 0, 255));
    }
}

public sealed class MixerTests
{
    [Fact] public void OverlappingSoundsMixWithoutRestartingEarlierVoices()
    {
        var mixer = new VoiceMixer { Volume = 1 };
        mixer.Play([0.1f, 0.2f, 0.3f, 0.4f]);
        var output = new float[2]; mixer.Read(output, 0, 2);
        Assert.Equal([0.1f, 0.2f], output);
        mixer.Play([0.2f, 0.1f]); mixer.Read(output, 0, 2);
        Assert.Equal(0.5f, output[0], 5); Assert.Equal(0.5f, output[1], 5);
        Assert.Equal(0, mixer.ActiveVoices);
    }
    [Fact] public void CapacityReplacesOldestAndBoundsMixedPeaks()
    {
        var mixer = new VoiceMixer(2) { Volume = 1 };
        mixer.Play([0.1f]); mixer.Play([0.2f]); mixer.Play([0.3f]);
        var output = new float[1]; mixer.Read(output, 0, 1); Assert.Equal(0.5f, output[0], 5);
        for (int i = 0; i < 100; i++) mixer.Play([1f, -1f]);
        Assert.Equal(2, mixer.ActiveVoices);
        output = new float[2]; mixer.Read(output, 0, 2);
        Assert.All(output, sample => Assert.InRange(sample, -0.981f, 0.981f));
    }
    [Fact] public void PauseClearsImmediatelyAndPreviewDoesNotEnableTyping()
    {
        var mixer = new VoiceMixer(); mixer.Play([0.5f, 0.5f]); mixer.SetEnabled(false);
        Assert.Equal(0, mixer.ActiveVoices); Assert.False(mixer.Play([0.4f]));
        var output = new float[2]; mixer.Read(output, 0, 2); Assert.All(output, value => Assert.Equal(0, value));
        Assert.True(mixer.Play([0.4f], preview: true)); Assert.False(mixer.Play([0.4f]));
        mixer.SetEnabled(true); Assert.True(mixer.Play([0.4f]));
    }
    [Fact] public void AudioAndInputThreadsCanRunConcurrently()
    {
        var mixer = new VoiceMixer();
        Parallel.Invoke(() => { for (int i = 0; i < 5000; i++) mixer.Play(new float[480]); },
            () => { var output = new float[480]; for (int i = 0; i < 5000; i++) mixer.Read(output, 0, output.Length); },
            () => { for (int i = 0; i < 100; i++) { mixer.SetEnabled(false); mixer.SetEnabled(true); } });
        Assert.InRange(mixer.ActiveVoices, 0, 32);
    }
}

public sealed class StorageAndWaveTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "KeyEchoTests", Guid.NewGuid().ToString("N"));
    [Fact] public void DefaultsAndSettingsRoundTripAndInvalidJsonRecovery()
    {
        var store = new SettingsStore(_directory);
        Assert.Equal(new AppSettings(), store.Load());
        var expected = new AppSettings { Enabled = false, PresetId = "custom", Volume = 0.76f, StartOnLogin = true, CustomFile = "abc.wav", CustomName = "自定义.wav" };
        store.Save(expected); Assert.Equal(expected, store.Load());
        File.WriteAllText(store.FilePath, "{broken"); Assert.Equal(new AppSettings(), store.Load());
        store.Save(expected with { Volume = 5, PresetId = "unknown" });
        Assert.Equal(1, store.Load().Volume); Assert.Equal(PresetCatalog.DefaultKeyboard, store.Load().PresetId);
    }
    [Fact] public void OwnedWaveWorksAfterOriginalIsDeletedAndRejectsCorruptFile()
    {
        Directory.CreateDirectory(_directory);
        string source = Path.Combine(_directory, "source.wav"), owned = Path.Combine(_directory, "owned.wav");
        using (var writer = new WaveFileWriter(source, new WaveFormat(44100, 16, 1)))
            for (int i = 0; i < 4410; i++) writer.WriteSample((float)(Math.Sin(i * 0.1) * 0.2));
        File.Copy(source, owned); File.Delete(source);
        var samples = WaveAsset.Load(owned); Assert.InRange(samples.Length, 9000, 10000);
        Assert.Contains(samples, sample => Math.Abs(sample) > 0.1);
        File.WriteAllText(source, "not a wav"); Assert.ThrowsAny<Exception>(() => WaveAsset.Load(source));
    }
    [Fact] public void RejectsOversizedAndOverlongWaves()
    {
        Directory.CreateDirectory(_directory);
        string path = Path.Combine(_directory, "large.wav");
        using (var stream = File.Create(path)) stream.SetLength(WaveAsset.MaximumFileBytes + 1);
        Assert.Throws<InvalidDataException>(() => WaveAsset.Load(path));
        using (var writer = new WaveFileWriter(path, new WaveFormat(8000, 16, 1))) writer.Write(new byte[31 * 8000 * 2]);
        Assert.Throws<InvalidDataException>(() => WaveAsset.Load(path));
    }
    public void Dispose() { if (Directory.Exists(_directory)) Directory.Delete(_directory, true); }
}
