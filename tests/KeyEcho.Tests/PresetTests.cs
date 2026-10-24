using System.Text.Json;
using KeyEcho.Core;

namespace KeyEcho.Tests;

public sealed class PresetTests
{
    [Fact] public void AllNineKeyboardPacksHaveCompleteIndependentKeys()
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "Sounds");
        Assert.Equal(9, PresetCatalog.Keyboards.Count);
        foreach (var preset in PresetCatalog.Keyboards)
        {
            var manifest = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(directory, preset.Id + ".json")))!;
            Assert.InRange(manifest.Count, 80, 130);
            Assert.Contains("30", manifest.Keys); Assert.Contains("57", manifest.Keys); Assert.Contains("28", manifest.Keys);
            var clips = manifest.ToDictionary(entry => int.Parse(entry.Key), entry => WaveAsset.Load(Path.Combine(directory, entry.Value)));
            foreach (var sample in clips.Values)
            {
                Assert.InRange(sample.Length, 96, 28800);
                Assert.All(sample, value => Assert.InRange(value, -.9f, .9f));
                Assert.Contains(sample, value => Math.Abs(value) > .005);
            }
            var bank = new SoundBank(clips, [clips[30], clips[31]]);
            Assert.Same(clips[57], bank.ForKey(57, 0));
            Assert.Same(clips[28], bank.ForKey(28, 0));
            if (clips.ContainsKey(0xE01C)) Assert.Same(clips[0xE01C], bank.ForKey(28, 2));
            Assert.NotEmpty(bank.ForKey(0xFE, 0));
        }
    }
    [Fact] public void OldMechanicalSelectionMigratesToGentlerRedSwitch()
    {
        Assert.Equal(PresetCatalog.DefaultKeyboard, (new AppSettings { PresetId = "mechanical" }).Normalize().PresetId);
        foreach (var preset in PresetCatalog.Keyboards) Assert.Equal(preset.Id, (new AppSettings { PresetId = preset.Id }).Normalize().PresetId);
    }
    [Fact] public void AllFourGunsIncludingAwmHaveDistinctPlayableOneShots()
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "Sounds");
        var clips = new[] { "pistol", "rifle", "shotgun", "awm" }.Select(id => WaveAsset.Load(Path.Combine(directory, id + ".wav"))).ToArray();
        for (int i = 0; i < clips.Length; i++)
        {
            var clip = clips[i];
            Assert.InRange(clip.Length, 9600, 192000);
            Assert.Contains(clip.Take(4800), value => Math.Abs(value) > .1f);
            float limit = i == 3 ? .892f : .73f; // Enhanced AWM retains 1 dB of headroom.
            Assert.All(clip, value => Assert.InRange(value, -limit, limit));
        }
        Assert.False(clips[1].SequenceEqual(clips[3]));
        Assert.Equal("awm", (new AppSettings { PresetId = "awm" }).Normalize().PresetId);
    }
    [Fact] public void FahMemeHasAudibleAttackAndHeadroom()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Sounds", "fah.wav");
        using var reader = new NAudio.Wave.WaveFileReader(path);
        Assert.Equal(2, reader.WaveFormat.Channels); // Keep the reference video's stereo image.
        var clip = WaveAsset.Load(path);
        Assert.InRange(clip.Length, 48000, 288000); // 0.5–3 seconds at 48 kHz stereo.
        Assert.Contains(Enumerable.Range(0, clip.Length / 2), frame => Math.Abs(clip[frame * 2] - clip[frame * 2 + 1]) > .05f);
        Assert.Contains(clip.Take(9600), value => Math.Abs(value) > .05f);
        Assert.All(clip, value => Assert.InRange(value, -.801f, .801f));
        Assert.InRange(Math.Abs(clip[0]), 0, .001f);
        Assert.InRange(Math.Abs(clip[^1]), 0, .001f);
    }
    [Theory]
    [InlineData("bing")]
    [InlineData("bruh")]
    [InlineData("augh")]
    [InlineData("metal-pipe")]
    [InlineData("shocked")]
    [InlineData("private-laugh")]
    [InlineData("wow")]
    public void NewMemeAssetsHaveAudibleAttacksAndHeadroom(string id)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Sounds", id + ".wav");
        using var reader = new NAudio.Wave.WaveFileReader(path);
        Assert.Equal(2, reader.WaveFormat.Channels);
        Assert.Equal(48000, reader.WaveFormat.SampleRate);
        var clip = WaveAsset.Load(path);
        Assert.InRange(clip.Length, 19200, WaveAsset.MaximumSeconds * 48000 * 2);
        Assert.Contains(clip.Take(9600), value => Math.Abs(value) > .05f);
        Assert.All(clip, value => Assert.InRange(value, -.801f, .801f));
        Assert.All(clip.Take(2).Concat(clip.TakeLast(2)), value => Assert.InRange(Math.Abs(value), 0, .001f));
    }
    [Theory]
    [InlineData("fah")]
    [InlineData("bing")]
    [InlineData("bruh")]
    [InlineData("augh")]
    [InlineData("metal-pipe")]
    [InlineData("shocked")]
    [InlineData("private-laugh")]
    [InlineData("wow")]
    public void MemeSelectionSurvivesSettingsReload(string id)
    {
        string directory = Path.Combine(Path.GetTempPath(), "KeyEcho-Fah-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new SettingsStore(directory);
            store.Save(new AppSettings { PresetId = id, MousePresetId = "follow" });
            var restored = store.Load();
            Assert.Equal(id, restored.PresetId);
            Assert.Equal("follow", restored.MousePresetId);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }
}
