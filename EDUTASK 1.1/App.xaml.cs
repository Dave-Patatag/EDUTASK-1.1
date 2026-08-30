using EDUTASK_1._1.Helpers;

namespace EDUTASK_1._1
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            AppThemeService.Initialize();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            // No title text: the caption bar carries the window buttons and
            // nothing else. The name of the app is on the pages.
            var window = new Window(new NavigationPage(new Views.LoginPage()))
            {
                Title = string.Empty
            };

#if WINDOWS
            // Use MAUI's window title-bar control so the black drag region and
            // the Windows caption buttons occupy one shared 32px row. Forcing
            // the system title bar instead creates a second row above the
            // FlyoutPage content.
            window.TitleBar = new TitleBar
            {
                Title = string.Empty,
                BackgroundColor = Colors.Black,
                ForegroundColor = Colors.White,
                HeightRequest = 32
            };

            // The MAUI title bar supplies the drag region. Native settings are
            // still used for the minimise, maximise, and close button colours.
            window.Created += (_, _) => ApplyCaptionBar(window);
            window.Activated += (_, _) => ApplyCaptionBar(window);
#endif

            return window;
        }

#if WINDOWS
        /// <summary>
        /// Paints the caption bar — the strip along the top of the window with
        /// the minimise, maximise and close buttons.
        ///
        /// MAUI otherwise draws its own strip up there, which is what put a
        /// white bar and an app title above the pages. Handing the caption bar
        /// back to Windows leaves the three system buttons and nothing else,
        /// and Windows will paint them in the colours set here.
        /// </summary>
        private static void ApplyCaptionBar(Window window)
        {
            if (window.Handler?.PlatformView is not Microsoft.UI.Xaml.Window platformWindow)
                return;

            // Windows 10 builds before 1809 have no customisable caption bar.
            // They keep the system default rather than losing the buttons.
            if (!Microsoft.UI.Windowing.AppWindowTitleBar.IsCustomizationSupported())
                return;

            nint handle = WinRT.Interop.WindowNative.GetWindowHandle(platformWindow);
            Microsoft.UI.WindowId id = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(handle);
            Microsoft.UI.Windowing.AppWindowTitleBar captionBar =
                Microsoft.UI.Windowing.AppWindow.GetFromWindowId(id).TitleBar;

            // Keep desktop window chrome neutral and distinct from the app's
            // own navigation colours.
            var fill = ToPlatformColor(Colors.Black);
            var glyph = ToPlatformColor(AppColors.TextInverse);

            // Keep the left side completely clear. The only title-bar controls
            // are Windows' minimise, maximise and close buttons on the right.
            captionBar.IconShowOptions =
                Microsoft.UI.Windowing.IconShowOptions.HideIconAndSystemMenu;

            captionBar.BackgroundColor = fill;
            captionBar.InactiveBackgroundColor = fill;
            captionBar.ButtonBackgroundColor = fill;
            captionBar.ButtonInactiveBackgroundColor = fill;

            captionBar.ForegroundColor = glyph;
            captionBar.InactiveForegroundColor = glyph;
            captionBar.ButtonForegroundColor = glyph;
            captionBar.ButtonInactiveForegroundColor = glyph;

            // Hover and press states are left visible: without them the buttons
            // give no feedback at all against a flat fill.
            captionBar.ButtonHoverBackgroundColor = ToPlatformColor(AppColors.Slate700);
            captionBar.ButtonHoverForegroundColor = glyph;
            captionBar.ButtonPressedBackgroundColor = ToPlatformColor(AppColors.Slate600);
            captionBar.ButtonPressedForegroundColor = glyph;
        }

        private static global::Windows.UI.Color ToPlatformColor(Color color) =>
            global::Windows.UI.Color.FromArgb(
                (byte)(color.Alpha * 255),
                (byte)(color.Red * 255),
                (byte)(color.Green * 255),
                (byte)(color.Blue * 255));
#endif
    }
}
