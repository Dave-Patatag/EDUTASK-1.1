using System.ComponentModel;

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
    private static readonly string[] RadioColors =
    [
        "#E74C3C",
        "#3498DB",
        "#2ECC71",
        "#9B59B6",
        "#F39C12",
        "#1ABC9C",
        "#E84393",
        "#5D6D7E"
    ];

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
    public string RadioColor { get; } = RadioColors[Random.Shared.Next(RadioColors.Length)];

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
    public string CommentText { get; init; } = string.Empty;
    public string MessageType { get; init; } = "Comment";
    public bool IsProofReturn => MessageType == "ProofReturn";
    public bool IsRegularComment => !IsProofReturn;
    public int ReturnSequence { get; set; }
    public string ReturnSequenceDisplay => ReturnSequence.ToString();
    public string MessageHeading => "↩  Changes requested";
    public DateTime CreatedAt { get; init; }
    public bool IsMine { get; set; }
    public string AuthorDisplay => $"{AuthorName} \u00B7 {AuthorType}";
    public string CreatedDisplay => CreatedAt.ToString("MMM dd, yyyy '\u00B7' h:mm tt");
    public Color BubbleColor => IsProofReturn ? Color.FromArgb("#FEF2F2") : AuthorType == "Teacher" ? Color.FromArgb("#E8F4FD") : Color.FromArgb("#ECF8F0");
    public Color BubbleStrokeColor => IsProofReturn ? Color.FromArgb("#F5B7B7") : Color.FromArgb("#D7DCE2");
    public int MessageColumn => IsMine ? 2 : 0;
}




