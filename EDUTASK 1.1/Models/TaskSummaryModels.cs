using EDUTASK_1._1.Helpers;

namespace EDUTASK_1._1.Models;

public sealed class TaskSummaryItem
{
    public int TaskID { get; init; }
    public string Title { get; init; } = string.Empty;
    public string TeacherName { get; init; } = string.Empty;
    public string Priority { get; init; } = string.Empty;
    public DateTime? Deadline { get; init; }
    public DateTime? CompletedAt { get; init; }
    public string Status { get; init; } = string.Empty;
    public Color StatusColor { get; init; } = AppColors.StatusNeutral;
    public string DeadlineDisplay => Deadline?.ToString("MMM d, yyyy") ?? "No deadline";
    public bool IsOverdue =>
        Status != "Completed" && Deadline.HasValue && Deadline.Value.Date < DateTime.Today;
    public string ReportCategory => IsOverdue
        ? "Overdue"
        : Status switch
        {
            "Completed" => "Completed",
            "Acknowledged" or "For Validation" or "Needs Revision" => "Ongoing",
            _ => "Pending"
        };
    public string DueDisplay => Deadline.HasValue ? $"Due {DeadlineDisplay}" : "No deadline";
    public Color DueTextColor => IsOverdue ? AppColors.StatusDanger : AppColors.TextSecondary;
    public Color ReportStatusColor => ReportCategory switch
    {
        "Pending" => AppColors.Accent500,
        "Ongoing" => AppColors.StatusWarning,
        "Completed" => AppColors.StatusSuccess,
        "Overdue" => AppColors.StatusDanger,
        _ => AppColors.StatusNeutral
    };
    public Color ReportStatusSurfaceColor => ReportCategory switch
    {
        "Pending" => AppColors.SelectionSurface,
        "Ongoing" => AppColors.StatusWarningSurface,
        "Completed" => AppColors.StatusSuccessSurface,
        "Overdue" => AppColors.StatusDangerSurface,
        _ => AppColors.StatusNeutralSurface
    };
    public Color ReportStatusBorderColor => ReportCategory switch
    {
        "Pending" => AppColors.StatusInfoBorder,
        "Ongoing" => AppColors.StatusWarningBorder,
        "Completed" => AppColors.StatusSuccessBorder,
        "Overdue" => AppColors.StatusDangerBorder,
        _ => AppColors.StatusNeutralBorder
    };
}
