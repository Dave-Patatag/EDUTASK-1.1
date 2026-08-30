namespace EDUTASK_1._1.Helpers
{
    /// <summary>
    /// The eye button that reveals and re-masks a password field.
    ///
    /// Login, Create Account and Forgot Password each had their own copy of the
    /// same four lines, and each carried the same defect: Android's TextView
    /// forces the MONOSPACE typeface on any EditText whose input type carries a
    /// password variation, and swaps back to the default face when that
    /// variation is removed. Flipping IsPassword therefore changed the font
    /// underneath the entry, and the text jumped sideways on every tap of the
    /// eye. Re-applying the font and alignment afterwards pins the field to the
    /// app's own type, so masked and revealed text start at the same place.
    /// </summary>
    public static class PasswordField
    {
        /// <summary>
        /// Swaps between two identically positioned entries: one permanently
        /// masked and one permanently visible. This avoids the native control
        /// rebuilding its text presentation during a reveal, which can produce
        /// a one-frame vertical jump even when both final baselines match.
        /// </summary>
        public static void ToggleVisibility(
            Entry maskedEntry,
            Entry visibleEntry,
            ImageButton toggle)
        {
            bool showPassword = maskedEntry.IsVisible;
            Entry currentEntry = showPassword ? maskedEntry : visibleEntry;
            Entry nextEntry = showPassword ? visibleEntry : maskedEntry;
            int textLength = currentEntry.Text?.Length ?? 0;
            int cursorPosition = Math.Clamp(currentEntry.CursorPosition, 0, textLength);
            int selectionLength = Math.Clamp(
                currentEntry.SelectionLength,
                0,
                textLength - cursorPosition);

            SynchronizeText(currentEntry, nextEntry);
            RestoreTextPresentation(maskedEntry);
            RestoreTextPresentation(visibleEntry);

            maskedEntry.IsVisible = !showPassword;
            visibleEntry.IsVisible = showPassword;
            UpdateToggle(toggle, passwordIsVisible: showPassword);

            nextEntry.Dispatcher.Dispatch(() =>
            {
                RestoreTextPresentation(nextEntry);
                nextEntry.Focus();
                nextEntry.CursorPosition = Math.Min(
                    cursorPosition,
                    nextEntry.Text?.Length ?? 0);
                nextEntry.SelectionLength = Math.Min(
                    selectionLength,
                    (nextEntry.Text?.Length ?? 0) - nextEntry.CursorPosition);
            });
        }

        public static void SynchronizeText(Entry source, Entry target)
        {
            if (!string.Equals(source.Text, target.Text, StringComparison.Ordinal))
                target.Text = source.Text;
        }

        /// <summary>
        /// Masks or reveals <paramref name="entry"/> and updates the eye button
        /// that drives it — icon, tooltip and screen-reader name together, so
        /// the control never announces the state it just left.
        /// </summary>
        public static void ToggleVisibility(Entry entry, ImageButton toggle)
        {
            int textLength = entry.Text?.Length ?? 0;
            int cursorPosition = Math.Clamp(entry.CursorPosition, 0, textLength);
            int selectionLength = Math.Clamp(entry.SelectionLength, 0, textLength - cursorPosition);

            entry.IsPassword = !entry.IsPassword;

            // The icon names the action available now, not the current state:
            // masked text offers "show".
            UpdateToggle(toggle, passwordIsVisible: !entry.IsPassword);

            RestoreTextPresentation(entry);

            // IsPassword changes the native input type. Some platforms finish
            // rebuilding that native presentation on the next UI pass, after
            // the property setter above has returned. Re-apply the exact same
            // insets and alignment then, and preserve the user's caret instead
            // of forcing the field to scroll horizontally to the end.
            entry.Dispatcher.Dispatch(() =>
            {
                RestoreTextPresentation(entry);
                entry.Focus();
                entry.CursorPosition = Math.Min(cursorPosition, entry.Text?.Length ?? 0);
                entry.SelectionLength = Math.Min(
                    selectionLength,
                    (entry.Text?.Length ?? 0) - entry.CursorPosition);
            });
        }

        private static void UpdateToggle(ImageButton toggle, bool passwordIsVisible)
        {
            string action = passwordIsVisible ? "Hide password" : "Show password";
            toggle.Source = passwordIsVisible
                ? "loginhidepassword.png"
                : "loginshowpassword.png";
            ToolTipProperties.SetText(toggle, action);
            SemanticProperties.SetDescription(toggle, action);
        }

        /// <summary>
        /// Pushes the entry's font and alignment to the platform control again.
        /// The values do not change — the point is that the input-type change
        /// above has quietly replaced them on the native view, and only the
        /// handler can put them back.
        /// </summary>
        private static void RestoreTextPresentation(Entry entry)
        {
            entry.HorizontalTextAlignment = TextAlignment.Start;
            entry.VerticalTextAlignment = TextAlignment.Center;

            if (entry.Handler is not { } handler)
                return;

            handler.UpdateValue(nameof(ITextStyle.Font));
            handler.UpdateValue(nameof(ITextStyle.CharacterSpacing));
            handler.UpdateValue(nameof(ITextAlignment.HorizontalTextAlignment));
            handler.UpdateValue(nameof(ITextAlignment.VerticalTextAlignment));

#if ANDROID
            if (handler.PlatformView is AndroidX.AppCompat.Widget.AppCompatEditText editText)
            {
                editText.SetPadding(0, 0, 0, 0);
                editText.SetIncludeFontPadding(false);
                editText.Gravity = Android.Views.GravityFlags.CenterVertical |
                                   Android.Views.GravityFlags.Start;
            }
#endif

#if WINDOWS
            if (handler.PlatformView is Microsoft.UI.Xaml.Controls.TextBox textBox)
            {
                textBox.Padding = new Microsoft.UI.Xaml.Thickness(0);
                textBox.TextAlignment = Microsoft.UI.Xaml.TextAlignment.Left;
                textBox.VerticalContentAlignment = Microsoft.UI.Xaml.VerticalAlignment.Center;
            }
#endif
        }
    }
}
