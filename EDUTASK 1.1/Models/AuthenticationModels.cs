namespace EDUTASK_1._1.Models;

public sealed record AuthenticationResult(bool Success, string Message, string Role, User? User, Teachers? Teacher);
public sealed record PendingAccountItem(int AccountID, string AccountType, string FullName, string Email, DateTime RequestedAt);
public sealed record DirectoryAccountItem(
    int AccountID, string AccountType, string FullName, string Username,
    string Email, string Contact_number, bool IsDisabled);

public static class SecurityQuestions
{
    public static readonly string[] All =
    [
        "What is your pet's name?",
        "What city were you born in?",
        "What was the name of your first school?",
        "What is your mother's maiden name?",
        "What was your childhood nickname?"
    ];
}
