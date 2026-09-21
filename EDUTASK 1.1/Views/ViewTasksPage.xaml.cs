using EDUTASK_1._1.Helpers;
using EDUTASK_1._1.ViewModels;
using EDUTASK_1._1.Views.Base;
using AdministratorTaskItem = EDUTASK_1._1.Models.AdministratorTaskItem;
using System.Windows.Input;

namespace EDUTASK_1._1.Views;

public partial class ViewTasksPage : EduTaskPage
{
    private const string StatusAll = "All Task";
    private const string StatusPending = "Pending";
    private const string StatusCompleted = "Completed";
    private const string StatusOverdue = "Overdue";

    private const string TeacherAll = "All Teacher";

    private const string PeriodWeek = "This Week";
    private const string PeriodMonth = "This Month";
    private const string PeriodYear = "This Year";
    private const string PeriodAll = "All Time";

    private readonly bool? _filterCompleted;
    private readonly bool _returnToSummaryReport;
    private readonly TaskListViewModel _viewModel;

    // Everything loaded from the database. The bound collection holds only what
    // survives the three filters, so the source list has to be kept separately.
    private readonly List<AdministratorTaskItem> _allTasks = [];

    private DropdownController _statusDropdown = null!;
    private DropdownController _teacherDropdown = null!;
    private DropdownController _periodDropdown = null!;

    private string _status = StatusAll;
    private string _teacher = TeacherAll;
    private string _period = PeriodAll;

    public ViewTasksPage() : this(null, false)
    {
    }

    public ViewTasksPage(bool? filterCompleted) : this(filterCompleted, false)
    {
    }

    public ViewTasksPage(bool? filterCompleted, bool returnToSummaryReport)
    {
        InitializeComponent();
        _filterCompleted = filterCompleted;
        _returnToSummaryReport = returnToSummaryReport;
        _viewModel = new TaskListViewModel(this);
        _viewModel.RefreshCommand = new Command(async () => await RefreshAsync());
        BindingContext = _viewModel;

        InitialiseFilters();
    }

    private void InitialiseFilters()
    {
        _statusDropdown = new DropdownController(
            FilterRow, StatusPill, StatusPillChevron, StatusDropdownPanel,
            StatusPillLabel, "Filter by status");
        _statusDropdown.SetOptions(StatusOptionsHost,
            [StatusAll, StatusPending, StatusCompleted, StatusOverdue]);
        _statusDropdown.SelectionChanged += (_, value) => { _status = value; ApplyFilters(); };
        _statusDropdown.SetInitialSelection(StatusAll);
        FilterButtonState.TrackDropdown(
            _statusDropdown, StatusPill, StatusAll, StatusPillLabel, StatusPillChevron);

        _teacherDropdown = new DropdownController(
            FilterRow, TeacherPill, TeacherPillChevron, TeacherDropdownPanel,
            TeacherPillLabel, "Filter by teacher");
        _teacherDropdown.SetOptions(TeacherOptionsHost, [TeacherAll]);
        _teacherDropdown.SelectionChanged += (_, value) => { _teacher = value; ApplyFilters(); };
        _teacherDropdown.SetInitialSelection(TeacherAll);
        FilterButtonState.TrackDropdown(
            _teacherDropdown, TeacherPill, TeacherAll, TeacherPillLabel, TeacherPillChevron);

        _periodDropdown = new DropdownController(
            FilterRow, PeriodPill, PeriodPillChevron, PeriodDropdownPanel,
            PeriodPillLabel, "Filter by period");
        _periodDropdown.SetOptions(PeriodOptionsHost,
            [PeriodAll, PeriodWeek, PeriodMonth, PeriodYear]);
        _periodDropdown.SelectionChanged += (_, value) => { _period = value; ApplyFilters(); };
        _periodDropdown.SetInitialSelection(PeriodAll);
        FilterButtonState.TrackDropdown(
            _periodDropdown, PeriodPill, PeriodAll, PeriodPillLabel, PeriodPillChevron);
    }

    /// <summary>
    /// Fills a pill while its panel is open and returns it to the outlined
    /// resting look when it closes, so it is obvious which filter is being
    /// changed. Applied to all three, including "All Task" — none of them is
    /// permanently filled.
    /// </summary>
    private static void TrackActiveState(DropdownController dropdown, Border pill, Label label, Label chevron)
    {
        dropdown.Opened += (_, _) => SetPillActive(pill, label, chevron, true);
        dropdown.Closed += (_, _) => SetPillActive(pill, label, chevron, false);

        // The controller repaints the display label on selection; keep it in
        // step with whichever look the pill is currently wearing.
        dropdown.SelectedLabelColor = AppColors.TextPrimary;
        dropdown.PlaceholderLabelColor = AppColors.TextPrimary;

        SetPillActive(pill, label, chevron, false);
    }

    private static void SetPillActive(Border pill, Label label, Label chevron, bool active)
    {
        pill.BackgroundColor = active ? AppColors.Brand800 : AppColors.SurfaceBase;
        pill.Stroke = active ? AppColors.Brand800 : AppColors.BorderStrong;

        var foreground = active ? AppColors.TextInverse : AppColors.TextPrimary;
        label.TextColor = foreground;
        chevron.TextColor = foreground;
        label.FontAttributes = active ? FontAttributes.Bold : FontAttributes.None;
    }

    // Opening one filter closes the other two, so only a single panel is ever
    // over the list.
    private async void OnStatusFilterTapped(object sender, TappedEventArgs e)
    {
        _teacherDropdown.CloseImmediate();
        _periodDropdown.CloseImmediate();
        await _statusDropdown.ToggleAsync();
    }

    private async void OnTeacherFilterTapped(object sender, TappedEventArgs e)
    {
        _statusDropdown.CloseImmediate();
        _periodDropdown.CloseImmediate();
        await _teacherDropdown.ToggleAsync();
    }

    private async void OnPeriodFilterTapped(object sender, TappedEventArgs e)
    {
        _statusDropdown.CloseImmediate();
        _teacherDropdown.CloseImmediate();
        await _periodDropdown.ToggleAsync();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        SetBusy(true);
        try
        {
            await _viewModel.LoadAsync(_filterCompleted);

            _allTasks.Clear();
            _allTasks.AddRange(_viewModel.Tasks);

            RebuildTeacherOptions();
            ApplyFilters();
        }
        finally
        {
            SetBusy(false);
            TaskRefreshView.IsRefreshing = false;
        }
    }

    /// <summary>
    /// Rebuilds the teacher list from whatever came back, keeping the current
    /// choice if that teacher still has tasks.
    /// </summary>
    private void RebuildTeacherOptions()
    {
        var teachers = _allTasks
            .Select(task => task.TeacherName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase);

        _teacherDropdown.SetOptions(TeacherOptionsHost, new[] { TeacherAll }.Concat(teachers));

        var stillPresent = _teacher == TeacherAll ||
            _allTasks.Any(task => task.TeacherName.Equals(_teacher, StringComparison.OrdinalIgnoreCase));

        if (!stillPresent)
            _teacher = TeacherAll;

        _teacherDropdown.SetInitialSelection(_teacher);
        FilterButtonState.Apply(
            TeacherPill,
            !string.Equals(_teacher, TeacherAll, StringComparison.Ordinal),
            TeacherPillLabel,
            TeacherPillChevron);
    }

    private void ApplyFilters()
    {
        var (start, end) = GetDateRange();

        var filtered = _allTasks.Where(task =>
            MatchesStatus(task) &&
            MatchesTeacher(task) &&
            MatchesPeriod(task, start, end));

        _viewModel.Tasks.Clear();
        foreach (var task in filtered)
            _viewModel.Tasks.Add(task);

        EmptyStateMessageLabel.Text = _status == StatusAll && _teacher == TeacherAll && _period == PeriodAll
            ? "Create a task to get started."
            : "Nothing matches these filters yet.";
    }

    private bool MatchesStatus(AdministratorTaskItem task) => _status switch
    {
        StatusCompleted => task.Status == "Completed",
        StatusOverdue => task.IsOverdue,
        // Pending covers anything not finished and not yet late, so an overdue
        // task appears under Overdue rather than in both buckets.
        StatusPending => task.Status != "Completed" && !task.IsOverdue,
        _ => true,
    };

    private bool MatchesTeacher(AdministratorTaskItem task) =>
        _teacher == TeacherAll ||
        task.TeacherName.Equals(_teacher, StringComparison.OrdinalIgnoreCase);

    private static bool MatchesPeriod(AdministratorTaskItem task, DateTime? start, DateTime? end)
    {
        if (!start.HasValue)
            return true;

        return task.Deadline.HasValue &&
               task.Deadline.Value.Date >= start.Value &&
               task.Deadline.Value.Date <= end!.Value;
    }

    private (DateTime? Start, DateTime? End) GetDateRange()
    {
        var today = DateTime.Today;
        var mondayOffset = ((int)today.DayOfWeek + 6) % 7;

        return _period switch
        {
            PeriodWeek => (today.AddDays(-mondayOffset), today.AddDays(6 - mondayOffset)),
            PeriodMonth => (new DateTime(today.Year, today.Month, 1),
                            new DateTime(today.Year, today.Month, 1).AddMonths(1).AddDays(-1)),
            PeriodYear => (new DateTime(today.Year, 1, 1), new DateTime(today.Year, 12, 31)),
            _ => (null, null),
        };
    }

    private void SetBusy(bool busy)
    {
        LoadingIndicator.IsVisible = busy;
        LoadingIndicator.IsRunning = busy;
    }

    private void OnViewTasksBackPressed(object sender, EventArgs e)
    {
        ViewTasksBackButton.BackgroundColor = AppColors.TextSecondary;
        ViewTasksBackButton.Source = "whitebackicon.png";
    }

    private void OnViewTasksBackReleased(object sender, EventArgs e)
    {
        ViewTasksBackButton.BackgroundColor = AppColors.SurfaceBase;
        ViewTasksBackButton.Source = "backicon.png";
    }

    private void OnBackClicked(object sender, EventArgs e)
    {
        if (_returnToSummaryReport)
        {
            DashboardFlyoutPage.Current?.ShowDetail(new TaskSummaryPage());
            return;
        }

        if (Navigation.NavigationStack.Count > 1)
            _ = Navigation.PopAsync(false);
        else
            DashboardFlyoutPage.Current?.ShowTasks();
    }

    private async void OnTaskSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not AdministratorTaskItem task)
            return;

        TaskCollectionView.SelectedItem = null;
        await Navigation.PushModalAsync(new EditTaskPage(task.Task_id), false);
    }
}
