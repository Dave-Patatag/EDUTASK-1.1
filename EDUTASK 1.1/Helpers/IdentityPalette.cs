namespace EDUTASK_1._1.Helpers
{
    /// <summary>
    /// The categorical colours that give a person or a subtask a stable visual
    /// identity in a list.
    ///
    /// The same eight hex values used to be declared three times — twice in the
    /// profile view models and once, as a different set, in the subtask draft
    /// model — so an avatar in Profile and the same avatar in Edit Profile were
    /// only the same colour by coincidence. They resolve from Colors.xaml now,
    /// like every other colour in the app.
    ///
    /// Initials are drawn on these in white, so each step clears WCAG AA (4.5:1)
    /// against white. Keep that true of anything added here.
    /// </summary>
    public static class IdentityPalette
    {
        /// <summary>Number of colours in the palette.</summary>
        public const int Count = 8;

        /// <summary>
        /// The colour at <paramref name="index"/>, wrapping so any hash can be
        /// passed straight in.
        /// </summary>
        public static Color At(int index) =>
            AppColors.Get($"Identity{Math.Abs(index) % Count + 1}");

        /// <summary>A colour chosen at random. For decoration, not identity.</summary>
        public static Color Random() => At(System.Random.Shared.Next(Count));
    }
}
