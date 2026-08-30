using EDUTASK_1._1.Helpers;
using EDUTASK_1._1.Services;
using EDUTASK_1._1.Views.Base;

namespace EDUTASK_1._1.Views
{
    public partial class FlyoutMenuPage : EduTaskPage
    {
        public FlyoutMenuPage(bool isDirectorOrStaff = true)
        {
            InitializeComponent();

            bool isDirector = UserSessionService.IsDirector;
            TaskSummaryItemBorder.IsVisible = isDirectorOrStaff;
            TeachersStaffItemBorder.IsVisible = isDirectorOrStaff;
            CompletionHistoryItemBorder.IsVisible = true;
            TeachersStaffMenuLabel.Text = isDirector ? "Teachers & Staff" : "Teachers";
        }

        private static readonly Color PressedItemBackground = AppColors.SurfaceMuted;

        private void OnItemPointerPressed(object? sender, PointerEventArgs e) =>
            SetItemPressed(sender, true);

        private void OnItemPointerReleased(object? sender, PointerEventArgs e) =>
            SetItemPressed(sender, false);

        private static void SetItemPressed(object? sender, bool pressed)
        {
            if (sender is not PointerGestureRecognizer { Parent: Border border })
                return;

            border.BackgroundColor = pressed ? PressedItemBackground : AppColors.SurfaceBase;

            if (!Motion.ReduceMotion)
                _ = border.ScaleTo(pressed ? 0.98 : 1.0, Motion.Instant, pressed ? Motion.Exit : Motion.Enter);
        }

        private void OnTaskSummaryTapped(object sender, EventArgs e)
        {
            DashboardFlyoutPage.Current?.ShowDetail(new TaskSummaryPage());
        }

        private void OnCalendarTapped(object sender, EventArgs e)
        {
            DashboardFlyoutPage.Current?.ShowDetail(new CalendarPage());
        }

        /// <summary>
        /// Opens the directory the same way every other menu item opens its page.
        ///
        /// This one used to call PushAsync on the detail's navigation stack, which
        /// made it the only destination in the app that animated — the platform
        /// slid it in from the right while Task Summary, Calendar and the bottom
        /// tabs all swapped instantly.
        ///
        /// The push was also stacking this page on top of whichever tab happened
        /// to be showing, which is always the Tasks tab. Coming back to Tasks from
        /// another tab then landed on the directory rather than the dashboard,
        /// because the tab's cached navigation stack still had it on top.
        /// </summary>
        private void OnTeachersStaffTapped(object sender, EventArgs e)
        {
            DashboardFlyoutPage.Current?.ShowDetail(new TeachersPage());
        }

        private void OnCompletionHistoryTapped(object sender, EventArgs e)
        {
            DashboardFlyoutPage.Current?.ShowCompletionHistory();
        }

        private void OnLogoutTapped(object sender, EventArgs e)
        {
            var window = Window;
            if (window is null)
                return;

            if (DashboardFlyoutPage.Current is { } flyout)
                flyout.IsPresented = false;

            UserSessionService.Clear();
            TeacherSessionService.Clear();
            window.Dispatcher.Dispatch(() =>
                window.Page = new NavigationPage(new LoginPage()));
        }
    }
}
