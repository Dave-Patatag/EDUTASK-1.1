using EDUTASK_1._1.Services;

namespace EDUTASK_1._1.Views
{
    public partial class LandingPage : ContentPage
    {
        private readonly DatabaseService _database = new();

        public LandingPage() => InitializeComponent();

        private async void OnDirectorClicked(object sender, EventArgs e) => await OpenUserDashboardAsync("Director");

        private async void OnStaffClicked(object sender, EventArgs e) => await OpenUserDashboardAsync("Staff");

        private async Task OpenUserDashboardAsync(string expectedRole)
        {
            try
            {
                var account = await _database.GetActiveUserByRoleAsync(expectedRole);
                if (account is null)
                {
                    await UiAlertService.ShowAsync(this, $"No active {expectedRole}",
                        $"Create and activate a {expectedRole} account before using this dashboard.", "OK");
                    return;
                }

                UserSessionService.SetCurrentUser(account);
                if (Window is not null)
                    Window.Page = new DashboardFlyoutPage();
            }
            catch (Exception ex)
            {
                await UiAlertService.ShowAsync(this, "Unable to sign in", ex.Message, "OK");
            }
        }

        private void OnTeacherClicked(object sender, EventArgs e)
        {
            UserSessionService.Clear();
            if (Window is not null)
                Window.Page = new DashboardFlyoutPage(new TeacherDashboardPage());
        }
    }
}