using System.IO;
using System.Windows;
using System.Windows.Interop;
using ProjectAppOrganizer.App.Windows;

namespace ProjectAppOrganizer.App.Themes;

public enum AppTheme { Dark, Warm }

/// <summary>
/// Swaps the colour palette (the first merged dictionary in App.xaml) while the app runs
/// and remembers the choice. Styles reference the palette with DynamicResource, so every
/// open window repaints as soon as the dictionary is replaced.
/// </summary>
public static class ThemeManager
{
    public static AppTheme Current { get; private set; } = AppTheme.Dark;

    public static event Action? Changed;

    private static string SettingsPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ProjectAppOrganizer", "theme.txt");

    /// <summary>Loads the remembered theme and applies it. Call before any window is created.</summary>
    public static void Initialize()
    {
        var saved = AppTheme.Dark;
        try
        {
            if (File.Exists(SettingsPath) &&
                Enum.TryParse<AppTheme>(File.ReadAllText(SettingsPath).Trim(), true, out var parsed))
                saved = parsed;
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }

        Apply(saved, persist: false);
    }

    public static void Toggle() =>
        Apply(Current == AppTheme.Dark ? AppTheme.Warm : AppTheme.Dark, persist: true);

    public static void Apply(AppTheme theme, bool persist)
    {
        var palette = new ResourceDictionary
        {
            Source = new Uri(
                $"pack://application:,,,/ProjectAppOrganizer.App;component/Themes/Palette.{theme}.xaml")
        };

        // App.xaml lists the palette first, the control styles second.
        var merged = Application.Current.Resources.MergedDictionaries;
        if (merged.Count > 0) merged[0] = palette;
        else merged.Add(palette);

        Current = theme;

        foreach (Window window in Application.Current.Windows)
            ApplyTitleBar(window);

        if (persist)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
                File.WriteAllText(SettingsPath, theme.ToString());
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        Changed?.Invoke();
    }

    /// <summary>Matches the native title bar to the current theme. No-op before the window has a handle.</summary>
    public static void ApplyTitleBar(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle != 0)
            Win32Interop.UseDarkTitleBar(handle, Current == AppTheme.Dark);
    }
}
