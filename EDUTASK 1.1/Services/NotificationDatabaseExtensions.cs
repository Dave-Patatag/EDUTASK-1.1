using EDUTASK_1._1.Models;
using Microsoft.Data.SqlClient;
using System.Data;

namespace EDUTASK_1._1.Services;

public static class NotificationDatabaseExtensions
{
    private static readonly SemaphoreSlim NotificationSchemaGate = new(1, 1);
    private static volatile bool _notificationSchemaEnsured;

    public static async Task<List<NotificationItem>> GetNotificationsAsync(this DatabaseService database,
        string recipientType, int recipientID, CancellationToken token = default)
    {
        await database.EnsureSubtaskProofSchemaAsync(token);
        await database.EnsureTaskDiscussionTableAsync(token);
        await database.EnsureTaskOwnershipSchemaAsync(token);
        await EnsureNotificationStateAsync(database, token);
        // Keep historical notification keys so existing read/hidden states remain attached.
        const string query = """
            WITH Activity AS (
              SELECT CONCAT('assigned:',ta.Assignment_id) Notification_key,N'New task assigned' Title,
                CONCAT(N'You were assigned â€œ',t.Title,N'â€, due ',FORMAT(ta.Deadline,'MMM d, yyyy'),N'.') Message,
                N'Action' Category,t.Task_id,ta.Assigned_at Created_at,N'Teacher' Recipient_type,ta.Teacher_id Recipient_id,
                CAST(NULL AS nvarchar(500)) ActorPhotoPath,CAST(NULL AS nvarchar(10)) ActorInitials
              FROM dbo.TaskAssignment ta JOIN dbo.[Task] t ON t.Task_id=ta.Task_id
              UNION ALL
              SELECT CONCAT('updated:',t.Task_id,':',CONVERT(nvarchar(33),t.Updated_at,126)),N'Task updated',
                CONCAT(N'â€œ',t.Title,N'â€ was updated. Review the latest task details.'),
                N'Action',t.Task_id,t.Updated_at,N'Teacher',ta.Teacher_id,NULL,NULL
              FROM dbo.[Task] t JOIN dbo.TaskAssignment ta ON ta.Task_id=t.Task_id
              WHERE t.Updated_at IS NOT NULL
              UNION ALL
              SELECT CONCAT('review:',h.Submission_id),CASE WHEN h.Proof_status=N'Approved' THEN N'Proof approved' ELSE N'Changes requested' END,
                CONCAT(N'Your proof for â€œ',s.Title,N'â€ was ',CASE WHEN h.Proof_status=N'Approved' THEN N'approved.' ELSE CONCAT(N'returned. ',COALESCE(h.Return_remarks,N'')) END),
                CASE WHEN h.Proof_status=N'Approved' THEN N'Success' ELSE N'Urgent' END,s.Task_id,COALESCE(h.Reviewed_at,h.Submitted_at),N'Teacher',ta.Teacher_id,
                u.Profile_photo,CONCAT(LEFT(u.First_name,1),LEFT(u.Last_name,1))
              FROM dbo.ProofSubmission h JOIN dbo.Subtask s ON s.Subtask_id=h.Subtask_id JOIN dbo.TaskAssignment ta ON ta.Task_id=s.Task_id
              LEFT JOIN dbo.[User] u ON u.User_id=h.Reviewedby_user_id
              WHERE h.Proof_status IN(N'Approved',N'Returned')
              UNION ALL
              SELECT CONCAT('proof:',h.Submission_id),N'Proof submitted',CONCAT(te.First_name,N' ',te.Last_name,N' submitted proof for â€œ',s.Title,N'â€.'),
                N'Action',s.Task_id,h.Submitted_at,N'User',t.Createdby_user_id,te.Profile_photo,
                CONCAT(LEFT(te.First_name,1),LEFT(te.Last_name,1))
              FROM dbo.ProofSubmission h JOIN dbo.Subtask s ON s.Subtask_id=h.Subtask_id JOIN dbo.[Task] t ON t.Task_id=s.Task_id
              OUTER APPLY(SELECT TOP 1 a.Teacher_id FROM dbo.TaskAssignment a WHERE a.Task_id=t.Task_id ORDER BY a.Assignment_id)x
              JOIN dbo.Teacher te ON te.Teacher_id=COALESCE(h.Submittedby_teacher_id,x.Teacher_id) WHERE h.Proof_status=N'Pending'
              UNION ALL
              SELECT CONCAT('ack:',ta.Assignment_id),N'Task acknowledged',CONCAT(te.First_name,N' ',te.Last_name,N' acknowledged â€œ',t.Title,N'â€.'),
                N'Ongoing',t.Task_id,ta.Acknowledged_at,N'User',t.Createdby_user_id,te.Profile_photo,
                CONCAT(LEFT(te.First_name,1),LEFT(te.Last_name,1))
              FROM dbo.TaskAssignment ta JOIN dbo.[Task] t ON t.Task_id=ta.Task_id JOIN dbo.Teacher te ON te.Teacher_id=ta.Teacher_id
              WHERE ta.Is_acknowledged=1 AND ta.Acknowledged_at IS NOT NULL
              UNION ALL
              SELECT CONCAT('completed:',ta.Assignment_id),N'Task completed',CONCAT(N'â€œ',t.Title,N'â€ has been marked complete.'),N'Success',
                t.Task_id,ta.Completed_at,N'Teacher',ta.Teacher_id,NULL,NULL
              FROM dbo.TaskAssignment ta JOIN dbo.[Task] t ON t.Task_id=ta.Task_id
              WHERE ta.Completion_status=N'Completed' AND ta.Completed_at IS NOT NULL
              UNION ALL
              SELECT CONCAT('completed-owner:',t.Task_id),N'Task completed',CONCAT(N'â€œ',t.Title,N'â€ has been marked complete.'),N'Success',
                t.Task_id,ta.Completed_at,N'User',t.Createdby_user_id,NULL,NULL
              FROM dbo.TaskAssignment ta JOIN dbo.[Task] t ON t.Task_id=ta.Task_id
              WHERE ta.Completion_status=N'Completed' AND ta.Completed_at IS NOT NULL
                AND ta.Assignment_id=(SELECT MIN(ta2.Assignment_id) FROM dbo.TaskAssignment ta2
                                      WHERE ta2.Task_id=ta.Task_id AND ta2.Completion_status=N'Completed' AND ta2.Completed_at IS NOT NULL)
                AND (t.Approvedby_user_id IS NULL OR t.Approvedby_user_id<>t.Createdby_user_id)
              UNION ALL
              SELECT CONCAT('deadline:',ta.Assignment_id),CASE WHEN ta.Deadline<CAST(GETDATE()AS date) THEN N'Task overdue' WHEN ta.Deadline=CAST(GETDATE()AS date) THEN N'Deadline today' ELSE N'Deadline approaching' END,
                CONCAT(N'â€œ',t.Title,N'â€ ',CASE WHEN ta.Deadline<CAST(GETDATE()AS date) THEN N'is overdue.' WHEN ta.Deadline=CAST(GETDATE()AS date) THEN N'is due today.' ELSE N'is due tomorrow.' END),N'Urgent',
                t.Task_id,CAST(ta.Deadline AS datetime),N'Teacher',ta.Teacher_id,NULL,NULL
              FROM dbo.TaskAssignment ta JOIN dbo.[Task] t ON t.Task_id=ta.Task_id
              WHERE ta.Completion_status<>N'Completed' AND ta.Deadline<=DATEADD(day,1,CAST(GETDATE()AS date))
              UNION ALL
              SELECT CONCAT('deadline-owner:',t.Task_id),CASE WHEN ta.Deadline<CAST(GETDATE()AS date) THEN N'Task overdue' WHEN ta.Deadline=CAST(GETDATE()AS date) THEN N'Deadline today' ELSE N'Deadline approaching' END,
                CONCAT(N'â€œ',t.Title,N'â€ ',CASE WHEN ta.Deadline<CAST(GETDATE()AS date) THEN N'is overdue.' WHEN ta.Deadline=CAST(GETDATE()AS date) THEN N'is due today.' ELSE N'is due tomorrow.' END),N'Urgent',
                t.Task_id,CAST(ta.Deadline AS datetime),N'User',t.Createdby_user_id,NULL,NULL
              FROM dbo.TaskAssignment ta JOIN dbo.[Task] t ON t.Task_id=ta.Task_id
              WHERE ta.Completion_status<>N'Completed' AND ta.Deadline<=DATEADD(day,1,CAST(GETDATE()AS date))
                AND ta.Assignment_id=(SELECT MIN(ta2.Assignment_id) FROM dbo.TaskAssignment ta2
                                      WHERE ta2.Task_id=ta.Task_id AND ta2.Completion_status<>N'Completed' AND ta2.Deadline<=DATEADD(day,1,CAST(GETDATE()AS date)))
              UNION ALL
              SELECT CONCAT('comment-owner:',c.Discussion_id),N'New discussion message',
                CONCAT(te.First_name,N' ',te.Last_name,N' commented on â€œ',t.Title,N'â€.'),N'Action',c.Task_id,c.Created_at,N'User',t.Createdby_user_id,
                te.Profile_photo,CONCAT(LEFT(te.First_name,1),LEFT(te.Last_name,1))
              FROM dbo.TaskDiscussion c
              JOIN dbo.[Task] t ON t.Task_id=c.Task_id
              JOIN dbo.Teacher te ON c.Sender_type=N'Teacher' AND te.Teacher_id=c.Sender_id
              UNION ALL
              SELECT CONCAT('comment-teacher:',c.Discussion_id,'-',ta.Teacher_id),N'New discussion message',
                CONCAT(u.First_name,N' ',u.Last_name,N' commented on â€œ',t.Title,N'â€.'),N'Action',c.Task_id,c.Created_at,N'Teacher',ta.Teacher_id,
                u.Profile_photo,CONCAT(LEFT(u.First_name,1),LEFT(u.Last_name,1))
              FROM dbo.TaskDiscussion c
              JOIN dbo.[Task] t ON t.Task_id=c.Task_id
              JOIN dbo.TaskAssignment ta ON ta.Task_id=c.Task_id
              JOIN dbo.[User] u ON c.Sender_type=N'User' AND u.User_id=c.Sender_id
            )
            SELECT TOP(100)a.Notification_key,a.Title,a.Message,a.Category,a.Task_id,a.Created_at,a.ActorPhotoPath,a.ActorInitials,
                CASE WHEN ns.Read_at IS NULL THEN 0 ELSE 1 END IsRead,
                CASE WHEN EXISTS(SELECT 1 FROM dbo.TaskAssignment ta_chk WHERE ta_chk.Task_id=a.Task_id AND ta_chk.Completion_status=N'Completed')
                     THEN 1 ELSE 0 END IsTaskCompleted
            FROM Activity a
            LEFT JOIN dbo.NotificationState ns ON ns.Recipient_type=a.Recipient_type AND ns.Recipient_id=a.Recipient_id AND ns.Notification_key=a.Notification_key
            WHERE a.Recipient_type=@Type AND a.Recipient_id=@ID AND ns.Hidden_at IS NULL
            ORDER BY a.Created_at DESC;
            """;
        DataTable table = await database.ExecuteQueryAsync(query,
            [new SqlParameter("@Type",SqlDbType.NVarChar,10){Value=recipientType},new SqlParameter("@ID",SqlDbType.Int){Value=recipientID}],token);
        bool hideViewTaskWhenCompleted = recipientType == "Teacher";
        return table.AsEnumerable().Select(row => new NotificationItem {
            Notification_key=row.Field<string>("Notification_key")??string.Empty,Title=row.Field<string>("Title")??"Task update",
            Message=row.Field<string>("Message")??string.Empty,Category=row.Field<string>("Category")??"Action",
            Task_id=row.IsNull("Task_id")?null:row.Field<int>("Task_id"),Created_at=row.Field<DateTime>("Created_at"),
            ActorPhotoPath=row.Field<string>("ActorPhotoPath"),ActorInitials=row.Field<string>("ActorInitials")??string.Empty,
            IsRead=Convert.ToInt32(row["IsRead"])==1,
            ShowViewTask=!(hideViewTaskWhenCompleted && Convert.ToInt32(row["IsTaskCompleted"])==1)}).ToList();
    }

    public static async System.Threading.Tasks.Task MarkNotificationReadAsync(this DatabaseService database,string type,int id,string key,CancellationToken token=default)
    {
        await EnsureNotificationStateAsync(database,token);
        await database.ExecuteNonQueryAsync(
            """
            MERGE dbo.NotificationState WITH (HOLDLOCK) AS target
            USING (SELECT @T Recipient_type, @I Recipient_id, @K Notification_key) AS source
              ON target.Recipient_type=source.Recipient_type
             AND target.Recipient_id=source.Recipient_id
             AND target.Notification_key=source.Notification_key
            WHEN MATCHED AND target.Read_at IS NULL THEN
                UPDATE SET Read_at=SYSDATETIME()
            WHEN NOT MATCHED THEN
                INSERT (Recipient_type,Recipient_id,Notification_key,Read_at,Hidden_at)
                VALUES (source.Recipient_type,source.Recipient_id,source.Notification_key,SYSDATETIME(),NULL);
            """,
            [new SqlParameter("@T",SqlDbType.NVarChar,10){Value=type},new SqlParameter("@I",SqlDbType.Int){Value=id},new SqlParameter("@K",SqlDbType.NVarChar,100){Value=key}],token);
    }

    public static async System.Threading.Tasks.Task HideNotificationsAsync(
        this DatabaseService database,
        string type,
        int id,
        IEnumerable<string> notificationKeys,
        CancellationToken token = default)
    {
        await EnsureNotificationStateAsync(database, token);
        const string command = """
            MERGE dbo.NotificationState WITH (HOLDLOCK) AS target
            USING (SELECT @T Recipient_type, @I Recipient_id, @K Notification_key) AS source
              ON target.Recipient_type=source.Recipient_type
             AND target.Recipient_id=source.Recipient_id
             AND target.Notification_key=source.Notification_key
            WHEN MATCHED AND target.Hidden_at IS NULL THEN
                UPDATE SET Hidden_at=SYSDATETIME()
            WHEN NOT MATCHED THEN
                INSERT (Recipient_type,Recipient_id,Notification_key,Read_at,Hidden_at)
                VALUES (source.Recipient_type,source.Recipient_id,source.Notification_key,NULL,SYSDATETIME());
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

    internal static async System.Threading.Tasks.Task EnsureNotificationStateAsync(
        DatabaseService database,
        CancellationToken token)
    {
        if (_notificationSchemaEnsured)
            return;

        await NotificationSchemaGate.WaitAsync(token);
        try
        {
            if (_notificationSchemaEnsured)
                return;

            await database.ExecuteNonQueryAsync(
                """
        IF COL_LENGTH(N'dbo.NotificationState',N'NtificationKey') IS NOT NULL
           AND COL_LENGTH(N'dbo.NotificationState',N'Notification_key') IS NULL
            EXEC sys.sp_rename N'dbo.NotificationState.NtificationKey', N'Notification_key', N'COLUMN';

        IF OBJECT_ID(N'dbo.NotificationState',N'U') IS NULL
            CREATE TABLE dbo.NotificationState(
                Recipient_type nvarchar(10) NOT NULL,
                Recipient_id int NOT NULL,
                Notification_key nvarchar(100) NOT NULL,
                Read_at datetime2 NULL,
                Hidden_at datetime2 NULL,
                CONSTRAINT PK_NotificationState PRIMARY KEY(Recipient_type,Recipient_id,Notification_key),
                CONSTRAINT CK_NotificationState_HasState CHECK(Read_at IS NOT NULL OR Hidden_at IS NOT NULL));

        IF OBJECT_ID(N'dbo.NotificationRead',N'U') IS NOT NULL
        BEGIN
            EXEC sys.sp_executesql N'
                MERGE dbo.NotificationState WITH (HOLDLOCK) AS target
                USING dbo.NotificationRead AS source
                  ON target.Recipient_type=source.Recipient_type
                 AND target.Recipient_id=source.Recipient_id
                 AND target.Notification_key=source.Notification_key
                WHEN MATCHED AND target.Read_at IS NULL THEN
                    UPDATE SET Read_at=source.Read_at
                WHEN NOT MATCHED THEN
                    INSERT (Recipient_type,Recipient_id,Notification_key,Read_at,Hidden_at)
                    VALUES (source.Recipient_type,source.Recipient_id,source.Notification_key,source.Read_at,NULL);';

            DROP TABLE dbo.NotificationRead;
        END;

        IF OBJECT_ID(N'dbo.NotificationHidden',N'U') IS NOT NULL
        BEGIN
            EXEC sys.sp_executesql N'
                MERGE dbo.NotificationState WITH (HOLDLOCK) AS target
                USING dbo.NotificationHidden AS source
                  ON target.Recipient_type=source.Recipient_type
                 AND target.Recipient_id=source.Recipient_id
                 AND target.Notification_key=source.Notification_key
                WHEN MATCHED AND target.Hidden_at IS NULL THEN
                    UPDATE SET Hidden_at=source.Hidden_at
                WHEN NOT MATCHED THEN
                    INSERT (Recipient_type,Recipient_id,Notification_key,Read_at,Hidden_at)
                    VALUES (source.Recipient_type,source.Recipient_id,source.Notification_key,NULL,source.Hidden_at);';

            DROP TABLE dbo.NotificationHidden;
        END;
        """,
                cancellationToken: token);
            _notificationSchemaEnsured = true;
        }
        finally
        {
            NotificationSchemaGate.Release();
        }
    }
}
