namespace EDUTASK_1._1.Helpers
{
    /// <summary>
    /// Typed access to the styles declared in Resources/Styles/Styles.xaml, for
    /// the screens that are built in code rather than XAML.
    ///
    /// Same reasoning as <see cref="AppColors"/>: a control created in C# used to
    /// carry its own copy of the metrics — the security-answer dialog drew a 50pt
    /// field while every XAML auth screen drew 52 — so the two drifted apart with
    /// nothing to catch it. Code-built controls now resolve the same style the
    /// markup uses.
    /// </summary>
    public static class AppStyles
    {
        /// <summary>The style registered under <paramref name="key"/>, or null.</summary>
        public static Style? Get(string key) =>
            Application.Current?.Resources?.TryGetValue(key, out var value) == true && value is Style style
                ? style
                : null;

        /// <summary>
        /// Applies the style registered under <paramref name="key"/>. A missing key
        /// leaves the element alone rather than clearing the style it already has.
        /// </summary>
        public static void Apply(VisualElement element, string key)
        {
            if (Get(key) is { } style)
                element.Style = style;
        }
    }
}
