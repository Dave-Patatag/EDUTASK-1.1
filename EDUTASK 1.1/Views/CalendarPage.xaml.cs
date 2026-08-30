using System.Data;
using EDUTASK_1._1.Helpers;
using EDUTASK_1._1.Services;
using EDUTASK_1._1.Views.Base;
using Microsoft.Maui.Controls.Shapes;

namespace EDUTASK_1._1.Views;

public partial class CalendarPage : EduTaskPage
{
    /// <summary>
    /// Widest the month card and day panel are allowed to get. Past this the
    /// week columns stretch into a grid you have to sweep your eyes across, and
    /// the task cards turn into single long lines. Phones are narrower than this
    /// and simply fill.
    /// </summary>
    private const double MaxContentWidth = 640;

    private readonly DatabaseService _database = new();
    private readonly Dictionary<DateTime, List<CalendarTaskItem>> _tasksByDate = [];
    private DateTime _displayedMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private DateTime _selectedDate = DateTime.Today;
    private bool _loaded;

    public CalendarPage()
    {
        InitializeComponent();
        SizeChanged += OnPageSizeChanged;
        BuildCalendar();
        RenderSelectedDate();
    }

    /// <summary>
    /// Centres the content column by padding it out to the sides.
    ///
    /// Done here rather than with HorizontalOptions="Center" in the markup: a
    /// centred layout is arranged at its own desired width, and the week grid's
    /// star columns have no natural width to report, so the card collapsed to
    /// the width of the digits inside it and sat in a narrow strip. Filling and
    /// then trimming the margin keeps the grid stretching to the space it has.
    /// </summary>
    private void OnPageSizeChanged(object? sender, EventArgs e)
    {
        if (Width <= 0)
            return;

        double side = Math.Max(0, (Width - MaxContentWidth) / 2);
        ContentColumn.Margin = new Thickness(side, 0, side, 0);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_loaded)
            await LoadTasksAsync();
    }

    private async Task LoadTasksAsync()
    {
        SetBusy(true);
        try
        {
            DataTable table;
            var currentTeacher = TeacherSessionService.CurrentTeacher;
            bool isTeacher = currentTeacher is not null;
            if (currentTeacher is not null)
                table = await _database.GetTeacherTasksAsync(currentTeacher.TeacherID);
            else
                table = await _database.GetAllTasksWithTeachersAsync();

            _tasksByDate.Clear();
            IEnumerable<DataRow> datedRows = table.AsEnumerable()
                .Where(row => !row.IsNull("Deadline"));

            foreach (IGrouping<(int TaskID, DateTime Date), DataRow> group in datedRows.GroupBy(row =>
                         (row.Field<int>("TaskID"), row.Field<DateTime>("Deadline").Date)))
            {
                DataRow first = group.First();
                string status = AggregateStatus(group);
                DateTime deadline = group.Key.Date;
                // Priority owns the left edge; overdue is a separate status.
                // Surface it explicitly so directors do not have to infer it
                // from the date or confuse it with the priority indicator.
                if (deadline < DateTime.Today && status != "Completed")
                    status = "Overdue";
                string priority = first.Field<string>("Priority") ?? "Unassigned";
                string ownerDisplay = isTeacher
                    ? string.Empty
                    : string.Join(", ", group
                        .Select(row => row.Table.Columns.Contains("TeacherName")
                            ? row.Field<string>("TeacherName")
                            : null)
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .Distinct(StringComparer.OrdinalIgnoreCase));

                var item = new CalendarTaskItem
                {
                    TaskID = group.Key.TaskID,
                    Title = first.Field<string>("Title") ?? "Untitled task",
                    OwnerDisplay = ownerDisplay,
                    Priority = priority,
                    Status = status
                };

                if (!_tasksByDate.TryGetValue(group.Key.Date, out List<CalendarTaskItem>? tasks))
                {
                    tasks = [];
                    _tasksByDate[group.Key.Date] = tasks;
                }
                tasks.Add(item);
            }

            foreach (List<CalendarTaskItem> tasks in _tasksByDate.Values)
                tasks.Sort((left, right) =>
                {
                    int priorityOrder = TaskPalette.PriorityRank(left.Priority).CompareTo(TaskPalette.PriorityRank(right.Priority));
                    return priorityOrder != 0
                        ? priorityOrder
                        : string.Compare(left.Title, right.Title, StringComparison.OrdinalIgnoreCase);
                });

            _loaded = true;
            BuildCalendar();
            RenderSelectedDate();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            await UiAlertService.ShowAsync(this, "Calendar unavailable",
                "The calendar could not be loaded. Please try again.", "OK");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void BuildCalendar()
    {
        MonthLabel.Text = _displayedMonth.ToString("MMMM yyyy");
        CalendarGrid.Children.Clear();
        CalendarGrid.RowDefinitions.Clear();

        // Always draw six complete weeks. Dates from the previous and next month
        // stay visible (with disabled text) so every month has a complete grid
        // and the divider remains in a consistent position.
        int offset = ((int)_displayedMonth.DayOfWeek + 6) % 7;
        const int rowCount = 6;
        const int cellCount = rowCount * 7;

        for (int row = 0; row < rowCount; row++)
            CalendarGrid.RowDefinitions.Add(new RowDefinition(new GridLength(52)));

        DateTime gridStart = _displayedMonth.AddDays(-offset);

        for (int cell = 0; cell < cellCount; cell++)
        {
            DateTime date = gridStart.AddDays(cell);
            bool isCurrentMonth = date.Month == _displayedMonth.Month && date.Year == _displayedMonth.Year;
            bool isSelected = date == _selectedDate;
            bool isToday = date == DateTime.Today;
            int taskCount = _tasksByDate.TryGetValue(date, out List<CalendarTaskItem>? tasks)
                ? tasks.Count
                : 0;

            var dateLabel = new Label
            {
                Text = date.Day.ToString(),
                FontSize = AppTypography.Body,
                // Today reads as bold even when another day is selected, so the
                // ring is not the only thing marking it.
                FontAttributes = isToday || isSelected ? FontAttributes.Bold : FontAttributes.None,
                TextColor = isSelected
                    ? AppColors.TextInverse
                    : isToday ? AppColors.CalendarAccent
                    : isCurrentMonth ? AppColors.TextPrimary
                    : AppColors.TextDisabled,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                InputTransparent = true
            };

            var dateCircle = new Border
            {
                WidthRequest = 38,
                HeightRequest = 38,
                Padding = 0,
                Stroke = AppColors.CalendarAccent,
                StrokeThickness = isToday && !isSelected ? 1.5 : 0,
                BackgroundColor = isSelected ? AppColors.CalendarAccent : Colors.Transparent,
                StrokeShape = new RoundRectangle { CornerRadius = 19 },
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                Content = dateLabel,
                InputTransparent = true
            };

            // Two explicit rows — circle above, dots below — rather than stacking
            // both in one cell and pushing the dots down with alignment. Overlaid,
            // the dots clipped the bottom of the selected day's filled circle and
            // had to be recoloured to stay visible against it.
            //
            // The cell, not the circle, is the tap target: a 38pt circle inside a
            // 52pt row would otherwise leave dead gutters between the days.
            var cellGrid = new Grid
            {
                RowDefinitions =
                {
                    new RowDefinition(new GridLength(38)),
                    new RowDefinition(GridLength.Star)
                },
                RowSpacing = 2
            };
            cellGrid.Add(dateCircle, 0, 0);

            if (taskCount > 0)
            {
                // One dot per task, up to three, each in its own priority colour.
                //
                // This replaces a count badge that set its number in 7pt — below
                // legible at any screen density — sitting on top of a second row
                // of dots that showed distinct priorities. Two indicators for one
                // fact, and the readable one carried no sense of how busy the day
                // was. Dots now carry both: how many, and how urgent.
                var indicators = new HorizontalStackLayout
                {
                    Spacing = 3,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Start,
                    InputTransparent = true
                };

                foreach (CalendarTaskItem task in tasks!.Take(3))
                {
                    indicators.Children.Add(new BoxView
                    {
                        WidthRequest = 5,
                        HeightRequest = 5,
                        CornerRadius = 2.5,
                        Color = task.PriorityColor,
                        InputTransparent = true
                    });
                }

                // A fourth mark means "and more", without pretending to count them.
                if (taskCount > 3)
                {
                    indicators.Children.Add(new Label
                    {
                        Text = "+",
                        FontSize = AppTypography.Micro,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = AppColors.TextTertiary,
                        VerticalTextAlignment = TextAlignment.Center,
                        InputTransparent = true
                    });
                }

                cellGrid.Add(indicators, 0, 1);
            }

            var tap = new TapGestureRecognizer { CommandParameter = date };
            tap.Tapped += OnDateTapped;
            cellGrid.GestureRecognizers.Add(tap);
            SemanticProperties.SetDescription(cellGrid,
                taskCount == 0 ? $"{date:MMMM d}" : $"{date:MMMM d}, {taskCount} task{(taskCount == 1 ? string.Empty : "s")} due");

            CalendarGrid.Add(cellGrid, cell % 7, cell / 7);
        }
    }

    private void OnDateTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not DateTime date)
            return;

        _selectedDate = date.Date;
        _displayedMonth = new DateTime(date.Year, date.Month, 1);
        BuildCalendar();
        RenderSelectedDate();
    }

    private void OnPreviousMonthClicked(object sender, EventArgs e) => ChangeMonth(-1);
    private void OnNextMonthClicked(object sender, EventArgs e) => ChangeMonth(1);

    private async void OnMonthYearTapped(object sender, TappedEventArgs e)
    {
        DateTime? selectedMonth = await MonthYearPickerDialog.ShowAsync(this, _displayedMonth);
        if (!selectedMonth.HasValue)
            return;

        _displayedMonth = new DateTime(selectedMonth.Value.Year, selectedMonth.Value.Month, 1);
        _selectedDate = _displayedMonth.Year == DateTime.Today.Year &&
                        _displayedMonth.Month == DateTime.Today.Month
            ? DateTime.Today
            : _displayedMonth;
        BuildCalendar();
        RenderSelectedDate();
    }

    /// <summary>
    /// Jumps back to today. Browsing a few months out and then tapping the arrow
    /// back the same number of times was the only way home.
    /// </summary>
    private void OnTodayClicked(object sender, EventArgs e)
    {
        _selectedDate = DateTime.Today;
        _displayedMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        BuildCalendar();
        RenderSelectedDate();
    }

    private void ChangeMonth(int offset)
    {
        _displayedMonth = _displayedMonth.AddMonths(offset);
        _selectedDate = _displayedMonth.Year == DateTime.Today.Year &&
                        _displayedMonth.Month == DateTime.Today.Month
            ? DateTime.Today
            : _displayedMonth;
        BuildCalendar();
        RenderSelectedDate();
    }

    private void RenderSelectedDate()
    {
        SelectedDateLabel.Text = _selectedDate.ToString("dddd, MMMM d");
        List<CalendarTaskItem> tasks = _tasksByDate.GetValueOrDefault(_selectedDate) ?? [];
        TaskCountLabel.Text = tasks.Count == 1 ? "1 task" : $"{tasks.Count} tasks";
        BindableLayout.SetItemsSource(TaskList, tasks);
        TaskList.IsVisible = tasks.Count > 0;
        EmptyState.IsVisible = _loaded && tasks.Count == 0;
    }

    private void SetBusy(bool busy)
    {
        LoadingIndicator.IsVisible = busy;
        LoadingIndicator.IsRunning = busy;
        if (busy)
            EmptyState.IsVisible = false;
    }

    private void OnMenuClicked(object sender, EventArgs e)
    {
        if (DashboardFlyoutPage.Current is { } flyout)
            flyout.IsPresented = true;
    }

    private async void OnTaskTapped(object sender, TappedEventArgs e)
    {
        int? taskID = e.Parameter is int parameter ? parameter : null;
        if (!taskID.HasValue && sender is Border card &&
            card.BindingContext is CalendarTaskItem item)
            taskID = item.TaskID;

        if (taskID.HasValue)
            await Navigation.PushModalAsync(new EditTaskPage(taskID.Value, true), false);
    }

    private static string AggregateStatus(IEnumerable<DataRow> rows)
    {
        DataRow[] assignments = rows.ToArray();
        string[] statuses = assignments
            .Select(row => row.Field<string>("CompletionStatus") ?? "Pending")
            .ToArray();
        bool acknowledged = assignments.Any(row =>
            !row.IsNull("IsAcknowledged") && row.Field<bool>("IsAcknowledged"));

        if (statuses.Length > 0 && statuses.All(status => status == "Completed"))
            return "Completed";
        if (statuses.Any(status => status is "Returned" or "Needs Revision"))
            return "Needs Revision";
        if (statuses.Any(status => status == "For Validation"))
            return "For Validation";
        return acknowledged ? "Acknowledged" : "Pending";
    }

    private sealed class CalendarTaskItem
    {
        public int TaskID { get; init; }
        public string Title { get; init; } = string.Empty;
        public string OwnerDisplay { get; init; } = string.Empty;
        public bool HasOwnerDisplay => !string.IsNullOrWhiteSpace(OwnerDisplay);
        public string Priority { get; init; } = string.Empty;
        public string Status { get; init; } = string.Empty;

        /// <summary>Accent edge and priority chip.</summary>
        public Color PriorityColor => TaskPalette.PriorityColor(Priority);

        /// <summary>Status badge colour.</summary>
        public Color StatusColor => TaskPalette.StatusColor(Status);

        /// <summary>What a screen reader announces for the whole card.</summary>
        public string CardDescription =>
            HasOwnerDisplay
                ? $"{Title}, assigned to {OwnerDisplay}. {Priority} priority, {Status}."
                : $"{Title}. {Priority} priority, {Status}.";
    }
}
