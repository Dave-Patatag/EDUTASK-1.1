using EDUTASK_1._1.Helpers;
using EDUTASK_1._1.Models;
using EDUTASK_1._1.Services;
using EDUTASK_1._1.ViewModels;
using EDUTASK_1._1.Views.Base;

namespace EDUTASK_1._1.Views;

public partial class AccountProfileDialog : EduTaskPage
{
    private readonly ProfileViewModel _viewModel = new();
    private Func<Task<bool>>? _onPromote;
    private Func<bool, Task<bool>>? _onAccountStateChange;
    private bool _isManagedAccountDisabled;
    private bool _isActive;
    private string _role = string.Empty;
    private bool _isClosing;
    private double _preferredDialogHeight = 312;

    public AccountProfileDialog(
        Teachers teacher,
        bool isManagedAccountDisabled = false,
        Func<Task<bool>>? onPromote = null,
        Func<bool, Task<bool>>? onAccountStateChange = null)
    {
        InitializeComponent();
        DialogCard = DialogPanel;
        BindingContext = _viewModel;

        ApplyProfile(
            avatarSeed: $"teacher-{teacher.TeacherID}",
            fullName: $"{teacher.FirstName} {teacher.LastName}".Trim(),
            username: teacher.Username,
            email: teacher.Email,
            contactNumber: teacher.ContactNumber,
            role: "Teacher",
            bio: teacher.Bio,
            profilePhotoPath: teacher.ProfilePhotoPath,
            accountCreated: teacher.AccountCreated,
            isActive: teacher.IsActive,
            isManagedAccountDisabled: isManagedAccountDisabled,
            onPromote: onPromote,
            onAccountStateChange: onAccountStateChange);
    }

    public AccountProfileDialog(
        User user,
        bool isManagedAccountDisabled = false,
        Func<Task<bool>>? onPromote = null,
        Func<bool, Task<bool>>? onAccountStateChange = null)
    {
        InitializeComponent();
        DialogCard = DialogPanel;
        BindingContext = _viewModel;

        ApplyProfile(
            avatarSeed: $"user-{user.UserID}",
            fullName: $"{user.FirstName} {user.LastName}".Trim(),
            username: user.Username,
            email: user.Email,
            contactNumber: user.ContactNumber,
            role: string.IsNullOrWhiteSpace(user.RoleName) ? "Staff" : user.RoleName,
            bio: user.Bio,
            profilePhotoPath: user.ProfilePhotoPath,
            accountCreated: user.AccountCreated,
            isActive: user.IsActive,
            isManagedAccountDisabled: isManagedAccountDisabled,
            onPromote: onPromote,
            onAccountStateChange: onAccountStateChange);
    }

    private void ApplyProfile(
        string avatarSeed,
        string fullName,
        string username,
        string email,
        string contactNumber,
        string role,
        string bio,
        string profilePhotoPath,
        DateTime accountCreated,
        bool isActive,
        bool isManagedAccountDisabled,
        Func<Task<bool>>? onPromote,
        Func<bool, Task<bool>>? onAccountStateChange)
    {
        _onPromote = onPromote;
        _onAccountStateChange = onAccountStateChange;
        _isActive = isActive;
        _isManagedAccountDisabled = isManagedAccountDisabled;
        _role = role;

        _viewModel.AvatarSeed = avatarSeed;
        _viewModel.FullName = fullName;
        _viewModel.Username = username;
        _viewModel.Email = email;
        _viewModel.ContactNumber = string.IsNullOrWhiteSpace(contactNumber)
            ? "No contact number"
            : contactNumber;
        _viewModel.Role = role;
        _viewModel.Bio = bio;
        _viewModel.ProfilePhotoPath = profilePhotoPath;

        RoleLabel.Text = role;
        MemberSinceLabel.Text = accountCreated.ToString("MMM d, yyyy");
        BioSection.IsVisible = !string.IsNullOrWhiteSpace(bio);

        string status = isManagedAccountDisabled
            ? "Disabled"
            : isActive ? "Active" : "Pending";
        StatusLabel.Text = status;
        StatusLabel.TextColor = isManagedAccountDisabled
            ? AppColors.StatusDanger
            : isActive ? AppColors.StatusSuccess : AppColors.StatusValidation;
        StatusCard.BackgroundColor = isManagedAccountDisabled
            ? AppColors.StatusDangerSurface
            : isActive ? AppColors.StatusSuccessSurface : AppColors.StatusValidationSurface;
        StatusCard.Stroke = isManagedAccountDisabled
            ? AppColors.StatusDangerBorder
            : isActive ? AppColors.StatusSuccessBorder : AppColors.StatusValidationBorder;

        bool isStaff = string.Equals(role, "Staff", StringComparison.OrdinalIgnoreCase);
        bool isPending = !isActive && !isManagedAccountDisabled;
        bool canChangeAccountState = !isPending && _onAccountStateChange is not null;
        ManagementActions.IsVisible = (isStaff && _onPromote is not null) || canChangeAccountState;
        PromoteButton.IsVisible = isStaff && isActive && !isManagedAccountDisabled && _onPromote is not null;
        AccountStateButton.IsVisible = canChangeAccountState;
        AccountStateButton.Text = isManagedAccountDisabled ? "Enable account" : "Disable account";
        AccountStateButton.BackgroundColor = isManagedAccountDisabled
            ? AppColors.StatusSuccessSurface
            : AppColors.StatusDangerSurface;
        AccountStateButton.BorderColor = isManagedAccountDisabled
            ? AppColors.StatusSuccessBorder
            : AppColors.StatusDangerBorder;
        AccountStateButton.TextColor = isManagedAccountDisabled
            ? AppColors.StatusSuccess
            : AppColors.StatusDanger;
        UpdatePreferredDialogHeight();
    }

    private void UpdatePreferredDialogHeight()
    {
        double bioHeight = BioSection.IsVisible ? 72 : 0;
        double actionHeight = !ManagementActions.IsVisible
            ? 0
            : PromoteButton.IsVisible && AccountStateButton.IsVisible ? 110 : 66;

        _preferredDialogHeight = Math.Min(494, 312 + bioHeight + actionHeight);
        if (Height > 0)
            DialogPanel.HeightRequest = Math.Min(_preferredDialogHeight, Math.Max(280, Height - 64));
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width <= 0 || height <= 0)
            return;

        // Leave room for the root grid's padding. The previous 300 minimum
        // could exceed the padded Android viewport and clip the right side,
        // making an otherwise centred card appear shifted to the left.
        DialogPanel.WidthRequest = Math.Min(380, Math.Max(260, width - 40));
        double availableHeight = Math.Max(280, height - 64);
        DialogPanel.MaximumHeightRequest = availableHeight;
        DialogPanel.HeightRequest = Math.Min(_preferredDialogHeight, availableHeight);
    }

    private async void OnPromoteClicked(object sender, EventArgs e)
    {
        if (_onPromote is null || !await _onPromote())
            return;

        _role = "Director";
        _viewModel.Role = _role;
        RoleLabel.Text = _role;
        ManagementActions.IsVisible = false;
        UpdatePreferredDialogHeight();
        await UiAlertService.ShowAsync(this, "Staff promoted",
            $"{_viewModel.FullName} is now a Director.");
    }

    private async void OnAccountStateClicked(object sender, EventArgs e)
    {
        if (_onAccountStateChange is null)
            return;

        bool disabling = !_isManagedAccountDisabled;
        if (!await _onAccountStateChange(disabling))
            return;

        _isManagedAccountDisabled = disabling;
        _isActive = !disabling;
        ApplyStatus(_isManagedAccountDisabled, _isActive);
    }

    private void ApplyStatus(bool isDisabled, bool isActive)
    {
        string status = isDisabled ? "Disabled" : isActive ? "Active" : "Pending";
        StatusLabel.Text = status;
        StatusLabel.TextColor = isDisabled
            ? AppColors.StatusDanger
            : isActive ? AppColors.StatusSuccess : AppColors.StatusValidation;
        StatusCard.BackgroundColor = isDisabled
            ? AppColors.StatusDangerSurface
            : isActive ? AppColors.StatusSuccessSurface : AppColors.StatusValidationSurface;
        StatusCard.Stroke = isDisabled
            ? AppColors.StatusDangerBorder
            : isActive ? AppColors.StatusSuccessBorder : AppColors.StatusValidationBorder;
        AccountStateButton.Text = isDisabled ? "Enable account" : "Disable account";
        AccountStateButton.BackgroundColor = isDisabled
            ? AppColors.StatusSuccessSurface
            : AppColors.StatusDangerSurface;
        AccountStateButton.BorderColor = isDisabled
            ? AppColors.StatusSuccessBorder
            : AppColors.StatusDangerBorder;
        AccountStateButton.TextColor = isDisabled
            ? AppColors.StatusSuccess
            : AppColors.StatusDanger;
        PromoteButton.IsVisible = string.Equals(_role, "Staff", StringComparison.OrdinalIgnoreCase)
            && isActive && !isDisabled && _onPromote is not null;
        UpdatePreferredDialogHeight();
    }

    private async void OnCloseClicked(object sender, EventArgs e) =>
        await CloseAsync();

    private async void OnBackdropTapped(object sender, TappedEventArgs e) =>
        await CloseAsync();

    protected override bool OnBackButtonPressed()
    {
        _ = CloseAsync();
        return true;
    }

    private async System.Threading.Tasks.Task CloseAsync()
    {
        if (_isClosing)
            return;

        _isClosing = true;
        await PlayExitAsync();
        if (Navigation.ModalStack.Contains(this))
            await Navigation.PopModalAsync(false);
    }
}
