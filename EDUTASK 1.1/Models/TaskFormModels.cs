using System.ComponentModel;
using EDUTASK_1._1.Helpers;

namespace EDUTASK_1._1.Models;

public sealed class TeacherOption
{
    public int TeacherID { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string DisplayName => $"{FirstName} {LastName}".Trim();
}

public sealed class SubtaskDraft : INotifyPropertyChanged
{
    private bool _isCompleted;

    public int? SubtaskID { get; init; }
    public string Title { get; set; } = string.Empty;
    public bool IsCompleted
    {
        get => _isCompleted;
        set
        {
            if (_isCompleted == value)
                return;
            _isCompleted = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCompleted)));
        }
    }
    public string SelectionGroup { get; } = $"Subtask_{Guid.NewGuid():N}";
    public Color RadioColor { get; } = IdentityPalette.Random();

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class TaskEditData
{
    public int TaskID { get; init; }
    public int CreatedByUserID { get; init; }
    public int? AssignmentID { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool IsDailyRemind { get; init; }
    public int? TeacherID { get; init; }
    public DateTime Deadline { get; init; }
    public string Priority { get; init; } = string.Empty;
    public string CompletionStatus { get; init; } = string.Empty;
    public DateTime? CompletedAt { get; init; }
}

public sealed class TaskCommentItem
{
    public int CommentID { get; init; }
    public int AuthorID { get; init; }
    public string AuthorName { get; init; } = string.Empty;
    public string AuthorType { get; init; } = string.Empty;
    public string AuthorRoleName { get; init; } = string.Empty;
    public string AuthorProfilePhotoPath { get; init; } = string.Empty;
    public string CommentText { get; init; } = string.Empty;
    public string MessageType { get; init; } = "Comment";
    public bool IsProofReturn => MessageType == "ProofReturn";
    public bool IsRegularComment => !IsProofReturn;
    public int ReturnSequence { get; set; }
    public string ReturnSequenceDisplay => ReturnSequence.ToString();
    public string MessageHeading => "↩  Changes requested";
    public DateTime CreatedAt { get; init; }
    public bool IsMine { get; set; }
    public bool ShowOtherAvatar => !IsMine;
    public bool HasAuthorProfilePhoto => !string.IsNullOrWhiteSpace(AuthorProfilePhotoPath);
    public bool ShowAuthorInitials => !HasAuthorProfilePhoto;
    public string SenderDisplay => $"{AuthorRoleName} \u00B7 {AuthorName}";
    public string AuthorInitials
    {
        get
        {
            string[] parts = AuthorName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "?";
            if (parts.Length == 1) return parts[0][..1].ToUpperInvariant();
            return $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant();
        }
    }
    public string AuthorDisplay => AuthorName;
    public string CreatedDisplay => CreatedAt.ToString("MMM dd, yyyy '\u00B7' h:mm tt");
    public Color BubbleColor => IsProofReturn ? AppColors.StatusDangerSurface : AuthorType == "Teacher" ? AppColors.StatusInfoSurface : AppColors.StatusSuccessSurface;
    public Color BubbleStrokeColor => IsProofReturn ? AppColors.StatusDangerBorder : AppColors.BorderDefault;
    public int MessageColumn => IsMine ? 2 : 1;
}




