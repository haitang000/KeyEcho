using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Threading;
using KeyEcho.App.Native;
using KeyEcho.Core;

namespace KeyEcho.App.Services;

public sealed class AppController : INotifyPropertyChanged, IDisposable
{
    private readonly Dispatcher _ui;
    private readonly SettingsStore _store;
    private readonly AudioEngine _audio = new();
    private readonly RawInputListener _input = new();
    private readonly MouseButtonPoller _mouseInput = new();
    private readonly Dictionary<string, SoundBank> _banks = [];
    private readonly SoundBankCache _keyboardCache = new(2);
    private readonly Dictionary<string, MouseSoundBank> _mouseBanks = [];
    private readonly DispatcherTimer _saveTimer;
    private SoundBank? _currentBank;
    private MouseSoundBank? _currentMouseBank;
    private int _activeInputTab;
    private KeyboardPreset _keyboardPreset = PresetCatalog.Keyboards[0];
    private AppSettings _settings;
    private string _audioStatus = "正在连接音频设备…";
    private string _notice = "";
    private volatile bool _ready;
    private bool _disposed;

    public AppController(Dispatcher ui, string? dataDirectory = null)
    {
        _ui = ui;
        _store = new SettingsStore(dataDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KeyEcho"));
        _settings = _store.Load() with { StartOnLogin = StartupRegistration.IsEnabled() };
        _keyboardPreset = PresetCatalog.Find(_settings.PresetId) ?? PresetCatalog.Keyboards[0];
        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); SaveNow(); };
        _audio.StatusChanged += text => Dispatch(() => { _audioStatus = text; Changed(nameof(AudioStatus)); });
        _input.KeyPressed += (scanCode, flags) =>
        {
            var bank = Volatile.Read(ref _currentBank);
            if (_ready && _audio.Available && bank is not null) _audio.Mixer.Play(bank.ForKey(scanCode, flags));
        };
        _mouseInput.ButtonPressed += button => PlayMouse(button);
        _mouseInput.Failed += _ => Dispatch(() => Notice = "鼠标点击检测已停止，请重新打开 KeyEcho。");
        _input.ToggleRequested += () => Dispatch(() =>
        {
            Enabled = !Enabled;
            var bank = Volatile.Read(ref _currentBank);
            if (_ready && _audio.Available && bank is not null) _audio.Mixer.Play(bank.ForKey(0x58, 0), preview: true);
        });
        _input.StateReset += () => { _audio.Mixer.Clear(); _mouseInput.Reset(); };
    }

    public bool Ready => _ready;
    internal bool ShortcutFallbackAvailable => _input.ShortcutFallbackAvailable;
    internal int CachedKeyboardPacks => _keyboardCache.Count;
    internal long CachedKeyboardBytes => _keyboardCache.CachedBytes;
    internal long MouseSamplesTaken => _mouseInput.SamplesTaken;
    public bool Enabled
    {
        get => _settings.Enabled;
        set
        {
            if (_settings.Enabled == value) return;
            _settings = _settings with { Enabled = value };
            _audio.Mixer.SetEnabled(value);
            UpdateMouseSampling();
            Changed(); Changed(nameof(StateText)); SaveSoon();
        }
    }
    public double Volume
    {
        get => Math.Round(_settings.Volume * 100);
        set
        {
            float next = (float)Math.Clamp(value / 100, 0, 1);
            if (Math.Abs(_settings.Volume - next) < 0.0001) return;
            _settings = _settings with { Volume = next }; _audio.Mixer.Volume = next;
            Changed(); Changed(nameof(ActiveVolume)); SaveSoon();
        }
    }
    public bool MouseEnabled
    {
        get => _settings.MouseEnabled;
        set
        {
            if (_settings.MouseEnabled == value) return;
            _settings = _settings with { MouseEnabled = value };
            _audio.Mixer.SetMouseEnabled(value);
            UpdateMouseSampling();
            Changed(); SaveSoon();
        }
    }
    public double MouseVolume
    {
        get => Math.Round(_settings.MouseVolume * 100);
        set
        {
            float next = (float)Math.Clamp(value / 100, 0, 1);
            if (Math.Abs(_settings.MouseVolume - next) < .0001) return;
            _settings = _settings with { MouseVolume = next }; _audio.Mixer.MouseVolume = next;
            UpdateMouseSampling();
            Changed(); Changed(nameof(ActiveVolume)); SaveSoon();
        }
    }
    public IReadOnlyList<MousePreset> MousePresets => PresetCatalog.Mice;
    public MousePreset SelectedMousePreset
    {
        get => PresetCatalog.Mice.First(preset => preset.Id == _settings.MousePresetId);
        set
        {
            if (value is null) return;
            _settings = _settings with { MousePresetId = value.Id };
            Volatile.Write(ref _currentMouseBank, _mouseBanks.GetValueOrDefault(value.Id));
            Changed(); Changed(nameof(ActiveSoundName)); Changed(nameof(ActiveSoundDescription)); SaveSoon();
        }
    }
    public int ActiveInputTab
    {
        get => _activeInputTab;
        set
        {
            _activeInputTab = Math.Clamp(value, 0, 1);
            Changed(); Changed(nameof(ActiveSoundName)); Changed(nameof(ActiveSoundDescription)); Changed(nameof(ActiveVolume)); Changed(nameof(ActiveVolumeLabel));
        }
    }
    public double ActiveVolume { get => ActiveInputTab == 1 ? MouseVolume : Volume; set { if (ActiveInputTab == 1) MouseVolume = value; else Volume = value; } }
    public string ActiveVolumeLabel => ActiveInputTab == 1 ? "鼠标音量 · 独立调节" : "键盘音量 · 独立调节";
    public string ActiveSoundName => ActiveInputTab == 1 ? (_settings.MousePresetId == "follow" ? "跟随：" + SelectedName : SelectedMousePreset.Name) : SelectedName;
    public string ActiveSoundDescription => ActiveInputTab == 1 ? SelectedMousePreset.Description : MechanicalSelected ? SelectedKeyboardPreset.Description : PresetId switch
    {
        "fah" => "TikTok 热门喊叫 · 每次按下，播放一次",
        "bing" or "bruh" or "augh" or "metal-pipe" or "shocked" or "private-laugh" or "wow" => "原声梗音效 · 每次按下，播放一次",
        _ => "每次按下，播放一次"
    };
    public bool StartOnLogin
    {
        get => _settings.StartOnLogin;
        set
        {
            try { StartupRegistration.SetEnabled(value); _settings = _settings with { StartOnLogin = value }; SaveSoon(); }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException) { Notice = "无法修改开机启动设置。"; }
            Changed();
        }
    }
    public string PresetId => _settings.PresetId;
    public IReadOnlyList<KeyboardPreset> KeyboardPresets => PresetCatalog.Keyboards;
    public KeyboardPreset SelectedKeyboardPreset
    {
        get => _keyboardPreset;
        set { if (value is not null) { Select(value.Id); Changed(); } }
    }
    public bool MechanicalSelected { get => PresetCatalog.IsKeyboard(PresetId); set { if (value) Select(_keyboardPreset.Id); } }
    public bool PistolSelected { get => PresetId == "pistol"; set { if (value) Select("pistol"); } }
    public bool RifleSelected { get => PresetId == "rifle"; set { if (value) Select("rifle"); } }
    public bool ShotgunSelected { get => PresetId == "shotgun"; set { if (value) Select("shotgun"); } }
    public bool AwmSelected { get => PresetId == "awm"; set { if (value) Select("awm"); } }
    public bool FahSelected { get => PresetId == "fah"; set { if (value) Select("fah"); } }
    public bool BingSelected { get => PresetId == "bing"; set { if (value) Select("bing"); } }
    public bool BruhSelected { get => PresetId == "bruh"; set { if (value) Select("bruh"); } }
    public bool AughSelected { get => PresetId == "augh"; set { if (value) Select("augh"); } }
    public bool MetalPipeSelected { get => PresetId == "metal-pipe"; set { if (value) Select("metal-pipe"); } }
    public bool ShockedSelected { get => PresetId == "shocked"; set { if (value) Select("shocked"); } }
    public bool PrivateLaughSelected { get => PresetId == "private-laugh"; set { if (value) Select("private-laugh"); } }
    public bool WowSelected { get => PresetId == "wow"; set { if (value) Select("wow"); } }
    public bool CustomSelected { get => PresetId == "custom"; set { if (value) Select("custom"); } }
    public bool HasCustom => _banks.ContainsKey("custom");
    public string CustomName => _settings.CustomName ?? "尚未导入音效";
    public string StateText => Enabled ? "键鼠音效已开启" : "键鼠音效已暂停";
    public string AudioStatus => _audioStatus;
    public string Notice { get => _notice; private set { _notice = value; Changed(); Changed(nameof(HasNotice)); } }
    public bool HasNotice => !string.IsNullOrWhiteSpace(Notice);
    public string SelectedName => PresetId switch { "pistol" => "手枪", "rifle" => "步枪", "shotgun" => "霰弹枪", "awm" => "AWM 狙击枪", "fah" => "FAHHHHHH", "bing" => "冰冰冰", "bruh" => "bruh", "augh" => "AUGHHHHH", "metal-pipe" => "钢管落地", "shocked" => "Shocked", "private-laugh" => "私人笑声", "wow" => "wow", "custom" => CustomName, _ => PresetCatalog.Find(PresetId)?.Name ?? "机械键盘" };

    public async Task InitializeAsync()
    {
        var loaded = await Task.Run(() =>
        {
            var banks = new Dictionary<string, SoundBank>();
            string sounds = Path.Combine(AppContext.BaseDirectory, "Assets", "Sounds");
            // Warm just the selected keyboard off the UI thread. Other packs load on selection.
            if (PresetCatalog.IsKeyboard(PresetId)) GetKeyboardBank(PresetId);
            foreach (string preset in new[] { "pistol", "rifle", "shotgun", "awm", "fah", "bing", "bruh", "augh", "metal-pipe", "shocked", "private-laugh", "wow" }) banks[preset] = SoundBank.Uniform(WaveAsset.Load(Path.Combine(sounds, preset + ".wav")));
            if (_settings.CustomFile is { } file)
            {
                // Settings only reference files owned by this application.
                if (Path.GetFileName(file) == file)
                {
                    try { banks["custom"] = SoundBank.Uniform(WaveAsset.Load(Path.Combine(_store.DirectoryPath, "Sounds", file))); }
                    catch (Exception ex) when (IsAudioFileError(ex)) { Dispatch(() => Notice = "上次导入的音效不可用，请重新导入。"); }
                }
            }
            return banks;
        });
        foreach (var (id, bank) in loaded) _banks[id] = bank;
        foreach (string id in new[] { "crisp", "soft" }) _mouseBanks[id] = MouseSoundBank.Load(Path.Combine(AppContext.BaseDirectory, "Assets", "Sounds"), id);
        Volatile.Write(ref _currentMouseBank, _mouseBanks.GetValueOrDefault(_settings.MousePresetId));
        _audio.Mixer.Volume = _settings.Volume;
        _audio.Mixer.MouseVolume = _settings.MouseVolume;
        _audio.Mixer.SetMouseEnabled(_settings.MouseEnabled);
        _audio.Mixer.SetEnabled(_settings.Enabled);
        Select(_banks.ContainsKey(PresetId) || PresetCatalog.IsKeyboard(PresetId) ? PresetId : PresetCatalog.DefaultKeyboard);
        await _audio.StartAsync();
        await _input.StartAsync();
        UpdateMouseSampling();
        await _mouseInput.StartAsync();
        if (!_input.HotkeyRegistered) Notice = "Ctrl + Alt + F12 已被其他程序占用，请使用窗口或托盘暂停音效。";
        else if (!_input.ShortcutFallbackAvailable) Notice = "快捷键按键声补偿未能启用，部分全局组合键可能没有声音。请尝试重新打开 KeyEcho。";
        _ready = true; Changed(nameof(Ready)); Changed(nameof(HasCustom));
    }

    private void Select(string id)
    {
        SoundBank? bank;
        try
        {
            if (PresetCatalog.IsKeyboard(id)) bank = GetKeyboardBank(id);
            else if (!_banks.TryGetValue(id, out bank)) return;
        }
        catch (Exception ex) when (IsAudioFileError(ex) || ex is JsonException)
        {
            Notice = "无法加载这套键盘音效，请重新安装完整音效文件。";
            Changed(nameof(SelectedKeyboardPreset)); return;
        }
        _audio.Mixer.Clear();
        Volatile.Write(ref _currentBank, bank);
        _settings = _settings with { PresetId = id };
        if (PresetCatalog.Find(id) is { } keyboard) { _keyboardPreset = keyboard; Changed(nameof(SelectedKeyboardPreset)); }
        foreach (string property in new[] { nameof(PresetId), nameof(MechanicalSelected), nameof(PistolSelected), nameof(RifleSelected), nameof(ShotgunSelected), nameof(AwmSelected), nameof(FahSelected), nameof(BingSelected), nameof(BruhSelected), nameof(AughSelected), nameof(MetalPipeSelected), nameof(ShockedSelected), nameof(PrivateLaughSelected), nameof(WowSelected), nameof(CustomSelected), nameof(SelectedName), nameof(ActiveSoundName), nameof(ActiveSoundDescription) }) Changed(property);
        SaveSoon();
    }

    private SoundBank GetKeyboardBank(string id) => _keyboardCache.GetOrLoad(id, () =>
    {
        string sounds = Path.Combine(AppContext.BaseDirectory, "Assets", "Sounds");
        var mapping = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(sounds, id + ".json"))) ?? throw new InvalidDataException("音效映射文件损坏。");
        var keys = mapping.ToDictionary(entry => int.Parse(entry.Key), entry => WaveAsset.Load(Path.Combine(sounds, entry.Value)));
        var fallback = keys.Where(entry => entry.Key is >= 30 and <= 38).Select(entry => entry.Value).ToArray();
        if (fallback.Length == 0) throw new InvalidDataException("键盘音效不完整。");
        return new SoundBank(keys, fallback);
    });

    public void Preview()
    {
        var bank = Volatile.Read(ref _currentBank);
        if (!_audio.Available) { Notice = "请先连接可用的音频输出设备。"; return; }
        if (bank is not null) _audio.Mixer.Play(bank.Preview(), preview: true);
    }

    public void PreviewActive()
    {
        if (ActiveInputTab == 0) { Preview(); return; }
        if (!_audio.Available) { Notice = "请先连接可用的音频输出设备。"; return; }
        PlayMouse(MouseButton.Left, preview: true);
    }

    internal bool PlayMouse(MouseButton button, bool preview = false)
    {
        if (!_ready || !_audio.Available || button is not (MouseButton.Left or MouseButton.Right or MouseButton.Middle or MouseButton.Back or MouseButton.Forward)) return false;
        var clip = Volatile.Read(ref _currentMouseBank)?.ForButton(button) ?? Volatile.Read(ref _currentBank)?.Preview();
        return clip is not null && _audio.Mixer.Play(clip, preview, SoundSource.Mouse);
    }

    public async Task ImportAsync(string source)
    {
        var imported = await Task.Run(() =>
        {
            string directory = Path.Combine(_store.DirectoryPath, "Sounds");
            Directory.CreateDirectory(directory);
            string file = Guid.NewGuid().ToString("N") + ".wav";
            string destination = Path.Combine(directory, file);
            try
            {
                if (new FileInfo(source).Length > WaveAsset.MaximumFileBytes) throw new InvalidDataException("请选择小于 32 MB 的 WAV 文件。");
                File.Copy(source, destination);
                return (file, samples: WaveAsset.Load(destination));
            }
            catch { if (File.Exists(destination)) File.Delete(destination); throw; }
        });
        string? previousFile = _settings.CustomFile;
        _banks["custom"] = SoundBank.Uniform(imported.samples);
        _settings = _settings with { CustomFile = imported.file, CustomName = Path.GetFileName(source) };
        Select("custom");
        // Persist the new owned file before removing its predecessor.
        if (SaveNow() && previousFile is not null && Path.GetFileName(previousFile) == previousFile)
        {
            try { File.Delete(Path.Combine(_store.DirectoryPath, "Sounds", previousFile)); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        Notice = "音效已导入，原始文件移动或删除后仍可使用。";
        Changed(nameof(HasCustom)); Changed(nameof(CustomName)); Changed(nameof(SelectedName));
    }

    public static bool IsAudioFileError(Exception ex) => ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or FormatException or NotSupportedException;
    public void DismissNotice() => Notice = "";
    private void UpdateMouseSampling() => _mouseInput.SetEnabled(_settings.Enabled && _settings.MouseEnabled && _settings.MouseVolume > 0);
    private void SaveSoon() { if (!_disposed) { _saveTimer.Stop(); _saveTimer.Start(); } }
    private bool SaveNow()
    {
        try { _store.Save(_settings); return true; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Notice = "设置暂时无法保存，请检查用户目录的访问权限。"; return false; }
    }
    private void Dispatch(Action action) { if (!_disposed && !_ui.HasShutdownStarted) _ui.BeginInvoke(action); }
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _ready = false; _saveTimer.Stop(); SaveNow();
        _input.Dispose(); _mouseInput.Dispose(); _audio.Dispose();
    }
}
