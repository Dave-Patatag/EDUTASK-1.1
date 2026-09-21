using System.Collections.Concurrent;

namespace EDUTASK_1._1.Helpers
{
    /// <summary>
    /// Typed access to the design tokens declared in Resources/Styles/Colors.xaml.
    ///
    /// Code-behind used to carry its own hex literals, which meant a colour could
    /// drift between XAML and C# without anyone noticing — Medium priority was
    /// amber in the styles and yellow in three dashboards. Everything now resolves
    /// from the same ResourceDictionary, so the palette has exactly one home.
    ///
    /// Lookups are cached; the dictionary is only read once per key.
    /// </summary>
    public static class AppColors
    {
        static readonly ConcurrentDictionary<string, Color> _cache = new();

        // Brand
        public static Color Brand900 => Get(nameof(Brand900));
        public static Color Brand800 => Get(nameof(Brand800));
        public static Color Brand700 => Get(nameof(Brand700));
        public static Color Brand600 => Get(nameof(Brand600));
        public static Color Brand500 => Get(nameof(Brand500));
        public static Color Brand200 => Get(nameof(Brand200));
        public static Color Brand100 => Get(nameof(Brand100));
        public static Color Brand50 => Get(nameof(Brand50));
        public static Color CalendarAccent => Get(nameof(CalendarAccent));

        // Brand green — the logo checkmark. BrandGreen is the raw logo value and
        // is legible on navy only; use Green600/700 on light surfaces.
        public static Color BrandGreen => Get(nameof(BrandGreen));
        public static Color Green400 => Get(nameof(Green400));
        public static Color Green500 => Get(nameof(Green500));
        public static Color Green600 => Get(nameof(Green600));
        public static Color Green700 => Get(nameof(Green700));
        public static Color Green100 => Get(nameof(Green100));
        public static Color Green200 => Get(nameof(Green200));

        // Slate
        public static Color Slate700 => Get(nameof(Slate700));
        public static Color Slate600 => Get(nameof(Slate600));
        public static Color Slate500 => Get(nameof(Slate500));
        public static Color Slate400 => Get(nameof(Slate400));
        public static Color Slate300 => Get(nameof(Slate300));
        public static Color Slate200 => Get(nameof(Slate200));
        public static Color Slate100 => Get(nameof(Slate100));

        // Accent
        public static Color Accent600 => Get(nameof(Accent600));
        public static Color Accent500 => Get(nameof(Accent500));
        public static Color Accent200 => Get(nameof(Accent200));
        public static Color Accent100 => Get(nameof(Accent100));
        public static Color SelectionSurface => Get(nameof(SelectionSurface));

        // Actions
        public static Color ActionPrimary => Get(nameof(ActionPrimary));
        public static Color ActionDanger => Get(nameof(ActionDanger));
        public static Color ActionDismiss => Get(nameof(ActionDismiss));

        // Text
        public static Color TextPrimary => Get(nameof(TextPrimary));
        public static Color TextSecondary => Get(nameof(TextSecondary));
        public static Color TextTertiary => Get(nameof(TextTertiary));
        public static Color TextDisabled => Get(nameof(TextDisabled));
        public static Color TextInverse => Get(nameof(TextInverse));
        public static Color TextInverseMuted => Get(nameof(TextInverseMuted));

        // Surface
        public static Color SurfaceBase => Get(nameof(SurfaceBase));
        public static Color SurfaceMuted => Get(nameof(SurfaceMuted));
        public static Color SurfaceSubtle => Get(nameof(SurfaceSubtle));
        public static Color SurfaceSunken => Get(nameof(SurfaceSunken));
        public static Color SurfaceHover => Get(nameof(SurfaceHover));
        public static Color SurfacePressed => Get(nameof(SurfacePressed));
        public static Color SurfaceBrand => Get(nameof(SurfaceBrand));
        public static Color Scrim => Get(nameof(Scrim));
        public static Color ScrimLight => Get(nameof(ScrimLight));

        // Elevation. Three levels: cards sit low, floating actions sit raised,
        // modals sit highest.
        public static Color ShadowAmbient => Get(nameof(ShadowAmbient));
        public static Color ShadowRaised => Get(nameof(ShadowRaised));
        public static Color ShadowOverlay => Get(nameof(ShadowOverlay));
        public static Color ViewerBackdrop => Get(nameof(ViewerBackdrop));

        // Border
        public static Color BorderStrong => Get(nameof(BorderStrong));
        public static Color BorderDefault => Get(nameof(BorderDefault));
        public static Color BorderSubtle => Get(nameof(BorderSubtle));

        // Status
        public static Color StatusSuccess => Get(nameof(StatusSuccess));
        public static Color StatusSuccessSurface => Get(nameof(StatusSuccessSurface));
        public static Color StatusSuccessBorder => Get(nameof(StatusSuccessBorder));
        public static Color StatusDanger => Get(nameof(StatusDanger));
        public static Color StatusDangerSurface => Get(nameof(StatusDangerSurface));
        public static Color StatusDangerBorder => Get(nameof(StatusDangerBorder));
        public static Color StatusWarning => Get(nameof(StatusWarning));
        public static Color StatusWarningSurface => Get(nameof(StatusWarningSurface));
        public static Color StatusWarningBorder => Get(nameof(StatusWarningBorder));
        public static Color StatusPending => Get(nameof(StatusPending));
        public static Color StatusPendingSurface => Get(nameof(StatusPendingSurface));
        public static Color StatusPendingBorder => Get(nameof(StatusPendingBorder));
        public static Color StatusOngoing => Get(nameof(StatusOngoing));
        public static Color StatusOngoingSurface => Get(nameof(StatusOngoingSurface));
        public static Color StatusOngoingBorder => Get(nameof(StatusOngoingBorder));
        public static Color StatusInfo => Get(nameof(StatusInfo));
        public static Color StatusInfoSurface => Get(nameof(StatusInfoSurface));
        public static Color StatusInfoBorder => Get(nameof(StatusInfoBorder));
        public static Color StatusNeutral => Get(nameof(StatusNeutral));
        public static Color StatusNeutralSurface => Get(nameof(StatusNeutralSurface));
        public static Color StatusNeutralBorder => Get(nameof(StatusNeutralBorder));
        public static Color StatusValidation => Get(nameof(StatusValidation));
        public static Color StatusValidationSurface => Get(nameof(StatusValidationSurface));
        public static Color StatusValidationBorder => Get(nameof(StatusValidationBorder));

        // Priority
        public static Color PriorityHigh => Get(nameof(PriorityHigh));
        public static Color PriorityMedium => Get(nameof(PriorityMedium));
        public static Color PriorityLow => Get(nameof(PriorityLow));

        // Chart fills. Not the Status tokens — those are foregrounds, and as
        // wedges they collapse into each other. See the CHART block in
        // Colors.xaml for the measurements.
        public static Color ChartCompleted => Get(nameof(ChartCompleted));
        public static Color ChartOngoing => Get(nameof(ChartOngoing));
        public static Color ChartOverdue => Get(nameof(ChartOverdue));
        public static Color ChartEmpty => Get(nameof(ChartEmpty));
        public static Color ChartGap => Get(nameof(ChartGap));

        // Focus
        public static Color FocusRing => Get(nameof(FocusRing));
        public static Color FocusRingSurface => Get(nameof(FocusRingSurface));

        /// <summary>
        /// Resolves a token by key. Falls back to magenta rather than throwing —
        /// a missing token should be loudly visible in the UI during development,
        /// not a crash in front of a user.
        /// </summary>
        public static Color Get(string key) => _cache.GetOrAdd(key, static k =>
        {
            if (Application.Current?.Resources?.TryGetValue(k, out var value) == true && value is Color color)
                return color;

            System.Diagnostics.Debug.WriteLine($"[AppColors] Missing colour token '{k}'.");
            return Colors.Magenta;
        });
    }
}
