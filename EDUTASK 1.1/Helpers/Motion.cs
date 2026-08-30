using System.Threading.Tasks;

namespace EDUTASK_1._1.Helpers
{
    /// <summary>
    /// Motion tokens for the EduTask UI.
    ///
    /// Durations are deliberately short. Animation here exists to explain a change
    /// (where a thing came from, that a tap registered, that a field is wrong) —
    /// never to decorate. Anything slower than ~320ms starts to feel like lag
    /// rather than feedback, so nothing routine exceeds it.
    /// </summary>
    public static class Motion
    {
        /// <summary>Press/release feedback. Must feel immediate.</summary>
        public const uint Instant = 90;

        /// <summary>Hover, focus rings, colour shifts.</summary>
        public const uint Fast = 140;

        /// <summary>The default. Element entrances, fades, small translations.</summary>
        public const uint Base = 220;

        /// <summary>Section expand/collapse, sheet and dialog entrances.</summary>
        public const uint Slow = 320;

        /// <summary>Distance an element travels on entrance, in device-independent units.</summary>
        public const double EnterOffset = 18;

        // Easings — decelerate on entry, accelerate on exit, symmetric for
        // reversible state changes.
        public static readonly Easing Enter = Easing.CubicOut;
        public static readonly Easing Exit = Easing.CubicIn;
        public static readonly Easing Standard = Easing.CubicInOut;
        public static readonly Easing Emphasis = Easing.SpringOut;

        static bool? _reduceMotion;

        /// <summary>
        /// Honours the OS "reduce motion" accessibility setting. When true every
        /// helper in <see cref="MotionExtensions"/> snaps to the end state instead
        /// of animating, so the UI stays correct for users who get motion sickness
        /// or simply turned animations off.
        /// </summary>
        public static bool ReduceMotion
        {
            get => _reduceMotion ??= DetectReduceMotion();
            set => _reduceMotion = value;
        }

        static bool DetectReduceMotion()
        {
            try
            {
#if WINDOWS
                return !new Windows.UI.ViewManagement.UISettings().AnimationsEnabled;
#elif ANDROID
                var context = Android.App.Application.Context;
                if (context?.ContentResolver is { } resolver)
                {
                    // 0 means the user disabled animations in Developer options
                    // or Accessibility > Remove animations.
                    var scale = Android.Provider.Settings.Global.GetFloat(
                        resolver,
                        Android.Provider.Settings.Global.AnimatorDurationScale,
                        1f);
                    return scale == 0f;
                }
                return false;
#elif IOS || MACCATALYST
                return UIKit.UIAccessibility.IsReduceMotionEnabled;
#else
                return false;
#endif
            }
            catch
            {
                // An accessibility probe must never be the reason the app fails to
                // draw. Fall back to "animations on".
                return false;
            }
        }
    }

    /// <summary>
    /// Composable animation helpers. Every method is safe to await, safe to fire
    /// and forget, and safe to call on an element that is mid-animation.
    /// </summary>
    public static class MotionExtensions
    {
        /// <summary>
        /// Fades an element in while it rises into place. The standard entrance
        /// for cards, headers and form sections.
        /// </summary>
        public static async Task FadeInUpAsync(
            this VisualElement element,
            double offset = Motion.EnterOffset,
            uint duration = Motion.Base,
            uint delay = 0)
        {
            if (element is null)
                return;

            if (Motion.ReduceMotion)
            {
                element.Opacity = 1;
                element.TranslationY = 0;
                return;
            }

            element.Opacity = 0;
            element.TranslationY = offset;

            if (delay > 0)
                await Task.Delay((int)delay);

            await Task.WhenAll(
                element.FadeTo(1, duration, Motion.Enter),
                element.TranslateTo(0, 0, duration, Motion.Enter));
        }

        /// <summary>
        /// Opens a dropdown panel: it fades in while unfolding from its top edge.
        ///
        /// Deliberately animates Opacity and ScaleY only, never TranslationY —
        /// dropdown panels are positioned by TranslationY relative to the field
        /// that opens them, and an animation that drove the same property would
        /// throw the panel back to the top of its container.
        /// </summary>
        public static async Task DropdownOpenAsync(this VisualElement panel, uint duration = Motion.Base)
        {
            if (panel is null)
                return;

            panel.IsVisible = true;

            if (Motion.ReduceMotion)
            {
                panel.Opacity = 1;
                panel.ScaleY = 1;
                return;
            }

            // Unfold from the top edge, the way a menu drops out of its trigger.
            panel.AnchorY = 0;
            panel.Opacity = 0;
            panel.ScaleY = 0.9;

            await Task.WhenAll(
                panel.FadeTo(1, duration, Motion.Enter),
                panel.ScaleYTo(1, duration, Motion.Enter));
        }

        /// <summary>Closes a dropdown panel and hides it. Mirror of <see cref="DropdownOpenAsync"/>.</summary>
        public static async Task DropdownCloseAsync(this VisualElement panel, uint duration = Motion.Fast)
        {
            if (panel is null)
                return;

            if (Motion.ReduceMotion)
            {
                panel.IsVisible = false;
                return;
            }

            panel.AnchorY = 0;

            await Task.WhenAll(
                panel.FadeTo(0, duration, Motion.Exit),
                panel.ScaleYTo(0.9, duration, Motion.Exit));

            panel.IsVisible = false;
        }

        /// <summary>Turns a disclosure chevron to match its section's state.</summary>
        public static Task RotateChevronAsync(this VisualElement element, bool expanded)
        {
            if (element is null)
                return Task.CompletedTask;

            var target = expanded ? 0 : -90;

            if (Motion.ReduceMotion)
            {
                element.Rotation = target;
                return Task.CompletedTask;
            }

            return element.RotateTo(target, Motion.Base, Motion.Standard);
        }
    }
}
