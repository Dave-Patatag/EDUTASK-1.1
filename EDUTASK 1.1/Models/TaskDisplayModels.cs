using System.ComponentModel;
using System.Windows.Input;

namespace EDUTASK_1._1.Models;

public sealed class AdministratorTaskItem
{
    public int TaskID { get; init; }
    public string Title { get; init; } = string.Empty;
    public string TeacherName { get; init; } = string.Empty;
    public string DeadlineDisplay { get; init; } = string.Empty;
    public string Priority { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public Color PriorityColor { get; init; } = Colors.Gray;
    public Color StatusColor { get; init; } = Colors.Gray;
}

public sealed class DashboardTaskItem : INotifyPropertyChanged
{
    private bool _isExpanded;
    private int _submittedProgressItems;
    private int _verifiedProgressItems;
    private int _totalProgressItems;
    public int TaskID { get; set; }
    public int AssignmentID { get; set; }
    public int CreatedByUserID { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TeacherName { get; set; } = string.Empty;
    public string DeadlineDisplay { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public Color PriorityColor { get; set; } = Colors.Gray;
    public string Status { get; set; } = string.Empty;
    public Color StatusColor { get; set; } = Colors.Gray;
    public DateTime? Deadline { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string DeadlineMonth => Deadline?.ToString("MMM").ToUpperInvariant() ?? "â€”";
    public string DeadlineDay => Deadline?.ToString("dd") ?? "â€”";
    public bool IsCompleted { get; set; }
    public int SubmittedProgressItems { get => _submittedProgressItems; set { if (_submittedProgressItems == value) return; _submittedProgressItems = value; NotifyProgressChanged(); } }
    public int VerifiedProgressItems { get => _verifiedProgressItems; set { if (_verifiedProgressItems == value) return; _verifiedProgressItems = value; NotifyProgressChanged(); } }
    public int TotalProgressItems { get => _totalProgressItems; set { if (_totalProgressItems == value) return; _totalProgressItems = value; NotifyProgressChanged(); } }
    public double ProgressValue => TotalProgressItems == 0
        ? 0
        : (double)VerifiedProgressItems / TotalProgressItems;
    public int ProgressPercent => (int)Math.Round(ProgressValue * 100, MidpointRounding.AwayFromZero);
    public string ProgressPercentText => $"{ProgressPercent}%";
    public string ProgressSummary => TotalProgressItems == 0 ? "No subtasks" : $"{VerifiedProgressItems} of {TotalProgressItems} approved";
    public string VerifiedProgressSummary => TotalProgressItems == 0 ? string.Empty : $"{VerifiedProgressItems} of {TotalProgressItems} approved";
    public double VerificationProgressValue => TotalProgressItems == 0 ? 0 : (double)VerifiedProgressItems / TotalProgressItems;
    public string VerificationProgressPercentText => $"{(int)Math.Round(VerificationProgressValue * 100, MidpointRounding.AwayFromZero)}%";
    public bool IsAwaitingValidation { get; set; }
    public bool ShowValidationActions => IsAwaitingValidation;
    public bool CanValidate => EDUTASK_1._1.Services.UserSessionService.CanApproveCompletion &&
                               !IsCompleted && TotalProgressItems > 0 && VerifiedProgressItems == TotalProgressItems;
    public bool CanEdit => !IsCompleted;
    public bool ShowAcknowledge { get; set; }
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            _isExpanded = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ViewButtonText)));
        }
    }
    public List<SubtaskDisplayItem> Subtasks { get; set; } = [];
    public bool HasSubtasks => Subtasks.Count > 0;
    public string ViewButtonText => IsExpanded ? "Hide Details" : "View Task";

    private void NotifyProgressChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SubmittedProgressItems)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VerifiedProgressItems)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TotalProgressItems)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProgressValue)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProgressPercent)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProgressPercentText)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ProgressSummary)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VerifiedProgressSummary)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VerificationProgressValue)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VerificationProgressPercentText)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanValidate)));
    }
    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class SubtaskDisplayItem : INotifyPropertyChanged
{
    private bool _isCompleted;
    private int _unreadDiscussionCount;
    public int SubtaskID { get; init; }
    public int TaskID { get; init; }
    public string Title { get; init; } = string.Empty;
    public int? ProofID { get; init; }
    public string? ProofFileName { get; init; }
    public string? ProofStatus { get; init; }
    public DateTime? ProofUploadedAt { get; init; }
    public string? AdminRemarks { get; init; }
    public List<SubtaskProofHistoryItem> ProofHistory { get; init; } = [];
    public int UnreadDiscussionCount
    {
        get => _unreadDiscussionCount;
        set
        {
            if (_unreadDiscussionCount == value) return;
            _unreadDiscussionCount = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UnreadDiscussionCount)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasUnreadDiscussion)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UnreadDiscussionDisplay)));
        }
    }
    public bool HasUnreadDiscussion => UnreadDiscussionCount > 0;
    public string UnreadDiscussionDisplay => UnreadDiscussionCount > 9 ? "9+" : UnreadDiscussionCount.ToString();
    public bool HasProof => ProofID.HasValue;
    public bool HasProofHistory => ProofHistory.Count > 0;
    public string ProofHistoryButtonText => CanReviewProof ? "Review Submission" : "View Submission History";
    public bool IsProofDraft => ProofStatus == "Draft";
    public bool IsProofPending => ProofStatus == "Pending";
    public bool IsProofApproved => ProofStatus == "Approved";
    public bool IsProofReturned => ProofStatus == "Returned";
    public bool ProofEditingIsAvailable { get; set; } = true;
    public bool CanUploadProof => !IsCompleted && ProofEditingIsAvailable &&
                                  (!HasProof || IsProofDraft || IsProofReturned);
    public bool CanConfirmProof => HasProof && IsProofDraft && !IsCompleted;
    public bool CanViewDraft => HasProof && IsProofDraft;
    public bool ShowTeacherProofButton => CanViewDraft || HasProofHistory;
    public string TeacherProofButtonText => CanViewDraft ? "Review draft" : "View submission history";
    public bool ReviewIsAvailable { get; set; }
    public bool CanReviewProof => ReviewIsAvailable && HasProof && IsProofPending;
    public bool CanRemoveProof => HasProof && !IsCompleted && ProofEditingIsAvailable && IsProofDraft;
    public bool ShowProofToReviewer => HasProof && !IsProofDraft;
    public string ProofActionText => HasProof ? "Replace file" : "Upload file";
    public string ProofStatusText => ProofStatus switch
    {
        "Draft" => "Ready to submit",
        "Pending" => "Pending review",
        "Approved" => "Approved",
        "Returned" => "Changes requested",
        _ => string.Empty
    };
    public Color ProofStatusColor => ProofStatus switch
    {
        "Approved" => Color.FromArgb("#16803A"),
        "Returned" => Color.FromArgb("#DC2626"),
        "Draft" => Color.FromArgb("#2563EB"),
        "Pending" => Color.FromArgb("#D97706"),
        _ => Color.FromArgb("#6B7280")
    };
    public bool IsCompleted
    {
        get => _isCompleted;
        set
        {
            if (_isCompleted == value) return;
            _isCompleted = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCompleted)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Marker)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MarkerColor)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TitleColor)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TitleDecoration)));
        }
    }
    public string Marker => IsCompleted ? string.Empty : "\u25CB";
    public Color MarkerColor => IsCompleted ? Color.FromArgb("#27AE60") : Color.FromArgb("#6B7280");
    public Color TitleColor => IsCompleted ? Color.FromArgb("#6B7280") : Color.FromArgb("#4B5563");
    public TextDecorations TitleDecoration => IsCompleted ? TextDecorations.Strikethrough : TextDecorations.None;
    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class DeadlineTaskGroup : INotifyPropertyChanged
{
    private bool _isExpanded;
    public DateTime? Deadline { get; init; }
    public string TaskTitle { get; init; } = string.Empty;
    public string DeadlineDisplay { get; init; } = string.Empty;
    public string TeacherSummary { get; init; } = string.Empty;
    public List<DashboardTaskItem> Tasks { get; init; } = [];
    public Color PriorityColor { get; init; } = Colors.Gray;
    public string StateSummary
    {
        get
        {
            if (Tasks.Count == 0)
                return "No tasks";
            if (Tasks.Count == 1)
                return Tasks[0].Status;

            return string.Join(" Â· ", Tasks
                .GroupBy(task => task.Status)
                .Select(group => $"{group.Count()} {group.Key}"));
        }
    }
    public Color StateColor
    {
        get
        {
            if (Tasks.Count > 0 && Tasks.All(task => task.IsCompleted))
                return Color.FromArgb("#16803A");
            if (Tasks.Any(task => task.Status == "Needs Revision"))
                return Color.FromArgb("#DC2626");
            if (Tasks.Any(task => task.IsAwaitingValidation))
                return Color.FromArgb("#6554C0");
            if (Tasks.Any(task => task.Status == "Acknowledged"))
                return Color.FromArgb("#2563EB");
            return Color.FromArgb("#D97706");
        }
    }
    public int TotalSubtasks => Tasks.Sum(task => task.TotalProgressItems);
    public int SubmittedSubtasks => Tasks.Sum(task => task.SubmittedProgressItems);
    public int ApprovedSubtasks => Tasks.Sum(task => task.VerifiedProgressItems);
    public double GroupProgress => TotalSubtasks == 0 ? 0d : (double)ApprovedSubtasks / TotalSubtasks;
    public string GroupProgressSummary => TotalSubtasks == 0
        ? "No subtasks"
        : $"{ApprovedSubtasks}/{TotalSubtasks} approved";
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            _isExpanded = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsCollapsed)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ArrowRotation)));
        }
    }
    public string Arrow => "\u203A";
    public bool IsCollapsed => !IsExpanded;
    public double ArrowRotation => IsExpanded ? 90d : 0d;
    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class TeacherTaskItem
{
    public int AssignmentID { get; set; }
    public int CreatedByUserID { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Deadline { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Color PriorityColor { get; set; } = Colors.Gray;
    public Color StatusColor { get; set; } = Colors.Gray;
    public bool ShowAcknowledge { get; set; }
    public ICommand? AcknowledgeCommand { get; set; }
}

public sealed class PreparedProofImage
{
    public required byte[] Data { get; init; }
    public required string FileName { get; init; }
    public required string ContentType { get; init; }
}

public sealed class SubtaskProofHistoryItem
{
    public int HistoryID { get; init; }
    public int AttemptNumber { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public string ValidationStatus { get; init; } = string.Empty;
    public DateTime SubmittedAt { get; init; }
    public DateTime? ReviewedAt { get; init; }
    public int? ReviewedByUserID { get; init; }
    public string? ReturnRemarks { get; init; }
    public string AttemptDisplay => AttemptNumber.ToString();
    public string SubmittedAtDisplay => SubmittedAt.ToString("MMM d, yyyy h:mm tt");
    public Color StatusColor => ValidationStatus switch
    {
        "Approved" => Color.FromArgb("#16803A"),
        "Returned" => Color.FromArgb("#DC2626"),
        "Pending" => Color.FromArgb("#D97706"),
        _ => Color.FromArgb("#6B7280")
    };
}


