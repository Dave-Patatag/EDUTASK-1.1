namespace EDUTASK_1._1.Views
{
    public partial class FlyoutMenuPage : ContentPage
    {
        public FlyoutMenuPage(bool showTaskSummary = true)
        {
            InitializeComponent();
            TaskSummaryMenuItem.IsVisible = showTaskSummary;
        }

        private void OnBackTapped(object sender, EventArgs e)
        {
            if (DashboardFlyoutPage.Current is { } flyout)
                flyout.IsPresented = false;
        }



        private void OnTasksTapped(object sender, EventArgs e)
        {
            DashboardFlyoutPage.Current?.ShowTasks();
        }

        private void OnLandingPageTapped(object sender, EventArgs e)
        {
            var window = Window;
            if (window is null)
                return;

            if (DashboardFlyoutPage.Current is { } flyout)
                flyout.IsPresented = false;

            window.Dispatcher.Dispatch(() =>
                window.Page = new NavigationPage(new LandingPage()));
        }

        private void OnTaskSummaryTapped(object sender, EventArgs e)
        {
            if (DashboardFlyoutPage.Current is { } flyout)
                flyout.ShowDetail(new TaskSummaryPage());
        }
    }
}
