using EDUTASK_1._1.Helpers;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace EDUTASK_1._1.Models;

public sealed class NotificationItem : INotifyPropertyChanged
{
    private bool _isRead;
    private bool _isSelectionMode;
    private bool _isSelected;

    public required string NotificationKey { get; init; }
    public required string Title { get; init; }
    public string TitleDisplay => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(Title.ToLower());
    public required string Message { get; init; }
    public required string Category { get; init; }
    public int? TaskID { get; init; }
    public DateTime CreatedAt { get; init; }
    public bool IsRead
    {
        get => _isRead;
        set
        {
            if (!SetField(ref _isRead, value))
                return;

            OnPropertyChanged(nameof(IsUnread));
            OnPropertyChanged(nameof(CardColor));
        }
    }
    public bool ShowViewTask { get; init; } = true;
    public string? ActorPhotoPath { get; init; }
    public string ActorInitials { get; init; } = string.Empty;

    // Colour, and what it means, live in NotificationPalette \u2014 pending blue,
    // ongoing orange, completed green, overdue red.
    public Color AccentColor => NotificationPalette.ToneColor(Category);
    public string IconText => NotificationPalette.ToneGlyph(Category);
    public Color IconTextColor => NotificationPalette.ToneGlyphColor(Category);
    public bool IsUnread => !IsRead;
    public Color CardColor => IsRead
        ? AppColors.SurfaceBase
        : AppColors.SelectionSurface;

    /// <summary>
    /// A person's disc stays neutral so their photo or initials carry the row;
    /// a system event has neither, so its disc carries the tone instead of
    /// sitting there as an empty grey circle.
    /// </summary>
    public Color AvatarBackgroundColor => ShowStatusIcon
        ? NotificationPalette.ToneSurface(Category)
        : AppColors.SurfaceSunken;

    public bool HasTime => CreatedAt.TimeOfDay != TimeSpan.Zero;
    public string TimeDisplay => HasTime ? CreatedAt.ToString("h:mm tt") : string.Empty;
    public bool ShowTime => HasTime && !IsSelectionMode;
    public bool IsSelectionMode
    {
        get => _isSelectionMode;
        set
        {
            if (!SetField(ref _isSelectionMode, value))
                return;

            OnPropertyChanged(nameof(ShowTime));
        }
    }
    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }
    public bool HasActorPhoto => !string.IsNullOrWhiteSpace(ActorPhotoPath);
    public bool ShowActorInitials => !HasActorPhoto && !string.IsNullOrWhiteSpace(ActorInitials);
    public bool ShowStatusIcon => !HasActorPhoto && !ShowActorInitials;

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class NotificationGroup : ObservableCollection<NotificationItem>
{
    private bool _isSelectionMode;

    public NotificationGroup(DateTime date, IEnumerable<NotificationItem> notifications)
        : base(notifications)
    {
        Date = date.Date;
        foreach (NotificationItem notification in this)
            notification.PropertyChanged += OnNotificationPropertyChanged;
    }

    public DateTime Date { get; }
    public bool IsSelectionMode
    {
        get => _isSelectionMode;
        set
        {
            if (_isSelectionMode == value)
                return;

            _isSelectionMode = value;
            foreach (NotificationItem notification in this)
            {
                notification.IsSelectionMode = value;
                if (!value)
                    notification.IsSelected = false;
            }

            RaiseSelectionProperties();
        }
    }
    public bool ShowMenu => !IsSelectionMode;
    public int SelectedCount => this.Count(notification => notification.IsSelected);
    public bool HasSelectedItems => SelectedCount > 0;
    public string DeleteSelectedText => SelectedCount > 0 ? $"Delete ({SelectedCount})" : "Delete";
    public string DateDisplay => Date == DateTime.Today
        ? "Today"
        : Date == DateTime.Today.AddDays(-1)
            ? "Yesterday"
            : Date.ToString("MMMM d, yyyy");

    private void OnNotificationPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(NotificationItem.IsSelected))
            RaiseSelectionProperties();
    }

    private void RaiseSelectionProperties()
    {
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(IsSelectionMode)));
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(ShowMenu)));
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(SelectedCount)));
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(HasSelectedItems)));
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(DeleteSelectedText)));
    }
}
