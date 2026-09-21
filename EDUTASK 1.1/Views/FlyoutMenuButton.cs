using EDUTASK_1._1.Helpers;
using Microsoft.Maui.Controls.Shapes;

namespace EDUTASK_1._1.Views;

/// <summary>
/// The shared flyout affordance used by every top-level flyout page.
/// Keeping the visual and tap behavior here prevents page headers from
/// drifting away from the Tasks dashboard standard.
/// </summary>
public sealed class FlyoutMenuButton : ContentView
{
    public FlyoutMenuButton()
    {
        WidthRequest = 38;
        HeightRequest = 38;
        HorizontalOptions = LayoutOptions.Start;
        VerticalOptions = LayoutOptions.Center;

        var icon = new Image
        {
            Source = "menubar.png",
            WidthRequest = 19,
            HeightRequest = 19,
            Aspect = Aspect.AspectFit,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };

        var button = new Border
        {
            WidthRequest = 38,
            HeightRequest = 38,
            Padding = 0,
            BackgroundColor = AppColors.SurfaceBase,
            Stroke = AppColors.BorderSubtle,
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Shadow = new Shadow
            {
                Brush = Colors.Black,
                Offset = new Point(0, 1),
                Radius = 3,
                Opacity = 0.07f
            },
            Content = icon
        };

        SemanticProperties.SetDescription(button, "Open navigation menu");
        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) =>
        {
            if (DashboardFlyoutPage.Current is { } flyout)
                flyout.IsPresented = true;
        };
        button.GestureRecognizers.Add(tap);
        Content = button;
    }
}
