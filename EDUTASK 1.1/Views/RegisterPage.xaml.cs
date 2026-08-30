using EDUTASK_1._1.Models;
using EDUTASK_1._1.Helpers;
using EDUTASK_1._1.Views.Base;
using EDUTASK_1._1.Services;

namespace EDUTASK_1._1.Views;

public partial class RegisterPage : EduTaskPage
{
    private readonly DatabaseService _database = new();
    private readonly string _role;
    private readonly List<string> _selectedQuestions = [];
    private readonly List<QuestionOption> _questionOptions;
    private OutsideTapCatcher _questionOutsideTap = null!;
    public string[] Questions => SecurityQuestions.All;

    public RegisterPage(string role)
    {
        _role = role;
        _questionOptions = SecurityQuestions.All.Select(question => new QuestionOption(question)).ToList();
        InitializeComponent();
        MoveQuestionDropdownToOverlayLayer();
        BindingContext = this;
        QuestionOptionsView.ItemsSource = _questionOptions;

        // Constructed after the panel has been reparented, so the catcher lands
        // in the overlay host rather than the panel's original container.
        _questionOutsideTap = new OutsideTapCatcher(
            QuestionDropdownPanel,
            CloseQuestionDropdownAsync);
    }

    private async void OnBackClicked(object sender, EventArgs e) =>
        await Navigation.PopModalAsync(false);

    private void MoveQuestionDropdownToOverlayLayer()
    {
        if (QuestionDropdownPanel.Parent is Layout questionParent)
            questionParent.Remove(QuestionDropdownPanel);

        ConfigureOverlayPanel(QuestionDropdownPanel);
        RegistrationOverlayHost.Add(QuestionDropdownPanel);
    }

    private static void ConfigureOverlayPanel(Border panel)
    {
        panel.Margin = 0;
        panel.VerticalOptions = LayoutOptions.Start;
        panel.HorizontalOptions = LayoutOptions.Fill;
        panel.ZIndex = 100;
    }

    // TranslationY is a render offset, so the panel can hang below its grid row
    // without being re-laid-out and without nudging the Create account button
    // underneath it. The dropdown animation only touches Opacity and ScaleY, so
    // it does not fight this.
    private static void PositionOverlayPanel(Border panel, VisualElement fieldContainer) =>
        panel.TranslationY = OverlayPanel.TopBelow(fieldContainer, panel.Parent);

    private void OnTogglePassword(object sender, EventArgs e) =>
        PasswordField.ToggleVisibility(PasswordEntry, VisiblePasswordEntry, PasswordToggleButton);

    private void OnToggleConfirmPassword(object sender, EventArgs e) =>
        PasswordField.ToggleVisibility(ConfirmPasswordEntry, VisibleConfirmPasswordEntry, ConfirmPasswordToggleButton);

    private void OnFullNameChanged(object sender, TextChangedEventArgs e) =>
        SetFieldError(FullNameBorder, FullNameErrorLabel, string.Empty);

    private void OnUsernameChanged(object sender, TextChangedEventArgs e) =>
        SetFieldError(UsernameBorder, UsernameErrorLabel, string.Empty);

    private void OnEmailChanged(object sender, TextChangedEventArgs e) =>
        SetFieldError(EmailBorder, EmailErrorLabel, string.Empty);

    private void OnRegisterPasswordChanged(object sender, TextChangedEventArgs e)
    {
        PasswordField.SynchronizeText(PasswordEntry, VisiblePasswordEntry);
        SetFieldError(PasswordBorder, PasswordErrorLabel, string.Empty);
        SetFieldError(ConfirmPasswordBorder, ConfirmPasswordErrorLabel, string.Empty);
    }

    private void OnVisibleRegisterPasswordChanged(object sender, TextChangedEventArgs e)
    {
        PasswordField.SynchronizeText(VisiblePasswordEntry, PasswordEntry);
        SetFieldError(PasswordBorder, PasswordErrorLabel, string.Empty);
        SetFieldError(ConfirmPasswordBorder, ConfirmPasswordErrorLabel, string.Empty);
    }

    private void OnConfirmPasswordChanged(object sender, TextChangedEventArgs e)
    {
        PasswordField.SynchronizeText(ConfirmPasswordEntry, VisibleConfirmPasswordEntry);
        SetFieldError(ConfirmPasswordBorder, ConfirmPasswordErrorLabel, string.Empty);
    }

    private void OnVisibleConfirmPasswordChanged(object sender, TextChangedEventArgs e)
    {
        PasswordField.SynchronizeText(VisibleConfirmPasswordEntry, ConfirmPasswordEntry);
        SetFieldError(ConfirmPasswordBorder, ConfirmPasswordErrorLabel, string.Empty);
    }

    private static void SetFieldError(Border border, Label label, string message) =>
        FormFieldValidation.SetFieldError(border, label, message);

    private void ClearRegistrationErrors()
    {
        SetFieldError(FullNameBorder, FullNameErrorLabel, string.Empty);
        SetFieldError(UsernameBorder, UsernameErrorLabel, string.Empty);
        SetFieldError(EmailBorder, EmailErrorLabel, string.Empty);
        SetFieldError(PasswordBorder, PasswordErrorLabel, string.Empty);
        SetFieldError(ConfirmPasswordBorder, ConfirmPasswordErrorLabel, string.Empty);
        SetFieldError(SecurityQuestionsBorder, SecurityErrorLabel, string.Empty);
    }

    private async void OnSelectQuestionsTapped(object sender, TappedEventArgs e)
    {
        PositionOverlayPanel(QuestionDropdownPanel, QuestionFieldContainer);

        // Chevron rests at 0 and turns to 180 when open — the same direction as
        // the role dropdowns, which this one used to contradict.
        if (QuestionDropdownPanel.IsVisible)
        {
            await CloseQuestionDropdownAsync();
            return;
        }

        // This panel is multi-select so it cannot use DropdownController, but it
        // shares the same tap-outside-to-dismiss behaviour as every other one.
        _questionOutsideTap.Attach();

        await System.Threading.Tasks.Task.WhenAll(
            QuestionDropdownPanel.DropdownOpenAsync(),
            QuestionsChevron.RotateTo(180, Motion.Base, Motion.Standard));
    }

    private System.Threading.Tasks.Task CloseQuestionDropdownAsync()
    {
        _questionOutsideTap.Detach();

        return System.Threading.Tasks.Task.WhenAll(
            QuestionDropdownPanel.DropdownCloseAsync(),
            QuestionsChevron.RotateTo(0, Motion.Fast, Motion.Standard));
    }

    private async void OnQuestionOptionClicked(object sender, EventArgs e)
    {
        if (sender is not Button button ||
            button.CommandParameter is not QuestionOption option)
            return;

        if (option.IsSelected)
        {
            option.IsSelected = false;
            _selectedQuestions.Remove(option.Text);
        }
        else
        {
            if (_selectedQuestions.Count == 2)
            {
                SetFieldError(SecurityQuestionsBorder, SecurityErrorLabel, "Choose only two questions.");
                return;
            }

            option.IsSelected = true;
            _selectedQuestions.Add(option.Text);
        }

        SetFieldError(SecurityQuestionsBorder, SecurityErrorLabel, string.Empty);
        SelectedQuestionsLabel.Text = _selectedQuestions.Count switch
        {
            0 => "Choose 2 security questions",
            1 => "1 selected — choose 1 more",
            _ => "Tap to change"
        };
        SelectedQuestionsLabel.TextColor = AppColors.TextPrimary;

        if (_selectedQuestions.Count == 2)
            await ShowSecurityAnswersAsync();
    }
    private async System.Threading.Tasks.Task ShowSecurityAnswersAsync()
    {
        await CloseQuestionDropdownAsync();

        var answerDialog = new SecurityAnswersPage(
            _selectedQuestions[0],
            _selectedQuestions[1],
            Answer1Entry.Text,
            Answer2Entry.Text);
        (string First, string Second)? answers = await answerDialog.ShowAsync(Navigation);
        if (answers is null)
            return;

        Answer1Entry.Text = answers.Value.First;
        Answer2Entry.Text = answers.Value.Second;
        SetFieldError(SecurityQuestionsBorder, SecurityErrorLabel, string.Empty);
    }

    private async void OnCreateClicked(object sender, EventArgs e)
    {
        string name = FullNameEntry.Text?.Trim() ?? string.Empty;
        string username = UsernameEntry.Text?.Trim() ?? string.Empty;
        string email = EmailEntry.Text?.Trim() ?? string.Empty;
        string password = PasswordEntry.Text ?? string.Empty;
        string confirm = ConfirmPasswordEntry.Text ?? string.Empty;
        string q1 = _selectedQuestions.ElementAtOrDefault(0) ?? string.Empty;
        string q2 = _selectedQuestions.ElementAtOrDefault(1) ?? string.Empty;
        string a1 = Answer1Entry.Text?.Trim() ?? string.Empty;
        string a2 = Answer2Entry.Text?.Trim() ?? string.Empty;
        ClearRegistrationErrors();

        VisualElement? firstInvalidEntry = null;
        if (name.Length == 0)
        {
            SetFieldError(FullNameBorder, FullNameErrorLabel, "Enter your full name.");
            firstInvalidEntry ??= FullNameEntry;
        }
        else if (name.Length < 2)
        {
            SetFieldError(FullNameBorder, FullNameErrorLabel, "Enter a valid full name.");
            firstInvalidEntry ??= FullNameEntry;
        }
        if (username.Length == 0)
        {
            SetFieldError(UsernameBorder, UsernameErrorLabel, "Create a username.");
            firstInvalidEntry ??= UsernameEntry;
        }
        else if (!FormFieldValidation.IsValidUsername(username))
        {
            SetFieldError(UsernameBorder, UsernameErrorLabel,
                "Use 3–30 letters, numbers, dots, underscores, or hyphens.");
            firstInvalidEntry ??= UsernameEntry;
        }
        if (email.Length == 0)
        {
            SetFieldError(EmailBorder, EmailErrorLabel, "Enter your email address.");
            firstInvalidEntry ??= EmailEntry;
        }
        else if (!FormFieldValidation.IsValidEmail(email))
        {
            SetFieldError(EmailBorder, EmailErrorLabel, "Enter a valid email address.");
            firstInvalidEntry ??= EmailEntry;
        }
        if (password.Length == 0)
        {
            SetFieldError(PasswordBorder, PasswordErrorLabel, "Create a password.");
            firstInvalidEntry ??= PasswordEntry;
        }
        else if (!FormFieldValidation.IsValidPassword(password))
        {
            SetFieldError(PasswordBorder, PasswordErrorLabel, "Use at least 8 characters.");
            firstInvalidEntry ??= PasswordEntry;
        }
        if (confirm.Length == 0)
        {
            SetFieldError(ConfirmPasswordBorder, ConfirmPasswordErrorLabel, "Enter your password again.");
            firstInvalidEntry ??= ConfirmPasswordEntry;
        }
        else if (password.Length > 0 && password != confirm)
        {
            SetFieldError(ConfirmPasswordBorder, ConfirmPasswordErrorLabel, "Passwords do not match.");
            firstInvalidEntry ??= ConfirmPasswordEntry;
        }
        bool questionsMissing = q1.Length == 0 || q2.Length == 0;
        if (questionsMissing)
            SetFieldError(SecurityQuestionsBorder, SecurityErrorLabel, "Choose two security questions.");

        if (firstInvalidEntry is not null || questionsMissing)
        {
            firstInvalidEntry?.Focus();
            return;
        }

        if (a1.Length == 0 || a2.Length == 0)
        {
            SetFieldError(SecurityQuestionsBorder, SecurityErrorLabel, "Answer both security questions.");
            await ShowSecurityAnswersAsync();
            return;
        }
        try
        {
            var result = await _database.RegisterAccountAsync(
                _role, name, username, email, password, q1, a1, q2, a2);
            if (!result.Success)
            {
                if (result.Message.Contains("username", StringComparison.OrdinalIgnoreCase))
                    SetFieldError(UsernameBorder, UsernameErrorLabel, result.Message);
                else if (result.Message.Contains("email", StringComparison.OrdinalIgnoreCase))
                    SetFieldError(EmailBorder, EmailErrorLabel, result.Message);
                else
                    SetFieldError(SecurityQuestionsBorder, SecurityErrorLabel, "Could not create account. Try again.");
                return;
            }

            await UiAlertService.ShowAsync(this, "Request submitted", result.Message);
            await ReturnToLoginAsync();
        }
        catch
        {
            SetFieldError(SecurityQuestionsBorder, SecurityErrorLabel, "Could not create account. Try again.");
        }
    }

    private async void OnSignInTapped(object sender, TappedEventArgs e) => await ReturnToLoginAsync();

    private async System.Threading.Tasks.Task ReturnToLoginAsync()
    {
        if (Window is not null)
        {
            Window.Page = new LoginPage();
            await System.Threading.Tasks.Task.CompletedTask;
            return;
        }

        while (Navigation.ModalStack.Count > 0)
            await Navigation.PopModalAsync(false);
    }

    private sealed class QuestionOption : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _isSelected;

        public QuestionOption(string text) => Text = text;

        public string Text { get; }

        // Matches the role dropdowns: a light tint and bold brand text.
        public Color BackgroundColor => IsSelected ? AppColors.Brand50 : Colors.Transparent;
        public Color TextColor => IsSelected ? AppColors.Brand800 : AppColors.TextPrimary;
        public FontAttributes FontAttributes => IsSelected ? FontAttributes.Bold : FontAttributes.None;

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                    return;

                _isSelected = value;
                PropertyChanged?.Invoke(this, new(nameof(IsSelected)));
                PropertyChanged?.Invoke(this, new(nameof(BackgroundColor)));
                PropertyChanged?.Invoke(this, new(nameof(TextColor)));
                PropertyChanged?.Invoke(this, new(nameof(FontAttributes)));
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }
}
