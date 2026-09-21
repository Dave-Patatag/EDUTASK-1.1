/*
   Final ERD-aligned cleanup.

   Retained security answers stay hashed; only their physical names change.
   Legacy Task ownership/revision fields and the unused activity-log table are
   removed after their useful creator data is preserved.

   Safe to run repeatedly.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @SchemaLockResult int;
    EXEC @SchemaLockResult = sys.sp_getapplock
        @Resource = N'EduTask.CleanConsistentSchema',
        @LockMode = N'Exclusive',
        @LockOwner = N'Transaction',
        @LockTimeout = 15000;
    IF @SchemaLockResult < 0
        THROW 51400, 'The database cleanup could not obtain its schema lock.', 1;

    /* Store the profile image reference under the ERD attribute name. */
    IF COL_LENGTH(N'dbo.[User]', N'Profile_photo') IS NULL
       AND COL_LENGTH(N'dbo.[User]', N'Profile_photo_path') IS NOT NULL
        EXEC sys.sp_rename N'dbo.[User].Profile_photo_path', N'Profile_photo', N'COLUMN';
    IF COL_LENGTH(N'dbo.Teacher', N'Profile_photo') IS NULL
       AND COL_LENGTH(N'dbo.Teacher', N'Profile_photo_path') IS NOT NULL
        EXEC sys.sp_rename N'dbo.Teacher.Profile_photo_path', N'Profile_photo', N'COLUMN';

    IF COL_LENGTH(N'dbo.[User]', N'Profile_photo') IS NOT NULL
       AND COL_LENGTH(N'dbo.[User]', N'Profile_photo_path') IS NOT NULL
        THROW 51403, 'User has both Profile_photo and Profile_photo_path.', 1;
    IF COL_LENGTH(N'dbo.Teacher', N'Profile_photo') IS NOT NULL
       AND COL_LENGTH(N'dbo.Teacher', N'Profile_photo_path') IS NOT NULL
        THROW 51404, 'Teacher has both Profile_photo and Profile_photo_path.', 1;

    /* Keep the entity spelling identical to the ERD. A temporary name is
       required because the database uses a case-insensitive collation. */
    IF EXISTS
    (
        SELECT 1 FROM sys.tables
        WHERE schema_id = SCHEMA_ID(N'dbo')
          AND name COLLATE Latin1_General_100_BIN2 = N'SubTask'
    )
    BEGIN
        EXEC sys.sp_rename N'dbo.SubTask', N'Subtask_rename_stage';
        EXEC sys.sp_rename N'dbo.Subtask_rename_stage', N'Subtask';
    END;

    /* The application records promotions only, so use the smaller and clearer
       Promotion event instead of a generic role-change log. */
    IF OBJECT_ID(N'dbo.AccountRoleChangeLog', N'U') IS NOT NULL
       AND OBJECT_ID(N'dbo.Promotion', N'U') IS NULL
        EXEC sys.sp_rename N'dbo.AccountRoleChangeLog', N'Promotion';
    ELSE IF OBJECT_ID(N'dbo.AccountRoleChangeLog', N'U') IS NOT NULL
        THROW 51402, 'Both AccountRoleChangeLog and Promotion exist.', 1;

    IF OBJECT_ID(N'dbo.Promotion', N'U') IS NOT NULL
    BEGIN
        IF OBJECT_ID(N'dbo.FK_AccountRoleChangeLog_PreviousRole', N'F') IS NOT NULL
            ALTER TABLE dbo.Promotion DROP CONSTRAINT FK_AccountRoleChangeLog_PreviousRole;
        IF OBJECT_ID(N'dbo.FK_AccountRoleChangeLog_NewRole', N'F') IS NOT NULL
            ALTER TABLE dbo.Promotion DROP CONSTRAINT FK_AccountRoleChangeLog_NewRole;

        IF COL_LENGTH(N'dbo.Promotion', N'Previous_role_id') IS NOT NULL
            ALTER TABLE dbo.Promotion DROP COLUMN Previous_role_id;
        IF COL_LENGTH(N'dbo.Promotion', N'New_role_id') IS NOT NULL
            ALTER TABLE dbo.Promotion DROP COLUMN New_role_id;

        IF COL_LENGTH(N'dbo.Promotion', N'Promotion_id') IS NULL
           AND COL_LENGTH(N'dbo.Promotion', N'Role_change_id') IS NOT NULL
            EXEC sys.sp_rename N'dbo.Promotion.Role_change_id', N'Promotion_id', N'COLUMN';
        IF COL_LENGTH(N'dbo.Promotion', N'Promoted_user_id') IS NULL
           AND COL_LENGTH(N'dbo.Promotion', N'User_id') IS NOT NULL
            EXEC sys.sp_rename N'dbo.Promotion.User_id', N'Promoted_user_id', N'COLUMN';
        IF COL_LENGTH(N'dbo.Promotion', N'Promotedby_user_id') IS NULL
           AND COL_LENGTH(N'dbo.Promotion', N'Changedby_user_id') IS NOT NULL
            EXEC sys.sp_rename N'dbo.Promotion.Changedby_user_id', N'Promotedby_user_id', N'COLUMN';
        IF COL_LENGTH(N'dbo.Promotion', N'Promoted_at') IS NULL
           AND COL_LENGTH(N'dbo.Promotion', N'Changed_at') IS NOT NULL
            EXEC sys.sp_rename N'dbo.Promotion.Changed_at', N'Promoted_at', N'COLUMN';

        IF OBJECT_ID(N'dbo.PK_AccountRoleChangeLog', N'PK') IS NOT NULL
            EXEC sys.sp_rename N'dbo.PK_AccountRoleChangeLog', N'PK_Promotion', N'OBJECT';
        IF OBJECT_ID(N'dbo.FK_AccountRoleChangeLog_User', N'F') IS NOT NULL
            EXEC sys.sp_rename N'dbo.FK_AccountRoleChangeLog_User', N'FK_Promotion_PromotedUser', N'OBJECT';
        IF OBJECT_ID(N'dbo.FK_AccountRoleChangeLog_ChangedBy', N'F') IS NOT NULL
            EXEC sys.sp_rename N'dbo.FK_AccountRoleChangeLog_ChangedBy', N'FK_Promotion_PromotedBy', N'OBJECT';
        IF OBJECT_ID(N'dbo.DF_AccountRoleChangeLog_ChangedAt', N'D') IS NOT NULL
            EXEC sys.sp_rename N'dbo.DF_AccountRoleChangeLog_ChangedAt', N'DF_Promotion_PromotedAt', N'OBJECT';
    END;

    /* Numbered security attributes: match the ERD exactly. */
    IF COL_LENGTH(N'dbo.User', N'Security_question_1') IS NULL
       AND COL_LENGTH(N'dbo.User', N'Security_question1') IS NOT NULL
        EXEC sys.sp_rename N'dbo.[User].Security_question1', N'Security_question_1', N'COLUMN';
    IF COL_LENGTH(N'dbo.User', N'Security_answer_1') IS NULL
       AND COL_LENGTH(N'dbo.User', N'Security_answer_hash1') IS NOT NULL
        EXEC sys.sp_rename N'dbo.[User].Security_answer_hash1', N'Security_answer_1', N'COLUMN';
    IF COL_LENGTH(N'dbo.User', N'Security_question_2') IS NULL
       AND COL_LENGTH(N'dbo.User', N'Security_question2') IS NOT NULL
        EXEC sys.sp_rename N'dbo.[User].Security_question2', N'Security_question_2', N'COLUMN';
    IF COL_LENGTH(N'dbo.User', N'Security_answer_2') IS NULL
       AND COL_LENGTH(N'dbo.User', N'Security_answer_hash2') IS NOT NULL
        EXEC sys.sp_rename N'dbo.[User].Security_answer_hash2', N'Security_answer_2', N'COLUMN';

    IF COL_LENGTH(N'dbo.Teacher', N'Security_question_1') IS NULL
       AND COL_LENGTH(N'dbo.Teacher', N'Security_question1') IS NOT NULL
        EXEC sys.sp_rename N'dbo.Teacher.Security_question1', N'Security_question_1', N'COLUMN';
    IF COL_LENGTH(N'dbo.Teacher', N'Security_answer_1') IS NULL
       AND COL_LENGTH(N'dbo.Teacher', N'Security_answer_hash1') IS NOT NULL
        EXEC sys.sp_rename N'dbo.Teacher.Security_answer_hash1', N'Security_answer_1', N'COLUMN';
    IF COL_LENGTH(N'dbo.Teacher', N'Security_question_2') IS NULL
       AND COL_LENGTH(N'dbo.Teacher', N'Security_question2') IS NOT NULL
        EXEC sys.sp_rename N'dbo.Teacher.Security_question2', N'Security_question_2', N'COLUMN';
    IF COL_LENGTH(N'dbo.Teacher', N'Security_answer_2') IS NULL
       AND COL_LENGTH(N'dbo.Teacher', N'Security_answer_hash2') IS NOT NULL
        EXEC sys.sp_rename N'dbo.Teacher.Security_answer_hash2', N'Security_answer_2', N'COLUMN';

    /* The ERD calls the final approver Approvedby_user_id. */
    IF COL_LENGTH(N'dbo.Task', N'Approvedby_user_id') IS NULL
       AND COL_LENGTH(N'dbo.Task', N'Completion_approvedby_user_id') IS NOT NULL
        EXEC sys.sp_rename N'dbo.Task.Completion_approvedby_user_id', N'Approvedby_user_id', N'COLUMN';

    IF OBJECT_ID(N'dbo.FK_Task_CompletionApprovedByUser', N'F') IS NOT NULL
       AND OBJECT_ID(N'dbo.FK_Task_ApprovedByUser', N'F') IS NULL
        EXEC sys.sp_rename N'dbo.FK_Task_CompletionApprovedByUser', N'FK_Task_ApprovedByUser', N'OBJECT';
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Task') AND name = N'IX_Task_CompletionApprovedByUserID')
       AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Task') AND name = N'IX_Task_ApprovedByUserID')
        EXEC sys.sp_rename N'dbo.Task.IX_Task_CompletionApprovedByUserID', N'IX_Task_ApprovedByUserID', N'INDEX';

    /* Preserve the creator before removing the original duplicate key. */
    IF COL_LENGTH(N'dbo.Task', N'User_id') IS NOT NULL
        EXEC sys.sp_executesql N'
            UPDATE dbo.[Task]
            SET Createdby_user_id = User_id
            WHERE Createdby_user_id IS NULL;';

    IF EXISTS (SELECT 1 FROM dbo.[Task] WHERE Createdby_user_id IS NULL)
        THROW 51401, 'A task has no creator, so its legacy creator column cannot be removed safely.', 1;

    IF EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.Task')
          AND name = N'IX_Task_CreatedByUserID'
    )
        DROP INDEX IX_Task_CreatedByUserID ON dbo.[Task];

    IF EXISTS
    (
        SELECT 1 FROM sys.columns
        WHERE object_id = OBJECT_ID(N'dbo.Task')
          AND name = N'Createdby_user_id'
          AND is_nullable = 1
    )
        ALTER TABLE dbo.[Task] ALTER COLUMN Createdby_user_id int NOT NULL;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.Task')
          AND name = N'IX_Task_CreatedByUserID'
    )
        CREATE INDEX IX_Task_CreatedByUserID ON dbo.[Task](Createdby_user_id);

    DECLARE @ObjectName sysname;
    DECLARE @Sql nvarchar(max);

    DECLARE ConstraintCursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT DISTINCT dependency.name
        FROM
        (
            SELECT fk.name
            FROM sys.foreign_keys fk
            INNER JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
            WHERE fk.parent_object_id = OBJECT_ID(N'dbo.Task')
              AND COL_NAME(fkc.parent_object_id, fkc.parent_column_id) IN
                  (N'User_id', N'Lastmodifiedby_user_id', N'Completion_approved_at',
                   N'Revision_requestedby_user_id', N'Revision_requested_at', N'Revision_reason')
            UNION
            SELECT dc.name
            FROM sys.default_constraints dc
            INNER JOIN sys.columns c ON c.object_id = dc.parent_object_id
                AND c.column_id = dc.parent_column_id
            WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Task')
              AND c.name IN
                  (N'User_id', N'Lastmodifiedby_user_id', N'Completion_approved_at',
                   N'Revision_requestedby_user_id', N'Revision_requested_at', N'Revision_reason')
            UNION
            SELECT cc.name
            FROM sys.check_constraints cc
            WHERE cc.parent_object_id = OBJECT_ID(N'dbo.Task')
              AND
              (
                  cc.definition LIKE N'%User_id%'
                  OR cc.definition LIKE N'%Lastmodifiedby_user_id%'
                  OR cc.definition LIKE N'%Completion_approved_at%'
                  OR cc.definition LIKE N'%Revision_requested%'
                  OR cc.definition LIKE N'%Revision_reason%'
              )
        ) dependency;

    OPEN ConstraintCursor;
    FETCH NEXT FROM ConstraintCursor INTO @ObjectName;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @Sql = N'ALTER TABLE dbo.[Task] DROP CONSTRAINT ' + QUOTENAME(@ObjectName) + N';';
        EXEC sys.sp_executesql @Sql;
        FETCH NEXT FROM ConstraintCursor INTO @ObjectName;
    END;
    CLOSE ConstraintCursor;
    DEALLOCATE ConstraintCursor;

    DECLARE IndexCursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT DISTINCT i.name
        FROM sys.indexes i
        INNER JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
        INNER JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
        WHERE i.object_id = OBJECT_ID(N'dbo.Task')
          AND i.is_primary_key = 0
          AND i.is_unique_constraint = 0
          AND c.name IN
              (N'User_id', N'Lastmodifiedby_user_id', N'Completion_approved_at',
               N'Revision_requestedby_user_id', N'Revision_requested_at', N'Revision_reason');
    OPEN IndexCursor;
    FETCH NEXT FROM IndexCursor INTO @ObjectName;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @Sql = N'DROP INDEX ' + QUOTENAME(@ObjectName) + N' ON dbo.[Task];';
        EXEC sys.sp_executesql @Sql;
        FETCH NEXT FROM IndexCursor INTO @ObjectName;
    END;
    CLOSE IndexCursor;
    DEALLOCATE IndexCursor;

    DECLARE @ColumnName sysname;
    DECLARE ColumnCursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT name
        FROM sys.columns
        WHERE object_id = OBJECT_ID(N'dbo.Task')
          AND name IN
              (N'User_id', N'Lastmodifiedby_user_id', N'Completion_approved_at',
               N'Revision_requestedby_user_id', N'Revision_requested_at', N'Revision_reason');
    OPEN ColumnCursor;
    FETCH NEXT FROM ColumnCursor INTO @ColumnName;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @Sql = N'ALTER TABLE dbo.[Task] DROP COLUMN ' + QUOTENAME(@ColumnName) + N';';
        EXEC sys.sp_executesql @Sql;
        FETCH NEXT FROM ColumnCursor INTO @ColumnName;
    END;
    CLOSE ColumnCursor;
    DEALLOCATE ColumnCursor;

    /* These attributes are not read or written by the current application. */
    IF COL_LENGTH(N'dbo.User', N'Birthdate') IS NOT NULL
        ALTER TABLE dbo.[User] DROP COLUMN Birthdate;
    IF COL_LENGTH(N'dbo.Teacher', N'Birthdate') IS NOT NULL
        ALTER TABLE dbo.Teacher DROP COLUMN Birthdate;
    IF COL_LENGTH(N'dbo.User', N'Bio') IS NOT NULL
        ALTER TABLE dbo.[User] DROP COLUMN Bio;
    IF COL_LENGTH(N'dbo.Teacher', N'Bio') IS NOT NULL
        ALTER TABLE dbo.Teacher DROP COLUMN Bio;
    IF COL_LENGTH(N'dbo.User', N'Position') IS NOT NULL
        ALTER TABLE dbo.[User] DROP COLUMN Position;
    IF COL_LENGTH(N'dbo.Teacher', N'Position') IS NOT NULL
        ALTER TABLE dbo.Teacher DROP COLUMN Position;

    DROP TABLE IF EXISTS dbo.TaskActivityLog;

    EXEC sys.sp_executesql N'
        CREATE OR ALTER PROCEDURE dbo.ApproveTaskCompletion
            @Task_id int,
            @Acting_user_id int
        AS
        BEGIN
            SET NOCOUNT ON; SET XACT_ABORT ON;
            EXEC dbo.AssertActiveUserRole @Acting_user_id, @AllowDirector = 1;
            BEGIN TRANSACTION;
            IF NOT EXISTS
                (SELECT 1 FROM dbo.TaskAssignment WITH (UPDLOCK, HOLDLOCK) WHERE Task_id = @Task_id)
                THROW 51110, ''The task does not exist or has no assignment.'', 1;
            IF EXISTS
                (SELECT 1 FROM dbo.TaskAssignment WHERE Task_id = @Task_id AND Completion_status <> N''For Validation'')
                THROW 51111, ''Every assignment must be For Validation before final approval.'', 1;
            IF EXISTS
            (
                SELECT 1
                FROM dbo.Subtask s
                OUTER APPLY
                (
                    SELECT TOP (1) h.Proof_status
                    FROM dbo.ProofSubmission h
                    WHERE h.Subtask_id = s.Subtask_id
                    ORDER BY h.Attempt_number DESC, h.Submission_id DESC
                ) latest
                WHERE s.Task_id = @Task_id
                  AND ISNULL(latest.Proof_status, N'''') <> N''Approved''
            ) THROW 51112, ''Every subtask must have approved proof before final completion.'', 1;
            UPDATE dbo.TaskAssignment
            SET Completion_status = N''Completed'', Completed_at = GETDATE()
            WHERE Task_id = @Task_id;
            UPDATE dbo.[Task]
            SET Approvedby_user_id = @Acting_user_id
            WHERE Task_id = @Task_id;
            COMMIT TRANSACTION;
        END;';

    EXEC sys.sp_executesql N'
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
                WHERE Task_id = @Task_id AND Completion_status = N''For Validation''
            ) THROW 51121, ''Only a task that is For Validation can be returned for revision.'', 1;
            UPDATE dbo.TaskAssignment
            SET Completion_status = N''Needs Revision'', Completed_at = NULL
            WHERE Task_id = @Task_id;
            COMMIT TRANSACTION;
        END;';

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF CURSOR_STATUS('local', 'ConstraintCursor') >= 0 CLOSE ConstraintCursor;
    IF CURSOR_STATUS('local', 'ConstraintCursor') >= -1 DEALLOCATE ConstraintCursor;
    IF CURSOR_STATUS('local', 'IndexCursor') >= 0 CLOSE IndexCursor;
    IF CURSOR_STATUS('local', 'IndexCursor') >= -1 DEALLOCATE IndexCursor;
    IF CURSOR_STATUS('local', 'ColumnCursor') >= 0 CLOSE ColumnCursor;
    IF CURSOR_STATUS('local', 'ColumnCursor') >= -1 DEALLOCATE ColumnCursor;
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
