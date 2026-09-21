-- Run against EduTaskDB. Also embedded and run by the app before discussion access.
-- Rename in place to preserve identity values, messages, and read receipts.
SET XACT_ABORT ON;
BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @LockResult int;
    EXEC @LockResult = sys.sp_getapplock @Resource = N'EduTask.TaskDiscussion.Schema',
        @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = 15000;
    IF @LockResult < 0
        THROW 50001, 'Could not lock the discussion schema for upgrade.', 1;

    IF OBJECT_ID(N'dbo.TaskComment', N'U') IS NOT NULL
    BEGIN
        IF OBJECT_ID(N'dbo.TaskDiscussion', N'U') IS NOT NULL
            THROW 50002, 'Both TaskComment and TaskDiscussion exist; reconcile them before upgrading.', 1;
        EXEC sys.sp_rename N'dbo.TaskComment', N'TaskDiscussion';
    END;
    IF OBJECT_ID(N'dbo.TaskCommentRead', N'U') IS NOT NULL
    BEGIN
        IF OBJECT_ID(N'dbo.TaskDiscussionRead', N'U') IS NOT NULL
            THROW 50003, 'Both discussion read tables exist; reconcile them before upgrading.', 1;
        EXEC sys.sp_rename N'dbo.TaskCommentRead', N'TaskDiscussionRead';
    END;

    IF COL_LENGTH(N'dbo.TaskDiscussion', N'CommentID') IS NOT NULL
        EXEC sys.sp_rename N'dbo.TaskDiscussion.CommentID', N'Discussion_id', N'COLUMN';
    IF COL_LENGTH(N'dbo.TaskDiscussion', N'Message_text') IS NULL
       AND COL_LENGTH(N'dbo.TaskDiscussion', N'DiscussionText') IS NOT NULL
        EXEC sys.sp_rename N'dbo.TaskDiscussion.DiscussionText', N'Message_text', N'COLUMN';
    IF COL_LENGTH(N'dbo.TaskDiscussion', N'Message_text') IS NULL
       AND COL_LENGTH(N'dbo.TaskDiscussion', N'CommentText') IS NOT NULL
        EXEC sys.sp_rename N'dbo.TaskDiscussion.CommentText', N'Message_text', N'COLUMN';
    IF COL_LENGTH(N'dbo.TaskDiscussionRead', N'LastReadCommentID') IS NOT NULL
        EXEC sys.sp_rename N'dbo.TaskDiscussionRead.LastReadCommentID', N'Lastread_discussion_id', N'COLUMN';

    -- The previous trigger makes legacy task-level rows immutable. Replace it
    -- after their author columns have been migrated in this transaction.
    IF OBJECT_ID(N'dbo.TR_TaskDiscussion_RequireSubtask', N'TR') IS NOT NULL
        DROP TRIGGER dbo.TR_TaskDiscussion_RequireSubtask;

    -- SQL Server preserves constraints when renaming tables; align their names too.
    DECLARE @OldName nvarchar(517), @NewName sysname;
    DECLARE discussion_constraints CURSOR LOCAL FAST_FORWARD FOR
        SELECT N'dbo.[' + REPLACE(name, N']', N']]') + N']',
               REPLACE(name, N'TaskComment', N'TaskDiscussion')
        FROM sys.objects
        WHERE parent_object_id IN (OBJECT_ID(N'dbo.TaskDiscussion'), OBJECT_ID(N'dbo.TaskDiscussionRead'))
          AND name LIKE N'%TaskComment%';
    OPEN discussion_constraints;
    FETCH NEXT FROM discussion_constraints INTO @OldName, @NewName;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        EXEC sys.sp_rename @OldName, @NewName, N'OBJECT';
        FETCH NEXT FROM discussion_constraints INTO @OldName, @NewName;
    END;
    CLOSE discussion_constraints;
    DEALLOCATE discussion_constraints;

    IF OBJECT_ID(N'dbo.TaskDiscussion', N'U') IS NULL
        CREATE TABLE dbo.TaskDiscussion
        (
            Discussion_id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_TaskDiscussion PRIMARY KEY,
            Task_id int NOT NULL,
            Subtask_id int NULL,
            Sender_id int NOT NULL,
            Sender_type nvarchar(10) NOT NULL,
            Message_text nvarchar(1000) NOT NULL,
            Message_type nvarchar(20) NOT NULL CONSTRAINT DF_TaskDiscussion_MessageType DEFAULT ('Discussion'),
            Created_at datetime NOT NULL CONSTRAINT DF_TaskDiscussion_CreatedAt DEFAULT (GETDATE()),
            CONSTRAINT FK_TaskDiscussion_Task FOREIGN KEY (Task_id)
                REFERENCES dbo.[Task](Task_id) ON DELETE CASCADE,
            CONSTRAINT CK_TaskDiscussion_SenderType
                CHECK (Sender_type IN (N'User', N'Teacher'))
        );

    IF COL_LENGTH(N'dbo.TaskDiscussion', N'Subtask_id') IS NULL
        ALTER TABLE dbo.TaskDiscussion ADD Subtask_id int NULL;
    IF COL_LENGTH(N'dbo.TaskDiscussion', N'Message_type') IS NULL
        ALTER TABLE dbo.TaskDiscussion ADD Message_type nvarchar(20) NOT NULL
            CONSTRAINT DF_TaskDiscussion_MessageType DEFAULT ('Discussion');
    IF COL_LENGTH(N'dbo.TaskDiscussion', N'Sender_id') IS NULL
        ALTER TABLE dbo.TaskDiscussion ADD Sender_id int NULL;
    IF COL_LENGTH(N'dbo.TaskDiscussion', N'Sender_type') IS NULL
        ALTER TABLE dbo.TaskDiscussion ADD Sender_type nvarchar(10) NULL;

    -- Convert the old two-column sender representation without losing messages.
    IF COL_LENGTH(N'dbo.TaskDiscussion', N'User_id') IS NOT NULL
       OR COL_LENGTH(N'dbo.TaskDiscussion', N'Teacher_id') IS NOT NULL
    BEGIN
        IF COL_LENGTH(N'dbo.TaskDiscussion', N'User_id') IS NULL
           OR COL_LENGTH(N'dbo.TaskDiscussion', N'Teacher_id') IS NULL
            THROW 50006, 'The legacy discussion sender columns are incomplete.', 1;

        EXEC(N'
            IF EXISTS
            (
                SELECT 1 FROM dbo.TaskDiscussion
                WHERE (User_id IS NULL AND Teacher_id IS NULL)
                   OR (User_id IS NOT NULL AND Teacher_id IS NOT NULL)
            )
                THROW 50008, ''Every discussion must have exactly one user or teacher sender.'', 1;

            UPDATE dbo.TaskDiscussion
            SET Sender_id = COALESCE(User_id, Teacher_id),
                Sender_type = CASE WHEN Teacher_id IS NOT NULL THEN N''Teacher'' ELSE N''User'' END
            WHERE Sender_id IS NULL OR Sender_type IS NULL;');
    END;

    IF COL_LENGTH(N'dbo.TaskDiscussion', N'AuthorType') IS NOT NULL
       OR COL_LENGTH(N'dbo.TaskDiscussion', N'AuthorID') IS NOT NULL
    BEGIN
        IF COL_LENGTH(N'dbo.TaskDiscussion', N'AuthorType') IS NULL
           OR COL_LENGTH(N'dbo.TaskDiscussion', N'AuthorID') IS NULL
            THROW 50009, 'The older discussion sender columns are incomplete.', 1;
        EXEC(N'
            UPDATE dbo.TaskDiscussion
            SET Sender_id = AuthorID, Sender_type = AuthorType
            WHERE Sender_id IS NULL OR Sender_type IS NULL;');
    END;

    EXEC(N'
        IF EXISTS
        (
            SELECT 1 FROM dbo.TaskDiscussion d
            WHERE d.Sender_id IS NULL
               OR d.Sender_type NOT IN (N''User'', N''Teacher'')
               OR (d.Sender_type = N''User'' AND NOT EXISTS
                    (SELECT 1 FROM dbo.[User] u WHERE u.User_id = d.Sender_id))
               OR (d.Sender_type = N''Teacher'' AND NOT EXISTS
                    (SELECT 1 FROM dbo.Teacher t WHERE t.Teacher_id = d.Sender_id))
        )
            THROW 50007, ''A discussion has an invalid sender.'', 1;');

    IF OBJECT_ID(N'dbo.FK_TaskDiscussion_User', N'F') IS NOT NULL
        ALTER TABLE dbo.TaskDiscussion DROP CONSTRAINT FK_TaskDiscussion_User;
    IF OBJECT_ID(N'dbo.FK_TaskDiscussion_Teacher', N'F') IS NOT NULL
        ALTER TABLE dbo.TaskDiscussion DROP CONSTRAINT FK_TaskDiscussion_Teacher;
    IF OBJECT_ID(N'dbo.CK_TaskDiscussion_ExactlyOneAuthor', N'C') IS NOT NULL
        ALTER TABLE dbo.TaskDiscussion DROP CONSTRAINT CK_TaskDiscussion_ExactlyOneAuthor;

    IF COL_LENGTH(N'dbo.TaskDiscussion', N'User_id') IS NOT NULL
        ALTER TABLE dbo.TaskDiscussion DROP COLUMN User_id;
    IF COL_LENGTH(N'dbo.TaskDiscussion', N'Teacher_id') IS NOT NULL
        ALTER TABLE dbo.TaskDiscussion DROP COLUMN Teacher_id;

    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.TaskDiscussion') AND name=N'Sender_id' AND is_nullable=1)
        ALTER TABLE dbo.TaskDiscussion ALTER COLUMN Sender_id int NOT NULL;
    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.TaskDiscussion') AND name=N'Sender_type' AND is_nullable=1)
        ALTER TABLE dbo.TaskDiscussion ALTER COLUMN Sender_type nvarchar(10) NOT NULL;
    IF OBJECT_ID(N'dbo.CK_TaskDiscussion_SenderType', N'C') IS NULL
        EXEC(N'ALTER TABLE dbo.TaskDiscussion WITH CHECK ADD CONSTRAINT CK_TaskDiscussion_SenderType
            CHECK (Sender_type IN (N''User'', N''Teacher''));');

    IF COL_LENGTH(N'dbo.TaskDiscussion', N'AuthorName') IS NOT NULL
        ALTER TABLE dbo.TaskDiscussion DROP COLUMN AuthorName;
    IF COL_LENGTH(N'dbo.TaskDiscussion', N'AuthorID') IS NOT NULL
        ALTER TABLE dbo.TaskDiscussion DROP COLUMN AuthorID;
    IF COL_LENGTH(N'dbo.TaskDiscussion', N'AuthorType') IS NOT NULL
        ALTER TABLE dbo.TaskDiscussion DROP COLUMN AuthorType;

    -- Compile against the columns after legacy tables have been upgraded.
    EXEC(N'UPDATE dbo.TaskDiscussion SET Message_type = N''Discussion'' WHERE Message_type = N''Comment'';');
    DECLARE @DefaultName sysname;
    SELECT @DefaultName = dc.name
    FROM sys.default_constraints dc
    JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID(N'dbo.TaskDiscussion') AND c.name = N'Message_type'
      AND dc.definition NOT LIKE N'%Discussion%';
    IF @DefaultName IS NOT NULL
    BEGIN
        DECLARE @DropDefault nvarchar(max) =
            N'ALTER TABLE dbo.TaskDiscussion DROP CONSTRAINT ['
            + REPLACE(@DefaultName, N']', N']]') + N']';
        EXEC sys.sp_executesql @DropDefault;
        EXEC(N'ALTER TABLE dbo.TaskDiscussion ADD CONSTRAINT DF_TaskDiscussion_MessageType DEFAULT (''Discussion'') FOR Message_type;');
    END;

    IF OBJECT_ID(N'dbo.FK_TaskDiscussion_Subtask', N'F') IS NULL
        EXEC(N'ALTER TABLE dbo.TaskDiscussion ADD CONSTRAINT FK_TaskDiscussion_Subtask
            FOREIGN KEY (Subtask_id) REFERENCES dbo.Subtask(Subtask_id);');

    -- Preserve legacy task-level messages, but enforce ownership for subtask messages.
    -- Stop without changing data if an earlier writer supplied inconsistent IDs.
    EXEC(N'IF EXISTS (
        SELECT 1 FROM dbo.TaskDiscussion d
        JOIN dbo.Subtask s ON s.Subtask_id = d.Subtask_id
        WHERE d.Task_id <> s.Task_id)
        THROW 50004, ''Discussion task/subtask IDs do not match. Reconcile these records before upgrading.'', 1;');

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Subtask') AND name = N'UQ_Subtask_Task_Subtask')
        CREATE UNIQUE INDEX UQ_Subtask_Task_Subtask ON dbo.Subtask(Task_id, Subtask_id);
    IF OBJECT_ID(N'dbo.FK_TaskDiscussion_Task_Subtask', N'F') IS NULL
        EXEC(N'ALTER TABLE dbo.TaskDiscussion WITH CHECK ADD CONSTRAINT FK_TaskDiscussion_Task_Subtask
            FOREIGN KEY (Task_id, Subtask_id) REFERENCES dbo.Subtask(Task_id, Subtask_id);');

    -- NULL remains available only for existing legacy rows. New messages need a subtask;
    -- legacy messages cannot be edited or reassigned through an older client.
    EXEC(N'CREATE OR ALTER TRIGGER dbo.TR_TaskDiscussion_RequireSubtask
        ON dbo.TaskDiscussion AFTER INSERT, UPDATE AS
        BEGIN
            SET NOCOUNT ON;
            IF EXISTS (SELECT 1 FROM inserted WHERE Subtask_id IS NULL)
                OR EXISTS (SELECT 1 FROM deleted WHERE Subtask_id IS NULL)
                THROW 50005, ''New discussion messages require a subtask. Previous task discussions are read-only.'', 1;
        END;');

    IF OBJECT_ID(N'dbo.TaskDiscussionRead', N'U') IS NULL
        CREATE TABLE dbo.TaskDiscussionRead
        (
            Subtask_id int NOT NULL,
            Reader_type nvarchar(10) NOT NULL,
            Reader_id int NOT NULL,
            Lastread_discussion_id int NOT NULL CONSTRAINT DF_TaskDiscussionRead_LastRead DEFAULT (0),
            Read_at datetime NOT NULL CONSTRAINT DF_TaskDiscussionRead_ReadAt DEFAULT (GETDATE()),
            CONSTRAINT PK_TaskDiscussionRead PRIMARY KEY (Subtask_id, Reader_type, Reader_id),
            CONSTRAINT FK_TaskDiscussionRead_Subtask FOREIGN KEY (Subtask_id)
                REFERENCES dbo.Subtask(Subtask_id) ON DELETE CASCADE
        );

    IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.TaskDiscussion') AND name = N'IX_TaskComment_Task_Subtask')
        EXEC sys.sp_rename N'dbo.TaskDiscussion.IX_TaskComment_Task_Subtask', N'IX_TaskDiscussion_Task_Subtask', N'INDEX';
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.TaskDiscussion') AND name = N'IX_TaskDiscussion_Task_Subtask')
        EXEC(N'CREATE INDEX IX_TaskDiscussion_Task_Subtask ON dbo.TaskDiscussion(Task_id, Subtask_id);');
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.TaskDiscussion') AND name = N'IX_TaskDiscussion_Sender')
        EXEC(N'CREATE INDEX IX_TaskDiscussion_Sender ON dbo.TaskDiscussion(Sender_type, Sender_id);');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
