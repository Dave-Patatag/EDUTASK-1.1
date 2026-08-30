using EDUTASK_1._1.Helpers;

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
    private static readonly Color Accent = AppColors.Accent500;
    private static readonly Color SelectedDate = AppColors.CalendarAccent;
    private static readonly Color Muted = AppColors.SurfaceSubtle;
    private static readonly Color Text = AppColors.TextPrimary;
    private static readonly Color SubtleText = AppColors.TextTertiary;

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
            .Where(task => task.Deadline.HasValue && !task.IsCompleted)
            .GroupBy(task => task.Deadline!.Value.Date)
            .ToDictionary(group => group.Key, group => group.ToList());

        var modal = new ContentPage
        {
            // Keep the page behind the sheet visible through a scrim. The
            // actual filter surface is attached to the bottom below.
            BackgroundColor = AppColors.ScrimLight,
            Padding = 0
        };

        var yearLabel = HeaderLabel();
        yearLabel.FontSize = AppTypography.Display;
        yearLabel.FontAttributes = FontAttributes.Bold;
        var monthLabel = HeaderLabel();
        monthLabel.FontSize = AppTypography.Heading;
        var monthBackButton = NavigationButton(string.Empty, 40);
        var yearGrid = new Grid { ColumnSpacing = 12, RowSpacing = 16 };
        for (int column = 0; column < 3; column++)
            yearGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        for (int row = 0; row < 4; row++)
            yearGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        var daysGrid = new Grid { RowSpacing = 4, ColumnSpacing = 0 };
        for (int column = 0; column < 7; column++)
            daysGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        for (int row = 0; row < 6; row++)
            daysGrid.RowDefinitions.Add(new RowDefinition(new GridLength(38)));

        var yearView = new VerticalStackLayout { Spacing = 14, HorizontalOptions = LayoutOptions.Fill };
        var monthView = new VerticalStackLayout { Spacing = 12, IsVisible = false };
        var selectionSummaryLabel = new Label
        {
            Text = current.StartDate.HasValue
                ? current.EndDate.HasValue
                    ? $"{current.StartDate:dd MMM yyyy} – {current.EndDate:dd MMM yyyy}"
                    : current.StartDate.Value.ToString("dd MMM yyyy")
                : "Choose a date or range",
            FontSize = AppTypography.Caption,
            TextColor = SubtleText,
            VerticalTextAlignment = TextAlignment.Center
        };
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
                colors.Add(AppColors.StatusDanger);
            if (datedTasks.Any(task => string.Equals(task.Priority, "Medium", StringComparison.OrdinalIgnoreCase)))
                colors.Add(AppColors.StatusWarning);
            if (datedTasks.Any(task => string.Equals(task.Priority, "Low", StringComparison.OrdinalIgnoreCase)))
                colors.Add(AppColors.StatusSuccess);
            return colors;
        }

        void ShowMonthView(DateTime month)
        {
            displayedMonth = new DateTime(month.Year, month.Month, 1);
            displayedYear = displayedMonth.Year;
            yearView.IsVisible = false;
            monthView.IsVisible = true;
            setQuickFilterVisibility(true);
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
                "This Year" => Range(new DateTime(today.Year, 1, 1), new DateTime(today.Year, 12, 31), "This Year"),
                _ => DeadlineFilterSelection.AnyDate
            };
            selectedStart = pending.StartDate;
            selectedEnd = pending.EndDate;
            selectionSummaryLabel.Text = pending.StartDate.HasValue
                ? pending.EndDate.HasValue
                    ? $"{pending.StartDate:dd MMM yyyy} – {pending.EndDate:dd MMM yyyy}"
                    : pending.StartDate.Value.ToString("dd MMM yyyy")
                : "Choose a date or range";
            if (selectedStart.HasValue)
                ShowMonthView(selectedStart.Value);
            else
            {
                RenderYear();
                RenderMonth();
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
            selectionSummaryLabel.Text = selectedEnd.HasValue
                ? $"{selectedStart:dd MMM yyyy} – {selectedEnd:dd MMM yyyy}"
                : selectedStart.Value.ToString("dd MMM yyyy");
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
                FontSize = AppTypography.BodySmall,
                FontAttributes = monthNumber == DateTime.Today.Month && displayedYear == DateTime.Today.Year
                    ? FontAttributes.Bold : FontAttributes.None,
                TextColor = monthNumber == DateTime.Today.Month && displayedYear == DateTime.Today.Year
                    ? Accent : Text
            };
            var monthCountLabel = new Label
            {
                IsVisible = monthTaskCount > 0,
                Text = $"({monthTaskCount})",
                FontSize = AppTypography.Micro,
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
                    FontSize = AppTypography.Micro,
                    TextColor = selected ? AppColors.TextInverse : Text,
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
            monthLabel.Text = displayedMonth.ToString("MMMM yyyy");
            monthBackButton.Text = "\u2039";
            daysGrid.Children.Clear();
            int offset = ((int)displayedMonth.DayOfWeek + 6) % 7;
            int count = DateTime.DaysInMonth(displayedMonth.Year, displayedMonth.Month);

            void AddAdjacentMonthDate(DateTime date, int cell)
            {
                var dateLabel = new Label
                {
                    Text = date.Day.ToString(),
                    FontSize = AppTypography.Caption,
                    HeightRequest = 34,
                    TextColor = AppColors.TextDisabled,
                    HorizontalTextAlignment = TextAlignment.Center,
                    VerticalTextAlignment = TextAlignment.Center
                };
                var dateTap = new TapGestureRecognizer { CommandParameter = date };
                dateTap.Tapped += (_, e) =>
                {
                    DateTime selectedDate = (DateTime)e.Parameter!;
                    displayedMonth = new DateTime(selectedDate.Year, selectedDate.Month, 1);
                    SelectCalendarDate(selectedDate);
                };
                dateLabel.GestureRecognizers.Add(dateTap);
                SemanticProperties.SetDescription(dateLabel, $"{date:MMMM d}, outside the displayed month");
                daysGrid.Add(dateLabel, cell % 7, cell / 7);
            }

            for (int cell = 0; cell < offset; cell++)
                AddAdjacentMonthDate(displayedMonth.AddDays(cell - offset), cell);
            for (int cell = offset + count; cell < 42; cell++)
                AddAdjacentMonthDate(displayedMonth.AddDays(cell - offset), cell);

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
                        HeightRequest = 34,
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
                    FontSize = AppTypography.Caption,
                    HeightRequest = 34,
                    TextColor = inRange || isToday ? AppColors.TextInverse : Text,
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
                        HeightRequest = 34,
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
                        BackgroundColor = AppColors.Accent500,
                        StrokeThickness = 0,
                        StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 7 },
                        HorizontalOptions = LayoutOptions.End,
                        VerticalOptions = LayoutOptions.Start,
                        Margin = new Thickness(0, -1, 2, 0),
                        InputTransparent = true,
                        Content = new Label
                        {
                            Text = dateTaskCount > 9 ? "9+" : dateTaskCount.ToString(),
                            FontSize = AppTypography.Micro,
                            FontAttributes = FontAttributes.Bold,
                            TextColor = AppColors.SurfaceBase,
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
        void SetCircularIconState(
            ImageButton button,
            bool active,
            string restingIcon,
            string activeIcon)
        {
            button.BackgroundColor = active ? SelectedDate : AppColors.SurfaceBase;
            button.Source = active ? activeIcon : restingIcon;
        }

        void AddCircularPressFeedback(
            ImageButton button,
            string restingIcon,
            string activeIcon)
        {
            button.Pressed += (_, _) => SetCircularIconState(button, true, restingIcon, activeIcon);
            button.Released += (_, _) => SetCircularIconState(button, false, restingIcon, activeIcon);
        }

        var quickMenuButton = new ImageButton
        {
            Source = "blackfiltericon.png",
            Padding = 12,
            WidthRequest = 48,
            HeightRequest = 48,
            CornerRadius = 24,
            BackgroundColor = AppColors.SurfaceBase,
            BorderWidth = 0,
            HorizontalOptions = LayoutOptions.Start,
            IsVisible = true
        };
        SemanticProperties.SetDescription(quickMenuButton, "Open quick date filters");
        AddCircularPressFeedback(quickMenuButton, "blackfiltericon.png", "whitefiltericon.png");
        // Keep this sheet focused on one primary action. Preset selection is
        // still supported by the existing logic, but its extra trigger is
        // intentionally hidden from the compact date surface.
        setQuickFilterVisibility = _ => quickMenuButton.IsVisible = false;
        var applyButton = new Button
        {
            Text = "✓",
            FontSize = 24,
            FontAttributes = FontAttributes.Bold,
            Padding = 0,
            WidthRequest = 48,
            HeightRequest = 48,
            MinimumHeightRequest = 48,
            CornerRadius = 24,
            BackgroundColor = AppColors.SurfaceBase,
            TextColor = AppColors.TextPrimary,
            HorizontalOptions = LayoutOptions.End
        };
        applyButton.Text = "Set";
        applyButton.FontSize = AppTypography.BodySmall;
        applyButton.WidthRequest = -1;
        // Keep the primary action compact while preserving a comfortable
        // Android touch target for the bottom sheet.
        applyButton.HeightRequest = 50;
        applyButton.MinimumHeightRequest = 50;
        applyButton.CornerRadius = 8;
        applyButton.BackgroundColor = AppColors.Brand800;
        applyButton.TextColor = AppColors.TextInverse;
        applyButton.BorderWidth = 0;
        applyButton.HorizontalOptions = LayoutOptions.Fill;
        SemanticProperties.SetDescription(applyButton, "Apply date filter");
        setApplyVisibility = isVisible => applyButton.IsVisible = isVisible;
        applyButton.IsVisible = true;
        applyButton.Clicked += async (_, _) => await CloseAsync(pending);
        var yearPickerCard = new Border
        {
            WidthRequest = 210,
            Padding = new Thickness(14, 9),
            BackgroundColor = AppColors.SurfaceSubtle,
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
        var yearPickerBackdrop = new BoxView { Color = AppColors.ScrimLight };
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
        yearOptions.Children.Add(new BoxView { HeightRequest = 1, Color = AppColors.TextDisabled });
        yearOptions.Children.Add(currentYearOption);
        yearOptions.Children.Add(new BoxView { HeightRequest = 1, Color = AppColors.TextDisabled });
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
        var nextYearButton = new ImageButton
        {
            Source = "uncollapse.png",
            WidthRequest = 24,
            HeightRequest = 16,
            Padding = 0,
            BackgroundColor = Colors.Transparent,
            HorizontalOptions = LayoutOptions.Center
        };
        var previousYearButton = new ImageButton
        {
            Source = "collapse.png",
            WidthRequest = 24,
            HeightRequest = 16,
            Padding = 0,
            BackgroundColor = Colors.Transparent,
            HorizontalOptions = LayoutOptions.Center
        };
        nextYearButton.Clicked += (_, _) =>
        {
            displayedYear++;
            displayedMonth = new DateTime(displayedYear, displayedMonth.Month, 1);
            RenderYear();
        };
        previousYearButton.Clicked += (_, _) =>
        {
            displayedYear--;
            displayedMonth = new DateTime(displayedYear, displayedMonth.Month, 1);
            RenderYear();
        };
        SemanticProperties.SetDescription(nextYearButton, "Next year");
        SemanticProperties.SetDescription(previousYearButton, "Previous year");
        var yearNavigation = new VerticalStackLayout
        {
            Spacing = 0,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Children = { nextYearButton, previousYearButton }
        };
        var yearHeader = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 10,
            HorizontalOptions = LayoutOptions.Fill
        };
        yearHeader.Add(yearLabel, 0);
        yearHeader.Add(yearNavigation, 1);
        var yearTitle = new VerticalStackLayout { Spacing = 5, HorizontalOptions = LayoutOptions.Fill };
        yearTitle.Children.Add(yearHeader);
        yearTitle.Children.Add(new BoxView
        {
            HeightRequest = 1,
            Color = AppColors.BorderStrong,
            HorizontalOptions = LayoutOptions.Fill
        });
        yearView.Children.Add(yearTitle);        yearView.Children.Add(new ScrollView { Content = yearGrid });

        var previousMonth = NavigationButton("\u2039", 40);
        var nextMonth = NavigationButton("\u203A", 40);
        monthBackButton.Text = "\u2039";
        monthBackButton.Clicked += (_, _) => ShowMonthView(displayedMonth.AddMonths(-1));
        nextMonth.Clicked += (_, _) => ShowMonthView(displayedMonth.AddMonths(1));
        var monthHeader = new Grid
        {
            HeightRequest = 38,
            VerticalOptions = LayoutOptions.Center,
            ColumnSpacing = 4,
            ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) }
        };
        monthBackButton.VerticalOptions = LayoutOptions.Center;
        monthLabel.HeightRequest = 38;
        monthLabel.VerticalOptions = LayoutOptions.Center;
        monthHeader.Add(monthBackButton, 0);
        monthHeader.Add(monthLabel, 1);
        monthHeader.Add(nextMonth, 2);
        var monthDivider = new BoxView
        {
            HeightRequest = 1,
            Color = AppColors.BorderDefault,
            Margin = new Thickness(0, 0, 0, 2)
        };
        var weekdays = new Grid { ColumnSpacing = 4 };
        string[] weekdayNames = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];
        for (int column = 0; column < 7; column++)
        {
            weekdays.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            weekdays.Add(new Label
            {
                Text = weekdayNames[column], FontSize = AppTypography.Micro, TextColor = SubtleText,
                HorizontalTextAlignment = TextAlignment.Center
            }, column);
        }
        monthView.Children.Add(monthHeader);
        monthView.Children.Add(monthDivider);
        monthView.Children.Add(weekdays);
        monthView.Children.Add(daysGrid);
        var monthScroll = new ScrollView
        {
            Content = monthView,
            VerticalScrollBarVisibility = ScrollBarVisibility.Never
        };

        var quickPickerCard = new Border
        {
            Padding = new Thickness(0, 10, 0, 18),
            BackgroundColor = AppColors.SurfaceBase,
            Stroke = Colors.Transparent,
            StrokeThickness = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle
            {
                CornerRadius = new CornerRadius(24, 24, 0, 0)
            },
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.End,
            Opacity = 1,
            TranslationY = 220
        };
        var quickPickerOverlay = new Grid
        {
            IsVisible = false,
            BackgroundColor = Colors.Transparent
        };
        var quickPickerBackdrop = new BoxView { Color = AppColors.ScrimLight };
        quickPickerOverlay.Add(quickPickerBackdrop);
        quickPickerOverlay.Add(quickPickerCard);

        async Task HideQuickPickerAsync()
        {
            await Task.WhenAll(
                quickPickerBackdrop.FadeTo(0, Motion.Fast, Motion.Exit),
                quickPickerCard.TranslateTo(0, 220, Motion.Fast, Motion.Exit));
            quickPickerOverlay.IsVisible = false;
            quickPickerBackdrop.Opacity = 1;
            quickPickerCard.TranslationY = 220;
            SetCircularIconState(quickMenuButton, false, "blackfiltericon.png", "whitefiltericon.png");
        }

        var quickBackdropTap = new TapGestureRecognizer();
        quickBackdropTap.Tapped += async (_, _) => await HideQuickPickerAsync();
        quickPickerBackdrop.GestureRecognizers.Add(quickBackdropTap);

        Border QuickOption(string text, string preset)
        {
            var label = new Label
            {
                Text = text,
                FontSize = AppTypography.Body,
                FontAttributes = FontAttributes.Bold,
                TextColor = Text,
                VerticalTextAlignment = TextAlignment.Center
            };
            var option = new Border
            {
                HeightRequest = 46,
                Padding = new Thickness(28, 0),
                BackgroundColor = Colors.Transparent,
                StrokeThickness = 0,
                Content = label
            };
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) =>
            {
                await SelectPresetAsync(preset);
                await HideQuickPickerAsync();
            };
            option.GestureRecognizers.Add(tap);
            return option;
        }

        var quickOptions = new VerticalStackLayout { Spacing = 0 };
        quickOptions.Children.Add(new BoxView
        {
            WidthRequest = 42,
            HeightRequest = 4,
            CornerRadius = 2,
            Color = AppColors.Slate200,
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 0, 0, 8)
        });
        quickOptions.Children.Add(QuickOption("Today", "Today"));
        quickOptions.Children.Add(new BoxView { HeightRequest = 1, Color = AppColors.BorderDefault });
        quickOptions.Children.Add(QuickOption("This Week", "This Week"));
        quickOptions.Children.Add(new BoxView { HeightRequest = 1, Color = AppColors.BorderDefault });
        quickOptions.Children.Add(QuickOption("Next Week", "Next Week"));
        quickOptions.Children.Add(new BoxView { HeightRequest = 1, Color = AppColors.BorderDefault });
        quickOptions.Children.Add(QuickOption("This Month", "This Month"));
        quickOptions.Children.Add(new BoxView { HeightRequest = 1, Color = AppColors.BorderDefault });
        quickOptions.Children.Add(QuickOption("Next Month", "Next Month"));
        quickOptions.Children.Add(new BoxView { HeightRequest = 1, Color = AppColors.BorderDefault });
        quickOptions.Children.Add(QuickOption("This Year", "This Year"));
        quickPickerCard.Content = quickOptions;

        quickMenuButton.Clicked += async (_, _) =>
        {
            SetCircularIconState(quickMenuButton, true, "blackfiltericon.png", "whitefiltericon.png");
            quickPickerOverlay.IsVisible = true;
            quickPickerBackdrop.Opacity = Motion.ReduceMotion ? 1 : 0;
            quickPickerCard.TranslationY = Motion.ReduceMotion ? 0 : 220;
            if (!Motion.ReduceMotion)
            {
                await Task.WhenAll(
                    quickPickerBackdrop.FadeTo(1, Motion.Fast, Motion.Enter),
                    quickPickerCard.TranslateTo(0, 0, Motion.Base, Motion.Emphasis));
            }
        };
        var closeButton = new ImageButton
        {
            Source = "backicon.png",
            WidthRequest = 42,
            HeightRequest = 38,
            Padding = 10,
            CornerRadius = 12,
            BackgroundColor = AppColors.SurfaceBase,
            BorderWidth = 0
        };
        SemanticProperties.SetDescription(closeButton, "Close calendar filter");
        AddCircularPressFeedback(closeButton, "backicon.png", "whitebackicon.png");
        closeButton.Clicked += async (_, _) => await CloseAsync(null);

        var titleRow = new Grid
        {
            HorizontalOptions = LayoutOptions.Fill,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };
        titleRow.Add(closeButton, 0);
        var whenTitle = new VerticalStackLayout
        {
            Spacing = 2,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label
                {
                    Text = "When",
                    FontSize = AppTypography.Title,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Text
                },
                selectionSummaryLabel
            }
        };
        titleRow.Add(whenTitle, 1);
        var titleBlock = new VerticalStackLayout
        {
            Spacing = 12,
            Children =
            {
                titleRow,
                new BoxView
                {
                    HeightRequest = 1,
                    Color = AppColors.BorderDefault
                }
            }
        };
        var quickPanel = new Grid
        {
            HorizontalOptions = LayoutOptions.Fill,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star)
            }
        };
        quickPanel.Add(applyButton, 0);
        var calendarHost = new Grid();
        calendarHost.Add(yearView);
        calendarHost.Add(monthScroll);
        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            RowSpacing = 8,
            Margin = new Thickness(12, 6)
        };
        // Keep the sheet focused on the calendar itself. The previous
        // “When / selected range” block consumed valuable vertical space on
        // Android without adding information that the calendar does not show.
        root.Add(calendarHost, 0, 0);
        root.Add(quickPanel, 0, 1);
        var sheetHandle = new BoxView
        {
            WidthRequest = 42,
            HeightRequest = 4,
            CornerRadius = 2,
            Color = AppColors.Slate200,
            HorizontalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, 0, 0, 6)
        };
        var sheetContent = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            }
        };
        sheetContent.Add(sheetHandle, 0, 0);
        sheetContent.Add(root, 0, 1);
        sheetContent.Add(yearPickerOverlay, 0, 1);
        sheetContent.Add(quickPickerOverlay, 0, 1);

        var sheet = new Border
        {
            BackgroundColor = AppColors.SurfaceBase,
            Stroke = Colors.Transparent,
            StrokeThickness = 0,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.End,
            // Android gives a ScrollView inside an auto row all remaining
            // height. Fix the sheet viewport to the calendar's real content
            // height so no blank band is inserted above the Set button.
            HeightRequest = 470,
            MaximumHeightRequest = 470,
            Padding = new Thickness(0, 8, 0, 8),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle
            {
                CornerRadius = new CornerRadius(24, 24, 0, 0)
            },
            Content = sheetContent
        };
        var modalBackdrop = new BoxView { Color = Colors.Transparent };
        var modalBackdropTap = new TapGestureRecognizer();
        modalBackdropTap.Tapped += async (_, _) => await CloseAsync(null);
        modalBackdrop.GestureRecognizers.Add(modalBackdropTap);

        var modalHost = new Grid();
        modalHost.Add(modalBackdrop);
        modalHost.Add(sheet);
        modal.Content = modalHost;

        string? currentPreset = current.Label is "Today" or "This Week" or "This Month"
            ? current.Label : null;
        ShowMonthView(displayedMonth);
        await owner.Navigation.PushModalAsync(modal, false);
        return await completion.Task;
    }

    private static Label HeaderLabel() => new()
    {
        FontSize = AppTypography.Heading,
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
        BackgroundColor = Colors.Transparent,
        TextColor = Text,
        HorizontalOptions = LayoutOptions.Center,
        VerticalOptions = LayoutOptions.Center
    };

    private static Button ActionButton(string label, Color background, Color foreground) => new()
    {
        Text = label,
        FontSize = AppTypography.BodySmall,
        FontAttributes = FontAttributes.Bold,
        HeightRequest = 44,
        MinimumHeightRequest = 44,
        CornerRadius = 10,
        BackgroundColor = background,
        TextColor = foreground
    };
}
