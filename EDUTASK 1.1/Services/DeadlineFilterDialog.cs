namespace EDUTASK_1._1.Services;

public enum DeadlineFilterKind
{
    AnyDate,
    DateRange,
    NoDeadline
}

public sealed record DeadlineFilterSelection(
    DeadlineFilterKind Kind,
    DateTime? StartDate,
    DateTime? EndDate,
    string Label)
{
    public static DeadlineFilterSelection AnyDate { get; } =
        new(DeadlineFilterKind.AnyDate, null, null, "Date");

    public static DeadlineFilterSelection NoDeadline { get; } =
        new(DeadlineFilterKind.NoDeadline, null, null, "No Deadline");

    public static DeadlineFilterSelection ForRange(DateTime start, DateTime end, string label) =>
        new(DeadlineFilterKind.DateRange, start.Date, end.Date, label);

    public bool Matches(DateTime? deadline)
    {
        if (Kind == DeadlineFilterKind.AnyDate)
            return true;
        if (Kind == DeadlineFilterKind.NoDeadline)
            return deadline is null;
        return deadline is not null && StartDate is not null && EndDate is not null &&
               deadline.Value.Date >= StartDate.Value.Date && deadline.Value.Date <= EndDate.Value.Date;
    }
}

public static class DeadlineFilterDialog
{
    private static readonly Color Accent = Color.FromArgb("#2563EB");
    private static readonly Color SelectedDate = Color.FromArgb("#5D6D7E");
    private static readonly Color Muted = Color.FromArgb("#F3F4F6");
    private static readonly Color Text = Color.FromArgb("#111827");
    private static readonly Color SubtleText = Color.FromArgb("#6B7280");

    public static async Task<DeadlineFilterSelection?> ShowAsync(
        Page owner,
        DeadlineFilterSelection current,
        IEnumerable<EDUTASK_1._1.Models.DashboardTaskItem>? tasks = null)
    {
        var completion = new TaskCompletionSource<DeadlineFilterSelection?>();
        DateTime displayedMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
        DateTime? selectedStart = current.Kind == DeadlineFilterKind.DateRange ? current.StartDate : null;
        DateTime? selectedEnd = current.Kind == DeadlineFilterKind.DateRange ? current.EndDate : null;
        if (selectedStart.HasValue)
            displayedMonth = new DateTime(selectedStart.Value.Year, selectedStart.Value.Month, 1);
        int displayedYear = displayedMonth.Year;
        DeadlineFilterSelection pending = current;
        var taskDates = (tasks ?? [])
            .Where(task => task.Deadline.HasValue)
            .GroupBy(task => task.Deadline!.Value.Date)
            .ToDictionary(group => group.Key, group => group.ToList());

        var modal = new ContentPage
        {
            BackgroundColor = Colors.White,
            Padding = 0
        };

        var yearLabel = HeaderLabel();
        yearLabel.FontSize = 30;
        yearLabel.FontAttributes = FontAttributes.Bold;
        var monthLabel = HeaderLabel();
        var monthBackButton = NavigationButton(string.Empty, 86);
        var yearGrid = new Grid { ColumnSpacing = 12, RowSpacing = 16 };
        for (int column = 0; column < 3; column++)
            yearGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        for (int row = 0; row < 4; row++)
            yearGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        var daysGrid = new Grid { RowSpacing = 4, ColumnSpacing = 0 };
        for (int column = 0; column < 7; column++)
            daysGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        for (int row = 0; row < 6; row++)
            daysGrid.RowDefinitions.Add(new RowDefinition(new GridLength(42)));

        var presetButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
        var yearView = new VerticalStackLayout { Spacing = 14 };
        var monthView = new VerticalStackLayout { Spacing = 12, IsVisible = false };
        Action<bool> setQuickFilterVisibility = _ => { };
        Action<bool> setApplyVisibility = _ => { };

        DeadlineFilterSelection Range(DateTime start, DateTime end, string label) =>
            DeadlineFilterSelection.ForRange(start, end, label);

        IReadOnlyList<Color> IndicatorColors(DateTime date)
        {
            if (!taskDates.TryGetValue(date.Date, out var datedTasks))
                return [];

            var colors = new List<Color>(3);
            if (datedTasks.Any(task => string.Equals(task.Priority, "High", StringComparison.OrdinalIgnoreCase)))
                colors.Add(Color.FromArgb("#DC2626"));
            if (datedTasks.Any(task => string.Equals(task.Priority, "Medium", StringComparison.OrdinalIgnoreCase)))
                colors.Add(Color.FromArgb("#D97706"));
            if (datedTasks.Any(task => string.Equals(task.Priority, "Low", StringComparison.OrdinalIgnoreCase)))
                colors.Add(Color.FromArgb("#16803A"));
            return colors;
        }

        void ShowYearView()
        {
            displayedYear = displayedMonth.Year;
            yearView.IsVisible = true;
            monthView.IsVisible = false;
            setQuickFilterVisibility(true);
            setApplyVisibility(false);
            RenderYear();
        }

        void ShowMonthView(DateTime month)
        {
            displayedMonth = new DateTime(month.Year, month.Month, 1);
            displayedYear = displayedMonth.Year;
            yearView.IsVisible = false;
            monthView.IsVisible = true;
            setQuickFilterVisibility(false);
            setApplyVisibility(true);
            RenderMonth();
        }

        async Task SelectPresetAsync(string name)
        {
            DateTime today = DateTime.Today;
            DateTime weekStart = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
            DateTime monthStart = new(today.Year, today.Month, 1);
            pending = name switch
            {
                "Today" => Range(today, today, "Today"),
                "This Week" => Range(weekStart, weekStart.AddDays(6), "This Week"),
                "Next Week" => Range(weekStart.AddDays(7), weekStart.AddDays(13), "Next Week"),
                "This Month" => Range(monthStart, monthStart.AddMonths(1).AddDays(-1), "This Month"),
                "Next Month" => Range(monthStart.AddMonths(1), monthStart.AddMonths(2).AddDays(-1), "Next Month"),
                _ => DeadlineFilterSelection.AnyDate
            };
            selectedStart = pending.StartDate;
            selectedEnd = pending.EndDate;
            UpdatePresetStyles(name);
            if (selectedStart.HasValue)
                ShowMonthView(selectedStart.Value);
            else
            {
                RenderYear();
                RenderMonth();
            }
        }

        void UpdatePresetStyles(string? selectedName)
        {
            foreach ((string name, Button button) in presetButtons)
            {
                bool selected = string.Equals(name, selectedName, StringComparison.Ordinal);
                button.BackgroundColor = selected ? SelectedDate : Muted;
                button.TextColor = selected ? Colors.White : Color.FromArgb("#374151");
            }
        }

        void SelectCalendarDate(DateTime date)
        {
            if (!selectedStart.HasValue || selectedEnd.HasValue)
            {
                selectedStart = date.Date;
                selectedEnd = null;
            }
            else if (date.Date < selectedStart.Value.Date)
            {
                selectedEnd = selectedStart.Value.Date;
                selectedStart = date.Date;
            }
            else
            {
                selectedEnd = date.Date;
            }

            DateTime end = selectedEnd ?? selectedStart.Value;
            string label = end == selectedStart.Value
                ? selectedStart.Value.ToString("MMM d")
                : $"{selectedStart.Value:MMM d} - {end:MMM d}";
            pending = Range(selectedStart.Value, end, label);
            UpdatePresetStyles(null);
            RenderMonth();
            RenderYear();
        }

        Border CreateMiniMonth(int monthNumber)
        {
            DateTime month = new(displayedYear, monthNumber, 1);
            int monthTaskCount = taskDates
                .Where(entry => entry.Key.Year == displayedYear && entry.Key.Month == monthNumber)
                .SelectMany(entry => entry.Value)
                .Select(task => task.TaskID)
                .Distinct()
                .Count();
            var monthName = new Label
            {
                Text = month.ToString("MMM"),
                FontSize = 13,
                FontAttributes = monthNumber == DateTime.Today.Month && displayedYear == DateTime.Today.Year
                    ? FontAttributes.Bold : FontAttributes.None,
                TextColor = monthNumber == DateTime.Today.Month && displayedYear == DateTime.Today.Year
                    ? Accent : Text
            };
            var monthCountLabel = new Label
            {
                IsVisible = monthTaskCount > 0,
                Text = $"({monthTaskCount})",
                FontSize = 8,
                TextColor = SubtleText,
                VerticalTextAlignment = TextAlignment.Center
            };
            SemanticProperties.SetDescription(monthCountLabel, $"{monthTaskCount} tasks in {month:MMMM}");
            var monthHeader = new HorizontalStackLayout
            {
                Spacing = 4,
                Children = { monthName, monthCountLabel }
            };
            var miniDays = new Grid { ColumnSpacing = 1, RowSpacing = 1 };
            for (int column = 0; column < 7; column++)
                miniDays.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            for (int row = 0; row < 6; row++)
                miniDays.RowDefinitions.Add(new RowDefinition(new GridLength(13)));
            int offset = ((int)month.DayOfWeek + 6) % 7;
            int count = DateTime.DaysInMonth(month.Year, month.Month);
            for (int day = 1; day <= count; day++)
            {
                DateTime date = new(month.Year, month.Month, day);
                int cell = offset + day - 1;
                bool selected = selectedStart.HasValue && date >= selectedStart.Value.Date &&
                                date <= (selectedEnd ?? selectedStart).Value.Date;
                miniDays.Add(new Label
                {
                    Text = day.ToString(),
                    FontSize = 7,
                    TextColor = selected ? Colors.White : Text,
                    BackgroundColor = selected ? SelectedDate : Colors.Transparent,
                    HorizontalTextAlignment = TextAlignment.Center,
                    VerticalTextAlignment = TextAlignment.Center
                }, cell % 7, cell / 7);
            }
            var content = new VerticalStackLayout { Spacing = 4, Children = { monthHeader, miniDays } };
            var border = new Border
            {
                Padding = new Thickness(5),
                BackgroundColor = Colors.Transparent,
                StrokeThickness = 0,
                Content = content
            };
            var tap = new TapGestureRecognizer { CommandParameter = month };
            tap.Tapped += (_, e) => ShowMonthView((DateTime)e.Parameter!);
            border.GestureRecognizers.Add(tap);
            return border;
        }

        void RenderYear()
        {
            yearLabel.Text = displayedYear.ToString();
            yearGrid.Children.Clear();
            for (int month = 1; month <= 12; month++)
                yearGrid.Add(CreateMiniMonth(month), (month - 1) % 3, (month - 1) / 3);
        }

        void RenderMonth()
        {
            monthLabel.Text = displayedMonth.ToString("MMMM");
            monthBackButton.Text = $"\u2039 {displayedMonth:yyyy}";
            daysGrid.Children.Clear();
            int offset = ((int)displayedMonth.DayOfWeek + 6) % 7;
            int count = DateTime.DaysInMonth(displayedMonth.Year, displayedMonth.Month);
            if (selectedStart.HasValue)
            {
                DateTime visibleRangeStart = selectedStart.Value.Date;
                DateTime visibleRangeEnd = (selectedEnd ?? selectedStart).Value.Date;
                for (int row = 0; row < 6; row++)
                {
                    var selectedCells = new List<int>();
                    for (int dayNumber = 1; dayNumber <= count; dayNumber++)
                    {
                        DateTime rowDate = new(displayedMonth.Year, displayedMonth.Month, dayNumber);
                        int rowCell = offset + dayNumber - 1;
                        if (rowCell / 7 == row && rowDate >= visibleRangeStart && rowDate <= visibleRangeEnd)
                            selectedCells.Add(rowCell % 7);
                    }
                    if (selectedCells.Count == 0) continue;

                    int firstColumn = selectedCells.Min();
                    int lastColumn = selectedCells.Max();
                    var rangeBar = new Border
                    {
                        HeightRequest = 36,
                        BackgroundColor = SelectedDate,
                        StrokeThickness = 0,
                        StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 18 },
                        VerticalOptions = LayoutOptions.Center,
                        InputTransparent = true
                    };
                    daysGrid.Add(rangeBar, firstColumn, row);
                    Grid.SetColumnSpan(rangeBar, lastColumn - firstColumn + 1);
                }
            }
            for (int day = 1; day <= count; day++)            {
                DateTime date = new(displayedMonth.Year, displayedMonth.Month, day);
                int cell = offset + day - 1;
                int dateTaskCount = taskDates.TryGetValue(date.Date, out var datedTasks)
                    ? datedTasks.Select(task => task.TaskID).Distinct().Count()
                    : 0;
                bool inRange = selectedStart.HasValue && date >= selectedStart.Value.Date &&
                               date <= (selectedEnd ?? selectedStart).Value.Date;
                bool isToday = date == DateTime.Today;
                var dateLabel = new Label
                {
                    Text = day.ToString(),
                    FontSize = 12,
                    HeightRequest = 36,
                    TextColor = inRange || isToday ? Colors.White : Text,
                    HorizontalTextAlignment = TextAlignment.Center,
                    VerticalTextAlignment = TextAlignment.Center
                };
                var dateTap = new TapGestureRecognizer { CommandParameter = date };
                dateTap.Tapped += (_, e) => SelectCalendarDate((DateTime)e.Parameter!);
                dateLabel.GestureRecognizers.Add(dateTap);

                var cellGrid = new Grid();
                if (isToday && !inRange)
                {
                    cellGrid.Add(new Border
                    {
                        WidthRequest = 36,
                        HeightRequest = 36,
                        StrokeThickness = 0,
                        BackgroundColor = SelectedDate,
                        StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 18 },
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.Center,
                        InputTransparent = true
                    });
                }
                cellGrid.Add(dateLabel);
                if (dateTaskCount > 0)
                {
                    var countBadge = new Border
                    {
                        WidthRequest = 14,
                        HeightRequest = 14,
                        Padding = 0,
                        BackgroundColor = Accent,
                        StrokeThickness = 0,
                        StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 7 },
                        HorizontalOptions = LayoutOptions.End,
                        VerticalOptions = LayoutOptions.Start,
                        Margin = new Thickness(0, -1, 2, 0),
                        InputTransparent = true,
                        Content = new Label
                        {
                            Text = dateTaskCount > 9 ? "9+" : dateTaskCount.ToString(),
                            FontSize = 7,
                            FontAttributes = FontAttributes.Bold,
                            TextColor = Colors.White,
                            HorizontalTextAlignment = TextAlignment.Center,
                            VerticalTextAlignment = TextAlignment.Center,
                            InputTransparent = true
                        }
                    };
                    SemanticProperties.SetDescription(countBadge, $"{dateTaskCount} tasks due on {date:MMMM d}");
                    cellGrid.Add(countBadge);
                }

                IReadOnlyList<Color> indicators = IndicatorColors(date);
                if (indicators.Count > 0)
                {
                    var indicatorRow = new HorizontalStackLayout
                    {
                        Spacing = 3,
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.End,
                        Margin = new Thickness(0, 0, 0, 4),
                        InputTransparent = true
                    };
                    foreach (Color indicator in indicators)
                    {
                        indicatorRow.Children.Add(new BoxView
                        {
                            WidthRequest = 4,
                            HeightRequest = 4,
                            CornerRadius = 2,
                            Color = indicator,
                            InputTransparent = true
                        });
                    }
                    cellGrid.Add(indicatorRow);
                }
                daysGrid.Add(cellGrid, cell % 7, cell / 7);
            }
        }

        bool closing = false;
        async Task CloseAsync(DeadlineFilterSelection? result)
        {
            if (closing) return;
            closing = true;
            await modal.Navigation.PopModalAsync(false);
            completion.TrySetResult(result);
        }
        Button PresetButton(string label)
        {
            var button = new Button
            {
                Text = label,
                FontSize = 10,
                Padding = new Thickness(7, 3),
                HeightRequest = 34,
                MinimumHeightRequest = 34,
                CornerRadius = 10,
                BackgroundColor = Muted,
                TextColor = Color.FromArgb("#374151")
            };
            button.Clicked += async (_, _) =>
            {
                await SelectPresetAsync(label);
                await CloseAsync(pending);
            };
            presetButtons[label] = button;
            return button;
        }

        var quickFilterButton = new Button
        {
            Text = "Today",
            FontSize = 11,
            Padding = new Thickness(18, 5),
            WidthRequest = 96,
            HeightRequest = 40,
            MinimumHeightRequest = 40,
            CornerRadius = 20,
            BackgroundColor = SelectedDate,
            TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.End
        };
        setQuickFilterVisibility = isVisible => quickFilterButton.IsVisible = isVisible;
        var applyButton = new Button
        {
            Text = "Apply",
            FontSize = 11,
            FontAttributes = FontAttributes.Bold,
            Padding = new Thickness(18, 5),
            WidthRequest = 96,
            HeightRequest = 40,
            MinimumHeightRequest = 40,
            CornerRadius = 20,
            BackgroundColor = SelectedDate,
            TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.End
        };
        setApplyVisibility = isVisible => applyButton.IsVisible = isVisible;
        applyButton.IsVisible = false;
        applyButton.Clicked += async (_, _) => await CloseAsync(pending);
        var yearPickerCard = new Border
        {
            WidthRequest = 210,
            Padding = new Thickness(14, 9),
            BackgroundColor = Color.FromArgb("#F3F4F6"),
            Stroke = Accent,
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Opacity = 0,
            Scale = 0.88
        };
        var yearPickerOverlay = new Grid
        {
            IsVisible = false,
            BackgroundColor = Colors.Transparent
        };
        var yearPickerBackdrop = new BoxView { Color = Color.FromArgb("#44000000") };
        yearPickerOverlay.Add(yearPickerBackdrop);
        yearPickerOverlay.Add(yearPickerCard);

        async Task HideYearPickerAsync()
        {
            await Task.WhenAll(yearPickerCard.FadeTo(0, 130), yearPickerCard.ScaleTo(0.92, 130, Easing.CubicIn));
            yearPickerOverlay.IsVisible = false;
        }

        var yearBackdropTap = new TapGestureRecognizer();
        yearBackdropTap.Tapped += async (_, _) => await HideYearPickerAsync();
        yearPickerBackdrop.GestureRecognizers.Add(yearBackdropTap);

        Label YearOption(double fontSize, FontAttributes attributes = FontAttributes.None) => new()
        {
            FontSize = fontSize,
            FontAttributes = attributes,
            TextColor = Text,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            Padding = new Thickness(0, 3)
        };

        var previousYearOption = YearOption(17);
        var currentYearOption = YearOption(28, FontAttributes.Bold);
        var nextYearOption = YearOption(17);
        var yearOptions = new VerticalStackLayout { Spacing = 0 };
        yearOptions.Children.Add(previousYearOption);
        yearOptions.Children.Add(new BoxView { HeightRequest = 1, Color = Color.FromArgb("#9CA3AF") });
        yearOptions.Children.Add(currentYearOption);
        yearOptions.Children.Add(new BoxView { HeightRequest = 1, Color = Color.FromArgb("#9CA3AF") });
        yearOptions.Children.Add(nextYearOption);
        yearPickerCard.Content = yearOptions;

        async Task ChooseYearAsync(int year)
        {
            displayedYear = year;
            displayedMonth = new DateTime(year, displayedMonth.Month, 1);
            RenderYear();
            await HideYearPickerAsync();
        }

        void SetYearOption(Label option, int offset)
        {
            int year = displayedYear + offset;
            option.Text = year.ToString();
            option.GestureRecognizers.Clear();
            var tap = new TapGestureRecognizer { CommandParameter = year };
            tap.Tapped += async (_, e) => await ChooseYearAsync((int)e.Parameter!);
            option.GestureRecognizers.Add(tap);
        }

        async Task ShowYearPickerAsync()
        {
            SetYearOption(previousYearOption, -1);
            SetYearOption(currentYearOption, 0);
            SetYearOption(nextYearOption, 1);
            yearPickerOverlay.IsVisible = true;
            yearPickerCard.Opacity = 0;
            yearPickerCard.Scale = 0.88;
            await Task.WhenAll(yearPickerCard.FadeTo(1, 180), yearPickerCard.ScaleTo(1, 180, Easing.CubicOut));
        }

        var yearTap = new TapGestureRecognizer();
        yearTap.Tapped += async (_, _) => await ShowYearPickerAsync();
        yearLabel.GestureRecognizers.Add(yearTap);
        yearLabel.HorizontalTextAlignment = TextAlignment.Start;
        yearLabel.HorizontalOptions = LayoutOptions.Start;
        var yearTitle = new VerticalStackLayout { Spacing = 5 };
        yearTitle.Children.Add(yearLabel);
        yearTitle.Children.Add(new BoxView
        {
            HeightRequest = 1,
            Color = Color.FromArgb("#D1D5DB"),
            HorizontalOptions = LayoutOptions.Fill
        });
        yearView.Children.Add(yearTitle);        yearView.Children.Add(new ScrollView { Content = yearGrid });

        var previousMonth = NavigationButton("\u2039", 40);
        var nextMonth = NavigationButton("\u203A", 40);
        monthBackButton.Clicked += (_, _) => ShowYearView();
        previousMonth.Clicked += (_, _) => ShowMonthView(displayedMonth.AddMonths(-1));
        nextMonth.Clicked += (_, _) => ShowMonthView(displayedMonth.AddMonths(1));
        var monthHeader = new Grid
        {
            ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto), new(GridLength.Auto) }
        };
        monthHeader.Add(monthBackButton, 0);
        monthHeader.Add(monthLabel, 1);
        monthHeader.Add(previousMonth, 2);
        monthHeader.Add(nextMonth, 3);
        var weekdays = new Grid { ColumnSpacing = 4 };
        string[] weekdayNames = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];
        for (int column = 0; column < 7; column++)
        {
            weekdays.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            weekdays.Add(new Label
            {
                Text = weekdayNames[column], FontSize = 9, TextColor = SubtleText,
                HorizontalTextAlignment = TextAlignment.Center
            }, column);
        }
        monthView.Children.Add(monthHeader);
        monthView.Children.Add(weekdays);
        monthView.Children.Add(daysGrid);

        var quickPickerCard = new Border
        {
            WidthRequest = 210,
            Padding = new Thickness(14, 9),
            BackgroundColor = Color.FromArgb("#F3F4F6"),
            Stroke = Accent,
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Opacity = 0,
            Scale = 0.88
        };
        var quickPickerOverlay = new Grid
        {
            IsVisible = false,
            BackgroundColor = Colors.Transparent
        };
        var quickPickerBackdrop = new BoxView { Color = Color.FromArgb("#44000000") };
        quickPickerOverlay.Add(quickPickerBackdrop);
        quickPickerOverlay.Add(quickPickerCard);

        async Task HideQuickPickerAsync()
        {
            await Task.WhenAll(
                quickPickerCard.FadeTo(0, 130),
                quickPickerCard.ScaleTo(0.92, 130, Easing.CubicIn));
            quickPickerOverlay.IsVisible = false;
        }

        var quickBackdropTap = new TapGestureRecognizer();
        quickBackdropTap.Tapped += async (_, _) => await HideQuickPickerAsync();
        quickPickerBackdrop.GestureRecognizers.Add(quickBackdropTap);

        Label QuickOption(string text, string preset, double fontSize, FontAttributes attributes = FontAttributes.None)
        {
            var option = new Label
            {
                Text = text,
                FontSize = fontSize,
                FontAttributes = attributes,
                TextColor = Text,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                Padding = new Thickness(0, 5)
            };
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) =>
            {
                quickFilterButton.Text = text;
                await SelectPresetAsync(preset);
                await HideQuickPickerAsync();
            };
            option.GestureRecognizers.Add(tap);
            return option;
        }

        var quickOptions = new VerticalStackLayout { Spacing = 0 };
        quickOptions.Children.Add(QuickOption("Today", "Today", 18));
        quickOptions.Children.Add(new BoxView { HeightRequest = 1, Color = Color.FromArgb("#9CA3AF") });
        quickOptions.Children.Add(QuickOption("Week", "This Week", 18));
        quickOptions.Children.Add(new BoxView { HeightRequest = 1, Color = Color.FromArgb("#9CA3AF") });
        quickOptions.Children.Add(QuickOption("Month", "This Month", 18));
        quickPickerCard.Content = quickOptions;

        quickFilterButton.Clicked += async (_, _) =>
        {
            quickPickerOverlay.IsVisible = true;
            quickPickerCard.Opacity = 0;
            quickPickerCard.Scale = 0.88;
            await Task.WhenAll(quickPickerCard.FadeTo(1, 180), quickPickerCard.ScaleTo(1, 180, Easing.CubicOut));
        };
        var closeButton = NavigationButton("\u2190", 38);
        closeButton.BackgroundColor = Colors.Transparent;
        closeButton.TextColor = SelectedDate;
        closeButton.FontSize = 24;
        closeButton.Clicked += async (_, _) => await CloseAsync(null);

        var titleRow = new Grid { HorizontalOptions = LayoutOptions.Start };
        titleRow.Add(closeButton);
        var quickPanel = new Grid
        {
            HorizontalOptions = LayoutOptions.Fill,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };
        quickPanel.Add(quickFilterButton, 2);
        quickPanel.Add(applyButton, 2);
        var calendarHost = new Grid();
        calendarHost.Add(yearView);
        calendarHost.Add(monthView);
        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            },
            RowSpacing = 12,
            Margin = new Thickness(18, 14)
        };
        root.Add(titleRow, 0, 0);
        root.Add(calendarHost, 0, 1);
        root.Add(quickPanel, 0, 2);
        var modalHost = new Grid();
        modalHost.Add(root);
        modalHost.Add(yearPickerOverlay);
        modalHost.Add(quickPickerOverlay);
        modal.Content = modalHost;

        string? currentPreset = current.Label is "Today" or "This Week" or "This Month"
            ? current.Label : null;
        UpdatePresetStyles(currentPreset);
        RenderYear();
        RenderMonth();
        await owner.Navigation.PushModalAsync(modal, false);
        return await completion.Task;
    }

    private static Label HeaderLabel() => new()
    {
        FontSize = 18,
        FontAttributes = FontAttributes.Bold,
        TextColor = Text,
        HorizontalTextAlignment = TextAlignment.Center,
        VerticalTextAlignment = TextAlignment.Center
    };

    private static Button NavigationButton(string label, double width) => new()
    {
        Text = label,
        FontSize = 21,
        Padding = 0,
        WidthRequest = width,
        HeightRequest = 38,
        MinimumHeightRequest = 38,
        CornerRadius = 18,
        BackgroundColor = Muted,
        TextColor = Text
    };

    private static Button ActionButton(string label, Color background, Color foreground) => new()
    {
        Text = label,
        FontSize = 13,
        FontAttributes = FontAttributes.Bold,
        HeightRequest = 44,
        MinimumHeightRequest = 44,
        CornerRadius = 10,
        BackgroundColor = background,
        TextColor = foreground
    };
}



