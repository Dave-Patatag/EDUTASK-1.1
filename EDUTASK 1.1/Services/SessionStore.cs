using EDUTASK_1._1.Models;

namespace EDUTASK_1._1.Services;

internal static class SessionStore
{
    public static Teachers? CurrentTeacher { get; private set; }
    public static User? CurrentUser { get; private set; }

    public static void SetCurrentTeacher(Teachers teacher)
    {
        CurrentUser = null;
        CurrentTeacher = teacher;
    }

    public static void SetCurrentUser(User user)
    {
        CurrentTeacher = null;
        CurrentUser = user;
    }

    public static void ClearTeacher() => CurrentTeacher = null;

    public static void ClearUser() => CurrentUser = null;
}
