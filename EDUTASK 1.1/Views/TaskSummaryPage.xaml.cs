using EDUTASK_1._1.Models;
using EDUTASK_1._1.Services;
using System.Collections.ObjectModel;
using System.Data;

namespace EDUTASK_1._1.Views;

public partial class TaskSummaryPage : ContentPage
{
    private readonly DatabaseService _database = new();
    private readonly List<ReportAssignment> _assignments = [];
    private readonly CompletionRingDrawable _completionRing = new();
    private bool _loaded;
    private bool _showTasks;
    private int _periodBeforeCustomIndex = 1;
    private bool _restoringPeriod;
    private bool _updatingTeacherFilter;
    private bool _hasActions;
    private string _statusFilter = "All";
    public ObservableCollection<TaskSummaryItem> ReportTasks { get; } = [];
    public ObservableCollection<TeacherSummaryItem> TeacherSummaries { get; } = [];

    public TaskSummaryPage()
    {
        InitializeComponent();
        BindingContext = this;
        CompletionRing.Drawable = _completionRing;
        PeriodPicker.ItemsSource = new[] { "This Week", "This Month", "This Year", "All", "Custom" };
        StartDatePicker.Date = DateTime.Today.AddDays(-30);
        EndDatePicker.Date = DateTime.Today;
        ReportDateLabel.Text = DateTime.Today.ToString("MMMM d, yyyy");
        PeriodPicker.SelectedIndex = 0;
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
            _assignments.Clear();
            foreach (DataRow row in table.Rows) _assignments.Add(ToAssignment(row));
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

    private void ApplyFilter()
    {
        if (!_loaded) return;
        (DateTime? start, DateTime? end) = GetDateRange();
        List<ReportAssignment> periodRows = _assignments.Where(item =>
            !start.HasValue || (item.Deadline.HasValue && item.Deadline.Value.Date >= start.Value && item.Deadline.Value.Date <= end!.Value)).ToList();
        string previousTeacher = TeacherPicker.SelectedItem?.ToString() ?? "All Teachers";
        List<string> teacherOptions = new[] { "All Teachers" }.Concat(periodRows.Select(item => item.TeacherName)
            .Where(name => name != "Unassigned").Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name)).ToList();
        string selectedTeacher = teacherOptions.FirstOrDefault(name => name.Equals(previousTeacher, StringComparison.OrdinalIgnoreCase)) ?? "All Teachers";
        _updatingTeacherFilter = true;
        TeacherPicker.ItemsSource = teacherOptions;
        TeacherPicker.SelectedItem = selectedTeacher;
        _updatingTeacherFilter = false;
        if (!_showTasks) selectedTeacher = "All Teachers";
        bool allTeachers = selectedTeacher == "All Teachers";
        List<ReportAssignment> rows = periodRows.Where(item => allTeachers || item.TeacherName.Equals(selectedTeacher, StringComparison.OrdinalIgnoreCase)).ToList();

        List<TaskSummaryItem> tasks = rows.GroupBy(item => item.TaskID).Select(ToTaskSummary).OrderBy(item => item.Deadline ?? DateTime.MaxValue).ToList();

        IEnumerable<TaskSummaryItem> visibleTasks = _statusFilter switch
        {
            "Completed" => tasks.Where(item => item.Status == "Completed"),
            "Pending" => tasks.Where(item => item.Status != "Completed"),
            "Overdue" => tasks.Where(item => item.Status != "Completed" && item.Deadline.HasValue && item.Deadline.Value.Date < DateTime.Today),
            _ => tasks
        };
        List<TaskSummaryItem> filteredTasks = visibleTasks.ToList();
        ReportTasks.Clear();
        foreach (TaskSummaryItem task in filteredTasks) ReportTasks.Add(task);
        TeacherSummaries.Clear();
        foreach (TeacherSummaryItem teacher in BuildTeacherSummaries(rows)) TeacherSummaries.Add(teacher);
        UpdateOverview(tasks);
        EmptyReportLabel.Text = _statusFilter == "All"
            ? "No tasks found for this period."
            : $"No {_statusFilter.ToLowerInvariant()} tasks found for this period.";
        EmptyReportLabel.IsVisible = filteredTasks.Count == 0;
        ActiveStatusFilterLabel.Text = _statusFilter == "All" ? "Showing: All tasks" : $"Showing: {_statusFilter}";
        ClearStatusFilterButton.IsVisible = _statusFilter != "All";
        UpdateSectionVisibility(allTeachers);
    }

    private (DateTime? Start, DateTime? End) GetDateRange()
    {
        DateTime today = DateTime.Today;
        return PeriodPicker.SelectedItem?.ToString() switch
        {
            "This Week" => (today.AddDays(-(((int)today.DayOfWeek + 6) % 7)), today.AddDays(6 - (((int)today.DayOfWeek + 6) % 7))),
            "This Month" => (new DateTime(today.Year, today.Month, 1), new DateTime(today.Year, today.Month, 1).AddMonths(1).AddDays(-1)),
            "This Year" => (new DateTime(today.Year, 1, 1), new DateTime(today.Year, 12, 31)),
            "Custom" => (StartDatePicker.Date, EndDatePicker.Date),
            _ => (null, null)
        };
    }

    private static ReportAssignment ToAssignment(DataRow row)
    {
        string completion = row.IsNull("CompletionStatus") ? "Pending" : row.Field<string>("CompletionStatus") ?? "Pending";
        bool acknowledged = !row.IsNull("IsAcknowledged") && row.Field<bool>("IsAcknowledged");
        return new ReportAssignment(
            row.Field<int>("TaskID"),
            row.Field<string>("Title") ?? "Untitled task",
            string.IsNullOrWhiteSpace(row.Field<string>("TeacherName")) ? "Unassigned" : row.Field<string>("TeacherName")!,
            row.IsNull("CreatedAt") ? DateTime.Today : row.Field<DateTime>("CreatedAt"),
            row.IsNull("Deadline") ? null : row.Field<DateTime>("Deadline"),
            NormalizeStatus(completion, acknowledged));
    }

    private static string NormalizeStatus(string completion, bool acknowledged) => completion switch
    {
        "Completed" => "Completed",
        "Returned" => "Needs Revision",
        "For Validation" => "For Validation",
        _ when acknowledged => "Acknowledged",
        _ => "Pending"
    };

    private static TaskSummaryItem ToTaskSummary(IGrouping<int, ReportAssignment> group)
    {
        ReportAssignment display = group.OrderByDescending(item => StatusRank(item.Status)).First();
        string teachers = string.Join(", ", group.Select(item => item.TeacherName).Distinct(StringComparer.OrdinalIgnoreCase));
        return new TaskSummaryItem { TaskID = group.Key, Title = display.Title, TeacherName = teachers, Deadline = display.Deadline, Status = display.Status, StatusColor = StatusColor(display.Status) };
    }

    private static int StatusRank(string status) => status switch { "Completed" => 5, "Needs Revision" => 4, "For Validation" => 3, "Acknowledged" => 2, _ => 1 };
    private static Color StatusColor(string status) => status switch { "Completed" => Color.FromArgb("#16803A"), "Needs Revision" => Color.FromArgb("#DC2626"), "For Validation" => Color.FromArgb("#6554C0"), "Acknowledged" => Color.FromArgb("#2563EB"), _ => Color.FromArgb("#D97706") };

    private static IEnumerable<TeacherSummaryItem> BuildTeacherSummaries(IEnumerable<ReportAssignment> rows) => rows
        .GroupBy(item => item.TeacherName, StringComparer.OrdinalIgnoreCase)
        .Select(group => new TeacherSummaryItem
        {
            TeacherName = group.Key,
            AssignedCount = group.Select(item => item.TaskID).Distinct().Count(),
            CompletedCount = group.Where(item => item.Status == "Completed").Select(item => item.TaskID).Distinct().Count(),
            OverdueCount = group.Where(IsOverdue).Select(item => item.TaskID).Distinct().Count()
        })
        .OrderByDescending(item => item.CompletionRate).ThenBy(item => item.TeacherName);

    private void UpdateOverview(IReadOnlyCollection<TaskSummaryItem> tasks)
    {
        int total = tasks.Count;
        int completed = tasks.Count(item => item.Status == "Completed");
        int overdue = tasks.Count(item => item.Status != "Completed" && item.Deadline.HasValue && item.Deadline.Value.Date < DateTime.Today);
        double progress = total == 0 ? 0 : (double)completed / total;
        TotalCountLabel.Text = total.ToString();
        CompletedCountLabel.Text = completed.ToString();
        PendingCountLabel.Text = (total - completed).ToString();
        OverdueCountLabel.Text = overdue.ToString();
        _completionRing.Progress = progress;
        CompletionRing.Invalidate();
        CompletionPercentLabel.Text = $"{progress:P0}";
        int validation = tasks.Count(item => item.Status == "For Validation");
        int revision = tasks.Count(item => item.Status == "Needs Revision");
        _hasActions = validation > 0 || revision > 0;
        StatusValidationLabel.Text = validation.ToString();
        StatusRevisionLabel.Text = revision.ToString();
        ValidationMetric.IsVisible = validation > 0;
        RevisionMetric.IsVisible = revision > 0;
        Grid.SetColumn(RevisionMetric, validation > 0 ? 1 : 0);
        Grid.SetColumnSpan(ValidationMetric, revision == 0 ? 2 : 1);
        Grid.SetColumnSpan(RevisionMetric, validation == 0 ? 2 : 1);
    }

    private static bool IsOverdue(ReportAssignment item) => item.Status != "Completed" && item.Deadline.HasValue && item.Deadline.Value.Date < DateTime.Today;

    private void OnPeriodChanged(object sender, EventArgs e)
    {
        if (_restoringPeriod) return;
        if (PeriodPicker.SelectedItem?.ToString() == "Custom")
        {
            CustomDateOverlay.IsVisible = true;
            return;
        }

        _periodBeforeCustomIndex = PeriodPicker.SelectedIndex;
        ApplyFilter();
    }

    private void OnCustomDateApplyClicked(object sender, EventArgs e)
    {
        CustomDateOverlay.IsVisible = false;
        ApplyFilter();
    }

    private void OnCustomDateCancelClicked(object sender, EventArgs e)
    {
        CustomDateOverlay.IsVisible = false;
        _restoringPeriod = true;
        PeriodPicker.SelectedIndex = _periodBeforeCustomIndex;
        _restoringPeriod = false;
    }


    private void OnReportDateTapped(object sender, TappedEventArgs e)
    {
        if (PeriodPicker.SelectedItem?.ToString() == "Custom") CustomDateOverlay.IsVisible = true;
    }

    private void UpdateReportDateLabel()
    {
        ReportDateLabel.Text = PeriodPicker.SelectedItem?.ToString() == "Custom"
            ? $"{StartDatePicker.Date:MMM d, yyyy} – {EndDatePicker.Date:MMM d, yyyy}  •  Tap to edit"
            : DateTime.Today.ToString("MMMM d, yyyy");
    }
    private void OnOverviewTabClicked(object sender, EventArgs e)
    {
        _showTasks = false;
        ApplyFilter();
    }

    private void OnTasksTabClicked(object sender, EventArgs e)
    {
        _showTasks = true;
        ApplyFilter();
    }

    private void OnSummaryCardTapped(object sender, TappedEventArgs e)
    {
        _statusFilter = e.Parameter?.ToString() ?? "All";
        _showTasks = true;
        ApplyFilter();
    }

    private void OnClearStatusFilterClicked(object sender, EventArgs e)
    {
        _statusFilter = "All";
        ApplyFilter();
    }

    private void OnTeacherChanged(object sender, EventArgs e)
    {
        if (!_updatingTeacherFilter) ApplyFilter();
    }
    private void UpdateSectionVisibility(bool allTeachers)
    {
        SummaryCards.IsVisible = !_showTasks;
        CompletionSection.IsVisible = !_showTasks;
        StatusSection.IsVisible = !_showTasks && _hasActions;
        TeacherSection.IsVisible = !_showTasks && allTeachers;
        TasksSection.IsVisible = _showTasks;
        TeacherFilterRow.IsVisible = _showTasks;
        OverviewTabButton.BackgroundColor = _showTasks ? Colors.Transparent : Color.FromArgb("#5D6D7E");
        OverviewTabButton.TextColor = _showTasks ? Color.FromArgb("#596574") : Colors.White;
        TasksTabButton.BackgroundColor = _showTasks ? Color.FromArgb("#5D6D7E") : Colors.Transparent;
        TasksTabButton.TextColor = _showTasks ? Colors.White : Color.FromArgb("#596574");
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

    private void OnTaskTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is not TaskSummaryItem task) return;
        TaskModalTitleLabel.Text = task.Title;
        TaskModalTeacherLabel.Text = task.TeacherName;
        TaskModalStatusLabel.Text = task.Status;
        TaskModalStatusLabel.TextColor = task.StatusColor;
        TaskModalDueDateLabel.Text = task.Deadline.HasValue ? task.Deadline.Value.ToString("MMMM d, yyyy") : "No deadline";
        TaskDetailOverlay.IsVisible = true;
    }

    private void OnTaskDetailCloseClicked(object sender, EventArgs e) => TaskDetailOverlay.IsVisible = false;

    private void OnBackClicked(object sender, EventArgs e)
    {
        DashboardFlyoutPage.Current?.ShowDetail(new DirectorStaffDashboardPage());
    }

    private void OnDashboardTapped(object sender, TappedEventArgs e) =>
        DashboardFlyoutPage.Current?.ShowDetail(new DirectorStaffDashboardPage());

    private void OnCalendarTapped(object sender, TappedEventArgs e) =>
        DashboardFlyoutPage.Current?.ShowDetail(new UpcomingDeadlinesPage());

    private void OnNotificationsTapped(object sender, TappedEventArgs e) =>
        DashboardFlyoutPage.Current?.ShowDetail(new NotificationsPage());

    private async void OnProfileTapped(object sender, TappedEventArgs e)
    {
        try
        {
            var user = await UserSessionService.GetCurrentUserAsync(forceRefresh: true);
            if (user is not null) DashboardFlyoutPage.Current?.ShowDetail(new ProfilePage(user));
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
        PeriodPicker.IsEnabled = !busy;
        TeacherPicker.IsEnabled = !busy;
    }

    private sealed record ReportAssignment(int TaskID, string Title, string TeacherName, DateTime CreatedAt, DateTime? Deadline, string Status);


    private sealed class CompletionRingDrawable : IDrawable
    {
        public double Progress { get; set; }

        public void Draw(ICanvas canvas, RectF dirtyRect)
        {
            const float stroke = 16f;
            float size = Math.Min(dirtyRect.Width, dirtyRect.Height) - stroke * 2;
            float x = (dirtyRect.Width - size) / 2;
            float y = (dirtyRect.Height - size) / 2;
            canvas.StrokeSize = stroke;
            canvas.StrokeColor = Color.FromArgb("#E8EDF3");
            canvas.DrawEllipse(x, y, size, size);
            float progress = (float)Math.Clamp(Progress, 0, 1);
            if (progress <= 0) return;
            canvas.StrokeColor = Color.FromArgb("#2563EB");
            if (progress >= 1) canvas.DrawEllipse(x, y, size, size);
            else
            {
                canvas.DrawArc(x, y, size, size, 90, 90 + 360 * progress, false, false);
                float radius = size / 2;
                float centerX = dirtyRect.Width / 2;
                float centerY = dirtyRect.Height / 2;
                float capRadius = stroke / 2;
                float endAngle = MathF.PI / 180 * (90 + 360 * progress);
                canvas.FillColor = Color.FromArgb("#2563EB");
                canvas.FillEllipse(centerX - capRadius, centerY - radius - capRadius, stroke, stroke);
                canvas.FillEllipse(centerX + radius * MathF.Cos(endAngle) - capRadius,
                    centerY - radius * MathF.Sin(endAngle) - capRadius, stroke, stroke);
            }
        }
    }}
