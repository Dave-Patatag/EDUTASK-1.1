SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    -- MigrateRolesAndAuthorization.sql intended to replace CK_TaskAssignment_CompletionStatus
    -- with a version that allows 'Needs Revision', but its guard (IF OBJECT_ID(...) IS NULL)
    -- found a constraint of that name already present from the original base schema (allowing
    -- the legacy 'Returned'/'In Progress' values instead) and silently skipped replacing it.
    -- That left dbo.RequestTaskRevision / dbo.ReopenTask writing a status the live constraint
    -- rejects. Normalize any lingering legacy values, then drop and recreate the constraint.
    UPDATE dbo.TaskAssignment SET Completion_status = N'Needs Revision' WHERE Completion_status = N'Returned';
    UPDATE dbo.TaskAssignment SET Completion_status = N'Pending' WHERE Completion_status = N'In Progress';

    IF OBJECT_ID(N'dbo.CK_TaskAssignment_CompletionStatus', N'C') IS NOT NULL
        ALTER TABLE dbo.TaskAssignment DROP CONSTRAINT CK_TaskAssignment_CompletionStatus;

    ALTER TABLE dbo.TaskAssignment WITH CHECK ADD CONSTRAINT CK_TaskAssignment_CompletionStatus
        CHECK (Completion_status IN (N'Pending', N'Acknowledged', N'For Validation', N'Needs Revision', N'Completed'));

    -- Email is uniquely constrained on both tables already; Username was not.
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[User]') AND name = N'UQ_User_Username')
        CREATE UNIQUE INDEX UQ_User_Username ON dbo.[User](Username);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Teacher') AND name = N'UQ_Teacher_Username')
        CREATE UNIQUE INDEX UQ_Teacher_Username ON dbo.Teacher(Username);

    -- Supporting indexes for columns filtered/joined on every dashboard, inbox, and discussion query.
    IF COL_LENGTH(N'dbo.TaskDiscussion', N'Task_id') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.TaskDiscussion') AND name = N'IX_TaskDiscussion_Task_Subtask')
        CREATE INDEX IX_TaskDiscussion_Task_Subtask ON dbo.TaskDiscussion(Task_id, Subtask_id);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.TaskAssignment') AND name = N'IX_TaskAssignment_Task_Teacher')
        CREATE INDEX IX_TaskAssignment_Task_Teacher ON dbo.TaskAssignment(Task_id, Teacher_id);

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

/* Validation: constraint should list Needs Revision, no rows should be blocked. */
SELECT definition FROM sys.check_constraints WHERE name = N'CK_TaskAssignment_CompletionStatus';
SELECT name FROM sys.indexes WHERE name IN (N'UQ_User_Username', N'UQ_Teacher_Username', N'IX_TaskDiscussion_Task_Subtask', N'IX_TaskAssignment_Task_Teacher');
