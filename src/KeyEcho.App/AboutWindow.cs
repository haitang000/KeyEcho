using System.Diagnostics;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace KeyEcho.App;

public sealed class AboutWindow : Window
{
    public AboutWindow()
    {
        Title = "关于 KeyEcho"; Width = 500; Height = 510; MinWidth = 420; MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner; Background = Brushes.White;
        FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI"); FontSize = 14;
        var panel = new StackPanel { Margin = new Thickness(26) };
        panel.Children.Add(new TextBlock { Text = "KeyEcho", FontSize = 28, FontWeight = FontWeights.SemiBold });
        AddText(panel, "版本 1.2.5 · Windows 键鼠音效", 0, 8);
        AddText(panel, "音效在本地播放，仅瞬时处理按键状态，不保存或上传输入内容。", 20, 12);
        AddText(panel, "音效来源与许可", 20, 12, true);
        AddLink(panel, "9 款键盘录音 — Mechvibes / Hai Nguyen（MIT）", "https://github.com/hainguyents13/mechvibes");
        AddLink(panel, "Chaingun, pistol, rifle, shotgun shots — Michel Baradari", "https://opengameart.org/content/chaingun-pistol-rifle-shotgun-shots");
        AddLink(panel, "Creative Commons Attribution 3.0", "https://creativecommons.org/licenses/by/3.0/");
        AddLink(panel, "鼠标点击录音 — 1j01（CC0）", "https://opengameart.org/content/middle-mouse-click");
        AddLink(panel, "FAHHHHHH — 用户指定视频 / HighQualitySFX", "https://www.youtube.com/watch?v=-vH2b55TSHI");
        AddText(panel, "FAHHHHHH：从用户提供的视频截取喊声，保留原速和双声道，调整峰值并添加短淡入淡出。原视频未附带独立许可证。", 8, 11);
        AddText(panel, "其余热门梗音效（冰冰冰、bruh、AUGHHHHH、钢管落地、Shocked、私人笑声、wow）：来自用户提供的 MP3，保留原速、音调和双声道，裁掉首尾静音并调整峰值。文件未附带独立许可证。", 8, 11);
        AddText(panel, "AWM：从用户提供的视频中提取单次枪响，移除后续拉栓段，提升响度并限幅，转换为 WAV。原视频未附带独立许可证。", 8, 11);
        AddText(panel, "修改：按键位提取单次录音、移除起始静音、保留自然余音；枪声移除换弹段。音效调整音量并加入短淡出，转换为 WAV。作者未为本应用背书。", 10, 11);
        AddText(panel, "NAudio · MIT 许可；.NET / WPF · MIT 许可。完整许可和版权说明随应用分发。", 20, 11);
        var close = new Button { Content = "关闭", HorizontalAlignment = HorizontalAlignment.Right, MinWidth = 80, Margin = new Thickness(0, 22, 0, 0), IsCancel = true };
        close.Click += (_, _) => Close(); panel.Children.Add(close);
        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }
    private static void AddText(Panel panel, string text, double top, double size, bool bold = false) => panel.Children.Add(new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, FontSize = size == 8 ? 13 : size, Margin = new Thickness(0, top, 0, 0), FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal });
    private static void AddLink(Panel panel, string title, string url)
    {
        var link = new Hyperlink(new Run(title)) { NavigateUri = new Uri(url) };
        link.RequestNavigate += (_, _) => { try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch (Win32Exception) { } };
        var text = new TextBlock { TextWrapping = TextWrapping.Wrap, FontSize = 12, Margin = new Thickness(0, 10, 0, 0) }; text.Inlines.Add(link); panel.Children.Add(text);
    }
}
