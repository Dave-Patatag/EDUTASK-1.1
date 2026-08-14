using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EDUTASK_1._1.ViewModels;

public sealed class ProfileViewModel : INotifyPropertyChanged
{
    private static readonly string[] AvatarColors =
    [
        "#5B8DEF", "#8B5CF6", "#EC4899", "#EF6C57",
        "#F59E0B", "#22A06B", "#0EA5A8", "#64748B"
    ];

    private string _fullName = string.Empty;
    private string _email = string.Empty;
    private string _contactNumber = string.Empty;
    private string _role = string.Empty;
    private string _username = string.Empty;
    private string _profilePhotoPath = string.Empty;
    private string _avatarSeed = string.Empty;

    public string FullName { get => _fullName; set { _fullName = value; OnPropertyChanged(); RefreshAvatar(); } }
    public string Email { get => _email; set { _email = value; OnPropertyChanged(); } }
    public string ContactNumber { get => _contactNumber; set { _contactNumber = value; OnPropertyChanged(); } }
    public string Role { get => _role; set { _role = value; OnPropertyChanged(); } }
    public string Username { get => _username; set { _username = value; OnPropertyChanged(); } }
    public string AvatarSeed { get => _avatarSeed; set { _avatarSeed = value; OnPropertyChanged(); OnPropertyChanged(nameof(AvatarColor)); } }
    public string ProfilePhotoPath { get => _profilePhotoPath; set { _profilePhotoPath = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasProfilePhoto)); OnPropertyChanged(nameof(ShowInitials)); } }
    public bool HasProfilePhoto => !string.IsNullOrWhiteSpace(ProfilePhotoPath);
    public bool ShowInitials => !HasProfilePhoto;
    public string AvatarInitials => GetInitials(FullName);
    public Color AvatarColor => Color.FromArgb(AvatarColors[StableColorIndex(AvatarSeed)]);

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
        return (int)(hash % AvatarColors.Length);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}