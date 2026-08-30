using EDUTASK_1._1.Helpers;
using EDUTASK_1._1.Services;

namespace EDUTASK_1._1.Views;

public enum BottomNavigationTab
{
    None,
    Tasks,
    Notification,
    Profile
}

/// <summary>
/// Shared navigation used by full-screen app destinations.
/// It owns role-aware routing so every page reaches the same cached task,
/// notification, and profile tabs.
/// </summary>
public sealed class BottomNavigationBar : ContentView
{
    public static readonly BindableProperty ActiveTabProperty = BindableProperty.Create(
        nameof(ActiveTab),
        typeof(BottomNavigationTab),
        typeof(BottomNavigationBar),
        BottomNavigationTab.None,
        propertyChanged: static (bindable, _, _) =>
            ((BottomNavigationBar)bindable).UpdateVisualState());

    private readonly Image _tasksIcon;
    private readonly Image _notificationIcon;
    private readonly Image _profileIcon;
    private readonly Label _tasksLabel;
    private readonly Label _notificationLabel;
    private readonly Label _profileLabel;
    private readonly Border _notificationBadge;
    private bool _isListeningForBadgeChanges;

    public BottomNavigationTab ActiveTab
    {
        get => (BottomNavigationTab)GetValue(ActiveTabProperty);
        set => SetValue(ActiveTabProperty, value);
    }

    public BottomNavigationBar()
    {
        BackgroundColor = Colors.Transparent;
        HeightRequest = 70;
        MinimumHeightRequest = 70;
        MaximumHeightRequest = 70;

        var navigation = new Grid
        {
            BackgroundColor = Colors.Transparent,
            HeightRequest = 58,
            RowDefinitions =
            {
                new RowDefinition(new GridLength(58))
            },
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            }
        };

        (_tasksIcon, _tasksLabel, Grid tasksItem) =
            CreateItem("Task", "Open tasks", OnTasksTapped);
        (_notificationIcon, _notificationLabel, Grid notificationItem) =
            CreateItem("Notification", "Open notifications", OnNotificationTapped);
        _notificationBadge = new Border
        {
            WidthRequest = 7,
            HeightRequest = 7,
            BackgroundColor = AppColors.StatusDanger,
            StrokeThickness = 0,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            TranslationX = 8,
            TranslationY = -8,
            IsVisible = false,
            InputTransparent = true
        };
        _notificationBadge.StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 4 };
        notificationItem.Add(_notificationBadge, 0, 0);
        _notificationBadge.ZIndex = 2;
        (_profileIcon, _profileLabel, Grid profileItem) =
            CreateItem("Profile", "Open profile", OnProfileTapped);

        navigation.Add(tasksItem, 0, 0);
        navigation.Add(notificationItem, 1, 0);
        navigation.Add(profileItem, 2, 0);

        var shell = new Border
        {
            BackgroundColor = AppColors.SurfaceBase,
            Stroke = AppColors.BorderSubtle,
            StrokeThickness = 1,
            Margin = new Thickness(6, 5, 6, 7),
            Padding = 0,
            Content = navigation
        };
        shell.StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 18 };
        Content = shell;
        UpdateVisualState();
        _ = RefreshNotificationBadgeAsync();
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (Handler is not null && !_isListeningForBadgeChanges)
        {
            NotificationBadgeState.BadgeChanged += OnNotificationBadgeChanged;
            NotificationBadgeState.RefreshRequested += OnNotificationBadgeRefreshRequested;
            _isListeningForBadgeChanges = true;
            _ = RefreshNotificationBadgeAsync();
        }
        else if (Handler is null && _isListeningForBadgeChanges)
        {
            NotificationBadgeState.BadgeChanged -= OnNotificationBadgeChanged;
            NotificationBadgeState.RefreshRequested -= OnNotificationBadgeRefreshRequested;
            _isListeningForBadgeChanges = false;
        }
    }

    private static (Image Icon, Label Label, Grid Item) CreateItem(
        string labelText,
        string semanticDescription,
        EventHandler<TappedEventArgs> tapped)
    {
        var icon = new Image
        {
            WidthRequest = 24,
            HeightRequest = 24,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };

        var label = new Label
        {
            Text = labelText,
            FontSize = AppTypography.Micro,
            FontAttributes = FontAttributes.Bold,
            TextColor = AppColors.Brand600,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            HeightRequest = 18,
            LineBreakMode = LineBreakMode.NoWrap,
            Opacity = 0
        };

        var item = new Grid
        {
            HeightRequest = 53,
            Padding = new Thickness(0, 4, 0, 3),
            RowSpacing = 0,
            RowDefinitions =
            {
                new RowDefinition(new GridLength(28)),
                new RowDefinition(new GridLength(18))
            },
            BackgroundColor = Colors.Transparent
        };
        item.Add(icon, 0, 0);
        item.Add(label, 0, 1);
        SemanticProperties.SetDescription(item, semanticDescription);

        var tap = new TapGestureRecognizer();
        tap.Tapped += tapped;
        item.GestureRecognizers.Add(tap);

        return (icon, label, item);
    }

    private void UpdateVisualState()
    {
        SetTabState(_tasksIcon, _tasksLabel, ActiveTab == BottomNavigationTab.Tasks,
            "taskselected.png", "taskunselected.png", selectedOffset: 0, defaultOffset: 5);
        SetTabState(_notificationIcon, _notificationLabel, ActiveTab == BottomNavigationTab.Notification,
            "selectedinbox.png", "defaultinbox.png", selectedOffset: 2, defaultOffset: 7);
        SetTabState(_profileIcon, _profileLabel, ActiveTab == BottomNavigationTab.Profile,
            "selectedprofile.png", "defaultprofile.png", selectedOffset: 0, defaultOffset: 6);
        _notificationBadge.IsVisible = ActiveTab != BottomNavigationTab.Notification && _notificationBadge.IsVisible;
    }

    public async Task RefreshNotificationBadgeAsync()
    {
        try
        {
            string type;
            int id;
            if (TeacherSessionService.CurrentTeacher is { } teacher)
            {
                type = "Teacher";
                id = teacher.TeacherID;
            }
            else if (UserSessionService.CurrentUser is { } user)
            {
                type = "User";
                id = user.UserID;
            }
            else return;

            List<EDUTASK_1._1.Models.NotificationItem> notifications =
                await new DatabaseService().GetNotificationsAsync(type, id);
            bool hasNew = notifications.Any(item =>
                !item.IsRead &&
                !NotificationBadgeState.WasSeen(type, id, item.NotificationKey));
            NotificationBadgeState.SetHasNewNotifications(type, id, hasNew);
        }
        catch
        {
            // The badge is supplemental; navigation should remain available if
            // notification data cannot be loaded.
        }
    }

    private void OnNotificationBadgeChanged(object? sender, NotificationBadgeChangedEventArgs e)
    {
        (string Type, int Id)? recipient = GetCurrentRecipient();
        if (recipient is null ||
            !string.Equals(recipient.Value.Type, e.RecipientType, StringComparison.Ordinal) ||
            recipient.Value.Id != e.RecipientId)
            return;

        MainThread.BeginInvokeOnMainThread(() =>
            _notificationBadge.IsVisible =
                ActiveTab != BottomNavigationTab.Notification && e.HasNewNotifications);
    }

    private void OnNotificationBadgeRefreshRequested(object? sender, EventArgs e) =>
        _ = RefreshNotificationBadgeAsync();

    private static (string Type, int Id)? GetCurrentRecipient()
    {
        if (TeacherSessionService.CurrentTeacher is { } teacher)
            return ("Teacher", teacher.TeacherID);
        if (UserSessionService.CurrentUser is { } user)
            return ("User", user.UserID);
        return null;
    }

    private static void SetTabState(
        Image icon,
        Label label,
        bool selected,
        string selectedSource,
        string defaultSource,
        double selectedOffset,
        double defaultOffset)
    {
        icon.Source = selected ? selectedSource : defaultSource;
        icon.TranslationY = selected ? selectedOffset : defaultOffset;
        label.Opacity = selected ? 1 : 0;
    }

    private static void OnTasksTapped(object? sender, TappedEventArgs e) =>
        DashboardFlyoutPage.Current?.ShowTasks();

    private static void OnNotificationTapped(object? sender, TappedEventArgs e)
    {
        DashboardFlyoutPage? flyout = DashboardFlyoutPage.Current;
        if (flyout is null)
            return;

        if (TeacherSessionService.CurrentTeacher is { } teacher)
            flyout.ShowNotification(teacher);
        else
            flyout.ShowNotification(UserSessionService.CurrentUser);
    }

    private static void OnProfileTapped(object? sender, TappedEventArgs e)
    {
        DashboardFlyoutPage? flyout = DashboardFlyoutPage.Current;
        if (flyout is null)
            return;

        if (TeacherSessionService.CurrentTeacher is { } teacher)
        {
            flyout.ShowProfile(teacher);
            return;
        }

        if (UserSessionService.CurrentUser is { } user)
            flyout.ShowProfile(user);
    }
}
