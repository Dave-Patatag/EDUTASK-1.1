using EDUTASK_1._1.Models;
using Microsoft.Data.SqlClient;
using System.Data;

namespace EDUTASK_1._1.Services;

public static class NotificationDatabaseExtensions
{
    public static async Task<List<NotificationItem>> GetNotificationsAsync(this DatabaseService database,
        string recipientType, int recipientID, CancellationToken token = default)
    {
        await EnsureTableAsync(database, token);
        const string query = """
            WITH Activity AS (
              SELECT CONCAT('assigned:',ta.AssignmentID) NotificationKey,N'New task assigned' Title,
                CONCAT(N'You were assigned “',t.Title,N'”, due ',FORMAT(ta.Deadline,'MMM d, yyyy'),N'.') Message,
                N'Action' Category,t.TaskID,ta.AssignedAt CreatedAt,N'Teacher' RecipientType,ta.TeacherID RecipientID,
                CAST(NULL AS nvarchar(500)) ActorPhotoPath,CAST(NULL AS nvarchar(10)) ActorInitials
              FROM dbo.TaskAssignment ta JOIN dbo.[Task] t ON t.TaskID=ta.TaskID
              UNION ALL
              SELECT CONCAT('review:',h.HistoryID),CASE WHEN h.ValidationStatus=N'Approved' THEN N'Proof approved' ELSE N'Changes requested' END,
                CONCAT(N'Your proof for “',s.Title,N'” was ',CASE WHEN h.ValidationStatus=N'Approved' THEN N'approved.' ELSE CONCAT(N'returned. ',COALESCE(h.ReturnRemarks,N'')) END),
                CASE WHEN h.ValidationStatus=N'Approved' THEN N'Success' ELSE N'Urgent' END,s.TaskID,COALESCE(h.ReviewedAt,h.SubmittedAt),N'Teacher',ta.TeacherID,
                u.ProfilePhotoPath,CONCAT(LEFT(u.FirstName,1),LEFT(u.LastName,1))
              FROM dbo.SubtaskProofHistory h JOIN dbo.Subtask s ON s.SubtaskID=h.SubtaskID JOIN dbo.TaskAssignment ta ON ta.TaskID=s.TaskID
              LEFT JOIN dbo.[User] u ON u.UserID=h.ReviewedByUserID
              WHERE h.ValidationStatus IN(N'Approved',N'Returned')
              UNION ALL
              SELECT CONCAT('proof:',h.HistoryID),N'Proof submitted',CONCAT(te.FirstName,N' ',te.LastName,N' submitted proof for “',s.Title,N'”.'),
                N'Action',s.TaskID,h.SubmittedAt,N'User',COALESCE(t.CreatedByUserID,t.UserID),te.ProfilePhotoPath,
                CONCAT(LEFT(te.FirstName,1),LEFT(te.LastName,1))
              FROM dbo.SubtaskProofHistory h JOIN dbo.Subtask s ON s.SubtaskID=h.SubtaskID JOIN dbo.[Task] t ON t.TaskID=s.TaskID
              CROSS APPLY(SELECT TOP 1 a.TeacherID FROM dbo.TaskAssignment a WHERE a.TaskID=t.TaskID ORDER BY a.AssignmentID)x
              JOIN dbo.Teacher te ON te.TeacherID=x.TeacherID WHERE h.ValidationStatus=N'Pending'
              UNION ALL
              SELECT CONCAT('ack:',ta.AssignmentID),N'Task acknowledged',CONCAT(te.FirstName,N' ',te.LastName,N' acknowledged “',t.Title,N'”.'),
                N'Ongoing',t.TaskID,ta.AcknowledgedAt,N'User',COALESCE(t.CreatedByUserID,t.UserID),te.ProfilePhotoPath,
                CONCAT(LEFT(te.FirstName,1),LEFT(te.LastName,1))
              FROM dbo.TaskAssignment ta JOIN dbo.[Task] t ON t.TaskID=ta.TaskID JOIN dbo.Teacher te ON te.TeacherID=ta.TeacherID
              WHERE ta.IsAcknowledged=1 AND ta.AcknowledgedAt IS NOT NULL
              UNION ALL
              SELECT CONCAT('completed:',ta.AssignmentID),N'Task completed',CONCAT(N'“',t.Title,N'” has been marked complete.'),N'Success',
                t.TaskID,ta.CompletedAt,N'Teacher',ta.TeacherID,NULL,NULL
              FROM dbo.TaskAssignment ta JOIN dbo.[Task] t ON t.TaskID=ta.TaskID
              WHERE ta.CompletionStatus=N'Completed' AND ta.CompletedAt IS NOT NULL
              UNION ALL
              SELECT CONCAT('completed-owner:',t.TaskID),N'Task completed',CONCAT(N'“',t.Title,N'” has been marked complete.'),N'Success',
                t.TaskID,ta.CompletedAt,N'User',COALESCE(t.CreatedByUserID,t.UserID),NULL,NULL
              FROM dbo.TaskAssignment ta JOIN dbo.[Task] t ON t.TaskID=ta.TaskID
              WHERE ta.CompletionStatus=N'Completed' AND ta.CompletedAt IS NOT NULL
                AND ta.AssignmentID=(SELECT MIN(ta2.AssignmentID) FROM dbo.TaskAssignment ta2
                                      WHERE ta2.TaskID=ta.TaskID AND ta2.CompletionStatus=N'Completed' AND ta2.CompletedAt IS NOT NULL)
                AND (t.CompletionApprovedByUserID IS NULL OR t.CompletionApprovedByUserID<>COALESCE(t.CreatedByUserID,t.UserID))
              UNION ALL
              SELECT CONCAT('deadline:',ta.AssignmentID),CASE WHEN ta.Deadline<CAST(GETDATE()AS date) THEN N'Task overdue' WHEN ta.Deadline=CAST(GETDATE()AS date) THEN N'Deadline today' ELSE N'Deadline approaching' END,
                CONCAT(N'“',t.Title,N'” ',CASE WHEN ta.Deadline<CAST(GETDATE()AS date) THEN N'is overdue.' WHEN ta.Deadline=CAST(GETDATE()AS date) THEN N'is due today.' ELSE N'is due tomorrow.' END),N'Urgent',
                t.TaskID,CAST(ta.Deadline AS datetime),N'Teacher',ta.TeacherID,NULL,NULL
              FROM dbo.TaskAssignment ta JOIN dbo.[Task] t ON t.TaskID=ta.TaskID
              WHERE ta.CompletionStatus<>N'Completed' AND ta.Deadline<=DATEADD(day,1,CAST(GETDATE()AS date))
              UNION ALL
              SELECT CONCAT('deadline-owner:',t.TaskID),CASE WHEN ta.Deadline<CAST(GETDATE()AS date) THEN N'Task overdue' WHEN ta.Deadline=CAST(GETDATE()AS date) THEN N'Deadline today' ELSE N'Deadline approaching' END,
                CONCAT(N'“',t.Title,N'” ',CASE WHEN ta.Deadline<CAST(GETDATE()AS date) THEN N'is overdue.' WHEN ta.Deadline=CAST(GETDATE()AS date) THEN N'is due today.' ELSE N'is due tomorrow.' END),N'Urgent',
                t.TaskID,CAST(ta.Deadline AS datetime),N'User',COALESCE(t.CreatedByUserID,t.UserID),NULL,NULL
              FROM dbo.TaskAssignment ta JOIN dbo.[Task] t ON t.TaskID=ta.TaskID
              WHERE ta.CompletionStatus<>N'Completed' AND ta.Deadline<=DATEADD(day,1,CAST(GETDATE()AS date))
                AND ta.AssignmentID=(SELECT MIN(ta2.AssignmentID) FROM dbo.TaskAssignment ta2
                                      WHERE ta2.TaskID=ta.TaskID AND ta2.CompletionStatus<>N'Completed' AND ta2.Deadline<=DATEADD(day,1,CAST(GETDATE()AS date)))
              UNION ALL
              SELECT CONCAT('comment-owner:',c.CommentID),N'New comment',
                CONCAT(c.AuthorName,N' commented on “',t.Title,N'”.'),N'Action',c.TaskID,c.CreatedAt,N'User',COALESCE(t.CreatedByUserID,t.UserID),NULL,NULL
              FROM dbo.TaskComment c
              JOIN dbo.[Task] t ON t.TaskID=c.TaskID
              WHERE c.AuthorType=N'Teacher' AND c.AuthorID<>COALESCE(t.CreatedByUserID,t.UserID)
              UNION ALL
              SELECT CONCAT('comment-teacher:',c.CommentID,'-',ta.TeacherID),N'New comment',
                CONCAT(c.AuthorName,N' commented on “',t.Title,N'”.'),N'Action',c.TaskID,c.CreatedAt,N'Teacher',ta.TeacherID,NULL,NULL
              FROM dbo.TaskComment c
              JOIN dbo.[Task] t ON t.TaskID=c.TaskID
              JOIN dbo.TaskAssignment ta ON ta.TaskID=c.TaskID
              WHERE c.AuthorType=N'User' AND c.AuthorID<>ta.TeacherID
            )
            SELECT TOP(100)a.NotificationKey,a.Title,a.Message,a.Category,a.TaskID,a.CreatedAt,a.ActorPhotoPath,a.ActorInitials,
                CASE WHEN nr.NotificationKey IS NULL THEN 0 ELSE 1 END IsRead,
                CASE WHEN EXISTS(SELECT 1 FROM dbo.TaskAssignment ta_chk WHERE ta_chk.TaskID=a.TaskID AND ta_chk.CompletionStatus=N'Completed')
                     THEN 1 ELSE 0 END IsTaskCompleted
            FROM Activity a
            LEFT JOIN dbo.NotificationRead nr ON nr.RecipientType=a.RecipientType AND nr.RecipientID=a.RecipientID AND nr.NotificationKey=a.NotificationKey
            LEFT JOIN dbo.NotificationHidden nh ON nh.RecipientType=a.RecipientType AND nh.RecipientID=a.RecipientID AND nh.NotificationKey=a.NotificationKey
            WHERE a.RecipientType=@Type AND a.RecipientID=@ID AND nh.NotificationKey IS NULL
            ORDER BY a.CreatedAt DESC;
            """;
        DataTable table = await database.ExecuteQueryAsync(query,
            [new SqlParameter("@Type",SqlDbType.NVarChar,10){Value=recipientType},new SqlParameter("@ID",SqlDbType.Int){Value=recipientID}],token);
        bool hideViewTaskWhenCompleted = recipientType == "Teacher";
        return table.AsEnumerable().Select(row => new NotificationItem {
            NotificationKey=row.Field<string>("NotificationKey")??string.Empty,Title=row.Field<string>("Title")??"Task update",
            Message=row.Field<string>("Message")??string.Empty,Category=row.Field<string>("Category")??"Action",
            TaskID=row.IsNull("TaskID")?null:row.Field<int>("TaskID"),CreatedAt=row.Field<DateTime>("CreatedAt"),
            ActorPhotoPath=row.Field<string>("ActorPhotoPath"),ActorInitials=row.Field<string>("ActorInitials")??string.Empty,
            IsRead=Convert.ToInt32(row["IsRead"])==1,
            ShowViewTask=!(hideViewTaskWhenCompleted && Convert.ToInt32(row["IsTaskCompleted"])==1)}).ToList();
    }

    public static async System.Threading.Tasks.Task MarkNotificationReadAsync(this DatabaseService database,string type,int id,string key,CancellationToken token=default)
    {
        await EnsureTableAsync(database,token);
        await database.ExecuteNonQueryAsync("IF NOT EXISTS(SELECT 1 FROM dbo.NotificationRead WHERE RecipientType=@T AND RecipientID=@I AND NotificationKey=@K) INSERT dbo.NotificationRead(RecipientType,RecipientID,NotificationKey)VALUES(@T,@I,@K);",
            [new SqlParameter("@T",SqlDbType.NVarChar,10){Value=type},new SqlParameter("@I",SqlDbType.Int){Value=id},new SqlParameter("@K",SqlDbType.NVarChar,100){Value=key}],token);
    }

    public static async System.Threading.Tasks.Task HideNotificationsAsync(
        this DatabaseService database,
        string type,
        int id,
        IEnumerable<string> notificationKeys,
        CancellationToken token = default)
    {
        await EnsureTableAsync(database, token);
        const string command = """
            IF NOT EXISTS(
                SELECT 1 FROM dbo.NotificationHidden
                WHERE RecipientType=@T AND RecipientID=@I AND NotificationKey=@K)
            INSERT dbo.NotificationHidden(RecipientType,RecipientID,NotificationKey)
            VALUES(@T,@I,@K);
            """;

        foreach (string key in notificationKeys.Where(key => !string.IsNullOrWhiteSpace(key)).Distinct())
        {
            token.ThrowIfCancellationRequested();
            await database.ExecuteNonQueryAsync(command,
                [
                    new SqlParameter("@T", SqlDbType.NVarChar, 10) { Value = type },
                    new SqlParameter("@I", SqlDbType.Int) { Value = id },
                    new SqlParameter("@K", SqlDbType.NVarChar, 100) { Value = key }
                ],
                token);
        }
    }

    private static async System.Threading.Tasks.Task EnsureTableAsync(DatabaseService database,CancellationToken token)=>await database.ExecuteNonQueryAsync(
        """
        IF OBJECT_ID(N'dbo.NotificationRead',N'U') IS NULL
            CREATE TABLE dbo.NotificationRead(
                RecipientType nvarchar(10) NOT NULL,
                RecipientID int NOT NULL,
                NotificationKey nvarchar(100) NOT NULL,
                ReadAt datetime2 NOT NULL DEFAULT SYSDATETIME(),
                CONSTRAINT PK_NotificationRead PRIMARY KEY(RecipientType,RecipientID,NotificationKey));

        IF OBJECT_ID(N'dbo.NotificationHidden',N'U') IS NULL
            CREATE TABLE dbo.NotificationHidden(
                RecipientType nvarchar(10) NOT NULL,
                RecipientID int NOT NULL,
                NotificationKey nvarchar(100) NOT NULL,
                HiddenAt datetime2 NOT NULL DEFAULT SYSDATETIME(),
                CONSTRAINT PK_NotificationHidden PRIMARY KEY(RecipientType,RecipientID,NotificationKey));
        """,
        cancellationToken:token);
}
