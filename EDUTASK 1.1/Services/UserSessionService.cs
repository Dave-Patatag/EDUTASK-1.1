using EDUTASK_1._1.Models;

namespace EDUTASK_1._1.Services;

public static class UserSessionService
{
    public const int FixedUserId = 1;

    public static User? CurrentUser => SessionStore.CurrentUser;
    public static int CurrentUserId => CurrentUser?.User_id
        ?? throw new InvalidOperationException("No Director or Staff account is signed in.");
    public static string CurrentRole => CurrentUser?.Role_name ?? string.Empty;
    public static bool IsDirector => CurrentRole == "Director";
    public static bool IsStaff => CurrentRole == "Staff";
    public static bool CanDeleteTasks => IsDirector;
    public static bool CanReviewSubtaskProof => IsDirector || IsStaff;
    public static bool CanApproveCompletion => IsDirector;

    public static async Task<User?> GetCurrentUserAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        if (CurrentUser is not null && !forceRefresh)
            return CurrentUser;
        if (CurrentUser is null)
            return null;

        var database = new DatabaseService();
        User? refreshed = await database.GetUserByIdAsync(CurrentUser.User_id, cancellationToken);
        if (refreshed is { Is_active: false })
            refreshed = null;
        if (refreshed is null)
            SessionStore.ClearUser();
        else
            SessionStore.SetCurrentUser(refreshed);
        return CurrentUser;
    }

    public static void SetCurrentUser(User user)
    {
        if (!user.Is_active || user.Role_name is not ("Director" or "Staff"))
            throw new InvalidOperationException("Only an active Director or Staff account can start this session.");
        SessionStore.SetCurrentUser(user);
    }

    public static void Clear() => SessionStore.ClearUser();
}
