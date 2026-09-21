using EDUTASK_1._1.Models;
using EDUTASK_1._1.Helpers;
using EDUTASK_1._1.Views.Base;
using EDUTASK_1._1.Services;
using EDUTASK_1._1.ViewModels;

namespace EDUTASK_1._1.Views;

public partial class UserProfilePage : EduTaskPage
{
    private readonly DatabaseService _database = new();
    private readonly ProfileViewModel _viewModel = new();
    private readonly DonutChartDrawable _chartDrawable = new();
    private bool _isProfileMenuAnimating;
    private bool _returnToDirectory;
    private bool _isManagedAccountDisabled;
    private User? _user;
    private Teachers? _teacher;

    public UserProfilePage(
        User user,
        bool returnToDirectory = false,
        bool isManagedAccountDisabled = false) : this()
    {
        _returnToDirectory = returnToDirectory;
        _isManagedAccountDisabled = isManagedAccountDisabled;
        ProfileBackButton.IsVisible = returnToDirectory;
        ProfileEditButton.IsVisible = !returnToDirectory;
        bool directorManagementMode = returnToDirectory && UserSessionService.IsDirector;
        ProfilePageTitleLabel.Text = directorManagementMode ? "Staff Account" : "Profile";
        OverviewSection.IsVisible = !directorManagementMode;
        BottomTabBar.IsVisible = !directorManagementMode;
        _user = user;
        ApplyUser(user);
    }

    public UserProfilePage(
        Teachers teacher,
        bool returnToDirectory = false,
        bool isManagedAccountDisabled = false) : this()
    {
        _returnToDirectory = returnToDirectory;
        _isManagedAccountDisabled = isManagedAccountDisabled;
        ProfileBackButton.IsVisible = returnToDirectory;
        ProfileEditButton.IsVisible = !returnToDirectory;
        _teacher = teacher;
        ApplyTeacher(teacher);
    }

    private UserProfilePage()
    {
        InitializeComponent();
        BindingContext = _viewModel;
        TaskChart.Drawable = _chartDrawable;
    }

    /// <summary>
    /// The stored email, not the displayed one. The two are the same today, but
    /// copying should always hand over the record rather than whatever the card
    /// happens to be rendering.
    /// </summary>
    private string RawEmail => _teacher?.Email ?? _user?.Email ?? string.Empty;

    /// <summary>
    /// The stored phone number. The card substitutes "No contact number" when
    /// this is blank, and copying that placeholder would be nonsense.
    /// </summary>
    private string RawContactNumber => _teacher?.Contact_number ?? _user?.Contact_number ?? string.Empty;

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        CloseProfileMenuImmediate();
        try
        {
            if (_teacher is not null)
            {
                var teacher = await _database.GetTeacherByIdAsync(_teacher.Teacher_id);
                if (teacher is not null)
                {
                    _teacher = teacher;
                    ApplyTeacher(teacher);
                }
                if (OverviewSection.IsVisible)
                    ApplySummary(await _database.GetTeacherProfileTaskSummaryAsync(_teacher.Teacher_id));
            }
            else if (_user is not null)
            {
                var user = await _database.GetUserByIdAsync(_user.User_id);
                if (user is not null)
                {
                    _user = user;
                    ApplyUser(user);
                }
                if (OverviewSection.IsVisible)
                    ApplySummary(await _database.GetUserProfileTaskSummaryAsync(_user.User_id));
            }
        }
        catch (Exception)
        {
            await UiAlertService.ShowAsync(this, "Profile unavailable", "The profile or task summary could not be loaded. Please try again.", "OK");
        }
    }

    private void ApplyUser(User user)
    {
        _viewModel.AvatarSeed = $"user-{user.User_id}";
        _viewModel.FullName = $"{user.First_name} {user.Last_name}".Trim();
        _viewModel.Email = user.Email;
        _viewModel.Contact_number = string.IsNullOrWhiteSpace(user.Contact_number) ? "No contact number" : user.Contact_number;
        _viewModel.Username = user.Username;
        _viewModel.Profile_photo = user.Profile_photo;
        _viewModel.Role = string.IsNullOrWhiteSpace(user.Role_name) ? "Staff" : user.Role_name;
        bool isDirector = string.Equals(user.Role_name, "Director", StringComparison.OrdinalIgnoreCase);
        bool isStaff = string.Equals(user.Role_name, "Staff", StringComparison.OrdinalIgnoreCase);
        bool isOwnDirectorProfile = !_returnToDirectory && isDirector;
        bool isOwnStaffProfile = !_returnToDirectory && isStaff;
        OverviewSection.IsVisible = !_returnToDirectory && !isDirector && !isStaff;
        DirectorProfileSection.IsVisible = isOwnDirectorProfile;
        StaffProfileSection.IsVisible = isOwnStaffProfile;
        TeacherDirectoryAccountSection.IsVisible = false;
        InformationHeadingLabel.Text = "Contact";
        if (isOwnDirectorProfile)
        {
            DirectorRoleLabel.Text = "Director";
            DirectorStatusLabel.Text = user.Is_active ? "Active" : "Inactive";
            DirectorUsernameLabel.Text = $"@{user.Username.TrimStart('@')}";
            DirectorMemberSinceLabel.Text = user.Account_created.ToString("MMM d, yyyy");
        }
        if (isOwnStaffProfile)
        {
            StaffRoleLabel.Text = "Staff";
            StaffStatusLabel.Text = user.Is_active ? "Active" : "Inactive";
            StaffStatusLabel.TextColor = user.Is_active ? AppColors.StatusSuccess : AppColors.StatusDanger;
            StaffUsernameLabel.Text = $"@{user.Username.TrimStart('@')}";
            StaffMemberSinceLabel.Text = user.Account_created.ToString("MMM d, yyyy");
            StaffLastActivityLabel.Text = "Not available";
        }
        _viewModel.TotalLabel = "Created";
        _viewModel.PendingLabel = "Ongoing";
        UpdateAccountManagement(user);
    }

    private void ApplyTeacher(Teachers teacher)
    {
        _viewModel.AvatarSeed = $"teacher-{teacher.Teacher_id}";
        _viewModel.FullName = $"{teacher.First_name} {teacher.Last_name}".Trim();
        _viewModel.Email = teacher.Email;
        _viewModel.Contact_number = string.IsNullOrWhiteSpace(teacher.Contact_number) ? "No contact number" : teacher.Contact_number;
        _viewModel.Username = teacher.Username;
        _viewModel.Profile_photo = teacher.Profile_photo;
        _viewModel.Role = "Teacher";
        bool directorDirectoryView = _returnToDirectory && UserSessionService.IsDirector;
        ProfilePageTitleLabel.Text = directorDirectoryView ? "Teacher Profile" : "Profile";
        InformationHeadingLabel.Text = "Contact";
        DirectorProfileSection.IsVisible = false;
        StaffProfileSection.IsVisible = false;
        TeacherDirectoryAccountSection.IsVisible = directorDirectoryView;
        OverviewSection.IsVisible = !directorDirectoryView;
        BottomTabBar.IsVisible = !directorDirectoryView;
        if (directorDirectoryView)
        {
            string status = _isManagedAccountDisabled
                ? "Disabled"
                : teacher.Is_active ? "Active" : "Pending";
            Color statusColor = _isManagedAccountDisabled
                ? AppColors.StatusDanger
                : teacher.Is_active ? AppColors.StatusSuccess : AppColors.StatusPending;
            TeacherDirectoryStatusLabel.Text = status;
            TeacherDirectoryStatusLabel.TextColor = statusColor;
            TeacherDirectoryStatusCard.BackgroundColor = _isManagedAccountDisabled
                ? AppColors.StatusDangerSurface
                : teacher.Is_active ? AppColors.StatusSuccessSurface : AppColors.StatusPendingSurface;
            TeacherDirectoryStatusCard.Stroke = _isManagedAccountDisabled
                ? AppColors.StatusDangerBorder
                : teacher.Is_active ? AppColors.StatusSuccessBorder : AppColors.StatusPendingBorder;
            TeacherDirectoryUsernameLabel.Text = $"@{teacher.Username.TrimStart('@')}";
            TeacherDirectoryMemberSinceLabel.Text = teacher.Account_created.ToString("MMM d, yyyy");
        }
        _viewModel.TotalLabel = "Total";
        _viewModel.PendingLabel = "Ongoing";
        AccountManagementSection.IsVisible = false;
    }

    private void UpdateAccountManagement(User user)
    {
        bool canManage = _returnToDirectory && UserSessionService.IsDirector;
        AccountManagementSection.IsVisible = canManage;
        if (!canManage)
            return;

        string role = string.IsNullOrWhiteSpace(user.Role_name) ? "Staff" : user.Role_name;
        bool isStaff = string.Equals(role, "Staff", StringComparison.OrdinalIgnoreCase);
        bool isPending = !user.Is_active && !_isManagedAccountDisabled;

        ManagedRoleLabel.Text = role;
        ManagedStatusLabel.Text = _isManagedAccountDisabled
            ? "Disabled"
            : user.Is_active ? "Active" : "Pending";
        ManagedStatusLabel.TextColor = _isManagedAccountDisabled
            ? AppColors.StatusDanger
            : user.Is_active ? AppColors.StatusSuccess : AppColors.StatusPending;
        ManagedStatusCard.BackgroundColor = _isManagedAccountDisabled
            ? AppColors.StatusDangerSurface
            : user.Is_active ? AppColors.StatusSuccessSurface : AppColors.StatusPendingSurface;
        ManagedStatusCard.Stroke = _isManagedAccountDisabled
            ? AppColors.StatusDangerBorder
            : user.Is_active ? AppColors.StatusSuccessBorder : AppColors.StatusPendingBorder;
        ManagedApprovalLabel.Text = isPending
            ? $"Pending · Requested {user.Account_created:MMM d, yyyy}"
            : $"Approved · Requested {user.Account_created:MMM d, yyyy}";

        PromoteToDirectorButton.IsVisible = isStaff && user.Is_active && !_isManagedAccountDisabled;
        ManagedAccountStateButton.IsVisible = isStaff && !isPending;
        bool showActions = isStaff && !isPending;
        ManagedActionsFooter.IsVisible = showActions;
        ProfileScrollView.Padding = showActions
            ? new Thickness(0, 0, 0, 140)
            : new Thickness(0);
        ManagedAccountStateButton.Text = _isManagedAccountDisabled
            ? "Enable account"
            : "Disable account";
        ManagedAccountStateButton.BackgroundColor = _isManagedAccountDisabled
            ? AppColors.StatusSuccessSurface
            : AppColors.ActionDanger;
        ManagedAccountStateButton.TextColor = _isManagedAccountDisabled
            ? AppColors.StatusSuccess
            : AppColors.TextInverse;
        ManagedAccountStateButton.BorderColor = _isManagedAccountDisabled
            ? AppColors.StatusSuccessBorder
            : AppColors.ActionDanger;
    }

    private async void OnPromoteToDirectorClicked(object sender, EventArgs e)
    {
        if (_user is null || !UserSessionService.IsDirector ||
            !string.Equals(_user.Role_name, "Staff", StringComparison.OrdinalIgnoreCase))
            return;

        string fullName = $"{_user.First_name} {_user.Last_name}".Trim();
        bool confirmed = await UiAlertService.ConfirmAsync(
            this,
            "Promote to Director",
            $"Promote {fullName} from Staff to Director? This gives the account approval and account-management permissions.",
            "Promote",
            "Cancel",
            stackedButtons: true);

        if (!confirmed)
            return;

        try
        {
            if (!await _database.PromoteStaffToDirectorAsync(_user.User_id))
            {
                await UiAlertService.ShowAsync(this, "Nothing changed",
                    "This account is no longer an active Staff account. Return to the directory and refresh.");
                return;
            }

            User? refreshed = await _database.GetUserByIdAsync(_user.User_id);
            if (refreshed is not null)
            {
                _user = refreshed;
                ApplyUser(refreshed);
            }

            await UiAlertService.ShowAsync(this, "Staff promoted",
                $"{fullName} is now a Director and will open the Director dashboard on the next sign-in.");
        }
        catch
        {
            await UiAlertService.ShowAsync(this, "Promotion failed",
                "This Staff account could not be promoted. Please try again.");
        }
    }

    private async void OnManagedAccountStateClicked(object sender, EventArgs e)
    {
        if (_user is null || !UserSessionService.IsDirector ||
            !string.Equals(_user.Role_name, "Staff", StringComparison.OrdinalIgnoreCase))
            return;

        bool disabling = !_isManagedAccountDisabled;
        string fullName = $"{_user.First_name} {_user.Last_name}".Trim();
        bool confirmed = await UiAlertService.ConfirmAsync(
            this,
            disabling ? "Disable account" : "Enable account",
            disabling
                ? $"Disable {fullName}'s account? They will not be able to sign in until it is enabled again."
                : $"Enable {fullName}'s account? They will be able to sign in again.",
            disabling ? "Disable" : "Enable",
            "Cancel");

        if (!confirmed)
            return;

        try
        {
            if (!await _database.SetAccountDisabledAsync(_user.User_id, "Staff", disabling))
            {
                await UiAlertService.ShowAsync(this, "Nothing changed",
                    "This account was already updated somewhere else. Return to the directory and refresh.");
                return;
            }

            _isManagedAccountDisabled = disabling;
            _user.Is_active = !disabling;
            UpdateAccountManagement(_user);
            await UiAlertService.ShowAsync(this,
                disabling ? "Account disabled" : "Account enabled",
                disabling
                    ? $"{fullName} can no longer sign in."
                    : $"{fullName} can sign in again.");
        }
        catch
        {
            await UiAlertService.ShowAsync(this,
                disabling ? "Disable failed" : "Enable failed",
                "This account could not be updated. Please try again.");
        }
    }

    private void ApplySummary(ProfileTaskSummary summary)
    {
        LastUpdatedLabel.Text = $"Updated {DateTime.Now:h:mm tt}";
        _viewModel.TotalCount = summary.Total;
        _viewModel.PendingCount = summary.Pending;
        _viewModel.OverdueCount = summary.Overdue;
        _viewModel.CompletedCount = summary.Completed;

        _chartDrawable.Completed = summary.Completed;
        _chartDrawable.Pending = summary.Pending;
        _chartDrawable.Overdue = summary.Overdue;

        string pending = _viewModel.PendingLabel;

        // The pie is decorative to a screen reader — every number in it is
        // already in the legend — so it gets one summary sentence rather than
        // a per-slice reading.
        SemanticProperties.SetDescription(TaskChart, summary.Total <= 0
            ? "Task overview: no tasks yet."
            : $"Task overview: {_viewModel.CompletedPercentage}% done. " +
              $"{summary.Completed} completed, {summary.Pending} {pending.ToLowerInvariant()}, {summary.Overdue} overdue.");

        // Grouping each legend row into one node stops a screen reader spelling
        // out a decorative square and then a bare number.
        SemanticProperties.SetDescription(CompletedLegendRow, $"Completed, {summary.Completed} tasks");
        SemanticProperties.SetDescription(PendingLegendRow, $"{pending}, {summary.Pending} tasks");
        SemanticProperties.SetDescription(OverdueLegendRow, $"Overdue, {summary.Overdue} tasks");

        TaskChart.Invalidate();
    }

    /// <summary>
    /// Sizes the pie against the row it sits in.
    ///
    /// A fixed square cannot serve both ends of the range: 140dp crowds the
    /// legend on a 320dp phone, and looks marooned beside a tablet's worth of
    /// whitespace. The pie instead takes a share of the row and is clamped at
    /// both ends — below ~112dp three wedges stop being tellable apart, above
    /// ~168dp it starts to dominate a page that is mostly text.
    ///
    /// The row's own width does not depend on the chart (its second column is
    /// star-sized and absorbs the remainder), so writing back a width here
    /// cannot feed a layout loop. The equality guard covers the repeat
    /// SizeChanged that some platforms raise anyway.
    /// </summary>
    private void OnTaskOverviewSizeChanged(object? sender, EventArgs e)
    {
        if (sender is not VisualElement row || row.Width <= 0)
            return;

        double size = Math.Round(Math.Clamp(row.Width * 0.42, 112, 168));
        if (Math.Abs(TaskChart.WidthRequest - size) < 0.5)
            return;

        TaskChart.WidthRequest = size;
        TaskChart.HeightRequest = size;
    }

    private async void OnProfileMenuClicked(object sender, EventArgs e)
    {
        if (ProfileMenuOverlay.IsVisible)
            await CloseProfileMenuAsync();
        else
            await OpenProfileMenuAsync();
    }

    private void SetProfileMenuButtonState(bool active)
    {
        ProfileMenuButton.BackgroundColor = active ? AppColors.TextSecondary : Colors.Transparent;
        if (active)
            ProfileEditButton.BackgroundColor = Colors.Transparent;
        ProfileMenuButton.Source = "dotmenu.png";
    }

    private void OnProfileMenuPressed(object sender, EventArgs e) =>
        SetProfileMenuButtonState(active: true);

    private void OnProfileMenuReleased(object sender, EventArgs e)
    {
        if (!ProfileMenuOverlay.IsVisible)
            SetProfileMenuButtonState(active: false);
    }

    private void SetProfileEditButtonState(bool active)
    {
        ProfileEditButton.BackgroundColor = active ? AppColors.TextSecondary : Colors.Transparent;
        if (active)
            ProfileMenuButton.BackgroundColor = Colors.Transparent;
        ProfileEditButton.Source = "profiledit.png";
    }

    private void OnProfileEditPressed(object sender, EventArgs e) =>
        SetProfileEditButtonState(active: true);

    private void OnProfileEditReleased(object sender, EventArgs e) =>
        SetProfileEditButtonState(active: false);

    private async void OnProfileEditClicked(object sender, EventArgs e)
    {
        SetProfileEditButtonState(active: true);
        await OpenEditProfileAsync();
        SetProfileEditButtonState(active: false);
    }

    private void OnProfileBackPressed(object sender, EventArgs e)
    {
        ProfileBackButton.BackgroundColor = AppColors.TextSecondary;
        ProfileBackButton.Source = "whitebackicon.png";
    }

    private void OnProfileBackReleased(object sender, EventArgs e)
    {
        ProfileBackButton.BackgroundColor = AppColors.SurfaceBase;
        ProfileBackButton.Source = "backicon.png";
    }

    private async void OnProfileBackClicked(object sender, EventArgs e)
    {
        if (_returnToDirectory)
            await ReturnToDirectoryAsync();
    }

    private async System.Threading.Tasks.Task ReturnToDirectoryAsync()
    {
        DashboardFlyoutPage? flyout = FindDashboardFlyout();
        if (flyout is not null)
        {
            flyout.ShowDetail(new TeachersPage());
            return;
        }

        if (Navigation.NavigationStack.Count > 1)
        {
            await Navigation.PopAsync(false);
        }
    }

    private DashboardFlyoutPage? FindDashboardFlyout()
    {
        Element? element = this;
        while (element is not null)
        {
            if (element is DashboardFlyoutPage flyout)
                return flyout;

            element = element.Parent;
        }

        return Window?.Page as DashboardFlyoutPage ?? DashboardFlyoutPage.Current;
    }

    private async System.Threading.Tasks.Task OpenProfileMenuAsync()
    {
        if (_isProfileMenuAnimating || ProfileMenuOverlay.IsVisible)
            return;

        _isProfileMenuAnimating = true;
        SetProfileMenuButtonState(active: true);
        ProfileMenuOverlay.IsVisible = true;
        ProfileMenuBackdrop.Opacity = Motion.ReduceMotion ? 1 : 0;
        ProfileMenuSheet.TranslationY = Motion.ReduceMotion ? 0 : 64;
        ProfileMenuSheet.Opacity = Motion.ReduceMotion ? 1 : 0.96;

        if (!Motion.ReduceMotion)
        {
            await System.Threading.Tasks.Task.WhenAll(
                ProfileMenuBackdrop.FadeTo(1, Motion.Fast, Motion.Enter),
                ProfileMenuSheet.FadeTo(1, Motion.Fast, Motion.Enter),
                ProfileMenuSheet.TranslateTo(0, 0, Motion.Base, Motion.Emphasis));
        }

        _isProfileMenuAnimating = false;
    }

    private async System.Threading.Tasks.Task CloseProfileMenuAsync()
    {
        if (_isProfileMenuAnimating || !ProfileMenuOverlay.IsVisible)
            return;

        _isProfileMenuAnimating = true;

        if (!Motion.ReduceMotion)
        {
            await System.Threading.Tasks.Task.WhenAll(
                ProfileMenuBackdrop.FadeTo(0, Motion.Fast, Motion.Exit),
                ProfileMenuSheet.FadeTo(0.96, Motion.Fast, Motion.Exit),
                ProfileMenuSheet.TranslateTo(0, 52, Motion.Fast, Motion.Exit));
        }

        CloseProfileMenuImmediate();
        _isProfileMenuAnimating = false;
    }

    private void CloseProfileMenuImmediate()
    {
        ProfileMenuOverlay.IsVisible = false;
        ProfileMenuBackdrop.Opacity = 1;
        ProfileMenuSheet.Opacity = 1;
        ProfileMenuSheet.TranslationY = 0;
        SetProfileMenuButtonState(active: false);
        _isProfileMenuAnimating = false;
    }

    protected override bool OnBackButtonPressed()
    {
        if (ProfileMenuOverlay.IsVisible)
        {
            _ = CloseProfileMenuAsync();
            return true;
        }

        if (_returnToDirectory)
        {
            _ = ReturnToDirectoryAsync();
            return true;
        }

        return base.OnBackButtonPressed();
    }

    private async void OnProfileMenuBackdropTapped(object sender, TappedEventArgs e) =>
        await CloseProfileMenuAsync();

    private async void OnCloseProfileMenuClicked(object sender, EventArgs e) =>
        await CloseProfileMenuAsync();

    private async void OnCopyEmailOptionTapped(object sender, TappedEventArgs e) =>
        await CopyFromProfileMenuAsync(RawEmail, "Email copied");

    private async void OnCopyPhoneOptionTapped(object sender, TappedEventArgs e) =>
        await CopyFromProfileMenuAsync(RawContactNumber, "Phone number copied");

    private async System.Threading.Tasks.Task CopyFromProfileMenuAsync(string? value, string confirmation)
    {
        await CloseProfileMenuAsync();
        await CopyAsync(value, confirmation);
    }

    private async void OnCopyEmailTapped(object sender, TappedEventArgs e) =>
        await CopyAsync(RawEmail, "Email copied");

    private async void OnCopyPhoneTapped(object sender, TappedEventArgs e) =>
        await CopyAsync(RawContactNumber, "Phone number copied");

    private async void OnChangePasswordTapped(object sender, TappedEventArgs e) =>
        await Navigation.PushModalAsync(new ForgotPasswordPage(), false);

    private async System.Threading.Tasks.Task OpenEditProfileAsync()
    {
        if (_teacher is not null)
            await Navigation.PushModalAsync(new EditProfilePage(_teacher), false);
        else if (_user is not null)
            await Navigation.PushModalAsync(new EditProfilePage(_user), false);
    }

    /// <summary>
    /// Copies a value and says so. A blank field reports that instead of
    /// silently putting an empty string on the clipboard.
    /// </summary>
    private async System.Threading.Tasks.Task CopyAsync(string? value, string confirmation)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            await ShowToastAsync("Nothing to copy");
            return;
        }

        try
        {
            await Clipboard.Default.SetTextAsync(value.Trim());
            await ShowToastAsync(confirmation);
        }
        catch (Exception)
        {
            await ShowToastAsync("Couldn't copy");
        }
    }

    /// <summary>
    /// Fades a short confirmation in over the content and takes it away again.
    /// Reduced-motion users get the same message without the fade.
    /// </summary>
    private async System.Threading.Tasks.Task ShowToastAsync(string message)
    {
        ToastLabel.Text = message;
        Toast.IsVisible = true;

        if (Motion.ReduceMotion)
        {
            Toast.Opacity = 1;
            await System.Threading.Tasks.Task.Delay(1600);
            Toast.Opacity = 0;
            Toast.IsVisible = false;
            return;
        }

        await Toast.FadeTo(1, Motion.Fast, Motion.Enter);
        await System.Threading.Tasks.Task.Delay(1400);
        await Toast.FadeTo(0, Motion.Base, Motion.Exit);
        Toast.IsVisible = false;
    }

    private void OnNotificationTapped(object sender, TappedEventArgs e)
    {
        if (_teacher is not null)
            DashboardFlyoutPage.Current?.ShowNotification(_teacher);
        else
            DashboardFlyoutPage.Current?.ShowNotification(_user);
    }

    private void OnTasksTapped(object sender, TappedEventArgs e) => DashboardFlyoutPage.Current?.ShowTasks();
}
