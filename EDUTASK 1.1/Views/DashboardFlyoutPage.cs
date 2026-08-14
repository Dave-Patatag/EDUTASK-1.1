namespace EDUTASK_1._1.Views;

public sealed class DashboardFlyoutPage : FlyoutPage
{
    private readonly bool _isTeacherDashboard;
    public DashboardFlyoutPage() : this(new DirectorStaffDashboardPage())
    {
    }

    public DashboardFlyoutPage(Page initialPage)
    {
        _isTeacherDashboard = initialPage is TeacherDashboardPage;
        FlyoutLayoutBehavior = FlyoutLayoutBehavior.Popover;
        Flyout = new FlyoutMenuPage(initialPage is DirectorStaffDashboardPage) { Title = "EduTask menu" };
        ShowDetail(initialPage);
    }

    public void ShowTasks() => ShowDetail(_isTeacherDashboard ? new TeacherDashboardPage() : new DirectorStaffDashboardPage());

    public void ShowDetail(Page page)
    {
        Detail = new NavigationPage(page)
        {
            BarBackgroundColor = Colors.White,
            BarTextColor = Colors.Black
        };
        IsPresented = false;
    }

    public static DashboardFlyoutPage? Current =>
        Application.Current?.Windows.FirstOrDefault()?.Page as DashboardFlyoutPage;
}
