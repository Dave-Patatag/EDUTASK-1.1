/* Removes redundant Task ownership/review columns, the derived Subtask
   completion flag, and current-proof review metadata duplicated by
   ProofSubmission. Task.Updated_at is retained for meaningful edit
   tracking. Safe to run repeatedly. */
USE EduTaskDB;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

CREATE OR ALTER PROCEDURE dbo.ApproveTaskCompletion
    @Task_id int,
    @Acting_user_id int
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    EXEC dbo.AssertActiveUserRole @Acting_user_id, @AllowDirector = 1;
    BEGIN TRANSACTION;
    DECLARE @Previous_status nvarchar(50);
    SELECT TOP (1) @Previous_status = Completion_status
    FROM dbo.TaskAssignment WITH (UPDLOCK, HOLDLOCK)
    WHERE Task_id = @Task_id;
    IF @Previous_status IS NULL THROW 51110, 'The task does not exist or has no assignment.', 1;
    IF EXISTS (SELECT 1 FROM dbo.TaskAssignment WHERE Task_id = @Task_id AND Completion_status <> N'For Validation')
        THROW 51111, 'Every assignment must be For Validation before final approval.', 1;
    IF EXISTS
    (
        SELECT 1 FROM dbo.Subtask s
        WHERE s.Task_id = @Task_id
          AND (s.Proof_validation_status IS NULL OR s.Proof_validation_status <> N'Approved')
    ) THROW 51112, 'Every subtask must have approved proof before final completion.', 1;

    UPDATE dbo.TaskAssignment
    SET Completion_status = N'Completed', Completed_at = GETDATE()
    WHERE Task_id = @Task_id;
    UPDATE dbo.[Task]
    SET Completion_approvedby_user_id = @Acting_user_id
    WHERE Task_id = @Task_id;
    COMMIT TRANSACTION;
END;
GO

CREATE OR ALTER PROCEDURE dbo.RequestTaskRevision
    @Task_id int,
    @Acting_user_id int
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    EXEC dbo.AssertActiveUserRole @Acting_user_id, @AllowDirector = 1, @AllowStaff = 1;
    BEGIN TRANSACTION;
    IF NOT EXISTS
    (
        SELECT 1 FROM dbo.TaskAssignment WITH (UPDLOCK, HOLDLOCK)
        WHERE Task_id = @Task_id AND Completion_status = N'For Validation'
    ) THROW 51121, 'Only a task that is For Validation can be returned for revision.', 1;
    UPDATE dbo.TaskAssignment
    SET Completion_status = N'Needs Revision', Completed_at = NULL
    WHERE Task_id = @Task_id;
    COMMIT TRANSACTION;
END;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    IF COL_LENGTH(N'dbo.Task', N'User_id') IS NOT NULL
        EXEC sys.sp_executesql N'
            UPDATE dbo.[Task]
            SET Createdby_user_id = User_id
            WHERE Createdby_user_id IS NULL;';

    IF EXISTS (SELECT 1 FROM dbo.[Task] WHERE Createdby_user_id IS NULL)
        THROW 51002, 'One or more tasks do not have a creator.', 1;

    IF EXISTS
    (
        SELECT 1 FROM sys.columns
        WHERE object_id = OBJECT_ID(N'dbo.Task')
          AND name = N'Createdby_user_id'
          AND is_nullable = 1
    )
        ALTER TABLE dbo.[Task] ALTER COLUMN Createdby_user_id int NOT NULL;

    DECLARE @ConstraintName sysname;
    DECLARE @TableName sysname;
    DECLARE @DropConstraint nvarchar(max);

    DECLARE ConstraintCursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT DISTINCT OBJECT_NAME(fk.parent_object_id), fk.name
        FROM sys.foreign_keys fk
        INNER JOIN sys.foreign_key_columns fkc
            ON fkc.constraint_object_id = fk.object_id
        WHERE
            (fk.parent_object_id = OBJECT_ID(N'dbo.Task')
             AND COL_NAME(fkc.parent_object_id, fkc.parent_column_id) IN
                 (N'User_id', N'Lastmodifiedby_user_id', N'Revision_requestedby_user_id'))
            OR
            (fk.parent_object_id = OBJECT_ID(N'dbo.Subtask')
             AND COL_NAME(fkc.parent_object_id, fkc.parent_column_id) = N'Proof_reviewedby_user_id');

    OPEN ConstraintCursor;
    FETCH NEXT FROM ConstraintCursor INTO @TableName, @ConstraintName;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @DropConstraint = N'ALTER TABLE dbo.' + QUOTENAME(@TableName)
            + N' DROP CONSTRAINT ' + QUOTENAME(@ConstraintName) + N';';
        EXEC sys.sp_executesql @DropConstraint;
        FETCH NEXT FROM ConstraintCursor INTO @TableName, @ConstraintName;
    END;
    CLOSE ConstraintCursor;
    DEALLOCATE ConstraintCursor;

    DECLARE DefaultCursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT OBJECT_NAME(dc.parent_object_id), dc.name
        FROM sys.default_constraints dc
        INNER JOIN sys.columns c
            ON c.object_id = dc.parent_object_id
            AND c.column_id = dc.parent_column_id
        WHERE
            (c.object_id = OBJECT_ID(N'dbo.Task') AND c.name = N'Updated_at')
            OR (c.object_id = OBJECT_ID(N'dbo.Subtask')
                AND c.name IN (N'Created_at', N'Is_completed'));

    OPEN DefaultCursor;
    FETCH NEXT FROM DefaultCursor INTO @TableName, @ConstraintName;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @DropConstraint = N'ALTER TABLE dbo.' + QUOTENAME(@TableName)
            + N' DROP CONSTRAINT ' + QUOTENAME(@ConstraintName) + N';';
        EXEC sys.sp_executesql @DropConstraint;
        FETCH NEXT FROM DefaultCursor INTO @TableName, @ConstraintName;
    END;
    CLOSE DefaultCursor;
    DEALLOCATE DefaultCursor;

    IF COL_LENGTH(N'dbo.Task', N'User_id') IS NOT NULL
        ALTER TABLE dbo.[Task] DROP COLUMN User_id;
    IF COL_LENGTH(N'dbo.Task', N'Lastmodifiedby_user_id') IS NOT NULL
        ALTER TABLE dbo.[Task] DROP COLUMN Lastmodifiedby_user_id;
    IF COL_LENGTH(N'dbo.Task', N'Completion_approved_at') IS NOT NULL
        ALTER TABLE dbo.[Task] DROP COLUMN Completion_approved_at;
    IF COL_LENGTH(N'dbo.Task', N'Revision_requestedby_user_id') IS NOT NULL
        ALTER TABLE dbo.[Task] DROP COLUMN Revision_requestedby_user_id;
    IF COL_LENGTH(N'dbo.Task', N'Revision_requested_at') IS NOT NULL
        ALTER TABLE dbo.[Task] DROP COLUMN Revision_requested_at;
    IF COL_LENGTH(N'dbo.Task', N'Revision_reason') IS NOT NULL
        ALTER TABLE dbo.[Task] DROP COLUMN Revision_reason;

    IF COL_LENGTH(N'dbo.Subtask', N'Created_at') IS NOT NULL
        ALTER TABLE dbo.Subtask DROP COLUMN Created_at;
    IF COL_LENGTH(N'dbo.Subtask', N'Is_completed') IS NOT NULL
        ALTER TABLE dbo.Subtask DROP COLUMN Is_completed;
    IF COL_LENGTH(N'dbo.Subtask', N'Completed_at') IS NOT NULL
        ALTER TABLE dbo.Subtask DROP COLUMN Completed_at;
    IF COL_LENGTH(N'dbo.Subtask', N'Proof_reviewed_at') IS NOT NULL
        ALTER TABLE dbo.Subtask DROP COLUMN Proof_reviewed_at;
    IF COL_LENGTH(N'dbo.Subtask', N'Proof_reviewedby_user_id') IS NOT NULL
        ALTER TABLE dbo.Subtask DROP COLUMN Proof_reviewedby_user_id;
    IF COL_LENGTH(N'dbo.Subtask', N'Proof_admin_remarks') IS NOT NULL
        ALTER TABLE dbo.Subtask DROP COLUMN Proof_admin_remarks;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF CURSOR_STATUS('local', 'ConstraintCursor') >= 0 CLOSE ConstraintCursor;
    IF CURSOR_STATUS('local', 'ConstraintCursor') >= -1 DEALLOCATE ConstraintCursor;
    IF CURSOR_STATUS('local', 'DefaultCursor') >= 0 CLOSE DefaultCursor;
    IF CURSOR_STATUS('local', 'DefaultCursor') >= -1 DEALLOCATE DefaultCursor;
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

