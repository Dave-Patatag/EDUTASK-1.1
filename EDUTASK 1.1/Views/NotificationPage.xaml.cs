using EDUTASK_1._1.Models;

using EDUTASK_1._1.Services;
using EDUTASK_1._1.Helpers;
using EDUTASK_1._1.Views.Base;
using System.Collections.ObjectModel;

namespace EDUTASK_1._1.Views
{
    public partial class NotificationPage : EduTaskPage
    {
        private readonly User? _user;
        private readonly Teachers? _teacher;
        private readonly DatabaseService _database = new();
        private readonly ObservableCollection<NotificationGroup> _notificationGroups = [];
        private readonly List<NotificationItem> _allNotifications = [];
        private NotificationGroup? _activeMenuGroup;
        private int _visibleNotificationCount;

        private const int NotificationPageSize = 10;

        public NotificationPage(User? user)
        {
            InitializeComponent();
            ConfigurePlatformGroupHeaders();
            _user = user;
            NotificationsView.ItemsSource = _notificationGroups;
            EmptyStateTitleLabel.Text = "No notifications";
            EmptyStateMessageLabel.Text = "Task activity and updates will appear here.";
        }

        public NotificationPage(Teachers? teacher)
        {
            InitializeComponent();
            ConfigurePlatformGroupHeaders();
            _teacher = teacher;
            NotificationsView.ItemsSource = _notificationGroups;
            EmptyStateTitleLabel.Text = "You're all caught up";
            EmptyStateMessageLabel.Text = "Task updates will appear here.";
        }

        private void ConfigurePlatformGroupHeaders()
        {
#if WINDOWS
            NotificationsView.HandlerChanged += (_, _) =>
            {
                if (NotificationsView.Handler?.PlatformView is not Microsoft.UI.Xaml.Controls.ListViewBase listView)
                    return;

                var headerStyle = new Microsoft.UI.Xaml.Style(
                    typeof(Microsoft.UI.Xaml.Controls.ListViewHeaderItem));
                headerStyle.Setters.Add(new Microsoft.UI.Xaml.Setter(
                    Microsoft.UI.Xaml.FrameworkElement.HeightProperty, 36d));
                headerStyle.Setters.Add(new Microsoft.UI.Xaml.Setter(
                    Microsoft.UI.Xaml.FrameworkElement.MinHeightProperty, 36d));
                headerStyle.Setters.Add(new Microsoft.UI.Xaml.Setter(
                    Microsoft.UI.Xaml.FrameworkElement.MaxHeightProperty, 36d));
                headerStyle.Setters.Add(new Microsoft.UI.Xaml.Setter(
                    Microsoft.UI.Xaml.Controls.Control.PaddingProperty,
                    new Microsoft.UI.Xaml.Thickness(0)));
                headerStyle.Setters.Add(new Microsoft.UI.Xaml.Setter(
                    Microsoft.UI.Xaml.Controls.Control.HorizontalContentAlignmentProperty,
                    Microsoft.UI.Xaml.HorizontalAlignment.Stretch));
                headerStyle.Setters.Add(new Microsoft.UI.Xaml.Setter(
                    Microsoft.UI.Xaml.Controls.Control.VerticalContentAlignmentProperty,
                    Microsoft.UI.Xaml.VerticalAlignment.Center));

                foreach (Microsoft.UI.Xaml.Controls.GroupStyle groupStyle in listView.GroupStyle)
                    groupStyle.HeaderContainerStyle = headerStyle;
            };
#endif
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            (string type, int id) = GetRecipient();
            if (id > 0)
                NotificationBadgeState.MarkVisited(type, id, []);
            await LoadNotificationsAsync();
        }

        private async System.Threading.Tasks.Task LoadNotificationsAsync()
        {
            try
            {
                (string type, int id) = GetRecipient();
                if (id == 0) { EmptyState.IsVisible = true; NotificationsView.IsVisible = false; return; }
                List<NotificationItem> items = await _database.GetNotificationsAsync(type, id);
                _allNotifications.Clear();
                _allNotifications.AddRange(items.OrderByDescending(item => item.CreatedAt));
                NotificationBadgeState.MarkVisited(
                    type,
                    id,
                    _allNotifications.Select(item => item.NotificationKey));
                _visibleNotificationCount = Math.Min(NotificationPageSize, _allNotifications.Count);
                RebuildVisibleNotifications();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Notification load failed: {ex}");
                await UiAlertService.ShowAsync(this, "Notifications couldn't load", "We couldn't load your notifications. Please try again.");
            }
        }

        private void RebuildVisibleNotifications()
        {
            List<NotificationItem> visibleItems = _allNotifications
                .Take(_visibleNotificationCount)
                .ToList();

            _notificationGroups.Clear();
            foreach (IGrouping<DateTime, NotificationItem> group in visibleItems
                    .GroupBy(item => item.CreatedAt.Date > DateTime.Today ? DateTime.Today : item.CreatedAt.Date)
                    .OrderByDescending(group => group.Key))
            {
                _notificationGroups.Add(new NotificationGroup(
                    group.Key,
                    group.OrderByDescending(item => item.CreatedAt)));
            }

            bool hasNotifications = _allNotifications.Count > 0;
            EmptyState.IsVisible = !hasNotifications;
            NotificationsView.IsVisible = hasNotifications;
            SeePreviousNotificationsButton.IsVisible =
                _visibleNotificationCount < _allNotifications.Count;
        }

        private void OnSeePreviousNotificationsClicked(object sender, EventArgs e)
        {
            _visibleNotificationCount = Math.Min(
                _visibleNotificationCount + NotificationPageSize,
                _allNotifications.Count);
            RebuildVisibleNotifications();
        }

        private async void OnNotificationSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.CurrentSelection.FirstOrDefault() is not NotificationItem item) return;
            NotificationsView.SelectedItem = null;

            NotificationGroup? selectionGroup =
                _notificationGroups.FirstOrDefault(group => group.IsSelectionMode);
            if (selectionGroup is not null)
            {
                if (selectionGroup.Contains(item))
                    item.IsSelected = !item.IsSelected;
                return;
            }

            await OpenNotificationAsync(item);
        }

        private async void OnNotificationMenuClicked(object sender, EventArgs e)
        {
            if (sender is not ImageButton { CommandParameter: NotificationItem item })
                return;

            string? action = await DisplayActionSheet(
                "Notification options", "Cancel", "Delete", "View");
            if (string.Equals(action, "View", StringComparison.Ordinal))
            {
                await OpenNotificationAsync(item);
                return;
            }

            if (!string.Equals(action, "Delete", StringComparison.Ordinal))
                return;

            bool confirmed = await UiAlertService.ConfirmAsync(
                this, "Delete notification?", "Remove this notification?", "Delete", "Cancel");
            if (!confirmed)
                return;

            try
            {
                string type = _teacher is not null ? "Teacher" : "User";
                int id = _teacher?.TeacherID ?? _user?.UserID ?? 0;
                if (id == 0)
                    return;

                await _database.HideNotificationsAsync(type, id, [item.NotificationKey]);
                await LoadNotificationsAsync();
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"Notification deletion failed: {exception}");
                await UiAlertService.ShowAsync(this, "Delete failed", "This notification could not be removed.");
            }
        }

        private async System.Threading.Tasks.Task OpenNotificationAsync(NotificationItem item)
        {
            string type = _teacher is not null ? "Teacher" : "User";
            int id = _teacher?.TeacherID ?? _user?.UserID ?? 0;
            if (!item.IsRead && id > 0)
            {
                await _database.MarkNotificationReadAsync(type, id, item.NotificationKey);
                item.IsRead = true;
            }

            DashboardFlyoutPage.Current?.ShowTasks(item.TaskID);
        }

        private void OnDateMenuClicked(object sender, EventArgs e)
        {
            if (sender is not ImageButton menuButton ||
                menuButton.BindingContext is not NotificationGroup group)
                return;

            _activeMenuGroup = group;
            (double top, double bottom) = GetAnchorBounds(menuButton);
            double panelHeight = DateMenuPanel.Height > 0 ? DateMenuPanel.Height : 100;
            double availableBottom = Math.Max(0, NotificationRoot.Height - 12);
            double menuTop = bottom + OverlayPanel.Gap;
            if (menuTop + panelHeight > availableBottom)
                menuTop = Math.Max(8, top - panelHeight - OverlayPanel.Gap);

            DateMenuPanel.TranslationY = menuTop;
            DateMenuOverlay.IsVisible = true;
        }

        private void OnSelectDateNotificationsClicked(object sender, EventArgs e)
        {
            if (_activeMenuGroup is null)
                return;

            NotificationGroup selectedGroup = _activeMenuGroup;
            CloseDateMenu();
            foreach (NotificationGroup group in _notificationGroups)
                group.IsSelectionMode = ReferenceEquals(group, selectedGroup);
        }

        private async void OnDeleteAllDateNotificationsClicked(object sender, EventArgs e)
        {
            if (_activeMenuGroup is null)
                return;

            NotificationGroup group = _activeMenuGroup;
            CloseDateMenu();
            await DeleteNotificationsAsync(group, group.ToList(), deleteAll: true);
        }

        private void OnDateMenuDismissClicked(object sender, EventArgs e) => CloseDateMenu();

        private void CloseDateMenu()
        {
            DateMenuOverlay.IsVisible = false;
            _activeMenuGroup = null;
        }

        private (double Top, double Bottom) GetAnchorBounds(VisualElement anchor)
        {
#if ANDROID
            if (anchor.Handler?.PlatformView is Android.Views.View nativeAnchor &&
                NotificationRoot.Handler?.PlatformView is Android.Views.View nativeRoot)
            {
                int[] anchorLocation = new int[2];
                int[] rootLocation = new int[2];
                nativeAnchor.GetLocationOnScreen(anchorLocation);
                nativeRoot.GetLocationOnScreen(rootLocation);
                double density = DeviceDisplay.Current.MainDisplayInfo.Density;
                double top = (anchorLocation[1] - rootLocation[1]) / density;
                return (top, top + nativeAnchor.Height / density);
            }
#elif WINDOWS
            if (anchor.Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement nativeAnchor &&
                NotificationRoot.Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement nativeRoot)
            {
                var transform = nativeAnchor.TransformToVisual(nativeRoot);
                Windows.Foundation.Point point =
                    transform.TransformPoint(new Windows.Foundation.Point(0, 0));
                return (point.Y, point.Y + nativeAnchor.ActualHeight);
            }
#endif
            double bottom = OverlayPanel.TopBelow(anchor, NotificationRoot) - OverlayPanel.Gap;
            return (bottom - anchor.Height, bottom);
        }

        private void OnCancelSelectionClicked(object sender, EventArgs e)
        {
            if (sender is Button { BindingContext: NotificationGroup group })
                group.IsSelectionMode = false;
        }

        private async void OnDeleteSelectedClicked(object sender, EventArgs e)
        {
            if (sender is not Button { BindingContext: NotificationGroup group })
                return;

            List<NotificationItem> selected =
                group.Where(notification => notification.IsSelected).ToList();
            if (selected.Count == 0)
                return;

            await DeleteNotificationsAsync(group, selected, deleteAll: false);
        }

        private async System.Threading.Tasks.Task DeleteNotificationsAsync(
            NotificationGroup group,
            IReadOnlyList<NotificationItem> notifications,
            bool deleteAll)
        {
            if (notifications.Count == 0)
                return;

            string date = group.DateDisplay.ToLowerInvariant();
            string message = deleteAll
                ? $"Delete all {notifications.Count} notifications from {date}?"
                : $"Delete {notifications.Count} selected notification{(notifications.Count == 1 ? string.Empty : "s")} from {date}?";
            bool confirmed = await UiAlertService.ConfirmAsync(
                this,
                deleteAll ? "Delete all notifications?" : "Delete selected notifications?",
                message,
                "Delete",
                "Cancel");
            if (!confirmed)
                return;

            try
            {
                string type = _teacher is not null ? "Teacher" : "User";
                int id = _teacher?.TeacherID ?? _user?.UserID ?? 0;
                if (id == 0)
                    return;

                await _database.HideNotificationsAsync(
                    type,
                    id,
                    notifications.Select(notification => notification.NotificationKey));
                group.IsSelectionMode = false;
                await LoadNotificationsAsync();
            }
            catch (Exception exception)
            {
                System.Diagnostics.Debug.WriteLine($"Notification deletion failed: {exception}");
                await UiAlertService.ShowAsync(
                    this,
                    "Notifications couldn't be deleted",
                    "We couldn't delete the selected notifications. Please try again.");
            }
        }

        private void OnMenuClicked(object sender, TappedEventArgs e)
        {
            if (DashboardFlyoutPage.Current is { } flyout)
                flyout.IsPresented = true;
        }

        private void OnTaskTapped(object sender, TappedEventArgs e) =>
            DashboardFlyoutPage.Current?.ShowTasks();

        private void OnProfileTapped(object sender, TappedEventArgs e)
        {
            if (_teacher is not null)
                DashboardFlyoutPage.Current?.ShowProfile(_teacher);
            else if (_user is not null)
                DashboardFlyoutPage.Current?.ShowProfile(_user);
        }

        private (string Type, int Id) GetRecipient() =>
            _teacher is not null
                ? ("Teacher", _teacher.TeacherID)
                : ("User", _user?.UserID ?? 0);

    }
}
