using EDUTASK_1._1.Models;
using EDUTASK_1._1.Helpers;
using EDUTASK_1._1.Views.Base;
using EDUTASK_1._1.Services;

namespace EDUTASK_1._1.Views;

public partial class LoginPage : EduTaskPage
{
    private readonly DatabaseService _database = new();
    private bool _isLoggingIn;

    public LoginPage()
    {
        InitializeComponent();
        BindingContext = this;
    }

    private void OnTogglePassword(object sender, EventArgs e) =>
        PasswordField.ToggleVisibility(PasswordEntry, VisiblePasswordEntry, PasswordToggleButton);

    private void OnUsernameChanged(object sender, TextChangedEventArgs e) =>
        ClearFieldAndLoginError(UsernameBorder, UsernameErrorLabel);

    private void OnPasswordChanged(object sender, TextChangedEventArgs e)
    {
        PasswordField.SynchronizeText(PasswordEntry, VisiblePasswordEntry);
        ClearFieldAndLoginError(PasswordBorder, PasswordErrorLabel);
    }

    private void OnVisiblePasswordChanged(object sender, TextChangedEventArgs e)
    {
        PasswordField.SynchronizeText(VisiblePasswordEntry, PasswordEntry);
        ClearFieldAndLoginError(PasswordBorder, PasswordErrorLabel);
    }

    private static void SetFieldError(Border border, Label label, string message) =>
        FormFieldValidation.SetFieldError(border, label, message);

    private void ClearFieldAndLoginError(Border border, Label label)
    {
        SetFieldError(border, label, string.Empty);
    }

    private void ClearLoginErrors()
    {
        SetFieldError(UsernameBorder, UsernameErrorLabel, string.Empty);
        SetFieldError(PasswordBorder, PasswordErrorLabel, string.Empty);
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        if (_isLoggingIn)
            return;

        string username = UsernameEntry.Text?.Trim() ?? string.Empty;
        string password = PasswordEntry.Text ?? string.Empty;
        ClearLoginErrors();

        bool hasValidationErrors = false;
        if (username.Length == 0)
        {
            SetFieldError(UsernameBorder, UsernameErrorLabel, "Enter your username.");
            hasValidationErrors = true;
        }
        if (password.Length == 0)
        {
            SetFieldError(PasswordBorder, PasswordErrorLabel, "Enter your password.");
            hasValidationErrors = true;
        }
        if (hasValidationErrors)
        {
            if (username.Length == 0)
                UsernameEntry.Focus();
            else if (password.Length == 0)
                PasswordEntry.Focus();
            return;
        }
        _isLoggingIn = true;
        LoginButton.IsEnabled = false;
        try
        {
            AuthenticationResult result = await _database.AuthenticateAsync(username, password);
            if (!result.Success)
            {
                bool accountStatusError =
                    result.Message.Contains("approval", StringComparison.OrdinalIgnoreCase) ||
                    result.Message.Contains("disabled", StringComparison.OrdinalIgnoreCase);
                if (!accountStatusError)
                {
                    // Keep the username so the user only needs to correct the
                    // failed credential, but never leave an invalid password
                    // sitting in either the masked or revealed entry.
                    PasswordEntry.Text = string.Empty;
                    VisiblePasswordEntry.Text = string.Empty;
                    const string credentialError = "The username or password is incorrect.";
                    // Set this after clearing the entries because their
                    // TextChanged handlers clear field errors.
                    SetFieldError(UsernameBorder, UsernameErrorLabel, string.Empty);
                    SetFieldError(PasswordBorder, PasswordErrorLabel, credentialError);
                    UsernameBorder.Stroke = FormFieldValidation.ErrorStrokeColor;
                    PasswordBorder.Stroke = FormFieldValidation.ErrorStrokeColor;
                }
                else
                {
                    SetFieldError(PasswordBorder, PasswordErrorLabel, result.Message);
                }
                return;
            }

            // Finish guarded, idempotent schema upgrades before dashboard
            // buttons can issue queries against the upgraded model.
            await _database.PrepareOperationalSchemaAsync();

            UserSessionService.Clear();
            TeacherSessionService.Clear();
            if (result.Teacher is not null)
            {
                TeacherSessionService.SetCurrentTeacher(result.Teacher);
                if (Window is not null) Window.Page = new DashboardFlyoutPage(new HomePage());
            }
            else if (result.User is not null)
            {
                UserSessionService.SetCurrentUser(result.User);
                if (Window is not null) Window.Page = new DashboardFlyoutPage(new HomePage());
            }
        }
        catch (Exception exception)
        {
#if ANDROID
            System.Diagnostics.Debug.WriteLine($"Android login API failed: {exception}");
            SetFieldError(PasswordBorder, PasswordErrorLabel,
                "Database service unavailable. Start EduTask.Api and try again.");
#else
            System.Diagnostics.Debug.WriteLine($"Login failed: {exception}");
            SetFieldError(PasswordBorder, PasswordErrorLabel, "Login unavailable. Try again.");
#endif
        }
        finally
        {
            _isLoggingIn = false;
            LoginButton.IsEnabled = true;
        }
    }

    private async void OnForgotPasswordTapped(object sender, TappedEventArgs e)
    {
        await Navigation.PushModalAsync(new ForgotPasswordPage(), false);
    }
    private async void OnCreateAccountTapped(object sender, TappedEventArgs e) =>
        await Navigation.PushModalAsync(new RegisterRolePage(), false);
}
