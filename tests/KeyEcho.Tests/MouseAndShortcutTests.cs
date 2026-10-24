using KeyEcho.Core;

namespace KeyEcho.Tests;

public sealed class MouseTests
{
    [Fact] public void EveryButtonFiresOnceOnPressAndRearmsOnRelease()
    {
        for (int i = 0; i < 5; i++)
        {
            var tracker = new MouseButtonStateTracker();
            var expected = (MouseButton)(1 << i);
            Assert.Equal(expected, tracker.Update(expected));
            Assert.Equal(MouseButton.None, tracker.Update(expected));
            Assert.Equal(MouseButton.None, tracker.Update(MouseButton.None));
            Assert.Equal(expected, tracker.Update(expected));
        }
    }
    [Fact] public void StableStateAndSimultaneousButtonsNeverRepeat()
    {
        var tracker = new MouseButtonStateTracker();
        Assert.Equal(MouseButton.None, tracker.Update(MouseButton.None));
        Assert.Equal(MouseButton.Left | MouseButton.Right, tracker.Update(MouseButton.Left | MouseButton.Right));
        for (int i = 0; i < 8000; i++) Assert.Equal(MouseButton.None, tracker.Update(MouseButton.Left | MouseButton.Right));
        Assert.Equal(MouseButton.None, tracker.Update(MouseButton.Right));
        Assert.Equal(MouseButton.Left, tracker.Update(MouseButton.Left | MouseButton.Right));
    }
    [Fact] public void ResumeBaselinesHeldButtonsWithoutPhantomClicks()
    {
        var tracker = new MouseButtonStateTracker();
        tracker.Reset(MouseButton.Left);
        Assert.Equal(MouseButton.None, tracker.Update(MouseButton.Left));
        Assert.Equal(MouseButton.None, tracker.Update(MouseButton.None));
        Assert.Equal(MouseButton.Left, tracker.Update(MouseButton.Left));
    }
    [Fact] public void MouseMuteAndVolumeNeverChangeKeyboardVoices()
    {
        var mixer = new VoiceMixer { Volume = .5f, MouseVolume = .25f };
        mixer.Play([.4f, .4f]); mixer.Play([.4f, .4f], source: SoundSource.Mouse);
        var samples = new float[1]; mixer.Read(samples, 0, 1);
        Assert.Equal(.3f, samples[0], 5);
        mixer.SetMouseEnabled(false);
        Assert.Equal(1, mixer.ActiveVoices);
        Assert.False(mixer.Play([.4f], source: SoundSource.Mouse));
        mixer.Read(samples, 0, 1); Assert.Equal(.2f, samples[0], 5);
        mixer.SetMouseEnabled(true); mixer.MouseVolume = 0;
        mixer.Play([.4f], source: SoundSource.Mouse); mixer.Read(samples, 0, 1);
        Assert.Equal(0, samples[0]);
        mixer.SetEnabled(false); Assert.False(mixer.Play([.4f], source: SoundSource.Mouse));
        Assert.True(mixer.Play([.4f], preview: true, source: SoundSource.Mouse));
    }
    [Fact] public void OldSettingsGetMouseDefaultsAndInvalidValuesAreNormalized()
    {
        var settings = System.Text.Json.JsonSerializer.Deserialize<AppSettings>("{\"Volume\":0.7,\"PresetId\":\"awm\"}")!;
        Assert.True(settings.MouseEnabled); Assert.Equal(.35f, settings.MouseVolume); Assert.Equal("crisp", settings.MousePresetId);
        var normalized = (settings with { MouseVolume = -2, MousePresetId = "bad" }).Normalize();
        Assert.Equal(0, normalized.MouseVolume); Assert.Equal("crisp", normalized.MousePresetId);
        Assert.Equal(.7f, normalized.Volume); Assert.Equal("awm", normalized.PresetId);
    }
    [Fact] public void BothMouseAssetsAreShortAudibleAndDifferent()
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "Sounds");
        var crisp = WaveAsset.Load(Path.Combine(directory, "mouse-crisp.wav"));
        var soft = WaveAsset.Load(Path.Combine(directory, "mouse-soft.wav"));
        Assert.InRange(crisp.Length, 1000, 24000);
        Assert.Contains(crisp, sample => Math.Abs(sample) > .3f);
        Assert.Contains(soft, sample => Math.Abs(sample) > .03f);
        Assert.False(crisp.SequenceEqual(soft));
    }

    [Fact] public void MouseClickVariationsNeverRepeatAndMiddleHasItsOwnRecording()
    {
        float[][] clicks = [[.1f], [.2f], [.3f], [.4f]];
        float[] middle = [.5f];
        var bank = new MouseSoundBank(clicks, middle);
        float[]? previous = null;
        for (int i = 0; i < 200; i++)
        {
            var button = new[] { MouseButton.Left, MouseButton.Right, MouseButton.Back, MouseButton.Forward }[i % 4];
            var clip = bank.ForButton(button);
            Assert.Contains(clicks, candidate => ReferenceEquals(candidate, clip));
            Assert.NotSame(previous, clip);
            Assert.Same(middle, bank.ForButton(MouseButton.Middle));
            previous = clip;
        }
        Assert.Throws<ArgumentOutOfRangeException>(() => bank.ForButton(MouseButton.None));
        Assert.Throws<ArgumentOutOfRangeException>(() => bank.ForButton(MouseButton.Left | MouseButton.Right));
    }

    [Theory]
    [InlineData("crisp")]
    [InlineData("soft")]
    public void EveryPackagedMouseTakeIsShortDistinctAndHasCleanBoundaries(string preset)
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "Sounds");
        var clips = Directory.GetFiles(directory, $"mouse-{preset}*.wav").Select(WaveAsset.Load).ToArray();
        Assert.Equal(5, clips.Length);
        for (int i = 0; i < clips.Length; i++)
        {
            var clip = clips[i];
            Assert.InRange(clip.Length, 3000, 9600); // 31–100 ms of 48 kHz stereo
            Assert.InRange(clip.Max(Math.Abs), .15f, .7f);
            Assert.Equal(0, clip[0]);
            Assert.Equal(0, clip[^1]);
            Assert.Contains(clip.Take(960), value => Math.Abs(value) > .03f); // attack within 10 ms
            for (int j = 0; j < i; j++) Assert.False(clip.SequenceEqual(clips[j]));
        }
        var bank = MouseSoundBank.Load(directory, preset);
        Assert.False(bank.ForButton(MouseButton.Left).SequenceEqual(bank.ForButton(MouseButton.Middle)));
    }
}

public sealed class ShortcutRoutingTests
{
    private static KeyboardStroke Down(long time, ushort scan = 15, ushort key = 9) => new(scan, 0, key, time);
    private static KeyboardStroke Up(long time, ushort scan = 15, ushort key = 9) => new(scan, 1, key, time);
    [Fact] public void ExpiredOrResetShortcutEventsNeverReplayAfterASystemPause()
    {
        var router = new KeyboardEventRouter(); var emitted = new List<KeyboardStroke>();
        router.OnHook(Down(100)); router.FlushFallbacks(500, emitted.Add);
        Assert.Empty(emitted); Assert.False(router.HasPending);
        router.Reset(); router.OnHook(Down(600)); router.Reset(); router.FlushFallbacks(620, emitted.Add);
        Assert.Empty(emitted);
    }
    [Fact] public void NormalKeysHaveImmediateRawSoundWithoutHookDoublePlayback()
    {
        var router = new KeyboardEventRouter(); var emitted = new List<KeyboardStroke>();
        router.OnHook(Down(100)); Assert.True(router.OnRaw(1, Down(101)));
        router.FlushFallbacks(130, emitted.Add); Assert.Empty(emitted);
    }
    [Fact] public void AltTabAndRegisteredShortcutsStillSoundWhenRawInputIsMissing()
    {
        var router = new KeyboardEventRouter(); var emitted = new List<KeyboardStroke>();
        router.OnHook(Down(100, 56, 0xA4)); Assert.True(router.OnRaw(1, Down(101, 56, 18))); // Alt
        router.OnHook(Down(110)); router.OnHook(Down(111));
        router.FlushFallbacks(121, emitted.Add); Assert.Empty(emitted);
        router.FlushFallbacks(122, emitted.Add); Assert.Single(emitted);
        router.OnHook(Up(130)); router.OnHook(Down(140));
        router.FlushFallbacks(152, emitted.Add); Assert.Equal(2, emitted.Count);
    }
    [Fact] public void DelayedRawDeliveryDoesNotRepeatAnAlreadyPlayedShortcut()
    {
        var router = new KeyboardEventRouter(); var emitted = new List<KeyboardStroke>();
        router.OnHook(Down(100)); router.FlushFallbacks(120, emitted.Add);
        Assert.Single(emitted); Assert.False(router.OnRaw(1, Down(101)));
    }
    [Fact] public void MissingRawReleaseDoesNotSilenceLaterPresses()
    {
        var router = new KeyboardEventRouter(); var emitted = new List<KeyboardStroke>();
        router.OnHook(Down(100)); Assert.True(router.OnRaw(1, Down(100)));
        router.OnHook(Up(120)); router.OnHook(Down(200));
        Assert.False(router.OnRaw(1, Down(200)));
        router.FlushFallbacks(212, emitted.Add); Assert.Single(emitted);
    }
    [Fact] public void OldShortcutDoesNotSuppressANewRawPressAndMultipleKeyboardsStillWork()
    {
        var router = new KeyboardEventRouter(); var emitted = new List<KeyboardStroke>();
        router.OnHook(Down(100)); router.FlushFallbacks(112, emitted.Add); router.OnHook(Up(120));
        router.OnHook(Down(200)); Assert.True(router.OnRaw(1, Down(201)));
        Assert.True(router.OnRaw(2, Down(203)));
        router.FlushFallbacks(220, emitted.Add); Assert.Single(emitted);
        router.RemoveDevice(1); Assert.True(router.OnRaw(1, Down(230)));
        router.Reset(); Assert.True(router.OnRaw(2, Down(240)));
    }
}
