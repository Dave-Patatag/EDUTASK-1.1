namespace EDUTASK_1._1.Helpers
{
    /// <summary>
    /// What a notification's colour means, in one place.
    ///
    /// Four tones, and only four:
    ///
    ///     Blue    pending — a task was assigned, or proof is waiting on you
    ///     Orange  ongoing — the teacher acknowledged the task
    ///     Green   completed, or proof approved
    ///     Red     overdue, or proof returned for changes
    ///
    /// The tone is carried twice on every row, because colour is never the only
    /// encoding: as the unread dot, and as the glyph on the disc a system
    /// notification shows in place of a person's photo.
    ///
    /// Those two uses want different tunings, which is why this maps to two
    /// different families rather than one. The dot is a 9dp fill with nothing
    /// read on top of it. The Status tokens are the wrong set for that — they
    /// are tuned as foregrounds, dark enough to clear 4.5:1 as badge text, and
    /// at dot size that tuning collapses: StatusWarning (#B45309) reads brown
    /// rather than orange and sits 1.12:1 from StatusDanger, so amber and red
    /// dots fuse. The fill-tuned hues carry the same meanings with room between
    /// them; Colors.xaml's CHART block works the arithmetic out in full. The
    /// glyph is the opposite case — text on a tint — so it takes the Status
    /// foreground over its matching Status surface, a pairing the palette
    /// already guarantees clears AA.
    ///
    /// Categories are the ones GetNotificationsAsync emits, and nothing else.
    /// An unrecognised category is treated as pending rather than given a
    /// colour that claims a meaning it has not earned.
    /// </summary>
    public static class NotificationPalette
    {
        public const string Pending = "Action";
        public const string Ongoing = "Ongoing";
        public const string Completed = "Success";
        public const string Overdue = "Urgent";

        /// <summary>The unread dot. A fill, so it takes the fill-tuned hues.</summary>
        public static Color ToneColor(string? category) => category switch
        {
            Overdue => AppColors.ChartOverdue,
            Completed => AppColors.ChartCompleted,
            Ongoing => AppColors.ChartOngoing,
            _ => AppColors.Accent500
        };

        /// <summary>The disc behind a system notification's glyph.</summary>
        public static Color ToneSurface(string? category) => category switch
        {
            Overdue => AppColors.StatusDangerSurface,
            Completed => AppColors.StatusSuccessSurface,
            Ongoing => AppColors.StatusWarningSurface,
            _ => AppColors.StatusInfoSurface
        };

        /// <summary>The glyph itself. Text on a tint, so it takes the foreground token.</summary>
        public static Color ToneGlyphColor(string? category) => category switch
        {
            Overdue => AppColors.StatusDanger,
            Completed => AppColors.StatusSuccess,
            Ongoing => AppColors.StatusWarning,
            _ => AppColors.StatusInfo
        };

        /// <summary>
        /// The second encoding of the tone, for anyone who cannot separate the
        /// first. Kept to characters every platform font carries — an emoji
        /// here renders as a replacement box on at least one of them.
        /// </summary>
        public static string ToneGlyph(string? category) => category switch
        {
            Overdue => "!",
            Completed => "✓",
            Ongoing => "→",
            _ => "i"
        };
    }
}
