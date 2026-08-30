using EDUTASK_1._1.Views.Base;
using EDUTASK_1._1.Helpers;
using EDUTASK_1._1.Services;

namespace EDUTASK_1._1.Views;

public partial class RegisterRolePage : EduTaskPage
{
    private DropdownController _roleDropdown = null!;

    private string _selectedRole => _roleDropdown.SelectedValue;

    public RegisterRolePage()
    {
        InitializeComponent();
        InitialiseRoleDropdown();
    }

    private void InitialiseRoleDropdown()
    {
        _roleDropdown = new DropdownController(
            RoleFieldContainer, RoleBorder, RoleChevron, RoleDropdownPanel,
            RoleSelectionLabel, "Select your role");

        // Director accounts are not self-registered.
        _roleDropdown.AddOption(StaffOption, StaffOptionLabel, StaffOptionButton, "Staff");
        _roleDropdown.AddOption(TeacherOption, TeacherOptionLabel, TeacherOptionButton, "Teacher");

        _roleDropdown.SelectionChanged += (_, _) => SetRoleError(string.Empty);
    }

    private async void OnBackClicked(object sender, EventArgs e) =>
        await Navigation.PopModalAsync(false);

    private async void OnRoleFieldTapped(object sender, TappedEventArgs e) =>
        await _roleDropdown.ToggleAsync();

    private async void OnRoleOptionClicked(object sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: string role })
            await _roleDropdown.SelectAsync(role);
    }

    private async void OnConfirmClicked(object sender, EventArgs e)
    {
        _roleDropdown.CloseImmediate();

        if (string.IsNullOrWhiteSpace(_selectedRole))
        {
            SetRoleError("Select the role for your account.");
            return;
        }

        SetRoleError(string.Empty);
        await Navigation.PushModalAsync(new RegisterPage(_selectedRole), false);
    }

    // Routed through the shared validator so this page gets the same error
    // treatment as every other form: red stroke and screen-reader announce.
    private void SetRoleError(string message) =>
        FormFieldValidation.SetFieldError(RoleBorder, RoleErrorLabel, message);

    private async void OnSignInTapped(object sender, TappedEventArgs e)
    {
        while (Navigation.ModalStack.Count > 0)
            await Navigation.PopModalAsync(false);
    }
}
