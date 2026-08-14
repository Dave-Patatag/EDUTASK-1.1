using EDUTASK_1._1.Models;

namespace EDUTASK_1._1.Services;

public static class UserSessionService
{
    public const int FixedUserId = 1;

    public static User? CurrentUser { get; private set; }
    public static int CurrentUserId => CurrentUser?.UserID
        ?? throw new InvalidOperationException("No Director or Staff account is signed in.");
    public static string CurrentRole => CurrentUser?.RoleName ?? string.Empty;
    public static bool IsDirector => CurrentRole == "Director";
    public static bool IsStaff => CurrentRole == "Staff";
    public static bool CanCreateTasks => IsDirector || IsStaff;
    public static bool CanDeleteTasks => IsDirector;
    public static bool CanReviewSubtaskProof => IsDirector || IsStaff;
    public static bool CanApproveCompletion => IsDirector;
    public static bool CanRequestRevision => IsDirector || IsStaff;

    public static async Task<User?> GetCurrentUserAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        if (CurrentUser is not null && !forceRefresh)
            return CurrentUser;
        if (CurrentUser is null)
            return null;

        var database = new DatabaseService();
        CurrentUser = await database.GetUserByIdAsync(CurrentUser.UserID, cancellationToken);
        if (CurrentUser is { IsActive: false })
            CurrentUser = null;
        return CurrentUser;
    }

    public static void SetCurrentUser(User user)
    {
        if (!user.IsActive || user.RoleName is not ("Director" or "Staff"))
            throw new InvalidOperationException("Only an active Director or Staff account can start this session.");
        CurrentUser = user;
    }

    public static void Clear() => CurrentUser = null;
}