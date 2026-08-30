using EDUTASK_1._1.Helpers;

namespace EDUTASK_1._1.Services;

public static class FormFieldValidation
{
    public const int MinPasswordLength = 8;

    public static readonly Color ErrorStrokeColor = AppColors.StatusDanger;
    public static readonly Color ClearStrokeColor = AppColors.BorderStrong;

    /// <summary>
    /// Puts a field into (or out of) its error state.
    ///
    /// Every form in the app routes through here, so the error treatment is
    /// identical everywhere: red stroke, inline message, and a screen-reader
    /// announcement. Colour is never the only signal — the message carries the
    /// meaning for anyone who cannot distinguish the stroke.
    ///
    /// The announcement fires only on the transition into an error. Callers
    /// clear errors on every keystroke, so announcing unconditionally would
    /// talk over the user as they type.
    /// </summary>
    public static void SetFieldError(Border border, Label label, string message)
    {
        var hadError = !string.IsNullOrEmpty(label.Text);
        var hasError = !string.IsNullOrEmpty(message);

        label.Text = message;
        label.IsVisible = hasError;
        border.Stroke = hasError ? ErrorStrokeColor : ClearStrokeColor;

        if (hasError && !hadError)
            TryAnnounce(message);
    }

    public static void ClearFieldError(Border border, Label label) =>
        SetFieldError(border, label, string.Empty);

    public static bool IsValidEmail(string email) =>
        System.Net.Mail.MailAddress.TryCreate(email, out _);

    public static bool IsValidUsername(string username) =>
        username.Length is >= 3 and <= 30 &&
        username.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-');

    public static bool IsValidPassword(string password) =>
        password.Length >= MinPasswordLength;

    static void TryAnnounce(string message)
    {
        try
        {
            SemanticScreenReader.Announce(message);
        }
        catch
        {
            // Not every platform/target has an active screen reader service.
            // A failed announcement must not break form validation.
        }
    }
}
