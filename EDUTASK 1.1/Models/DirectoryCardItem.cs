using EDUTASK_1._1.Helpers;

namespace EDUTASK_1._1.Models;

/// <summary>
/// One row of the user management directory, ready to bind.
///
/// The page shows three lists — active accounts, accounts waiting for approval,
/// and accounts a Director has switched off — through a single card. The card
/// asks this type what to draw rather than carrying three templates and
/// toggling between them.
/// </summary>
public sealed class DirectoryCardItem
{
    private DirectoryCardItem(int accountID, string accountType, string fullName, string username, string email)
    {
        AccountID = accountID;
        AccountType = accountType;
        FullName = fullName;
        Username = username;
        Email = email;
    }

    public int AccountID { get; }
    public string AccountType { get; }
    public string FullName { get; }
    public string Username { get; }
    public string Email { get; }

    /// <summary>True for an account switched off by a Director.</summary>
    public bool IsDisabled { get; private init; }

    /// <summary>True for an account still waiting on approval.</summary>
    public bool IsRequest { get; private init; }

    /// <summary>Handle under the name. Falls back to the email for older rows
    /// that were created before usernames were required.</summary>
    public string UsernameDisplay
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Username))
                return Email;

            string handle = Username.Trim().TrimStart('@');
            return string.IsNullOrWhiteSpace(handle) ? Email : $"@{handle}";
        }
    }

    /// <summary>Up to two letters for the avatar, so a row without a photo
    /// still reads as a person rather than an empty disc.</summary>
    public string Initials
    {
        get
        {
            var parts = FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length switch
            {
                0 => "?",
                1 => parts[0][..1].ToUpperInvariant(),
                _ => $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant()
            };
        }
    }

    /// <summary>
    /// The pill on the right of the card. State is spelled out rather than left
    /// to the colour, so it survives greyscale and colour-blind viewing.
    /// </summary>
    public string BadgeText => IsRequest ? AccountType : IsDisabled ? "Disabled" : "Active";

    public Color BadgeColor => IsRequest
        ? AppColors.StatusPending
        : IsDisabled ? AppColors.StatusDanger : AppColors.StatusSuccess;

    /// <summary>The overflow menu is a Director-only affordance.</summary>
    public bool ShowMenu { get; private init; }

    public bool ShowApprove { get; private init; }

    /// <summary>A request row carries the Approve button where the others
    /// carry their state badge.</summary>
    public bool ShowBadge => !ShowApprove;

    /// <summary>Accessible name for the overflow trigger.</summary>
    public string MenuAction => $"Actions for {FullName}";

    public static DirectoryCardItem FromAccount(DirectoryAccountItem account, bool isDirector) =>
        new(account.AccountID, account.AccountType, account.FullName, account.Username, account.Email)
        {
            IsDisabled = account.IsDisabled,
            ShowMenu = isDirector
        };

    public static DirectoryCardItem FromRequest(PendingAccountItem request, bool isDirector) =>
        new(request.AccountID, request.AccountType, request.FullName, string.Empty, request.Email)
        {
            IsRequest = true,
            ShowApprove = isDirector
        };
}
