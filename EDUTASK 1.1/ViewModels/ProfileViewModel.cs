using System.ComponentModel;
using EDUTASK_1._1.Helpers;
using System.Runtime.CompilerServices;

namespace EDUTASK_1._1.ViewModels;

public sealed class ProfileViewModel : INotifyPropertyChanged
{
    private string _fullName = string.Empty;
    private string _email = string.Empty;
    private string _contactNumber = string.Empty;
    private string _role = string.Empty;
    private string _username = string.Empty;
    private string _profilePhotoPath = string.Empty;
    private string _avatarSeed = string.Empty;
    private string _totalLabel = "Total";
    private string _pendingLabel = "Pending";
    private int _totalCount;
    private int _pendingCount;
    private int _overdueCount;
    private int _completedCount;

    public string FullName { get => _fullName; set { _fullName = value; OnPropertyChanged(); RefreshAvatar(); } }
    public string Email { get => _email; set { _email = value; OnPropertyChanged(); } }
    public string Contact_number { get => _contactNumber; set { _contactNumber = value; OnPropertyChanged(); } }
    public string Role { get => _role; set { _role = value; OnPropertyChanged(); } }
    public string Username { get => _username; set { _username = value; OnPropertyChanged(); OnPropertyChanged(nameof(UsernameDisplay)); } }
    public string AvatarSeed { get => _avatarSeed; set { _avatarSeed = value; OnPropertyChanged(); OnPropertyChanged(nameof(AvatarColor)); } }
    public string Profile_photo { get => _profilePhotoPath; set { _profilePhotoPath = LocalProfilePhoto.ExistingOrEmpty(value); OnPropertyChanged(); OnPropertyChanged(nameof(HasProfilePhoto)); OnPropertyChanged(nameof(ShowInitials)); } }
    public string TotalLabel { get => _totalLabel; set { _totalLabel = value; OnPropertyChanged(); } }
    public string PendingLabel { get => _pendingLabel; set { _pendingLabel = value; OnPropertyChanged(); } }
    public int TotalCount { get => _totalCount; set { _totalCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(CompletedPercentage)); OnPropertyChanged(nameof(HasTasks)); OnPropertyChanged(nameof(HasNoTasks)); } }
    public int PendingCount { get => _pendingCount; set { _pendingCount = value; OnPropertyChanged(); } }
    public int OverdueCount { get => _overdueCount; set { _overdueCount = value; OnPropertyChanged(); } }
    public int CompletedCount { get => _completedCount; set { _completedCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(CompletedPercentage)); } }
    public bool HasProfilePhoto => !string.IsNullOrWhiteSpace(Profile_photo);
    public bool ShowInitials => !HasProfilePhoto;
    public bool HasTasks => TotalCount > 0;

    /// <summary>
    /// Drives the chart's empty state. XAML has no negation operator, so the
    /// inverse is a property rather than a converter for one call site.
    /// </summary>
    public bool HasNoTasks => TotalCount <= 0;

    /// <summary>
    /// The handle as it is shown under the name. Usernames are stored with the
    /// leading "@" — registration requires it — but a record saved before that
    /// rule, or one edited around it, would otherwise render as a bare word next
    /// to everyone else's handle.
    /// </summary>
    public string UsernameDisplay =>
        string.IsNullOrWhiteSpace(Username) ? string.Empty
        : Username.TrimStart().StartsWith('@') ? Username.Trim()
        : $"@{Username.Trim()}";

    public int CompletedPercentage => TotalCount == 0 ? 0 : (int)Math.Round(CompletedCount * 100.0 / TotalCount);
    public string AvatarInitials => GetInitials(FullName);
    public Color AvatarColor => IdentityPalette.At(StableColorIndex(AvatarSeed));

    private void RefreshAvatar()
    {
        OnPropertyChanged(nameof(AvatarInitials));
        OnPropertyChanged(nameof(AvatarColor));
    }

    private static string GetInitials(string fullName)
    {
        string[] names = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (names.Length == 0)
            return "?";

        string initials = names.Length == 1
            ? names[0][0].ToString()
            : $"{names[0][0]}{names[^1][0]}";
        return initials.ToUpperInvariant();
    }

    private static int StableColorIndex(string avatarSeed)
    {
        string key = string.IsNullOrWhiteSpace(avatarSeed) ? "default-avatar" : avatarSeed;
        uint hash = 2166136261;
        foreach (char character in key.ToUpperInvariant())
        {
            hash ^= character;
            hash *= 16777619;
        }
        return (int)(hash % IdentityPalette.Count);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
