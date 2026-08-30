/* =====================================================================
   EduTaskDB — health check + bring-up-to-date
   Run against: EduTaskDB   ((localdb)\MSSQLLocalDB)

   Safe to run repeatedly. Every change is guarded, so re-running it
   does nothing the second time. It does not touch any of your data.
   ===================================================================== */

USE EduTaskDB;
GO

SET NOCOUNT ON;

/* ---------------------------------------------------------------------
   PART 1 — Report. Read-only; changes nothing.
   --------------------------------------------------------------------- */
PRINT '=== Missing tables (should be empty) ===';
SELECT expected.name AS MissingTable
FROM (VALUES
    ('Roles'),('User'),('Teacher'),('Task'),('TaskAssignment'),
    ('SubTask'),('SubtaskProof'),('SubtaskProofHistory'),
    ('TaskComment'),('TaskCommentRead'),('TaskActivityLog'),
    ('AccountRoleChangeLog'),('NotificationRead'),('NotificationHidden')
) AS expected(name)
WHERE OBJECT_ID(N'dbo.' + QUOTENAME(expected.name), N'U') IS NULL;

PRINT '=== Missing columns (should be empty) ===';
SELECT expected.tbl + '.' + expected.col AS MissingColumn
FROM (VALUES
    ('User','RoleID'),('User','Username'),
    ('User','ProfilePhotoPath'),('User','IsActive'),
    ('Teacher','RoleID'),('Teacher','Username'),
    ('Teacher','ProfilePhotoPath'),('Teacher','IsActive'),
    ('Task','CreatedByUserID'),('Task','LastModifiedByUserID'),
    ('Task','CompletionApprovedByUserID'),('Task','CompletionApprovedAt'),
    ('Task','RevisionRequestedByUserID'),('Task','RevisionRequestedAt'),
    ('Task','RevisionReason')
) AS expected(tbl,col)
WHERE COL_LENGTH(N'dbo.' + QUOTENAME(expected.tbl), expected.col) IS NULL;

PRINT '=== Missing indexes (should be empty) ===';
SELECT expected.name AS MissingIndex
FROM (VALUES
    ('IX_User_RoleID'),('IX_Teacher_RoleID'),('IX_Task_CreatedByUserID'),
    ('IX_Task_CompletionApprovedByUserID'),('IX_TaskAssignment_Task_Teacher'),
    ('IX_TaskComment_Task_Subtask'),('IX_TaskActivityLog_Task_CreatedAt'),
    ('IX_TaskActivityLog_User_CreatedAt'),('IX_TaskActivityLog_Teacher_CreatedAt')
) AS expected(name)
WHERE NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = expected.name);

PRINT '=== Stored procedures the app calls (all three must be present) ===';
SELECT expected.name AS ProcedureName,
       CASE WHEN OBJECT_ID(N'dbo.' + expected.name, N'P') IS NULL
            THEN 'MISSING' ELSE 'ok' END AS Status
FROM (VALUES
    ('AssertActiveUserRole'),('ApproveTaskCompletion'),('RequestTaskRevision')
) AS expected(name);

PRINT '=== Data health ===';
SELECT
    (SELECT COUNT(*) FROM dbo.[User]  WHERE RoleID IS NULL)          AS UsersWithoutRole,
    (SELECT COUNT(*) FROM dbo.Teacher WHERE RoleID IS NULL)          AS TeachersWithoutRole,
    (SELECT COUNT(*) FROM dbo.[Task]  WHERE CreatedByUserID IS NULL) AS TasksWithoutCreator;

PRINT '=== CompletionStatus values in use (must all be in the allowed set) ===';
SELECT CompletionStatus, COUNT(*) AS AssignmentCount
FROM dbo.TaskAssignment
GROUP BY CompletionStatus
ORDER BY CompletionStatus;
GO

/* ---------------------------------------------------------------------
   PART 2 — The only change this script makes.

   dbo.ReopenTask is not called from anywhere in the app. It has been
   removed from MigrateRolesAndAuthorization.sql; this drops the copy
   still sitting in the database.
   --------------------------------------------------------------------- */
IF OBJECT_ID(N'dbo.ReopenTask', N'P') IS NOT NULL
BEGIN
    DROP PROCEDURE dbo.ReopenTask;
    PRINT 'Dropped unused procedure dbo.ReopenTask.';
END
ELSE
    PRINT 'dbo.ReopenTask already absent — nothing to do.';
GO

/* ---------------------------------------------------------------------
   PART 3 — OPTIONAL. Left commented out on purpose.

   Backfills the creator on older tasks that predate the CreatedByUserID
   column, copying the owning UserID into it.

   You do not need this. Every query in the app already reads
   COALESCE(CreatedByUserID, UserID), so these rows behave correctly as
   they are. Note also that new tasks are inserted WITHOUT setting
   CreatedByUserID, so this will not stay at zero — running it is
   cosmetic unless the insert in DatabaseService.cs is changed too.

   UPDATE dbo.[Task] SET CreatedByUserID = UserID WHERE CreatedByUserID IS NULL;
   --------------------------------------------------------------------- */

PRINT '=== Done. ===';
GO
