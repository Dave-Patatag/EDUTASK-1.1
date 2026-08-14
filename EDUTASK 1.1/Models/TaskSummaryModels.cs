namespace EDUTASK_1._1.Models;

public sealed class TaskSummaryItem
{
    public int TaskID { get; init; }
    public string Title { get; init; } = string.Empty;
    public string TeacherName { get; init; } = string.Empty;
    public DateTime? Deadline { get; init; }
    public string Status { get; init; } = string.Empty;
    public Color StatusColor { get; init; } = Colors.Gray;
    public string DeadlineDisplay => Deadline?.ToString("MMM d, yyyy") ?? "No deadline";
}

public sealed class TeacherSummaryItem
{
    public string TeacherName { get; init; } = string.Empty;
    public int AssignedCount { get; init; }
    public int CompletedCount { get; init; }
    public int OverdueCount { get; init; }
    public double CompletionRate => AssignedCount == 0 ? 0 : (double)CompletedCount / AssignedCount;
    public string CompletionDisplay => $"{CompletedCount} of {AssignedCount} completed";
    public string PercentDisplay => $"{CompletionRate:P0}";
}
