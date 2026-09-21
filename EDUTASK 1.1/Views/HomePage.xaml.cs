using EDUTASK_1._1.Models;
using EDUTASK_1._1.Services;
using EDUTASK_1._1.Views.Base;

namespace EDUTASK_1._1.Views;

public partial class HomePage : EduTaskPage
{
    public HomePage()
    {
        InitializeComponent();
        ConfigureForCurrentRole();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ConfigureForCurrentRole();
    }

    private void ConfigureForCurrentRole()
    {
        string firstName;
        string role;

        if (TeacherSessionService.CurrentTeacher is { } teacher)
        {
            firstName = teacher.First_name;
            role = "Teacher";
        }
        else
        {
            User? user = UserSessionService.CurrentUser;
            firstName = user?.First_name ?? string.Empty;
            role = string.IsNullOrWhiteSpace(user?.Role_name) ? "Staff" : user.Role_name;
        }

        GreetingLabel.Text = "Welcome";
        UserNameLabel.Text = string.IsNullOrWhiteSpace(firstName)
            ? $"{role}!"
            : $"{role} {firstName}!";
        UpdateHeroSizing();
    }

    private void UpdateHeroSizing()
    {
        int identityLength = UserNameLabel.Text?.Length ?? 0;
        UserNameLabel.FontSize = identityLength switch
        {
            <= 16 => 24,
            <= 22 => 22,
            <= 29 => 20,
            _ => 18
        };
    }
}
