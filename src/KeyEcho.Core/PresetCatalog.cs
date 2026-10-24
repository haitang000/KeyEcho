namespace KeyEcho.Core;

public sealed record KeyboardPreset(string Id, string Name, string Description);

public static class PresetCatalog
{
    public const string DefaultKeyboard = "cherrymx-red-pbt";
    public static IReadOnlyList<KeyboardPreset> Keyboards { get; } =
    [
        new("cherrymx-red-pbt", "Cherry MX 红轴 · PBT", "轻柔顺滑 · 低调的敲击声"),
        new("cherrymx-red-abs", "Cherry MX 红轴 · ABS", "轻快清亮 · 线性手感"),
        new("cherrymx-brown-pbt", "Cherry MX 茶轴 · PBT", "柔和段落 · 厚实自然"),
        new("cherrymx-brown-abs", "Cherry MX 茶轴 · ABS", "轻微段落 · 清晰反馈"),
        new("cherrymx-blue-pbt", "Cherry MX 青轴 · PBT", "经典咔嗒 · 清脆响亮"),
        new("cherrymx-blue-abs", "Cherry MX 青轴 · ABS", "高亮咔嗒 · 更鲜明的机械感"),
        new("cherrymx-black-pbt", "Cherry MX 黑轴 · PBT", "沉稳敲击 · 扎实有力"),
        new("cherrymx-black-abs", "Cherry MX 黑轴 · ABS", "利落线性 · 直接干脆"),
        new("topre-purple-hybrid-pbt", "Topre 静电容 · PBT", "低沉圆润 · 柔和的咚咚声")
    ];
    public static bool IsKeyboard(string id) => Keyboards.Any(preset => preset.Id == id);
    public static IReadOnlyList<MousePreset> Mice { get; } =
    [
        new("crisp", "清脆点击", "真实鼠标录音 · 清晰的咔嗒声"),
        new("soft", "柔和点击", "削弱高频 · 更轻柔的点击声"),
        new("follow", "跟随键盘音效", "使用当前选中的键盘、枪械或热门梗音效")
    ];
    public static KeyboardPreset? Find(string id) => Keyboards.FirstOrDefault(preset => preset.Id == id);
}

public sealed class SoundBank(IReadOnlyDictionary<int, float[]> keys, float[][] fallback)
{
    public long CachedBytes { get; } = keys.Values.Concat(fallback).Distinct<float[]>(ReferenceEqualityComparer.Instance).Sum(samples => (long)samples.Length * sizeof(float));
    public float[] ForKey(ushort scanCode, ushort flags)
    {
        int code = scanCode | ((flags & 2) != 0 ? 0xE000 : 0);
        return keys.TryGetValue(code, out var sample) ? sample : Preview();
    }
    public float[] Preview() => fallback[Random.Shared.Next(fallback.Length)];
    public static SoundBank Uniform(params float[][] samples) => new(new Dictionary<int, float[]>(), samples);
}
