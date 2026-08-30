using Microsoft.Maui.Controls.Shapes;

namespace EDUTASK_1._1.Helpers;

/// <summary>
/// Keeps filter triggers visually consistent without changing their labels or
/// filtering behavior.
/// </summary>
public static class FilterButtonState
{
    public const double Height = 32;

    public static void Apply(Border button, bool active, params Label[] labels)
    {
        button.HeightRequest = Height;
        button.MinimumHeightRequest = Height;
        button.StrokeShape = new RoundRectangle { CornerRadius = 9 };
        button.BackgroundColor = active ? AppColors.Brand800 : AppColors.Slate100;
        button.Stroke = Colors.Transparent;
        button.StrokeThickness = 0;

        Color foreground = active ? AppColors.TextInverse : AppColors.TextPrimary;
        foreach (Label label in labels)
        {
            label.TextColor = foreground;
            label.FontAttributes = active ? FontAttributes.Bold : FontAttributes.None;
        }
    }

    public static void Apply(Border button, bool active, Image icon)
    {
        Apply(button, active);
        icon.Source = active ? "whitefiltericon.png" : "blackfiltericon.png";
    }

    public static void Apply(Button button, bool active)
    {
        button.HeightRequest = Height;
        button.MinimumHeightRequest = Height;
        button.CornerRadius = (int)(Height / 2);
        button.Padding = new Thickness(12, 0);
        button.FontSize = AppTypography.Caption;
        button.BackgroundColor = active ? AppColors.Brand800 : AppColors.Slate100;
        button.BorderColor = Colors.Transparent;
        button.BorderWidth = 0;
        button.TextColor = active ? AppColors.TextInverse : AppColors.TextPrimary;
        button.FontAttributes = active ? FontAttributes.Bold : FontAttributes.None;
    }

    public static void TrackDropdown(
        DropdownController dropdown,
        Border button,
        string inactiveValue,
        params Label[] labels)
    {
        dropdown.SelectedLabelColor = AppColors.TextInverse;
        dropdown.PlaceholderLabelColor = AppColors.TextPrimary;

        dropdown.Opened += (_, _) => Apply(button, active: true, labels);
        dropdown.Closed += (_, _) => Apply(
            button,
            !string.Equals(dropdown.SelectedValue, inactiveValue, StringComparison.Ordinal),
            labels);

        Apply(
            button,
            !string.Equals(dropdown.SelectedValue, inactiveValue, StringComparison.Ordinal),
            labels);
    }
}
