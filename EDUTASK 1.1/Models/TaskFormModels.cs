using EDUTASK_1._1.Helpers;

namespace EDUTASK_1._1.Models;

public sealed class TeacherOption
{
    public int Teacher_id { get; init; }
    public string First_name { get; init; } = string.Empty;
    public string Last_name { get; init; } = string.Empty;
    public string DisplayName => $"{First_name} {Last_name}".Trim();
}

public sealed class SubtaskDraft
{
    public int? Subtask_id { get; init; }
    public string Title { get; set; } = string.Empty;
    public string? ProofStatus { get; init; }
    public bool Is_completed => string.Equals(ProofStatus, "Approved", StringComparison.OrdinalIgnoreCase);
    public string SelectionGroup { get; } = $"Subtask_{Guid.NewGuid():N}";
    public Color RadioColor { get; } = IdentityPalette.Random();
}

public sealed class TaskEditData
{
    public int Task_id { get; init; }
    public int Createdby_user_id { get; init; }
    public int? Assignment_id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool IsDailyRemind { get; init; }
    public int? Teacher_id { get; init; }
    public DateTime Deadline { get; init; }
    public string Priority { get; init; } = string.Empty;
    public string Completion_status { get; init; } = string.Empty;
    public DateTime? Completed_at { get; init; }
    public DateTime? Updated_at { get; init; }
}

public sealed class TaskDiscussionItem
{
    public int Discussion_id { get; init; }
    public int Sender_id { get; init; }
    public string Sender_type { get; init; } = string.Empty;
    public string AuthorName { get; init; } = string.Empty;
    public string AuthorRoleName { get; init; } = string.Empty;
    public string AuthorProfilePhotoPath { get; init; } = string.Empty;
    public string Message_text { get; init; } = string.Empty;
    public string Message_type { get; init; } = "Discussion";
    public bool IsProofReturn => Message_type == "ProofReturn";
    public bool IsRegularDiscussion => !IsProofReturn;
    public int ReturnSequence { get; set; }
    public string ReturnSequenceDisplay => ReturnSequence.ToString();
    public string MessageHeading => "↩  Changes requested";
    public DateTime Created_at { get; init; }
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
    public string CreatedDisplay => Created_at.ToString("MMM dd, yyyy '\u00B7' h:mm tt");
    public Color BubbleColor => IsProofReturn ? AppColors.StatusDangerSurface : Sender_type == "Teacher" ? AppColors.StatusInfoSurface : AppColors.StatusSuccessSurface;
    public Color BubbleStrokeColor => IsProofReturn ? AppColors.StatusDangerBorder : AppColors.BorderDefault;
    public int MessageColumn => IsMine ? 2 : 1;
}




