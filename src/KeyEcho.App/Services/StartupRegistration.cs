using Microsoft.Win32;

namespace KeyEcho.App.Services;

public static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue("KeyEcho") is string;
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
        {
            string executable = Environment.ProcessPath ?? throw new InvalidOperationException("找不到应用程序路径。");
            key.SetValue("KeyEcho", $"\"{executable}\" --background");
        }
        else key.DeleteValue("KeyEcho", false);
    }
}
