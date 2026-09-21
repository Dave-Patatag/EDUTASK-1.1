using EDUTASK_1._1.Models;
using EDUTASK_1._1.Helpers;
using EDUTASK_1._1.Views.Base;
using EDUTASK_1._1.Services;

namespace EDUTASK_1._1.Views;

public partial class TeachersPage : EduTaskPage
{
    // The three lists the page can show. They are also the page menu's options,
    // so the active list remains highlighted.
    private const string ViewActive = "Active Accounts";
    private const string ViewRequests = "Request Approval";
    private const string ViewDisabled = "Disabled Accounts";

    // The scope filter narrows whichever list is showing.
    private const string ScopeAll = "All";
    private const string ScopeTeachers = "Teachers";
    private const string ScopeStaff = "Staff";

    private readonly DatabaseService _database = new();
    private readonly bool _isDirector = UserSessionService.IsDirector;

    private DropdownController _scopeDropdown = null!;
    private ActionMenuController? _rowActionMenu;

    private List<DirectoryCardItem> _accounts = [];
    private string _view = ViewActive;
    private string _scope = ScopeAll;
    private bool _isPageMenuAnimating;

    public TeachersPage(bool showApprovalRequests = false)
    {
        if (showApprovalRequests && _isDirector)
            _view = ViewRequests;

        InitializeComponent();
        InitialiseMenus();

        // A teacher or staff member sees the active roster and nothing else:
        // approvals and disabling are the Director's, so the menu that offers
        // them is not shown at all rather than shown and refused.
        MenuButton.IsVisible = _isDirector;
        ScopePill.IsVisible = _isDirector;

        if (!_isDirector)
            HeaderTitleLabel.Text = "Teachers";
    }

    private void InitialiseMenus()
    {
        _scopeDropdown = new DropdownController(
            FilterRow, ScopePill, ScopePillChevron, ScopeDropdownPanel,
            ScopePillLabel, ScopeAll);

        _scopeDropdown.SetOptions(ScopeOptionsHost, [ScopeAll, ScopeTeachers, ScopeStaff]);
        _scopeDropdown.SelectionChanged += (_, value) =>
        {
            _scope = value;
            ApplyFilters();
        };
        _scopeDropdown.SetInitialSelection(ScopeAll);
        FilterButtonState.TrackDropdown(
            _scopeDropdown, ScopePill, ScopeAll, ScopePillLabel, ScopePillChevron);

        UpdatePageMenuSelection();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async void OnScopeFilterTapped(object sender, TappedEventArgs e)
    {
        ClosePageMenuImmediate();
        _rowActionMenu?.CloseImmediate();
        await _scopeDropdown.ToggleAsync();
    }

    private static void SetHeaderButtonState(ImageButton button, bool active, bool isBackButton)
    {
        button.BackgroundColor = active ? AppColors.TextSecondary : AppColors.SurfaceBase;
        button.Source = isBackButton
            ? active ? "whitebackicon.png" : "backicon.png"
            : "dotmenu.png";
    }

    private void OnHeaderMenuPressed(object sender, EventArgs e) =>
        SetHeaderButtonState(MenuButton, active: true, isBackButton: false);

    private void OnHeaderMenuReleased(object sender, EventArgs e)
    {
        if (!PageMenuOverlay.IsVisible)
            SetHeaderButtonState(MenuButton, active: false, isBackButton: false);
    }

    private async void OnPageMenuClicked(object sender, EventArgs e)
    {
        _scopeDropdown.CloseImmediate();
        _rowActionMenu?.CloseImmediate();
        await OpenPageMenuAsync();
    }

    private async System.Threading.Tasks.Task OpenPageMenuAsync()
    {
        if (_isPageMenuAnimating || PageMenuOverlay.IsVisible)
            return;

        _isPageMenuAnimating = true;
        UpdatePageMenuSelection();
        SetHeaderButtonState(MenuButton, active: true, isBackButton: false);
        PageMenuOverlay.IsVisible = true;
        PageMenuBackdrop.Opacity = Motion.ReduceMotion ? 1 : 0;
        PageMenuSheet.TranslationY = Motion.ReduceMotion ? 0 : 48;
        PageMenuSheet.Opacity = Motion.ReduceMotion ? 1 : 0.96;

        if (!Motion.ReduceMotion)
        {
            await System.Threading.Tasks.Task.WhenAll(
                PageMenuBackdrop.FadeTo(1, Motion.Fast, Motion.Enter),
                PageMenuSheet.FadeTo(1, Motion.Fast, Motion.Enter),
                PageMenuSheet.TranslateTo(0, 0, Motion.Base, Motion.Emphasis));
        }

        _isPageMenuAnimating = false;
    }

    private async System.Threading.Tasks.Task ClosePageMenuAsync()
    {
        if (_isPageMenuAnimating || !PageMenuOverlay.IsVisible)
            return;

        _isPageMenuAnimating = true;

        if (!Motion.ReduceMotion)
        {
            await System.Threading.Tasks.Task.WhenAll(
                PageMenuBackdrop.FadeTo(0, Motion.Fast, Motion.Exit),
                PageMenuSheet.FadeTo(0.96, Motion.Fast, Motion.Exit),
                PageMenuSheet.TranslateTo(0, 40, Motion.Fast, Motion.Exit));
        }

        ClosePageMenuImmediate();
        _isPageMenuAnimating = false;
    }

    private void ClosePageMenuImmediate()
    {
        PageMenuOverlay.IsVisible = false;
        PageMenuBackdrop.Opacity = 1;
        PageMenuSheet.Opacity = 1;
        PageMenuSheet.TranslationY = 0;
        SetHeaderButtonState(MenuButton, active: false, isBackButton: false);
    }

    private void UpdatePageMenuSelection()
    {
        bool activeSelected = _view == ViewActive;
        bool requestsSelected = _view == ViewRequests;
        bool disabledSelected = _view == ViewDisabled;

        ActiveAccountsOption.BackgroundColor = Colors.Transparent;
        RegisterRequestsOption.BackgroundColor = Colors.Transparent;
        DisabledAccountsOption.BackgroundColor = Colors.Transparent;

        ActiveAccountsOptionTitle.TextColor = activeSelected ? AppColors.Accent600 : AppColors.TextPrimary;
        RegisterRequestsOptionTitle.TextColor = requestsSelected ? AppColors.Accent600 : AppColors.TextPrimary;
        DisabledAccountsOptionTitle.TextColor = disabledSelected ? AppColors.Accent600 : AppColors.TextPrimary;
    }

    private async System.Threading.Tasks.Task SelectViewFromSheetAsync(string view)
    {
        await ClosePageMenuAsync();
        await ShowViewAsync(view);
        UpdatePageMenuSelection();
    }

    private async void OnPageMenuBackdropTapped(object sender, TappedEventArgs e) =>
        await ClosePageMenuAsync();

    private async void OnCloseMenuSheetTapped(object sender, EventArgs e) =>
        await ClosePageMenuAsync();

    private async void OnActiveAccountsTapped(object sender, TappedEventArgs e) =>
        await SelectViewFromSheetAsync(ViewActive);

    private async void OnRegisterRequestsTapped(object sender, TappedEventArgs e) =>
        await SelectViewFromSheetAsync(ViewRequests);

    private async void OnDisabledAccountsTapped(object sender, TappedEventArgs e) =>
        await SelectViewFromSheetAsync(ViewDisabled);

    protected override bool OnBackButtonPressed()
    {
        if (_rowActionMenu?.IsOpen == true)
        {
            _ = _rowActionMenu.CloseAsync();
            return true;
        }

        if (PageMenuOverlay.IsVisible)
        {
            _ = ClosePageMenuAsync();
            return true;
        }

        return base.OnBackButtonPressed();
    }

    private async System.Threading.Tasks.Task ShowViewAsync(string view)
    {
        if (_view == view)
            return;

        _view = view;
        await LoadAsync();
    }

    /// <summary>
    /// Loads whichever list the page menu is pointing at. All three go through
    /// one path so the scope filter, the search box and the empty state behave
    /// the same wherever you are.
    /// </summary>
    private async System.Threading.Tasks.Task LoadAsync()
    {
        _rowActionMenu?.CloseImmediate();

        try
        {
            _accounts = _view switch
            {
                ViewRequests when _isDirector =>
                    (await _database.GetPendingAccountsAsync())
                        .Select(request => DirectoryCardItem.FromRequest(request, _isDirector))
                        .ToList(),

                ViewDisabled when _isDirector =>
                    (await _database.GetDisabledDirectoryAsync(includeStaff: true))
                        .Select(account => DirectoryCardItem.FromAccount(account, _isDirector))
                        .ToList(),

                _ => (await _database.GetActiveDirectoryAsync(includeStaff: _isDirector))
                        .Select(account => DirectoryCardItem.FromAccount(account, _isDirector))
                        .ToList()
            };

            ApplyFilters();
        }
        catch
        {
            await UiAlertService.ShowAsync(this, "Directory unavailable",
                "We couldn't load the teacher and staff list. Please try again.");
        }
        finally
        {
            DirectoryRefresh.IsRefreshing = false;
        }
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e) => ApplyFilters();

    private void ApplyFilters()
    {
        string search = SearchEntry.Text?.Trim() ?? string.Empty;

        IEnumerable<DirectoryCardItem> rows = _accounts;

        rows = _scope switch
        {
            ScopeTeachers => rows.Where(row => row.AccountType == "Teacher"),
            ScopeStaff => rows.Where(row => row.AccountType == "Staff"),
            _ => rows
        };

        if (!string.IsNullOrWhiteSpace(search))
            rows = rows.Where(row =>
                row.FullName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                row.Username.Contains(search, StringComparison.OrdinalIgnoreCase));

        List<DirectoryCardItem> filteredAccounts = rows.ToList();
        DirectoryView.ItemsSource = filteredAccounts;
        DirectoryEmptyState.IsVisible = filteredAccounts.Count == 0;
        UpdateEmptyState(search);
    }

    private void UpdateEmptyState(string search)
    {
        EmptyStateImage.IsVisible =
            string.IsNullOrWhiteSpace(search) && _view == ViewRequests;

        if (!string.IsNullOrWhiteSpace(search))
        {
            EmptyStateTitleLabel.Text = "No matches";
            EmptyStateMessageLabel.Text = $"Nobody here matches “{search}”.";
            return;
        }

        (EmptyStateTitleLabel.Text, EmptyStateMessageLabel.Text) = _view switch
        {
            ViewRequests => ("No requests", "Nobody is waiting for an account right now."),
            ViewDisabled => ("No disabled accounts", "Every account in the directory is active."),
            _ when _scope == ScopeTeachers => ("No teachers", "No active teacher accounts yet."),
            _ when _scope == ScopeStaff => ("No staff", "No active staff accounts yet."),
            _ => ("No accounts found", _isDirector
                ? "No active teacher or staff accounts yet."
                : "No active teacher accounts yet.")
        };
    }

    private async void OnRefresh(object sender, EventArgs e) => await LoadAsync();

    /// <summary>
    /// Back out of the directory.
    ///
    /// The page is now shown as a detail root rather than pushed onto a tab's
    /// stack, so there is usually nothing to pop — back means "return to the
    /// dashboard". The pop is kept for the case where something does push this
    /// page, so the button never becomes a no-op.
    /// </summary>
    private async void OnBack(object sender, EventArgs e)
    {
        if (Navigation.NavigationStack.Count > 1)
        {
            await Navigation.PopAsync(false);
            return;
        }

        DashboardFlyoutPage.Current?.ShowTasks();
    }

    /// <summary>
    /// The card's overflow button. One account, one action: switch it off, or
    /// switch a disabled one back on. Both confirm first — a disabled account
    /// cannot sign in, so it is not a change to make on a stray tap.
    /// </summary>
    private async void OnRowMenuTapped(object sender, EventArgs e)
    {
        if (!_isDirector) return;
        if (sender is not ImageButton anchor ||
            anchor.CommandParameter is not DirectoryCardItem account)
            return;

        _scopeDropdown.CloseImmediate();
        ClosePageMenuImmediate();
        _rowActionMenu?.CloseImmediate();

        _rowActionMenu = new ActionMenuController(
            RowActionPanel, RowActionOptionsHost, anchor);
        Color? accountStateColor = account.IsDisabled
            ? null
            : AppColors.StatusDanger;
        _rowActionMenu.SetActions(
        [
            ("View Profile", () => OpenAccountProfileAsync(account), (Color?)null),
            (account.IsDisabled ? "Enable" : "Disable",
                async () => { await ChangeAccountStateAsync(account); }, accountStateColor)
        ]);
        await _rowActionMenu.OpenAsync();
    }

    private async void OnAccountCardTapped(object sender, TappedEventArgs e)
    {
        if (sender is not Border card ||
            card.BindingContext is not DirectoryCardItem account)
            return;

        await OpenAccountProfileAsync(account);
    }

    private async System.Threading.Tasks.Task OpenAccountProfileAsync(DirectoryCardItem account)
    {
        try
        {
            if (string.Equals(account.AccountType, "Teacher", StringComparison.OrdinalIgnoreCase))
            {
                Teachers? teacher = await _database.GetTeacherByIdAsync(account.AccountID);
                if (teacher is null)
                    throw new InvalidOperationException();

                await Navigation.PushModalAsync(
                    new AccountProfileDialog(
                        teacher,
                        isManagedAccountDisabled: _isDirector && account.IsDisabled,
                        onAccountStateChange: _isDirector
                            ? disabling => ChangeAccountStateAsync(account, disabling)
                            : null),
                    animated: false);
                return;
            }

            User? user = await _database.GetUserByIdAsync(account.AccountID);
            if (user is null)
                throw new InvalidOperationException();

            await Navigation.PushModalAsync(
                new AccountProfileDialog(
                    user,
                    isManagedAccountDisabled: account.IsDisabled,
                    onPromote: () => PromoteStaffFromProfileAsync(account),
                    onAccountStateChange: disabling => ChangeAccountStateAsync(account, disabling)),
                animated: false);
        }
        catch
        {
            await UiAlertService.ShowAsync(this, "Profile unavailable",
                "This account's profile could not be loaded. Please try again.");
        }
    }

    private async System.Threading.Tasks.Task<bool> ChangeAccountStateAsync(
        DirectoryCardItem account,
        bool? requestedDisabling = null)
    {
        bool disabling = requestedDisabling ?? !account.IsDisabled;
        bool isTeacher = string.Equals(
            account.AccountType, "Teacher", StringComparison.OrdinalIgnoreCase);

        if (disabling && isTeacher)
        {
            try
            {
                int unfinishedTaskCount = await _database
                    .GetTeacherUnfinishedTaskCountAsync(account.AccountID);
                if (unfinishedTaskCount > 0)
                {
                    await ShowUnfinishedTasksBlockAsync(account, unfinishedTaskCount);
                    return false;
                }
            }
            catch
            {
                await UiAlertService.ShowAsync(this, "Task check unavailable",
                    "The teacher's active tasks could not be checked, so the account was not disabled. Please try again.");
                return false;
            }
        }

        string message = disabling
            ? $"Disable {account.FullName}'s account? They will not be able to sign in until it is enabled again. Completed task history will be kept."
            : $"Enable {account.FullName}'s account? They will be able to sign in again.";

        bool confirmed = await UiAlertService.ConfirmAsync(this,
            disabling ? "Disable account" : "Enable account", message,
            disabling ? "Disable" : "Enable", "Cancel");

        if (!confirmed)
            return false;

        try
        {
            if (await _database.SetAccountDisabledAsync(account.AccountID, account.AccountType, disabling))
            {
                await LoadAsync();
                return true;
            }
            else
            {
                // The database guard can still block the write if another user
                // assigned work after the preflight check but before confirmation.
                if (disabling && isTeacher)
                {
                    int unfinishedTaskCount = await _database
                        .GetTeacherUnfinishedTaskCountAsync(account.AccountID);
                    if (unfinishedTaskCount > 0)
                    {
                        await ShowUnfinishedTasksBlockAsync(account, unfinishedTaskCount);
                        return false;
                    }
                }

                await UiAlertService.ShowAsync(this, "Nothing changed",
                    "That account was already updated somewhere else. Pull down to refresh.");
            }
        }
        catch
        {
            await UiAlertService.ShowAsync(this, disabling ? "Disable failed" : "Enable failed",
                "This account could not be updated. Please try again.");
        }

        return false;
    }

    private System.Threading.Tasks.Task ShowUnfinishedTasksBlockAsync(
        DirectoryCardItem account,
        int unfinishedTaskCount)
    {
        string taskLabel = unfinishedTaskCount == 1 ? "task" : "tasks";
        return UiAlertService.ShowAsync(this, "Reassign active tasks first",
            $"{account.FullName} still has {unfinishedTaskCount} unfinished {taskLabel}. Reassign or complete them from Active Task before disabling this account.");
    }

    private async System.Threading.Tasks.Task<bool> PromoteStaffFromProfileAsync(DirectoryCardItem account)
    {
        if (!_isDirector || account.IsDisabled || account.AccountType != "Staff")
            return false;

        bool confirmed = await UiAlertService.ConfirmAsync(this, "Promote to Director",
            $"Promote {account.FullName} from Staff to Director? This gives the account approval and account-management permissions.",
            "Promote", "Cancel", stackedButtons: true);
        if (!confirmed)
            return false;

        try
        {
            bool promoted = await _database.PromoteStaffToDirectorAsync(account.AccountID);
            if (promoted)
                await LoadAsync();
            else
                await UiAlertService.ShowAsync(this, "Nothing changed",
                    "This account is no longer an active Staff account. Refresh and try again.");
            return promoted;
        }
        catch
        {
            await UiAlertService.ShowAsync(this, "Promotion failed",
                "This Staff account could not be promoted. Please try again.");
            return false;
        }
    }

    private async void OnApprove(object sender, EventArgs e)
    {
        if (!_isDirector) return;
        if ((sender as Button)?.CommandParameter is not DirectoryCardItem account) return;

        bool confirmed = await UiAlertService.ConfirmAsync(this, "Approve account",
            $"Approve {account.FullName} as {account.AccountType}?", "Approve", "Cancel");

        if (!confirmed)
            return;

        try
        {
            if (await _database.ApproveAccountAsync(account.AccountID, account.AccountType))
                await LoadAsync();
        }
        catch
        {
            await UiAlertService.ShowAsync(this, "Approval failed",
                "This account could not be approved. Please try again.");
        }
    }
}
