using EDUTASK_1._1.Models;

namespace EDUTASK_1._1.Services;

public static class TeacherSessionService
{
    public static Teachers? CurrentTeacher => SessionStore.CurrentTeacher;
    public static void SetCurrentTeacher(Teachers teacher) => SessionStore.SetCurrentTeacher(teacher);
    public static void Clear() => SessionStore.ClearTeacher();
}
