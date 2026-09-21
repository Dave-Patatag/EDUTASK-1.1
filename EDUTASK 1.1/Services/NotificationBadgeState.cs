namespace EDUTASK_1._1.Services;

public static class NotificationBadgeState
{
    private const string SeenNotificationsPreferenceKey = "notification-badge-seen-keys";
    private static readonly object _sync = new();
    private static readonly HashSet<string> _seenNotificationKeys = new(StringComparer.Ordinal);

    public static event EventHandler<NotificationBadgeChangedEventArgs>? BadgeChanged;
    public static event EventHandler? RefreshRequested;

    static NotificationBadgeState()
    {
        try
        {
            string storedKeys = Preferences.Default.Get(SeenNotificationsPreferenceKey, string.Empty);
            if (string.IsNullOrWhiteSpace(storedKeys))
                return;

            HashSet<string>? savedKeys =
                System.Text.Json.JsonSerializer.Deserialize<HashSet<string>>(storedKeys);
            if (savedKeys is not null)
                _seenNotificationKeys.UnionWith(savedKeys);
        }
        catch
        {
            // A corrupt preference must not prevent notification navigation.
        }
    }

    public static bool WasSeen(string recipientType, int recipientId, string key)
    {
        lock (_sync)
            return _seenNotificationKeys.Contains(ScopedKey(recipientType, recipientId, key));
    }

    public static void MarkVisited(string recipientType, int recipientId, IEnumerable<string> keys)
    {
        lock (_sync)
        {
            foreach (string key in keys.Where(key => !string.IsNullOrWhiteSpace(key)))
                _seenNotificationKeys.Add(ScopedKey(recipientType, recipientId, key));

            try
            {
                Preferences.Default.Set(
                    SeenNotificationsPreferenceKey,
                    System.Text.Json.JsonSerializer.Serialize(_seenNotificationKeys));
            }
            catch
            {
                // In-memory tracking still works if local preferences are unavailable.
            }
        }

        BadgeChanged?.Invoke(
            null,
            new NotificationBadgeChangedEventArgs(recipientType, recipientId, false));
    }

    public static void SetHasNewNotifications(string recipientType, int recipientId, bool hasNewNotifications) =>
        BadgeChanged?.Invoke(
            null,
            new NotificationBadgeChangedEventArgs(recipientType, recipientId, hasNewNotifications));

    public static void RequestRefresh() => RefreshRequested?.Invoke(null, EventArgs.Empty);

    private static string ScopedKey(string recipientType, int recipientId, string key) =>
        $"{recipientType}:{recipientId}:{key}";
}

public sealed record NotificationBadgeChangedEventArgs(
    string Recipient_type,
    int RecipientId,
    bool HasNewNotifications);
