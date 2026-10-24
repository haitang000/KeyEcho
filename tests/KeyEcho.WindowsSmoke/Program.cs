using System.IO;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using KeyEcho.App.Native;
using KeyEcho.App.Services;
using KeyEcho.Core;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace KeyEcho.WindowsSmoke;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        #pragma warning disable WPF0001
        var application = new System.Windows.Application { ShutdownMode = ShutdownMode.OnExplicitShutdown, ThemeMode = ThemeMode.Light };
        #pragma warning restore WPF0001
        application.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/KeyEcho.App;component/Theme.xaml") });
        var dispatcher = Dispatcher.CurrentDispatcher;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
        int exitCode = 0;
        var task = args.Contains("--input-probe") ? InputProbeAsync(args) : RunAsync(args);
        _ = task.ContinueWith(completed => dispatcher.BeginInvoke(() =>
        {
            if (completed.Exception is { } error) { Console.Error.WriteLine(error.Flatten()); exitCode = 1; }
            dispatcher.InvokeShutdown();
        }), TaskScheduler.Default);
        Dispatcher.Run();
        GC.KeepAlive(application);
        return exitCode;
    }

    private static async Task InputProbeAsync(string[] args)
    {
        string report = args.FirstOrDefault(arg => arg.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) ?? "input-probe.json";
        int received = 0;
        using var listener = new RawInputListener();
        listener.KeyPressed += (_, _) => Interlocked.Increment(ref received);
        await listener.StartAsync();
        Console.WriteLine("INPUT_PROBE_READY");
        await Task.Delay(TimeSpan.FromSeconds(20));
        File.WriteAllText(report, JsonSerializer.Serialize(new { acceptedPresses = received, hotkeyRegistered = listener.HotkeyRegistered }));
        Console.WriteLine($"Accepted presses: {received}");
        Require(received > 0, "No raw keyboard presses reached the background listener.");
    }

    private static async Task RunAsync(string[] args)
    {
        string output = Path.GetFullPath(args.FirstOrDefault() ?? "artifacts/qa");
        Directory.CreateDirectory(output);
        await VerifySamplerTransitionsAsync();
        var samplerProfile = await ProfileSamplerAsync();
        string data = Path.Combine(output, "smoke-data-" + Guid.NewGuid().ToString("N"));
        var controller = new AppController(Dispatcher.CurrentDispatcher, data);
        try
        {
            await controller.InitializeAsync();
            Require(controller.Ready, "Controller initialization");
            Require(controller.ShortcutFallbackAvailable, "Global shortcut hook installation");
            Require(!HasRawMouseRegistration(), "KeyEcho must never register raw mouse movement input");
            Require(controller.CachedKeyboardPacks <= 1, "Startup should decode only the selected keyboard pack");
            foreach (var preset in PresetCatalog.Keyboards)
            {
                controller.SelectedKeyboardPreset = preset;
                Require(controller.PresetId == preset.Id, "Keyboard selection: " + preset.Id);
                Require(controller.CachedKeyboardPacks <= 2, "Keyboard cache must remain bounded");
            }
            controller.AwmSelected = true;
            Require(controller.SelectedName == "AWM 狙击枪", "AWM selection");

            // Capture this test process only. Keep only the numerical peak, never audio.
            await using var capture = await new WasapiRecorderBuilder()
                .WithProcessLoopback((uint)Environment.ProcessId, ProcessLoopbackMode.IncludeTargetProcessTree)
                .WithFormat(WaveFormat.CreateIeeeFloatWaveFormat(48000, 2)).BuildAsync();
            var peakLock = new object();
            float peak = 0;
            capture.DataAvailable += (bytes, _, _, _) =>
            {
                var samples = MemoryMarshal.Cast<byte, float>(bytes);
                float local = 0;
                foreach (float sample in samples) local = Math.Max(local, Math.Abs(sample));
                lock (peakLock) peak = Math.Max(peak, local);
            };
            capture.StartRecording();
            await Task.Delay(150);
            controller.Volume = 35;
            controller.Preview();
            await Task.Delay(1800);
            capture.StopRecording();
            Require(peak > .002f, "WASAPI process loopback found no rendered audio.");

            controller.Volume = 0;
            controller.MouseVolume = 48;
            controller.MouseEnabled = true;
            controller.SelectedMousePreset = PresetCatalog.Mice.First(preset => preset.Id == "crisp");
            controller.ActiveInputTab = 1;
            Require(controller.ActiveVolume == 48 && controller.Volume == 0, "Independent mouse volume");
            Require(controller.ActiveSoundName == "清脆点击", "Mouse tab display");
            float mousePeak = await MeasureMouseAudioAsync(controller);
            Require(mousePeak > .002f, "No mouse audio reached WASAPI while keyboard volume was zero.");
            var mousePresetPeaks = new Dictionary<string, Dictionary<string, float>>();
            foreach (string preset in new[] { "crisp", "soft" })
            {
                controller.SelectedMousePreset = PresetCatalog.Mice.First(item => item.Id == preset);
                var peaks = new Dictionary<string, float>();
                foreach (var button in new[] { KeyEcho.Core.MouseButton.Left, KeyEcho.Core.MouseButton.Right, KeyEcho.Core.MouseButton.Middle, KeyEcho.Core.MouseButton.Back, KeyEcho.Core.MouseButton.Forward })
                {
                    float buttonPeak = await MeasureAudioAsync(() => Require(controller.PlayMouse(button), preset + " " + button + " dispatch"));
                    Require(buttonPeak > .002f, preset + " " + button + " must reach WASAPI");
                    peaks[button.ToString()] = buttonPeak;
                }
                controller.Enabled = false;
                float previewPeak = await MeasureAudioAsync(controller.PreviewActive);
                Require(previewPeak > .002f && !controller.Enabled, preset + " preview while paused");
                peaks["pausedPreview"] = previewPeak;
                controller.Enabled = true;
                mousePresetPeaks[preset] = peaks;
            }
            controller.MouseEnabled = false;
            Require(!controller.PlayMouse(KeyEcho.Core.MouseButton.Left), "Disabled mouse must not play");
            await Task.Delay(30);
            long stoppedSamples = controller.MouseSamplesTaken;
            await Task.Delay(60);
            Require(controller.MouseSamplesTaken == stoppedSamples, "Mouse disabled must stop sampling completely");
            controller.MouseEnabled = true;
            controller.Enabled = false;
            Require(!controller.PlayMouse(KeyEcho.Core.MouseButton.Right), "Master pause must also pause the mouse");
            controller.Enabled = true;
            controller.SelectedMousePreset = PresetCatalog.Mice.First(preset => preset.Id == "follow");
            Require(controller.ActiveSoundName.Contains("AWM"), "Mouse follow mode must use current AWM preset");
            controller.Volume = 35;

            controller.FahSelected = true;
            controller.ActiveInputTab = 0;
            Require(controller.PresetId == "fah" && controller.SelectedName == "FAHHHHHH" && !controller.AwmSelected && !controller.MechanicalSelected, "FAHHHHHH selection and previous preset reset");
            Require(controller.ActiveSoundName == "FAHHHHHH" && controller.ActiveSoundDescription.Contains("TikTok"), "FAHHHHHH display");
            controller.Enabled = false;
            float fahPeak = await MeasureAudioAsync(controller.PreviewActive, 2400);
            Require(fahPeak > .002f && !controller.Enabled, "FAHHHHHH preview must reach WASAPI while paused without enabling global audio");
            controller.Enabled = true;
            controller.ActiveInputTab = 1;
            Require(controller.ActiveSoundName == "跟随：FAHHHHHH", "Mouse follows FAHHHHHH");
            float fahMousePeak = await MeasureAudioAsync(() => Require(controller.PlayMouse(KeyEcho.Core.MouseButton.Left), "FAHHHHHH mouse dispatch"), 2400);
            Require(fahMousePeak > .002f, "FAHHHHHH mouse playback must reach WASAPI");
            controller.Dispose();
            controller = new AppController(Dispatcher.CurrentDispatcher, data);
            await controller.InitializeAsync();
            Require(controller.FahSelected && controller.SelectedName == "FAHHHHHH" && controller.SelectedMousePreset.Id == "follow", "FAHHHHHH selection and mouse follow mode restore after restart");

            var memePeaks = new Dictionary<string, object>();
            foreach (var (id, name) in new[] { ("bing", "冰冰冰"), ("bruh", "bruh"), ("augh", "AUGHHHHH"), ("metal-pipe", "钢管落地"), ("shocked", "Shocked"), ("private-laugh", "私人笑声"), ("wow", "wow") })
            {
                using var asset = new WaveFileReader(Path.Combine(AppContext.BaseDirectory, "Assets", "Sounds", id + ".wav"));
                int playbackMilliseconds = (int)Math.Ceiling(asset.TotalTime.TotalMilliseconds) + 250;
                if (id == "bing") controller.BingSelected = true;
                else if (id == "bruh") controller.BruhSelected = true;
                else if (id == "augh") controller.AughSelected = true;
                else if (id == "metal-pipe") controller.MetalPipeSelected = true;
                else if (id == "shocked") controller.ShockedSelected = true;
                else if (id == "private-laugh") controller.PrivateLaughSelected = true;
                else controller.WowSelected = true;
                controller.ActiveInputTab = 0;
                Require(controller.PresetId == id && controller.ActiveSoundName == name && !controller.FahSelected && !controller.MechanicalSelected && !controller.CustomSelected, name + " selection and display");
                Require(new[] { controller.BingSelected, controller.BruhSelected, controller.AughSelected, controller.MetalPipeSelected, controller.ShockedSelected, controller.PrivateLaughSelected, controller.WowSelected }.Count(selected => selected) == 1, "Only one additional meme preset may be selected");
                controller.Enabled = false;
                float previewPeak = await MeasureAudioAsync(controller.PreviewActive, playbackMilliseconds);
                Require(previewPeak > .002f && !controller.Enabled, name + " preview while paused reaches WASAPI");
                controller.Enabled = true;
                controller.ActiveInputTab = 1;
                Require(controller.ActiveSoundName == "跟随：" + name, "Mouse follows " + name);
                float followPeak = await MeasureAudioAsync(() => Require(controller.PlayMouse(KeyEcho.Core.MouseButton.Left), name + " mouse dispatch"), playbackMilliseconds);
                Require(followPeak > .002f, name + " mouse playback reaches WASAPI");
                controller.Dispose();
                controller = new AppController(Dispatcher.CurrentDispatcher, data);
                await controller.InitializeAsync();
                Require(controller.PresetId == id && controller.SelectedName == name && controller.SelectedMousePreset.Id == "follow", name + " selection restores after restart");
                memePeaks[id] = new { previewPeak, mouseFollowPeak = followPeak };
            }

            string original = Path.Combine(output, "temporary-import-source.wav");
            File.Copy(Path.Combine(AppContext.BaseDirectory, "Assets", "Sounds", "awm.wav"), original, true);
            await controller.ImportAsync(original);
            Require(controller.CustomSelected && controller.HasCustom, "WAV import");
            File.Delete(original);
            string selected = controller.CustomName;
            string invalid = Path.Combine(output, "invalid.wav");
            File.WriteAllText(invalid, "invalid wave");
            bool rejected = false;
            try { await controller.ImportAsync(invalid); }
            catch (Exception ex) when (AppController.IsAudioFileError(ex)) { rejected = true; }
            Require(rejected && controller.CustomName == selected && controller.CustomSelected, "Failed import must retain the previous selection");
            File.Delete(invalid);
            controller.Enabled = false;
            controller.Volume = 22;
            controller.Dispose();

            controller = new AppController(Dispatcher.CurrentDispatcher, data);
            await controller.InitializeAsync();
            Require(controller.CustomSelected && controller.HasCustom && !controller.Enabled && controller.Volume == 22, "Reload owned WAV after original deletion and restore settings");
            Require(controller.MouseEnabled && controller.MouseVolume == 48 && controller.SelectedMousePreset.Id == "follow", "Mouse settings restore");
            controller.WowSelected = true;
            controller.Enabled = true;
            controller.Volume = 35;
            controller.SelectedMousePreset = PresetCatalog.Mice.First(preset => preset.Id == "follow");
            controller.DismissNotice();
            RenderLayouts(controller, output);
            File.WriteAllText(Path.Combine(output, "windows-smoke.json"), JsonSerializer.Serialize(new
            {
                passed = true,
                keyboardPacks = PresetCatalog.Keyboards.Count,
                audioLoopbackPeak = peak,
                mouseLoopbackPeak = mousePeak,
                mousePresetLoopbackPeaks = mousePresetPeaks,
                fahLoopbackPeak = fahPeak,
                fahMouseLoopbackPeak = fahMousePeak,
                newMemeLoopbackPeaks = memePeaks,
                shortcutHookInstalled = controller.ShortcutFallbackAvailable,
                cachedKeyboardPacks = controller.CachedKeyboardPacks,
                cachedKeyboardMiB = controller.CachedKeyboardBytes / 1048576.0,
                rawMouseRegistered = HasRawMouseRegistration(),
                samplerProfile,
                audio = controller.AudioStatus,
                scenarios = new[] { "button sampler press/hold/release", "sampler pause/resume without phantom clicks", "no raw mouse movement registration", "disabled mouse stops sampling", "all keyboard selections", "AWM selection", "FAHHHHHH selection and display", "FAHHHHHH preview while paused reaches WASAPI", "mouse follows FAHHHHHH with actual WASAPI output", "FAHHHHHH selection restores after restart", "all additional meme presets selection and mutually exclusive display", "all additional meme presets preview while paused reaches WASAPI", "mouse follows all additional meme presets with actual WASAPI output", "all additional meme presets restore after restart", "process-specific WASAPI output", "mouse WASAPI output with keyboard volume zero", "independent mouse volume and mute", "mouse follows AWM", "mouse settings restore", "global shortcut hook installation", "owned WAV import", "invalid WAV rollback", "original file deletion", "settings reload", "both tabs at 100/150/200 percent offscreen layout rendering" },
                note = "Physical unplug/replug, lock/resume and alternate real monitor DPI require manual hardware verification."
            }, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"PASS: Windows smoke; no raw mouse registration; button sampler bounded and pauses; keyboard peak {peak:F5}; mouse peak {mousePeak:F5}; FAHHHHHH preview {fahPeak:F5}, mouse {fahMousePeak:F5}; all additional memes preview/follow/restore; shortcut hook; independent mute/volume; follow mode; import/restore.");
        }
        finally { controller.Dispose(); }
    }

    private static async Task VerifySamplerTransitionsAsync()
    {
        int held = 0;
        var events = new ConcurrentQueue<KeyEcho.Core.MouseButton>();
        using var sampler = new MouseButtonPoller(() => (KeyEcho.Core.MouseButton)Volatile.Read(ref held));
        sampler.ButtonPressed += events.Enqueue;
        await sampler.StartAsync(); sampler.SetEnabled(true);
        await WaitUntilAsync(() => sampler.SamplesTaken >= 4);
        Volatile.Write(ref held, (int)KeyEcho.Core.MouseButton.Left);
        await WaitUntilAsync(() => events.Count == 1);
        long prior = sampler.SamplesTaken;
        await WaitUntilAsync(() => sampler.SamplesTaken >= prior + 5);
        Require(events.Count == 1, "Holding a mouse button must not repeat");
        Volatile.Write(ref held, 0); prior = sampler.SamplesTaken;
        await WaitUntilAsync(() => sampler.SamplesTaken >= prior + 2);
        Volatile.Write(ref held, (int)(KeyEcho.Core.MouseButton.Right | KeyEcho.Core.MouseButton.Middle));
        await WaitUntilAsync(() => events.Count == 3);
        sampler.SetEnabled(false); await Task.Delay(30);
        prior = sampler.SamplesTaken; await Task.Delay(50);
        Require(sampler.SamplesTaken == prior, "Paused sampler must have no wakeups");
        Volatile.Write(ref held, (int)KeyEcho.Core.MouseButton.Back);
        sampler.SetEnabled(true); prior = sampler.SamplesTaken;
        await WaitUntilAsync(() => sampler.SamplesTaken >= prior + 4);
        Require(events.Count == 3, "Held button on resume must not produce a phantom click");
        Volatile.Write(ref held, 0); prior = sampler.SamplesTaken;
        await WaitUntilAsync(() => sampler.SamplesTaken >= prior + 2);
        Volatile.Write(ref held, (int)KeyEcho.Core.MouseButton.Back);
        await WaitUntilAsync(() => events.Count == 4);
    }

    private static async Task<object> ProfileSamplerAsync()
    {
        using var sampler = new MouseButtonPoller();
        await sampler.StartAsync(); sampler.SetEnabled(true);
        await Task.Delay(250);
        using var process = Process.GetCurrentProcess();
        process.Refresh(); double initialCpu = process.TotalProcessorTime.TotalMilliseconds;
        long initialSamples = sampler.SamplesTaken;
        var watch = Stopwatch.StartNew();
        await Task.Delay(1500); watch.Stop(); process.Refresh();
        long samples = sampler.SamplesTaken - initialSamples;
        double cpuMs = process.TotalProcessorTime.TotalMilliseconds - initialCpu;
        sampler.SetEnabled(false);
        Require(samples > 0 && samples / watch.Elapsed.TotalSeconds <= 500, "Mouse sampling must remain bounded regardless of pointer report rate");
        return new
        {
            targetIntervalMilliseconds = MouseButtonPoller.IntervalMilliseconds,
            measuredSamplesPerSecond = samples / watch.Elapsed.TotalSeconds,
            diagnosticProcessCpuPercentOfMachine = 100 * cpuMs / watch.Elapsed.TotalMilliseconds / Environment.ProcessorCount,
            measurementSeconds = watch.Elapsed.TotalSeconds,
            note = "Sampler-only diagnostics phase; does not measure browser frame times or full app CPU."
        };
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var watch = Stopwatch.StartNew();
        while (!condition()) { if (watch.Elapsed > TimeSpan.FromSeconds(2)) throw new TimeoutException("Mouse sampler did not observe the expected state."); await Task.Delay(5); }
    }

    private static bool HasRawMouseRegistration()
    {
        uint count = 0, size = (uint)Marshal.SizeOf<RegisteredDevice>();
        if (GetRegisteredRawInputDevices(null, ref count, size) == uint.MaxValue) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        var devices = new RegisteredDevice[count];
        if (count > 0 && GetRegisteredRawInputDevices(devices, ref count, size) == uint.MaxValue) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        return devices.Any(device => device.UsagePage == 1 && device.Usage == 2);
    }
    [StructLayout(LayoutKind.Sequential)] private struct RegisteredDevice { public ushort UsagePage, Usage; public uint Flags; public nint Target; }
    [DllImport("user32.dll", SetLastError = true)] private static extern uint GetRegisteredRawInputDevices([Out] RegisteredDevice[]? devices, ref uint count, uint size);

    private static void RenderLayouts(AppController controller, string directory)
    {
        var window = new KeyEcho.App.MainWindow(controller);
        var view = (FrameworkElement)window.Content;
        window.Content = null;
        view.SetValue(System.Windows.Controls.Panel.BackgroundProperty, new SolidColorBrush(Color.FromRgb(245, 247, 250)));
        view.DataContext = controller;
        view.Measure(new Size(814, 701));
        view.Arrange(new Rect(0, 0, 814, 701));
        view.UpdateLayout();
        foreach (int tab in new[] { 0, 1 })
        {
            controller.ActiveInputTab = tab;
            view.Measure(new Size(814, 701)); view.Arrange(new Rect(0, 0, 814, 701)); view.UpdateLayout();
            if (tab == 0)
            {
                var keyboardScroll = (System.Windows.Controls.ScrollViewer)window.FindName("KeyboardSoundsScrollViewer");
                keyboardScroll.ScrollToVerticalOffset(200);
                view.UpdateLayout();
            }
            foreach (double scale in new[] { 1.0, 1.5, 2.0 })
            {
                var image = new RenderTargetBitmap((int)(814 * scale), (int)(701 * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
                image.Render(view);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
                using var stream = File.Create(Path.Combine(directory, $"{(tab == 0 ? "layout" : "mouse-layout")}-{scale * 100:0}.png")); encoder.Save(stream);
            }
        }
        window.CloseForExit();
    }

    private static Task<float> MeasureMouseAudioAsync(AppController controller) =>
        MeasureAudioAsync(() => Require(controller.PlayMouse(KeyEcho.Core.MouseButton.Left), "Mouse playback dispatch"));

    private static async Task<float> MeasureAudioAsync(Action play, int durationMilliseconds = 350)
    {
        await using var capture = await new WasapiRecorderBuilder()
            .WithProcessLoopback((uint)Environment.ProcessId, ProcessLoopbackMode.IncludeTargetProcessTree)
            .WithFormat(WaveFormat.CreateIeeeFloatWaveFormat(48000, 2)).BuildAsync();
        float peak = 0;
        var gate = new object();
        capture.DataAvailable += (bytes, _, _, _) =>
        {
            float local = 0;
            foreach (float sample in MemoryMarshal.Cast<byte, float>(bytes)) local = Math.Max(local, Math.Abs(sample));
            lock (gate) peak = Math.Max(peak, local);
        };
        capture.StartRecording(); await Task.Delay(100);
        play();
        await Task.Delay(durationMilliseconds); capture.StopRecording();
        lock (gate) return peak;
    }

    private static void Require(bool passed, string scenario) { if (!passed) throw new InvalidOperationException("FAIL: " + scenario); }
}
