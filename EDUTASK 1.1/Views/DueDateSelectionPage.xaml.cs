using Microsoft.Maui.Controls.Shapes;
using EDUTASK_1._1.Helpers;
using EDUTASK_1._1.Views.Base;
namespace EDUTASK_1._1.Views;

public partial class DueDateSelectionPage : EduTaskPage
{
    private static readonly Color SelectedColor = AppColors.Brand800;
    private static readonly Color TodayStrokeColor = AppColors.Brand800;
    private static readonly Color PastTextColor = AppColors.BorderStrong;
    private static readonly Color NormalTextColor = AppColors.TextPrimary;

    private readonly TaskCompletionSource<DateTime?> _completion = new();
    private DateTime _displayedMonth;
    private readonly DateTime? _selectedDate;

    public DueDateSelectionPage(DateTime? selectedDate)
    {
        InitializeComponent();
        _selectedDate = selectedDate;
        DateTime initialDate = selectedDate ?? DateTime.Today;
        _displayedMonth = new DateTime(initialDate.Year, initialDate.Month, 1);
        BuildCalendar();
    }

    public async Task<DateTime?> ShowAsync(INavigation navigation)
    {
        await navigation.PushModalAsync(this, false);
        return await _completion.Task;
    }

    protected override bool OnBackButtonPressed()
    {
        _ = CloseAsync(null);
        return true;
    }

    private void OnPreviousMonthClicked(object sender, EventArgs e) { _displayedMonth = _displayedMonth.AddMonths(-1); BuildCalendar(); }
    private void OnNextMonthClicked(object sender, EventArgs e) { _displayedMonth = _displayedMonth.AddMonths(1); BuildCalendar(); }

    private async void OnCancelClicked(object sender, EventArgs e) => await CloseAsync(null);

    private void BuildCalendar()
    {
        YearLabel.Text = _displayedMonth.ToString("yyyy");
        MonthLabel.Text = _displayedMonth.ToString("MMMM");
        DateTime today = DateTime.Today;

        CalendarGrid.Children.Clear();
        CalendarGrid.RowDefinitions.Clear();
        CalendarGrid.ColumnDefinitions.Clear();
        for (int column = 0; column < 7; column++)
            CalendarGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        CalendarGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        string[] weekdays = ["S", "M", "T", "W", "T", "F", "S"];
        for (int column = 0; column < weekdays.Length; column++)
        {
            var weekday = new Label { Text = weekdays[column], FontSize = AppTypography.Caption, TextColor = AppColors.TextTertiary, HorizontalTextAlignment = TextAlignment.Center };
            CalendarGrid.Add(weekday, column, 0);
        }
        int daysInMonth = DateTime.DaysInMonth(_displayedMonth.Year, _displayedMonth.Month);
        int startOffset = (int)_displayedMonth.DayOfWeek;
        int rows = (int)Math.Ceiling((startOffset + daysInMonth) / 7.0);
        for (int row = 0; row < rows; row++) CalendarGrid.RowDefinitions.Add(new RowDefinition(new GridLength(42)));
        for (int cell = 0; cell < rows * 7; cell++)
        {
            int dayNumber = cell - startOffset + 1;
            if (dayNumber < 1 || dayNumber > daysInMonth) continue;
            DateTime cellDate = new(_displayedMonth.Year, _displayedMonth.Month, dayNumber);
            bool isPast = cellDate.Date < today;
            bool isSelected = _selectedDate?.Date == cellDate.Date;
            bool isToday = cellDate.Date == today;

            var dayLabel = new Label
            {
                Text = dayNumber.ToString(),
                FontSize = 12,
                TextColor = isPast ? PastTextColor : isSelected ? AppColors.TextInverse : NormalTextColor,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            };
            var dayBorder = new Border
            {
                HeightRequest = 28,
                WidthRequest = 28,
                Padding = 0,
                StrokeThickness = isToday && !isSelected ? 1 : 0,
                Stroke = TodayStrokeColor,
                BackgroundColor = isSelected ? SelectedColor : Colors.Transparent,
                StrokeShape = new RoundRectangle { CornerRadius = 14 },
                Content = dayLabel
            };

            if (!isPast)
            {
                var tap = new TapGestureRecognizer();
                tap.Tapped += (_, _) => _ = CloseAsync(cellDate);
                dayBorder.GestureRecognizers.Add(tap);
            }

            Grid.SetRow(dayBorder, cell / 7 + 1); Grid.SetColumn(dayBorder, cell % 7); CalendarGrid.Add(dayBorder);
        }
    }

    private async Task CloseAsync(DateTime? result)
    {
        if (_completion.Task.IsCompleted)
            return;

        _completion.TrySetResult(result);
        await Navigation.PopModalAsync(false);
    }
}
