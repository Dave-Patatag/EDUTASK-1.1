using EDUTASK_1._1.Helpers;
using EDUTASK_1._1.Models;
using EDUTASK_1._1.Services;

namespace EDUTASK_1._1.Views;

public sealed class DashboardFlyoutPage : FlyoutPage
{
    private readonly bool _isTeacher;
    private readonly Dictionary<string, NavigationPage> _tabPages = [];
    private string? _activeTab;
    public DashboardFlyoutPage() : this(new HomePage())
    {
    }

    public DashboardFlyoutPage(Page initialPage)
    {
        _isTeacher = initialPage is TeacherDashboardPage || TeacherSessionService.CurrentTeacher is not null;
        FlyoutLayoutBehavior = FlyoutLayoutBehavior.Popover;
        Flyout = new FlyoutMenuPage(!_isTeacher) { Title = "EduTask menu" };
        ShowTab(initialPage is HomePage ? "home" : "tasks", initialPage);
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

#if WINDOWS
        // Every page already provides its own menu control. Hide the WinUI
        // NavigationView toggle that would otherwise occupy a gray block in
        // the custom black window title bar.
        if (Handler?.PlatformView is Microsoft.UI.Xaml.Controls.NavigationView navigationView)
        {
            navigationView.IsPaneToggleButtonVisible = false;
            navigationView.IsTitleBarAutoPaddingEnabled = false;
        }
#endif
    }

    public void ShowHome() =>
        ShowTab("home", () => new HomePage());

    public void ShowTasks(int? expandTaskID = null)
    {
        ShowTab("tasks", () =>
            _isTeacher ? new TeacherDashboardPage() : new DirectorStaffDashboardPage());

        if (!_tabPages.TryGetValue("tasks", out NavigationPage? navigationPage))
            return;

        if (expandTaskID is int taskID)
        {
            if (navigationPage.RootPage is TeacherDashboardPage teacherDashboard)
                _ = teacherDashboard.FocusTaskAsync(taskID);
            else if (navigationPage.RootPage is DirectorStaffDashboardPage directorDashboard)
                _ = directorDashboard.FocusTaskAsync(taskID);
        }
    }

    public async Task ShowCreateTaskAsync()
    {
        IsPresented = false;
        await Detail.Navigation.PushModalAsync(new CreateTaskPage(), false);
    }

    public void InvalidateTaskData()
    {
        if (_tabPages.TryGetValue("tasks", out NavigationPage? navigationPage) &&
            navigationPage.RootPage is DirectorStaffDashboardPage dashboard)
        {
            dashboard.RequestDataRefresh();
        }
    }

    public void ShowNotification(User? user) =>
        ShowTab($"notification:user:{user?.User_id ?? 0}", () => new NotificationPage(user));

    public void ShowNotification(Teachers? teacher) =>
        ShowTab($"notification:teacher:{teacher?.Teacher_id ?? 0}", () => new NotificationPage(teacher));

    public void ShowProfile(User user) =>
        ShowTab($"profile:user:{user.User_id}", () => new UserProfilePage(user));

    public void ShowProfile(Teachers teacher) =>
        ShowTab($"profile:teacher:{teacher.Teacher_id}", () => new UserProfilePage(teacher));

    public async void ShowCompletionHistory()
    {
        if (_tabPages.TryGetValue("tasks", out NavigationPage? navigationPage) &&
            navigationPage.RootPage is TeacherDashboardPage dashboard)
        {
            ShowDetail(await dashboard.CreateCompletionHistoryPageAsync());
            return;
        }

        if (_tabPages.TryGetValue("tasks", out navigationPage) &&
            navigationPage.RootPage is DirectorStaffDashboardPage directorDashboard)
        {
            ShowDetail(await directorDashboard.CreateCompletionHistoryPageAsync());
            return;
        }

        if (_isTeacher)
        {
            var teacherDashboard = new TeacherDashboardPage();
            ShowDetail(await teacherDashboard.CreateCompletionHistoryPageAsync());
        }
        else
        {
            var userDashboard = new DirectorStaffDashboardPage();
            ShowDetail(await userDashboard.CreateCompletionHistoryPageAsync());
        }
    }

    // Kept as a compatibility wrapper for existing callers.
    public void ShowTeacherCompletionHistory() => ShowCompletionHistory();

    public void ShowDetail(Page page)
    {
        _activeTab = null;
        Detail = CreateNavigationPage(page);
        IsPresented = false;
    }

    private void ShowTab(string key, Func<Page> pageFactory)
    {
        if (_activeTab == key)
        {
            IsPresented = false;
            RefreshNotificationBadges();
            return;
        }

        if (!_tabPages.TryGetValue(key, out NavigationPage? navigationPage))
        {
            navigationPage = CreateNavigationPage(pageFactory());
            _tabPages[key] = navigationPage;
        }

        Detail = navigationPage;
        _activeTab = key;
        IsPresented = false;
        RefreshNotificationBadges();
    }

    private void ShowTab(string key, Page page)
    {
        _tabPages[key] = CreateNavigationPage(page);
        Detail = _tabPages[key];
        _activeTab = key;
        IsPresented = false;
        RefreshNotificationBadges();
    }

    private void RefreshNotificationBadges()
        => Services.NotificationBadgeState.RequestRefresh();

    private NavigationPage CreateNavigationPage(Page page)
    {
        ApplyDimOverlay(page);
        return new NavigationPage(page)
        {
            BarBackgroundColor = AppColors.SurfaceBase,
            BarTextColor = AppColors.TextPrimary
        };
    }

    private void ApplyDimOverlay(Page page)
    {
        if (page is not ContentPage contentPage || contentPage.Content is null)
            return;

        View originalContent = contentPage.Content;
        var scrim = new BoxView { Color = AppColors.Brand900, Opacity = 0.45, IsVisible = false };
        scrim.SetBinding(BoxView.IsVisibleProperty, new Binding(nameof(IsPresented), source: this));
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => IsPresented = false;
        scrim.GestureRecognizers.Add(tap);

        contentPage.Content = new Grid { Children = { originalContent, scrim } };
    }

    public static DashboardFlyoutPage? Current =>
        Application.Current?.Windows.FirstOrDefault()?.Page as DashboardFlyoutPage;
}
