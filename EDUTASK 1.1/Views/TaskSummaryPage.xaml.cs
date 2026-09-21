using EDUTASK_1._1.Models;
using EDUTASK_1._1.Helpers;
using EDUTASK_1._1.Views.Base;
using EDUTASK_1._1.Services;
using System.Collections.ObjectModel;
using System.Data;

namespace EDUTASK_1._1.Views;

public partial class TaskSummaryPage : EduTaskPage
{
    private readonly DatabaseService _database = new();
    private readonly List<ReportAssignment> _assignments = [];
    private readonly List<TaskSummaryItem> _allTasks = [];
    private readonly List<TaskSummaryItem> _periodTasks = [];
    private readonly CompletionRingDrawable _completionRing = new();
    public ObservableCollection<TaskSummaryItem> FilteredTasks { get; } = [];
    private bool _isPeriodSheetAnimating;
    private bool _loaded;
    private string? _selectedSummaryStatus;
    private string _period = PeriodAll;
    private string _periodBeforeCustom = PeriodAll;

    private DropdownController _taskTeacherDropdown = null!;
    private string _taskStatus = TaskStatusAll;
    private string _taskTeacher = TaskTeacherAll;

    private const string PeriodWeek = "This Week";
    private const string PeriodMonth = "This Month";
    private const string PeriodYear = "This Year";
    private const string PeriodAll = "All Time";
    private const string PeriodCustom = "Custom Date";

    private const string TaskStatusAll = "All Task";
    private const string TaskStatusPending = "Pending";
    private const string TaskStatusOngoing = "Ongoing";
    private const string TaskStatusCompleted = "Completed";
    private const string TaskStatusOverdue = "Overdue";
    private const string TaskTeacherAll = "All Teacher";
    private const string TaskPeriodAll = "All Time";

    public TaskSummaryPage()
    {
        InitializeComponent();
        BindingContext = this;
        CompletionRing.Drawable = _completionRing;
        StartDatePicker.Date = DateTime.Today.AddDays(-30);
        EndDatePicker.Date = DateTime.Today;
        UpdatePeriodSheetSelection();
        SetReportMenuState(active: false);
        InitialiseTaskFilters();
    }

    private void InitialiseTaskFilters()
    {
        _taskTeacherDropdown = new DropdownController(
            TaskFilterRow, TaskTeacherPill, TaskTeacherPillChevron,
            TaskTeacherDropdownPanel, TaskTeacherPillLabel, "Filter tasks by teacher");
        _taskTeacherDropdown.SetOptions(TaskTeacherOptionsHost, [TaskTeacherAll]);
        _taskTeacherDropdown.SelectionChanged += (_, value) =>
        {
            _taskTeacher = value;
            UpdateFilteredTasks();
        };
        _taskTeacherDropdown.SetInitialSelection(TaskTeacherAll);
        FilterButtonState.TrackDropdown(
            _taskTeacherDropdown, TaskTeacherPill, TaskTeacherAll,
            TaskTeacherPillLabel, TaskTeacherPillChevron);
        _taskTeacherDropdown.Opened += (_, _) => ApplyTeacherPillFill();
        _taskTeacherDropdown.Closed += (_, _) => ApplyTeacherPillFill();
        ApplyTeacherPillFill();

    }

    private void ApplyTeacherPillFill() =>
        FilterButtonState.Apply(
            TaskTeacherPill,
            active: true,
            TaskTeacherPillLabel,
            TaskTeacherPillChevron);

    private async void OnPeriodFilterTapped(object sender, TappedEventArgs e)
    {
        _taskTeacherDropdown.CloseImmediate();
        if (PeriodSheetOverlay.IsVisible)
            await ClosePeriodSheetAsync();
        else
            await OpenPeriodSheetAsync();
    }

    private async System.Threading.Tasks.Task OpenPeriodSheetAsync()
    {
        if (_isPeriodSheetAnimating || PeriodSheetOverlay.IsVisible)
            return;

        _isPeriodSheetAnimating = true;
        UpdatePeriodSheetSelection();
        SetReportMenuState(active: true);
        PeriodSheetOverlay.IsVisible = true;
        PeriodSheetBackdrop.Opacity = Motion.ReduceMotion ? 1 : 0;
        PeriodSheet.TranslationY = Motion.ReduceMotion ? 0 : -8;
        PeriodSheet.Opacity = Motion.ReduceMotion ? 1 : 0;

        if (!Motion.ReduceMotion)
        {
            await System.Threading.Tasks.Task.WhenAll(
                PeriodSheetBackdrop.FadeTo(1, Motion.Fast, Motion.Enter),
                PeriodSheet.FadeTo(1, Motion.Fast, Motion.Enter),
                PeriodSheet.TranslateTo(0, 0, Motion.Base, Motion.Emphasis));
        }

        _isPeriodSheetAnimating = false;
    }

    private async System.Threading.Tasks.Task ClosePeriodSheetAsync()
    {
        if (_isPeriodSheetAnimating || !PeriodSheetOverlay.IsVisible)
            return;

        _isPeriodSheetAnimating = true;
        if (!Motion.ReduceMotion)
        {
            await System.Threading.Tasks.Task.WhenAll(
                PeriodSheetBackdrop.FadeTo(0, Motion.Fast, Motion.Exit),
                PeriodSheet.FadeTo(0, Motion.Fast, Motion.Exit),
                PeriodSheet.TranslateTo(0, -6, Motion.Fast, Motion.Exit));
        }

        ClosePeriodSheetImmediate();
    }

    private void ClosePeriodSheetImmediate()
    {
        PeriodSheetOverlay.IsVisible = false;
        PeriodSheetBackdrop.Opacity = 1;
        PeriodSheet.Opacity = 1;
        PeriodSheet.TranslationY = 0;
        SetReportMenuState(active: false);
        _isPeriodSheetAnimating = false;
    }

    private void SetReportMenuState(bool active)
    {
        FilterButton.BackgroundColor = AppColors.Brand800;
        PeriodFilterLabel.TextColor = AppColors.TextInverse;
    }

    private async void OnPeriodOptionClicked(object sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: string period })
        {
            await ClosePeriodSheetAsync();
            OnPeriodSelected(this, period);
        }
    }

    private async void OnPeriodSheetBackdropTapped(object sender, TappedEventArgs e) =>
        await ClosePeriodSheetAsync();

    private void UpdatePeriodSheetSelection()
    {
        bool weekSelected = _period == PeriodWeek;
        bool monthSelected = _period == PeriodMonth;
        bool yearSelected = _period == PeriodYear;
        bool allSelected = _period == PeriodAll;
        bool customSelected = _period == PeriodCustom;

        WeekOptionLabel.TextColor = weekSelected ? AppColors.Accent600 : AppColors.TextPrimary;
        MonthOptionLabel.TextColor = monthSelected ? AppColors.Accent600 : AppColors.TextPrimary;
        YearOptionLabel.TextColor = yearSelected ? AppColors.Accent600 : AppColors.TextPrimary;
        AllTimeOptionLabel.TextColor = allSelected ? AppColors.Accent600 : AppColors.TextPrimary;
        CustomOptionLabel.TextColor = customSelected ? AppColors.Accent600 : AppColors.TextPrimary;
        WeekOptionLabel.FontAttributes = weekSelected ? FontAttributes.Bold : FontAttributes.None;
        MonthOptionLabel.FontAttributes = monthSelected ? FontAttributes.Bold : FontAttributes.None;
        YearOptionLabel.FontAttributes = yearSelected ? FontAttributes.Bold : FontAttributes.None;
        AllTimeOptionLabel.FontAttributes = allSelected ? FontAttributes.Bold : FontAttributes.None;
        CustomOptionLabel.FontAttributes = customSelected ? FontAttributes.Bold : FontAttributes.None;
        WeekOption.BackgroundColor = weekSelected ? AppColors.SelectionSurface : Colors.Transparent;
        MonthOption.BackgroundColor = monthSelected ? AppColors.SelectionSurface : Colors.Transparent;
        YearOption.BackgroundColor = yearSelected ? AppColors.SelectionSurface : Colors.Transparent;
        AllTimeOption.BackgroundColor = allSelected ? AppColors.SelectionSurface : Colors.Transparent;
        CustomOption.BackgroundColor = customSelected ? AppColors.SelectionSurface : Colors.Transparent;
        // Selection is communicated by the highlighted row; no extra checkmark
        // is needed in the compact mobile menu.
        PeriodFilterLabel.Text = _period;
    }

    private void OnPeriodSelected(object? sender, string period)
    {
        if (period == PeriodCustom)
        {
            CustomDateOverlay.IsVisible = true;
            _period = period;
            UpdatePeriodSheetSelection();
            return;
        }

        _period = period;
        _periodBeforeCustom = period;
        UpdatePeriodSheetSelection();
        ApplyFilter();
    }

    private async void OnTaskTeacherFilterTapped(object sender, TappedEventArgs e)
    {
        if (PeriodSheetOverlay.IsVisible)
            await ClosePeriodSheetAsync();
        await _taskTeacherDropdown.ToggleAsync();
    }

    private void CloseTaskDropdowns()
    {
        _taskTeacherDropdown.CloseImmediate();
    }

    private void OnSummaryCardTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is not string status)
            return;

        _selectedSummaryStatus = string.Equals(_selectedSummaryStatus, status, StringComparison.Ordinal)
            ? null
            : status;
        _taskStatus = _selectedSummaryStatus ?? TaskStatusAll;
        UpdateSummarySelectionStyle();
        // Rebuild the active period before deriving either the percentage or
        // the task cards. This keeps the overview count, ring and list on the
        // exact same snapshot when a status is selected.
        ApplyFilter();
    }

    private async void OnFilteredTaskTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is TaskSummaryItem task)
        {
            // The editor can update or delete the selected task. Mark this
            // report stale before opening it so the cards and totals are
            // rebuilt as soon as the modal closes.
            _loaded = false;
            await Navigation.PushModalAsync(new EditTaskPage(task.Task_id), false);
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_loaded) await LoadAsync();
    }

    private async System.Threading.Tasks.Task LoadAsync()
    {
        SetBusy(true);
        try
        {
            DataTable table = await _database.GetAllTasksWithTeachersAsync();
            int[] taskIDs = table.AsEnumerable()
                .Select(row => row.Field<int>("Task_id"))
                .Distinct()
                .ToArray();
            Dictionary<int, Task<List<SubtaskDisplayItem>>> subtaskLoads = taskIDs
                .ToDictionary(taskID => taskID, taskID => _database.GetTaskSubtasksAsync(taskID));
            await Task.WhenAll(subtaskLoads.Values);
            Dictionary<int, string> subtaskKeys = subtaskLoads.ToDictionary(
                pair => pair.Key,
                pair => string.Join("\u001e", pair.Value.Result.Select(subtask => NormalizeKeyPart(subtask.Title))));

            _assignments.Clear();
            foreach (DataRow row in table.Rows)
                _assignments.Add(ToAssignment(row, subtaskKeys));
            _allTasks.Clear();
            _allTasks.AddRange(_assignments
                .GroupBy(BuildCooperativeTaskKey)
                .Select(ToTaskSummary)
                .OrderBy(item => TaskPalette.PriorityRank(item.Priority))
                .ThenBy(item => item.Deadline ?? DateTime.MaxValue));
            RebuildTaskTeacherOptions();
            _loaded = true;
            ApplyFilter();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            await UiAlertService.ShowAsync(this, "Report unavailable", "The task summary could not be loaded. Please try again.", "OK");
        }
        finally { SetBusy(false); }
    }

    private void RebuildTaskTeacherOptions()
    {
        IEnumerable<string> teachers = _assignments
            .Select(item => item.TeacherName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase);

        _taskTeacherDropdown.SetOptions(
            TaskTeacherOptionsHost,
            new[] { TaskTeacherAll }.Concat(teachers));

        bool selectionStillExists = _taskTeacher == TaskTeacherAll ||
            _assignments.Any(item => item.TeacherName.Equals(
                _taskTeacher, StringComparison.OrdinalIgnoreCase));
        if (!selectionStillExists)
            _taskTeacher = TaskTeacherAll;

        _taskTeacherDropdown.SetInitialSelection(_taskTeacher);
        FilterButtonState.Apply(
            TaskTeacherPill,
            _taskTeacher != TaskTeacherAll,
            TaskTeacherPillLabel,
            TaskTeacherPillChevron);
    }

    private void ApplyFilter()
    {
        if (!_loaded) return;
        (DateTime? start, DateTime? end) = GetDateRange();
        List<TaskSummaryItem> tasks = _allTasks
            .Where(item => !start.HasValue || IsTaskInDateRange(item, start.Value, end!.Value))
            .OrderBy(item => TaskPalette.PriorityRank(item.Priority))
            .ThenBy(item => item.Deadline ?? DateTime.MaxValue)
            .ToList();

        _periodTasks.Clear();
        _periodTasks.AddRange(tasks);
        UpdateOverview(tasks);
        UpdateFilteredTasks();
    }

    private static bool IsTaskInDateRange(TaskSummaryItem task, DateTime start, DateTime end)
    {
        // A completed report is about when work was completed. Active work
        // continues to use its deadline for the report period.
        DateTime? reportDate = task.ReportCategory == TaskStatusCompleted
            ? task.Completed_at
            : task.Deadline;
        return reportDate.HasValue &&
               reportDate.Value.Date >= start.Date &&
               reportDate.Value.Date <= end.Date;
    }

    private (DateTime? Start, DateTime? End) GetDateRange()
    {
        DateTime today = DateTime.Today;
        return _period switch
        {
            PeriodWeek => (today.AddDays(-(((int)today.DayOfWeek + 6) % 7)), today.AddDays(6 - (((int)today.DayOfWeek + 6) % 7))),
            PeriodMonth => (new DateTime(today.Year, today.Month, 1), new DateTime(today.Year, today.Month, 1).AddMonths(1).AddDays(-1)),
            PeriodYear => (new DateTime(today.Year, 1, 1), new DateTime(today.Year, 12, 31)),
            PeriodAll => (null, null),
            PeriodCustom => (StartDatePicker.Date, EndDatePicker.Date),
            _ => (null, null)
        };
    }

    private static ReportAssignment ToAssignment(
        DataRow row,
        IReadOnlyDictionary<int, string> subtaskKeys)
    {
        string completion = row.IsNull("Completion_status") ? "Pending" : row.Field<string>("Completion_status") ?? "Pending";
        bool acknowledged = !row.IsNull("Is_acknowledged") && row.Field<bool>("Is_acknowledged");
        int taskID = row.Field<int>("Task_id");
        return new ReportAssignment(
            taskID,
            row.Field<int>("Createdby_user_id"),
            row.Field<string>("Title") ?? "Untitled task",
            row.Field<string>("Description") ?? string.Empty,
            string.IsNullOrWhiteSpace(row.Field<string>("TeacherName")) ? "Unassigned" : row.Field<string>("TeacherName")!,
            row.Field<string>("Priority") ?? "Unassigned",
            row.IsNull("Created_at") ? DateTime.Today : row.Field<DateTime>("Created_at"),
            row.IsNull("Deadline") ? null : row.Field<DateTime>("Deadline"),
            row.IsNull("Completed_at") ? null : row.Field<DateTime>("Completed_at"),
            NormalizeStatus(completion, acknowledged),
            subtaskKeys.GetValueOrDefault(taskID, string.Empty));
    }

    private static string NormalizeStatus(string completion, bool acknowledged) => completion switch
    {
        "Completed" => "Completed",
        "Returned" => "Needs Revision",
        "For Validation" => "For Validation",
        _ when acknowledged => "Acknowledged",
        _ => "Pending"
    };

    private static TaskSummaryItem ToTaskSummary(IGrouping<string, ReportAssignment> group)
    {
        ReportAssignment display = group.First();
        string[] statuses = group.Select(item => item.Status).ToArray();
        string status = statuses.All(item => item == TaskStatusCompleted)
            ? TaskStatusCompleted
            : statuses.Any(item => item == "Needs Revision")
                ? "Needs Revision"
                : statuses.Any(item => item == "For Validation")
                    ? "For Validation"
                    : statuses.Any(item => item == "Acknowledged")
                        ? "Acknowledged"
                        : TaskStatusPending;
        string teachers = string.Join(", ", group.Select(item => item.TeacherName).Distinct(StringComparer.OrdinalIgnoreCase));
        string priority = group.OrderBy(item => TaskPalette.PriorityRank(item.Priority)).First().Priority;
        DateTime? completedAt = status == TaskStatusCompleted
            ? group.Select(item => item.Completed_at).Max()
            : null;
        int[] groupedTaskIDs = group.Select(item => item.Task_id).Distinct().ToArray();
        return new TaskSummaryItem
        {
            Task_id = display.Task_id,
            TaskIDs = groupedTaskIDs,
            Title = display.Title,
            TeacherName = teachers,
            Priority = priority,
            Deadline = group.Select(item => item.Deadline).Min(),
            Completed_at = completedAt,
            Status = status,
            StatusColor = StatusColor(status)
        };
    }

    private static Color StatusColor(string status) => TaskPalette.StatusColor(status);

    private static string BuildCooperativeTaskKey(ReportAssignment item) =>
        string.Join("\u001f",
            item.Createdby_user_id,
            item.Created_at.Date.ToString("yyyyMMdd"),
            NormalizeKeyPart(item.Title),
            NormalizeKeyPart(item.Description),
            item.Deadline?.Date.ToString("yyyyMMdd") ?? string.Empty,
            NormalizeKeyPart(item.Priority),
            item.SubtaskKey);

    private static string NormalizeKeyPart(string? value) =>
        value?.Trim().ToUpperInvariant() ?? string.Empty;

    private void UpdateOverview(IReadOnlyCollection<TaskSummaryItem> tasks)
    {
        int total = tasks.Count;
        int pending = tasks.Count(item => item.ReportCategory == "Pending");
        int ongoing = tasks.Count(item => item.ReportCategory == "Ongoing");
        int completed = tasks.Count(item => item.ReportCategory == "Completed");
        int overdue = tasks.Count(item => item.ReportCategory == "Overdue");
        PendingCountLabel.Text = pending.ToString();
        OngoingCountLabel.Text = ongoing.ToString();
        CompletedCountLabel.Text = completed.ToString();
        OverdueCountLabel.Text = overdue.ToString();
        UpdateDisplayedPercentage(tasks);
    }

    private static string FormatPercentage(string category, IReadOnlyCollection<TaskSummaryItem> tasks)
    {
        if (tasks.Count == 0)
            return "0%";

        string[] categories = [TaskStatusPending, TaskStatusOngoing, TaskStatusCompleted, TaskStatusOverdue];
        int[] counts = categories.Select(status => tasks.Count(task => task.ReportCategory == status)).ToArray();
        int[] percentages = counts.Select(count => (int)(count * 100L / tasks.Count)).ToArray();

        // Allocate the remaining points to the largest fractional remainders so
        // all four displayed percentages total 100. Break ties by count, then
        // card order, keeping the result stable when switching status cards.
        var remainderOrder = Enumerable.Range(0, categories.Length)
            .OrderByDescending(index => counts[index] * 100L % tasks.Count)
            .ThenByDescending(index => counts[index])
            .ThenBy(index => index);
        foreach (int index in remainderOrder.Take(100 - percentages.Sum()))
            percentages[index]++;

        return $"{percentages[Array.IndexOf(categories, category)]}%";
    }

    private void UpdateDisplayedPercentage(IReadOnlyCollection<TaskSummaryItem> tasks)
    {
        string category = _selectedSummaryStatus ?? TaskStatusCompleted;
        CompletionHeadingLabel.Text = category switch
        {
            TaskStatusPending => "Pending",
            TaskStatusOngoing => "Ongoing",
            TaskStatusOverdue => "Overdue",
            _ => "Completed"
        };
        int total = tasks.Count;
        int count = tasks.Count(item => item.ReportCategory == category);
        double progress = total <= 0 ? 0 : (double)count / total;
        Color progressColor = _selectedSummaryStatus is null
            ? AppColors.Accent500
            : category switch
            {
                TaskStatusPending => AppColors.StatusPending,
                TaskStatusOngoing => AppColors.StatusOngoing,
                TaskStatusOverdue => AppColors.StatusDanger,
                _ => AppColors.StatusSuccess
            };

        string percentage = FormatPercentage(category, tasks);
        CompletionPercentLabel.Text = percentage;
        CompletionPercentLabel.TextColor = progressColor;
        _completionRing.Progress = progress;
        _completionRing.ProgressColor = progressColor;
        CompletionRing.Invalidate();
        SemanticProperties.SetDescription(
            CompletionRing,
            $"{category}, {percentage} of tasks in {_period.ToLowerInvariant()}");
    }

    private void UpdateFilteredTasks()
    {
        List<TaskSummaryItem> matches = _periodTasks
            .Where(MatchesTaskStatus)
            .Where(MatchesTaskTeacher)
            .OrderBy(item => item.Deadline ?? DateTime.MaxValue)
            .ThenBy(item => item.Title)
            .ToList();

        FilteredTasks.Clear();
        foreach (TaskSummaryItem task in matches)
            FilteredTasks.Add(task);

        FilteredTasksHeadingLabel.Text = _taskStatus == TaskStatusAll
            ? "Tasks"
            : $"{_taskStatus} Tasks";
        FilteredTasksEmptyLabel.Text = AreTaskFiltersAtDefaults()
            ? "No tasks yet"
            : "No tasks match these filters";
        FilteredTasksEmptyState.IsVisible = matches.Count == 0;
        FilteredTasksSection.IsVisible = true;
    }

    private bool MatchesTaskStatus(TaskSummaryItem task) =>
        _taskStatus == TaskStatusAll || task.ReportCategory == _taskStatus;

    private bool MatchesTaskTeacher(TaskSummaryItem task) =>
        _taskTeacher == TaskTeacherAll ||
        _assignments.Any(item => task.TaskIDs.Contains(item.Task_id) &&
            item.TeacherName.Equals(_taskTeacher, StringComparison.OrdinalIgnoreCase));

    private bool AreTaskFiltersAtDefaults() =>
        _taskStatus == TaskStatusAll &&
        _taskTeacher == TaskTeacherAll;

    private void UpdateSummarySelectionStyle()
    {
        ResetSummaryCard(PendingCard);
        ResetSummaryCard(OngoingCard);
        ResetSummaryCard(CompletedCard);
        ResetSummaryCard(OverdueCard);

        (Border? card, Color? surface, Color? border) = _selectedSummaryStatus switch
        {
            "Pending" => (PendingCard, AppColors.StatusPendingSurface, AppColors.StatusPendingBorder),
            "Ongoing" => (OngoingCard, AppColors.StatusOngoingSurface, AppColors.StatusOngoingBorder),
            "Completed" => (CompletedCard, AppColors.StatusSuccessSurface, AppColors.StatusSuccessBorder),
            "Overdue" => (OverdueCard, AppColors.StatusDangerSurface, AppColors.StatusDangerBorder),
            _ => (null, null, null)
        };

        if (card is not null && surface is not null && border is not null)
        {
            card.BackgroundColor = surface;
            card.Stroke = border;
            card.StrokeThickness = 2;
        }
    }

    private static void ResetSummaryCard(Border card)
    {
        card.BackgroundColor = AppColors.SurfaceBase;
        card.Stroke = AppColors.BorderDefault;
        card.StrokeThickness = 1;
    }

    private void OnCustomDateApplyClicked(object sender, EventArgs e)
    {
        CustomDateOverlay.IsVisible = false;
        _periodBeforeCustom = PeriodCustom;
        UpdatePeriodSheetSelection();
        ApplyFilter();
    }

    private void OnCustomDateCancelClicked(object sender, EventArgs e)
    {
        CustomDateOverlay.IsVisible = false;
        _period = _periodBeforeCustom;
        UpdatePeriodSheetSelection();
        ApplyFilter();
    }

    private void OnCustomDateChanged(object sender, DateChangedEventArgs e)
    {
        if (StartDatePicker.Date > EndDatePicker.Date)
        {
            if (sender == StartDatePicker) EndDatePicker.Date = StartDatePicker.Date;
            else StartDatePicker.Date = EndDatePicker.Date;
        }
        ApplyFilter();
    }

    protected override bool OnBackButtonPressed()
    {
        if (_taskTeacherDropdown.IsOpen)
        {
            CloseTaskDropdowns();
            return true;
        }

        if (PeriodSheetOverlay.IsVisible)
        {
            _ = ClosePeriodSheetAsync();
            return true;
        }

        return base.OnBackButtonPressed();
    }

    private void OnDashboardTapped(object sender, TappedEventArgs e) =>
        DashboardFlyoutPage.Current?.ShowTasks();

    private void OnCalendarTapped(object sender, TappedEventArgs e) =>
        DashboardFlyoutPage.Current?.ShowDetail(new CalendarPage());

    private void OnNotificationTapped(object sender, TappedEventArgs e) =>
        DashboardFlyoutPage.Current?.ShowNotification(UserSessionService.CurrentUser);

    private async void OnProfileTapped(object sender, TappedEventArgs e)
    {
        try
        {
            var user = await UserSessionService.GetCurrentUserAsync(forceRefresh: true);
            if (user is not null) DashboardFlyoutPage.Current?.ShowProfile(user);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            await UiAlertService.ShowAsync(this, "Profile unavailable", "Your profile could not be loaded. Please try again.", "OK");
        }
    }

    private void SetBusy(bool busy)
    {
        LoadingIndicator.IsVisible = busy;
        LoadingIndicator.IsRunning = busy;
        FilterButton.IsEnabled = !busy;
        TaskTeacherPill.IsEnabled = !busy;
    }

    private sealed record ReportAssignment(
        int Task_id,
        int Createdby_user_id,
        string Title,
        string Description,
        string TeacherName,
        string Priority,
        DateTime Created_at,
        DateTime? Deadline,
        DateTime? Completed_at,
        string Status,
        string SubtaskKey);

    private sealed class CompletionRingDrawable : IDrawable
    {
        public double Progress { get; set; }
        public Color ProgressColor { get; set; } = AppColors.Accent500;

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            const float stroke = 18f;
            float size = Math.Min(dirtyRect.Width, dirtyRect.Height) - stroke * 2;
            float x = (dirtyRect.Width - size) / 2;
            float y = (dirtyRect.Height - size) / 2;
            canvas.StrokeSize = stroke;
            canvas.StrokeColor = AppColors.TextPrimary;
            canvas.DrawEllipse(x, y, size, size);
            float progress = (float)Math.Clamp(Progress, 0, 1);
            if (progress <= 0) return;
            canvas.StrokeColor = ProgressColor;
            if (progress >= 1) canvas.DrawEllipse(x, y, size, size);
            else
            {
                canvas.DrawArc(x, y, size, size, 90, 90 + 360 * progress, false, false);
                float radius = size / 2;
                float centerX = dirtyRect.Width / 2;
                float centerY = dirtyRect.Height / 2;
                float capRadius = stroke / 2;
                float endAngle = MathF.PI / 180 * (90 + 360 * progress);
                canvas.FillColor = ProgressColor;
                canvas.FillEllipse(centerX - capRadius, centerY - radius - capRadius, stroke, stroke);
                canvas.FillEllipse(centerX + radius * MathF.Cos(endAngle) - capRadius,
                    centerY - radius * MathF.Sin(endAngle) - capRadius, stroke, stroke);
            }
        }
    }
}
