using System.Collections.Concurrent;

namespace EDUTASK_1._1.Helpers;

/// <summary>
/// Typed access to the typography tokens declared in Resources/Styles/Styles.xaml.
/// Keeps controls created in C# on the same scale as XAML pages.
/// </summary>
public static class AppTypography
{
    private static readonly ConcurrentDictionary<string, double> Cache = new();

    public static double Display => Get("FontDisplay", 28);
    public static double Title => Get("FontTitle", 20);
    public static double Heading => Get("FontHeading", 16);
    public static double Body => Get("FontBody", 14);
    public static double BodySmall => Get("FontBodySmall", 13);
    public static double Caption => Get("FontCaption", 12);
    public static double Label => Get("FontLabel", 11);
    public static double Micro => Get("FontMicro", 10);

    private static double Get(string key, double fallback) => Cache.GetOrAdd(key, _ =>
    {
        if (Application.Current?.Resources?.TryGetValue(key, out var value) == true && value is double size)
            return size;

        System.Diagnostics.Debug.WriteLine($"[AppTypography] Missing font token '{key}'.");
        return fallback;
    });
}
