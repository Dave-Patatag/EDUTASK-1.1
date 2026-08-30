using EDUTASK_1._1.Helpers;
using System.Threading.Tasks;

namespace EDUTASK_1._1.Views.Base
{
    /// <summary>How a page announces itself when it appears.</summary>
    public enum PageTransition
    {
        /// <summary>Content fades in while rising.</summary>
        Slide,

        /// <summary>Content fades in on the spot. The default for full pages.</summary>
        Fade,

        /// <summary>Scrim fades while the card scales up. For modal pages that sit over a scrim.</summary>
        Dialog,

        /// <summary>No entrance animation.</summary>
        None
    }

    /// <summary>
    /// Base page for every EduTask screen.
    ///
    /// Gives all pages one entrance behaviour so navigation feels like a single
    /// system rather than 28 separately-built screens, and provides the shared
    /// exit animation so leaving a page mirrors arriving at it.
    ///
    /// Pages opt out or change style via <see cref="Transition"/>. The OS
    /// "reduce motion" setting is honoured throughout — see <see cref="Motion.ReduceMotion"/>.
    /// </summary>
    public class EduTaskPage : ContentPage
    {
        bool _hasAnimatedIn;

        public EduTaskPage()
        {
            // Every page in this app draws its own header and navigation chrome.
            NavigationPage.SetHasNavigationBar(this, false);
            Shell.SetNavBarIsVisible(this, false);
        }

        /// <summary>The entrance style for this page. Defaults to <see cref="PageTransition.Fade"/>.</summary>
        public PageTransition Transition { get; set; } = PageTransition.Fade;

        /// <summary>
        /// The element the dialog transition scales. Defaults to <see cref="ContentPage.Content"/>.
        /// Set this when the card is nested rather than being the direct content.
        /// </summary>
        public VisualElement? DialogCard { get; set; }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            await PlayEntranceAsync();
        }

        protected override void OnDisappearing()
        {
            base.OnDisappearing();

            // Re-arm so returning to this page animates again rather than
            // snapping in.
            _hasAnimatedIn = false;
        }

        /// <summary>
        /// Runs the page's entrance animation. Safe to call more than once; only
        /// the first call per appearance does anything.
        /// </summary>
        protected async Task PlayEntranceAsync()
        {
            if (_hasAnimatedIn || Transition == PageTransition.None || Content is null)
                return;

            _hasAnimatedIn = true;

            if (Motion.ReduceMotion)
            {
                ResetToRestingState();
                return;
            }

            switch (Transition)
            {
                case PageTransition.Slide:
                    await Content.FadeInUpAsync(Motion.EnterOffset, Motion.Base);
                    break;

                case PageTransition.Fade:
                    Content.Opacity = 0;
                    await Content.FadeTo(1, Motion.Base, Motion.Enter);
                    break;

                case PageTransition.Dialog:
                    await PlayDialogEntranceAsync();
                    break;
            }
        }

        async Task PlayDialogEntranceAsync()
        {
            var card = DialogCard ?? Content;
            if (card is null)
                return;

            // The card grows into place rather than sliding, so the eye reads it
            // as arriving on top of the page behind it rather than replacing it.
            card.Opacity = 0;
            card.Scale = 0.94;

            await Task.WhenAll(
                card.FadeTo(1, Motion.Slow, Motion.Enter),
                card.ScaleTo(1.0, Motion.Slow, Motion.Emphasis));
        }

        /// <summary>
        /// Plays the page's exit animation. Call this immediately before a
        /// navigation pop so the screen leaves the way it arrived.
        /// </summary>
        public async Task PlayExitAsync()
        {
            if (Transition == PageTransition.None || Content is null || Motion.ReduceMotion)
                return;

            if (Transition == PageTransition.Dialog)
            {
                var card = DialogCard ?? Content;
                await Task.WhenAll(
                    card.FadeTo(0, Motion.Fast, Motion.Exit),
                    card.ScaleTo(0.96, Motion.Fast, Motion.Exit));
                return;
            }

            await Content.FadeTo(0, Motion.Fast, Motion.Exit);
        }

        void ResetToRestingState()
        {
            if (Content is null)
                return;

            Content.Opacity = 1;
            Content.TranslationY = 0;
            Content.Scale = 1;

            if (DialogCard is not null)
            {
                DialogCard.Opacity = 1;
                DialogCard.Scale = 1;
            }
        }
    }
}
