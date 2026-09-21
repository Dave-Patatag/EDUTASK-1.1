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
    private double _preferredDialogHeight = 330;

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
            avatarSeed: $"teacher-{teacher.Teacher_id}",
            fullName: $"{teacher.First_name} {teacher.Last_name}".Trim(),
            username: teacher.Username,
            email: teacher.Email,
            contactNumber: teacher.Contact_number,
            role: "Teacher",
            profilePhotoPath: teacher.Profile_photo,
            accountCreated: teacher.Account_created,
            isActive: teacher.Is_active,
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
            avatarSeed: $"user-{user.User_id}",
            fullName: $"{user.First_name} {user.Last_name}".Trim(),
            username: user.Username,
            email: user.Email,
            contactNumber: user.Contact_number,
            role: string.IsNullOrWhiteSpace(user.Role_name) ? "Staff" : user.Role_name,
            profilePhotoPath: user.Profile_photo,
            accountCreated: user.Account_created,
            isActive: user.Is_active,
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
        _viewModel.Contact_number = string.IsNullOrWhiteSpace(contactNumber)
            ? "No contact number"
            : contactNumber;
        _viewModel.Role = role;
        _viewModel.Profile_photo = profilePhotoPath;

        RoleLabel.Text = role;
        MemberSinceLabel.Text = accountCreated.ToString("MMM d, yyyy");

        string status = isManagedAccountDisabled
            ? "Disabled"
            : isActive ? "Active" : "Pending";
        StatusLabel.Text = status;
        StatusLabel.TextColor = isManagedAccountDisabled
            ? AppColors.StatusDanger
            : isActive ? AppColors.StatusSuccess : AppColors.StatusPending;
        StatusCard.BackgroundColor = isManagedAccountDisabled
            ? AppColors.StatusDangerSurface
            : isActive ? AppColors.StatusSuccessSurface : AppColors.StatusPendingSurface;
        StatusCard.Stroke = isManagedAccountDisabled
            ? AppColors.StatusDangerBorder
            : isActive ? AppColors.StatusSuccessBorder : AppColors.StatusPendingBorder;

        bool isStaff = string.Equals(role, "Staff", StringComparison.OrdinalIgnoreCase);
        bool isPending = !isActive && !isManagedAccountDisabled;
        bool canChangeAccountState = !isPending && _onAccountStateChange is not null;
        ManagementActions.IsVisible = (isStaff && _onPromote is not null) || canChangeAccountState;
        PromoteButton.IsVisible = isStaff && isActive && !isManagedAccountDisabled && _onPromote is not null;
        AccountStateButton.IsVisible = canChangeAccountState;
        AccountStateButton.Text = isManagedAccountDisabled ? "Enable account" : "Disable account";
        AccountStateButton.BackgroundColor = isManagedAccountDisabled
            ? AppColors.StatusSuccessSurface
            : AppColors.ActionDanger;
        AccountStateButton.BorderColor = isManagedAccountDisabled
            ? AppColors.StatusSuccessBorder
            : AppColors.ActionDanger;
        AccountStateButton.TextColor = isManagedAccountDisabled
            ? AppColors.StatusSuccess
            : AppColors.TextInverse;
        UpdatePreferredDialogHeight();
    }

    private void UpdatePreferredDialogHeight()
    {
        double actionHeight = !ManagementActions.IsVisible
            ? 0
            : PromoteButton.IsVisible && AccountStateButton.IsVisible ? 110 : 66;

        _preferredDialogHeight = Math.Min(560, 330 + actionHeight);
        if (Height > 0)
            DialogPanel.HeightRequest = Math.Min(_preferredDialogHeight, Math.Max(300, Height - 24));
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width <= 0 || height <= 0)
            return;

        // The sheet occupies the full viewport width and its dedicated auto
        // grid row keeps it attached to the bottom edge on every platform.
        DialogPanel.WidthRequest = width;
        double availableHeight = Math.Max(300, height - 24);
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
            : isActive ? AppColors.StatusSuccess : AppColors.StatusPending;
        StatusCard.BackgroundColor = isDisabled
            ? AppColors.StatusDangerSurface
            : isActive ? AppColors.StatusSuccessSurface : AppColors.StatusPendingSurface;
        StatusCard.Stroke = isDisabled
            ? AppColors.StatusDangerBorder
            : isActive ? AppColors.StatusSuccessBorder : AppColors.StatusPendingBorder;
        AccountStateButton.Text = isDisabled ? "Enable account" : "Disable account";
        AccountStateButton.BackgroundColor = isDisabled
            ? AppColors.StatusSuccessSurface
            : AppColors.ActionDanger;
        AccountStateButton.BorderColor = isDisabled
            ? AppColors.StatusSuccessBorder
            : AppColors.ActionDanger;
        AccountStateButton.TextColor = isDisabled
            ? AppColors.StatusSuccess
            : AppColors.TextInverse;
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
