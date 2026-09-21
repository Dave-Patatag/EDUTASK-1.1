/* =====================================================================
   EduTaskDB â€” health check + bring-up-to-date
   Run against: EduTaskDB   ((localdb)\MSSQLLocalDB)

   Safe to run repeatedly. Every change is guarded, so re-running it
   does nothing the second time. It does not touch any of your data.
   ===================================================================== */

USE EduTaskDB;
GO

SET NOCOUNT ON;

/* ---------------------------------------------------------------------
   PART 1 â€” Report. Read-only; changes nothing.
   --------------------------------------------------------------------- */
PRINT '=== Missing tables (should be empty) ===';
SELECT expected.name AS MissingTable
FROM (VALUES
    ('Roles'),('User'),('Teacher'),('Task'),('TaskAssignment'),
    ('Subtask'),('ProofSubmission'),('ProofAttachment'),
    ('TaskDiscussion'),('TaskDiscussionRead'),
    ('Promotion'),('NotificationState')
) AS expected(name)
WHERE OBJECT_ID(N'dbo.' + QUOTENAME(expected.name), N'U') IS NULL;

PRINT '=== Missing columns (should be empty) ===';
SELECT expected.tbl + '.' + expected.col AS MissingColumn
FROM (VALUES
    ('User','Role_id'),('User','Username'),
    ('User','Profile_photo'),('User','Is_active'),
    ('User','Security_question_1'),('User','Security_answer_1'),
    ('User','Security_question_2'),('User','Security_answer_2'),
    ('Teacher','Role_id'),('Teacher','Username'),
    ('Teacher','Profile_photo'),('Teacher','Is_active'),
    ('Teacher','Security_question_1'),('Teacher','Security_answer_1'),
    ('Teacher','Security_question_2'),('Teacher','Security_answer_2'),
    ('Task','Createdby_user_id'),('Task','Approvedby_user_id'),
    ('Task','Updated_at'),
    ('Subtask','Subtask_id'),('Subtask','Task_id'),('Subtask','Title'),
    ('ProofSubmission','Submission_id'),('ProofSubmission','Subtask_id'),
    ('ProofSubmission','Attempt_number'),('ProofSubmission','Proof_status'),
    ('ProofSubmission','Submitted_at'),('ProofSubmission','Submittedby_teacher_id'),
    ('ProofSubmission','Reviewed_at'),('ProofSubmission','Reviewedby_user_id'),
    ('ProofSubmission','Return_remarks'),
    ('ProofAttachment','Submission_id'),('ProofAttachment','Sort_order'),
    ('ProofAttachment','File_name'),
    ('ProofAttachment','File_type'),('ProofAttachment','Proof_file'),
    ('TaskDiscussion','Sender_id'),('TaskDiscussion','Sender_type'),
    ('TaskDiscussion','Message_text'),
    ('Promotion','Promotion_id'),('Promotion','Promoted_user_id'),
    ('Promotion','Promotedby_user_id'),('Promotion','Promoted_at'),
    ('NotificationState','Recipient_type'),('NotificationState','Recipient_id'),
    ('NotificationState','Notification_key'),('NotificationState','Read_at'),
    ('NotificationState','Hidden_at')
) AS expected(tbl,col)
WHERE COL_LENGTH(N'dbo.' + QUOTENAME(expected.tbl), expected.col) IS NULL;

PRINT '=== Missing indexes (should be empty) ===';
SELECT expected.name AS MissingIndex
FROM (VALUES
    ('IX_User_RoleID'),('IX_Teacher_RoleID'),('IX_Task_CreatedByUserID'),
    ('IX_Task_ApprovedByUserID'),('IX_TaskAssignment_Task_Teacher'),
    ('IX_TaskDiscussion_Subtask')
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
    (SELECT COUNT(*) FROM dbo.[User]  WHERE Role_id IS NULL)          AS UsersWithoutRole,
    (SELECT COUNT(*) FROM dbo.Teacher WHERE Role_id IS NULL)          AS TeachersWithoutRole,
    (SELECT COUNT(*) FROM dbo.[Task]  WHERE Createdby_user_id IS NULL) AS TasksWithoutCreator;

PRINT '=== Completion_status values in use (must all be in the allowed set) ===';
SELECT Completion_status, COUNT(*) AS AssignmentCount
FROM dbo.TaskAssignment
GROUP BY Completion_status
ORDER BY Completion_status;
GO

/* ---------------------------------------------------------------------
   PART 2 â€” The only change this script makes.

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
    PRINT 'dbo.ReopenTask already absent â€” nothing to do.';
GO

PRINT '=== Done. ===';
GO
