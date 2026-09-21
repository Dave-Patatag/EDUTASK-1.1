using EDUTASK_1._1.Helpers;
using Microsoft.Maui.Controls.Shapes;

namespace EDUTASK_1._1.Services;

/// <summary>
/// Small calendar navigation dialog. It changes the month being viewed, not
/// the task deadline filter, so it stays deliberately separate from
/// <see cref="DeadlineFilterDialog"/>.
/// </summary>
public static class MonthYearPickerDialog
{
    public static async Task<DateTime?> ShowAsync(Page owner, DateTime current)
    {
        var completion = new TaskCompletionSource<DateTime?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        int selectedYear = current.Year;
        int selectedMonth = current.Month;
        bool closing = false;

        var modal = new ContentPage
        {
            BackgroundColor = AppColors.Scrim,
            Padding = 16
        };
        NavigationPage.SetHasNavigationBar(modal, false);
        Shell.SetNavBarIsVisible(modal, false);

        var monthGrid = new Grid
        {
            ColumnSpacing = 8,
            RowSpacing = 8,
            Margin = new Thickness(0, 12, 0, 8)
        };
        for (int column = 0; column < 3; column++)
            monthGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        for (int row = 0; row < 4; row++)
            monthGrid.RowDefinitions.Add(new RowDefinition(44));

        var yearLabel = new Label
        {
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = AppColors.TextPrimary,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center
        };

        var previousYearButton = YearButton("‹", "Previous year");
        var nextYearButton = YearButton("›", "Next year");
        var applyButton = new Button
        {
            Text = "Apply",
            HeightRequest = 50,
            MinimumHeightRequest = 50,
            CornerRadius = 8,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            BackgroundColor = AppColors.ActionPrimary,
            TextColor = AppColors.TextInverse
        };
        var cancelButton = new Button
        {
            Text = "Cancel",
            HeightRequest = 50,
            MinimumHeightRequest = 50,
            CornerRadius = 8,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            BackgroundColor = AppColors.ActionDismiss,
            TextColor = Colors.White,
            BorderColor = AppColors.ActionDismiss,
            BorderWidth = 1
        };

        var card = new Border
        {
            WidthRequest = 390,
            MaximumWidthRequest = 420,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            Padding = new Thickness(20, 18, 20, 16),
            BackgroundColor = AppColors.SurfaceBase,
            Stroke = AppColors.BorderDefault,
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 18 },
            Shadow = new Shadow
            {
                Brush = AppColors.ShadowOverlay,
                Offset = new Point(0, 6),
                Radius = 18,
                Opacity = 0.6f
            }
        };

        var title = new Label
        {
            Text = "Choose month",
            FontSize = 22,
            FontAttributes = FontAttributes.Bold,
            TextColor = AppColors.TextPrimary,
            VerticalTextAlignment = TextAlignment.Center
        };

        var yearHeader = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(44),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(44)
            },
            Margin = new Thickness(0, 14, 0, 0)
        };
        yearHeader.Add(previousYearButton, 0, 0);
        yearHeader.Add(yearLabel, 1, 0);
        yearHeader.Add(nextYearButton, 2, 0);

        var header = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star) }
        };
        header.Add(title, 0, 0);

        var footer = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            },
            RowSpacing = 10,
            Margin = new Thickness(0, 8, 0, 0)
        };
        footer.Add(applyButton, 0, 0);
        footer.Add(cancelButton, 0, 1);

        card.Content = new VerticalStackLayout
        {
            Spacing = 0,
            Children = { header, yearHeader, monthGrid, footer }
        };

        var backdrop = new BoxView { Color = Colors.Transparent };
        var root = new Grid();
        root.Add(backdrop);
        root.Add(card);
        modal.Content = root;

        Button YearButton(string text, string description)
        {
            var button = new Button
            {
                Text = text,
                WidthRequest = 44,
                HeightRequest = 40,
                Padding = 0,
                CornerRadius = 10,
                FontSize = 24,
                FontAttributes = FontAttributes.Bold,
                BackgroundColor = Colors.Transparent,
                TextColor = AppColors.TextSecondary
            };
            SemanticProperties.SetDescription(button, description);
            return button;
        }

        void RenderMonths()
        {
            yearLabel.Text = selectedYear.ToString();
            monthGrid.Children.Clear();

            for (int month = 1; month <= 12; month++)
            {
                int monthValue = month;
                var button = new Button
                {
                    Text = new DateTime(selectedYear, month, 1).ToString("MMMM"),
                    HeightRequest = 44,
                    CornerRadius = 10,
                    Padding = new Thickness(4, 0),
                    FontSize = 13,
                    FontAttributes = month == selectedMonth ? FontAttributes.Bold : FontAttributes.None,
                    BackgroundColor = month == selectedMonth
                        ? AppColors.CalendarAccent
                        : AppColors.SurfaceMuted,
                    TextColor = month == selectedMonth
                        ? AppColors.TextInverse
                        : AppColors.TextPrimary
                };
                button.Clicked += (_, _) =>
                {
                    selectedMonth = monthValue;
                    RenderMonths();
                };
                monthGrid.Add(button, (month - 1) % 3, (month - 1) / 3);
            }
        }

        async Task CloseAsync(DateTime? result)
        {
            if (closing)
                return;
            closing = true;
            completion.TrySetResult(result);
            if (owner.Navigation.ModalStack.Contains(modal))
                await owner.Navigation.PopModalAsync(false);
        }

        previousYearButton.Clicked += (_, _) =>
        {
            selectedYear--;
            RenderMonths();
        };
        nextYearButton.Clicked += (_, _) =>
        {
            selectedYear++;
            RenderMonths();
        };
        applyButton.Clicked += async (_, _) =>
            await CloseAsync(new DateTime(selectedYear, selectedMonth, 1));
        cancelButton.Clicked += async (_, _) => await CloseAsync(null);
        var backdropTap = new TapGestureRecognizer();
        backdropTap.Tapped += async (_, _) => await CloseAsync(null);
        backdrop.GestureRecognizers.Add(backdropTap);
        modal.Appearing += (_, _) => RenderMonths();

        await owner.Navigation.PushModalAsync(modal, false);
        return await completion.Task;
    }
}
