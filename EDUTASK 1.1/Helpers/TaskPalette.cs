namespace EDUTASK_1._1.Helpers
{
    /// <summary>
    /// The colour a task wears, by priority and by status.
    ///
    /// This existed as a private <c>GetPriorityColor</c> / <c>StatusColor</c> pair
    /// copied into four screens, and the copies had drifted: the calendar drew a
    /// High task amber and a Medium task blue, while every dashboard drew the same
    /// two red and amber. A task changed colour depending on which screen you were
    /// looking at, and the calendar's month dots inherited the wrong colours too.
    ///
    /// One mapping now, so they cannot drift again.
    /// </summary>
    public static class TaskPalette
    {
        /// <summary>
        /// Priority colour. Deliberately the Priority tokens rather than the
        /// Status ones: the palette keeps them apart so a High-priority task is
        /// never mistaken for an overdue one.
        ///
        /// The app offers exactly Low, Medium and High — see the priority picker
        /// on Create Task and Edit Task. Anything else is a data error and gets
        /// the neutral tone rather than a colour that claims a meaning.
        /// </summary>
        public static Color PriorityColor(string? priority) => priority switch
        {
            "High" => AppColors.PriorityHigh,
            "Medium" => AppColors.PriorityMedium,
            "Low" => AppColors.PriorityLow,
            _ => AppColors.StatusNeutral
        };

        /// <summary>Sort order for priority. High first.</summary>
        public static int PriorityRank(string? priority) => priority?.Trim().ToUpperInvariant() switch
        {
            "HIGH" => 0,
            "MEDIUM" => 1,
            "LOW" => 2,
            _ => 3
        };

        /// <summary>Foreground for a status badge.</summary>
        public static Color StatusColor(string? status) => status switch
        {
            "Completed" => AppColors.StatusSuccess,
            "Overdue" or "Needs Revision" or "Returned" => AppColors.StatusDanger,
            "For Validation" => AppColors.StatusValidation,
            "Acknowledged" => AppColors.StatusOngoing,
            "Pending" => AppColors.StatusPending,
            "Ongoing" => AppColors.StatusOngoing,
            _ => AppColors.StatusWarning
        };

        /// <summary>
        /// Tinted fill behind a status badge. Every pair here clears WCAG AA
        /// against its own foreground — keep that true of anything added.
        /// </summary>
        public static Color StatusSurface(string? status) => status switch
        {
            "Completed" => AppColors.StatusSuccessSurface,
            "Overdue" or "Needs Revision" or "Returned" => AppColors.StatusDangerSurface,
            "For Validation" => AppColors.StatusValidationSurface,
            "Acknowledged" => AppColors.StatusOngoingSurface,
            "Pending" => AppColors.StatusPendingSurface,
            "Ongoing" => AppColors.StatusOngoingSurface,
            _ => AppColors.StatusWarningSurface
        };

        /// <summary>Hairline around a status badge, one step darker than its fill.</summary>
        public static Color StatusBorder(string? status) => status switch
        {
            "Completed" => AppColors.StatusSuccessBorder,
            "Overdue" or "Needs Revision" or "Returned" => AppColors.StatusDangerBorder,
            "For Validation" => AppColors.StatusValidationBorder,
            "Acknowledged" => AppColors.StatusOngoingBorder,
            "Pending" => AppColors.StatusPendingBorder,
            "Ongoing" => AppColors.StatusOngoingBorder,
            _ => AppColors.StatusWarningBorder
        };
    }
}
