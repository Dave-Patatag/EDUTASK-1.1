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
/// notification, and profile tabs. Home remains available from the flyout.
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

    private readonly TabVisual _tasksTab;
    private readonly TabVisual _notificationTab;
    private readonly TabVisual _profileTab;
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

        _tasksTab = CreateItem(
            "Tasks", "Open tasks", "taskselected.png", "taskunselected.png", OnTasksTapped);
        _notificationTab = CreateItem(
            "Notifications", "Open notifications", "selectedinbox.png", "defaultinbox.png", OnNotificationTapped);
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
        _notificationTab.Item.Add(_notificationBadge, 0, 0);
        _notificationBadge.ZIndex = 2;
        _profileTab = CreateItem(
            "Profile", "Open profile", "selectedprofile.png", "defaultprofile.png", OnProfileTapped);

        navigation.Add(_tasksTab.Item, 0, 0);
        navigation.Add(_notificationTab.Item, 1, 0);
        navigation.Add(_profileTab.Item, 2, 0);

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

    private static TabVisual CreateItem(
        string labelText,
        string semanticDescription,
        string selectedSource,
        string idleSource,
        EventHandler<TappedEventArgs> tapped)
    {
        var selectedIcon = CreateIcon(selectedSource);
        var idleIcon = CreateIcon(idleSource);
        var iconHost = new Grid
        {
            WidthRequest = 24,
            HeightRequest = 24,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        iconHost.Add(idleIcon);
        iconHost.Add(selectedIcon);

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
        item.Add(iconHost, 0, 0);
        item.Add(label, 0, 1);
        SemanticProperties.SetDescription(item, semanticDescription);

        var tap = new TapGestureRecognizer();
        tap.Tapped += tapped;
        item.GestureRecognizers.Add(tap);

        return new TabVisual(selectedIcon, idleIcon, iconHost, label, item);
    }

    private static Image CreateIcon(string source) => new()
    {
        Source = source,
        WidthRequest = 24,
        HeightRequest = 24,
        HorizontalOptions = LayoutOptions.Center,
        VerticalOptions = LayoutOptions.Center,
        InputTransparent = true
    };

    private void UpdateVisualState()
    {
        SetTabState(_tasksTab, ActiveTab == BottomNavigationTab.Tasks,
            selectedOffset: 0, defaultOffset: 5);
        SetTabState(_notificationTab, ActiveTab == BottomNavigationTab.Notification,
            selectedOffset: 2, defaultOffset: 7);
        SetTabState(_profileTab, ActiveTab == BottomNavigationTab.Profile,
            selectedOffset: 0, defaultOffset: 6);
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
                id = teacher.Teacher_id;
            }
            else if (UserSessionService.CurrentUser is { } user)
            {
                type = "User";
                id = user.User_id;
            }
            else return;

            List<EDUTASK_1._1.Models.NotificationItem> notifications =
                await new DatabaseService().GetNotificationsAsync(type, id);
            bool hasNew = notifications.Any(item =>
                !item.IsRead &&
                !NotificationBadgeState.WasSeen(type, id, item.Notification_key));
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
            !string.Equals(recipient.Value.Type, e.Recipient_type, StringComparison.Ordinal) ||
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
            return ("Teacher", teacher.Teacher_id);
        if (UserSessionService.CurrentUser is { } user)
            return ("User", user.User_id);
        return null;
    }

    private static void SetTabState(
        TabVisual tab,
        bool selected,
        double selectedOffset,
        double defaultOffset)
    {
        tab.SelectedIcon.Opacity = selected ? 1 : 0;
        tab.IdleIcon.Opacity = selected ? 0 : 1;
        tab.IconHost.TranslationY = selected ? selectedOffset : defaultOffset;
        tab.Label.Opacity = selected ? 1 : 0;
    }

    private sealed record TabVisual(
        Image SelectedIcon,
        Image IdleIcon,
        Grid IconHost,
        Label Label,
        Grid Item);

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
