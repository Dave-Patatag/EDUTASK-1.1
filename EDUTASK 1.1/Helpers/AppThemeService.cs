namespace EDUTASK_1._1.Helpers;

/// <summary>
/// Pins the app to the light theme.
///
/// EduTask has no dark palette: Colors.xaml declares no AppThemeBinding, and
/// MauiProgram forces the native Windows controls light to match. Left to
/// follow the OS, a device in dark mode would draw MAUI's stock dark chrome
/// around this app's light surfaces. So the theme is set once, explicitly.
///
/// The user-facing dark mode toggle was removed; if it comes back, it belongs
/// here alongside a dark palette, not as a preference read in isolation.
/// </summary>
public static class AppThemeService
{
    /// <summary>Key written by the retired dark mode toggle. Nothing reads it now.</summary>
    private const string RetiredDarkModePreferenceKey = "dark_mode_enabled";

    public static void Initialize()
    {
        // Clears the stale preference left on devices that used the old toggle.
        Preferences.Default.Remove(RetiredDarkModePreferenceKey);

        if (Application.Current is { } app)
            app.UserAppTheme = AppTheme.Light;
    }
}
