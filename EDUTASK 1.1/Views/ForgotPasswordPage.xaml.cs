using EDUTASK_1._1.Services;
using EDUTASK_1._1.Helpers;
using EDUTASK_1._1.Views.Base;
namespace EDUTASK_1._1.Views;

public partial class ForgotPasswordPage : EduTaskPage
{
    private readonly DatabaseService _database = new();
    private int _accountId;
    private string _role = string.Empty;
    private int _attemptsRemaining = 5;

    public ForgotPasswordPage()
    {
        InitializeComponent();
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        if (PasswordStep.IsVisible)
        {
            ShowQuestionsStep();
            return;
        }

        if (QuestionsStep.IsVisible)
        {
            ShowIdentifyStep();
            return;
        }

        await Navigation.PopModalAsync(false);
    }

    protected override bool OnBackButtonPressed()
    {
        if (PasswordStep.IsVisible)
        {
            ShowQuestionsStep();
            return true;
        }

        if (QuestionsStep.IsVisible)
        {
            ShowIdentifyStep();
            return true;
        }

        return base.OnBackButtonPressed();
    }

    private void ShowIdentifyStep()
    {
        HeaderTitleLabel.Text = "Forgot Password";
        QuestionsStep.IsVisible = false;
        PasswordStep.IsVisible = false;
        IdentifyStep.IsVisible = true;
    }

    private void ShowQuestionsStep()
    {
        HeaderTitleLabel.Text = "Security Verification";
        IdentifyStep.IsVisible = false;
        PasswordStep.IsVisible = false;
        QuestionsStep.IsVisible = true;
    }

    private void OnIdentifierChanged(object sender, TextChangedEventArgs e) =>
        SetError(IdentifierBorder, IdentifierErrorLabel, string.Empty);

    private void OnAnswer1Changed(object sender, TextChangedEventArgs e) =>
        SetError(Answer1Border, Answer1ErrorLabel, string.Empty);

    private void OnAnswer2Changed(object sender, TextChangedEventArgs e) =>
        SetError(Answer2Border, Answer2ErrorLabel, string.Empty);

    private void OnNewPasswordChanged(object sender, TextChangedEventArgs e) =>
        SetError(NewPasswordBorder, NewPasswordErrorLabel, string.Empty);

    private void OnConfirmPasswordChanged(object sender, TextChangedEventArgs e) =>
        SetError(ConfirmPasswordBorder, ConfirmPasswordErrorLabel, string.Empty);

    private void OnToggleNewPassword(object sender, EventArgs e) =>
        PasswordField.ToggleVisibility(NewPasswordEntry, NewPasswordToggleButton);

    private void OnToggleConfirmPassword(object sender, EventArgs e) =>
        PasswordField.ToggleVisibility(ConfirmPasswordEntry, ConfirmPasswordToggleButton);

    private async void OnFindAccount(object? sender, EventArgs e)
    {
        string identifier = IdentifierEntry.Text?.Trim() ?? string.Empty;
        if (identifier.Length == 0)
        {
            SetError(IdentifierBorder, IdentifierErrorLabel, "Enter your username or email.");
            return;
        }

        try
        {
            var challenge = await _database.GetPasswordRecoveryChallengeAsync(identifier);
            if (!challenge.Success)
            {
                SetError(IdentifierBorder, IdentifierErrorLabel, "We couldn't verify those account details.");
                return;
            }

            _accountId = challenge.AccountId;
            _role = challenge.Role;
            Question1Label.Text = $"Q: {challenge.Question1}";
            Question2Label.Text = $"Q: {challenge.Question2}";
            HeaderTitleLabel.Text = "Security Verification";
            IdentifyStep.IsVisible = false;
            QuestionsStep.IsVisible = true;
        }
        catch
        {
            SetError(IdentifierBorder, IdentifierErrorLabel, "Recovery is unavailable. Try again.");
        }
    }

    private async void OnVerifyAnswers(object? sender, EventArgs e)
    {
        string answer1 = Answer1Entry.Text?.Trim() ?? string.Empty;
        string answer2 = Answer2Entry.Text?.Trim() ?? string.Empty;
        bool invalid = false;
        if (answer1.Length == 0)
        {
            SetError(Answer1Border, Answer1ErrorLabel, "Enter your answer.");
            invalid = true;
        }
        if (answer2.Length == 0)
        {
            SetError(Answer2Border, Answer2ErrorLabel, "Enter your answer.");
            invalid = true;
        }
        if (invalid)
            return;

        bool verified;
        try
        {
            verified = await _database.VerifyPasswordRecoveryAnswersAsync(_accountId, _role, answer1, answer2);
        }
        catch
        {
            VerificationErrorLabel.Text = "Verification is unavailable. Try again.";
            return;
        }

        if (!verified)
        {
            _attemptsRemaining--;
            VerificationErrorLabel.Text = _attemptsRemaining == 0
                ? "Too many attempts. Start again later."
                : $"Answers not verified. {_attemptsRemaining} attempts left.";
            if (_attemptsRemaining == 0)
            {
                Answer1Entry.IsEnabled = false;
                Answer2Entry.IsEnabled = false;
                if (sender is Button button)
                    button.IsEnabled = false;
            }
            return;
        }

        HeaderTitleLabel.Text = "Reset Your Password";
        QuestionsStep.IsVisible = false;
        PasswordStep.IsVisible = true;
    }

    private async void OnResetPassword(object? sender, EventArgs e)
    {
        string password = NewPasswordEntry.Text ?? string.Empty;
        string confirm = ConfirmPasswordEntry.Text ?? string.Empty;
        bool invalid = false;
        if (!FormFieldValidation.IsValidPassword(password))
        {
            SetError(NewPasswordBorder, NewPasswordErrorLabel,
                password.Length == 0 ? "Enter a new password." : "Use at least 8 characters.");
            invalid = true;
        }
        if (confirm.Length == 0)
        {
            SetError(ConfirmPasswordBorder, ConfirmPasswordErrorLabel, "Confirm your new password.");
            invalid = true;
        }
        else if (password != confirm)
        {
            SetError(ConfirmPasswordBorder, ConfirmPasswordErrorLabel, "Passwords do not match.");
            invalid = true;
        }
        if (invalid)
            return;

        try
        {
            var result = await _database.ResetPasswordAsync(_accountId, _role, password);
            if (!result.Success)
            {
                SetError(NewPasswordBorder, NewPasswordErrorLabel, result.Message);
                return;
            }

            await UiAlertService.ShowAsync(this, "Password updated", "You can now sign in with your new password.");
            await Navigation.PopModalAsync(false);
        }
        catch
        {
            SetError(NewPasswordBorder, NewPasswordErrorLabel, "Password could not be updated.");
        }
    }

    private static void SetError(Border border, Label label, string message) =>
        FormFieldValidation.SetFieldError(border, label, message);
}
