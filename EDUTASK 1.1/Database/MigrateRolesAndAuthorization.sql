SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.Roles', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Roles
        (
            Role_id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Roles PRIMARY KEY,
            Role_name nvarchar(50) NOT NULL,
            CONSTRAINT UQ_Roles_RoleName UNIQUE (Role_name)
        );
    END;

    MERGE dbo.Roles WITH (HOLDLOCK) AS target
    USING (VALUES
        (N'Director'),
        (N'Staff'),
        (N'Teacher')
    ) AS source(Role_name)
    ON target.Role_name = source.Role_name
    WHEN NOT MATCHED THEN
        INSERT (Role_name) VALUES (source.Role_name);

    IF COL_LENGTH(N'dbo.User', N'Role_id') IS NULL
        ALTER TABLE dbo.[User] ADD Role_id int NULL;
    IF COL_LENGTH(N'dbo.User', N'Is_active') IS NULL
        ALTER TABLE dbo.[User] ADD Is_active bit NOT NULL
            CONSTRAINT DF_User_IsActive DEFAULT (1) WITH VALUES;

    EXEC sys.sp_executesql N'UPDATE dbo.[User] SET Role_id = (SELECT Role_id FROM dbo.Roles WHERE Role_name = N''Director'') WHERE Role_id IS NULL;';

    EXEC sys.sp_executesql N'IF EXISTS (SELECT 1 FROM dbo.[User] WHERE Role_id IS NULL) THROW 51000, ''One or more User accounts could not be assigned a role.'', 1;';

    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.User') AND name = N'Role_id' AND is_nullable = 1)
        ALTER TABLE dbo.[User] ALTER COLUMN Role_id int NOT NULL;

    IF OBJECT_ID(N'dbo.FK_User_Roles', N'F') IS NULL
        ALTER TABLE dbo.[User] WITH CHECK ADD CONSTRAINT FK_User_Roles
            FOREIGN KEY (Role_id) REFERENCES dbo.Roles(Role_id);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.User') AND name = N'IX_User_RoleID')
        CREATE INDEX IX_User_RoleID ON dbo.[User](Role_id);

    IF COL_LENGTH(N'dbo.Teacher', N'Role_id') IS NULL
        ALTER TABLE dbo.Teacher ADD Role_id int NULL;
    IF COL_LENGTH(N'dbo.Teacher', N'Is_active') IS NULL
        ALTER TABLE dbo.Teacher ADD Is_active bit NOT NULL
            CONSTRAINT DF_Teacher_IsActive DEFAULT (1) WITH VALUES;

    EXEC sys.sp_executesql N'UPDATE dbo.Teacher SET Role_id = (SELECT Role_id FROM dbo.Roles WHERE Role_name = N''Teacher'') WHERE Role_id IS NULL;';

    EXEC sys.sp_executesql N'IF EXISTS (SELECT 1 FROM dbo.Teacher WHERE Role_id IS NULL) THROW 51001, ''One or more Teacher accounts could not be assigned a role.'', 1;';

    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Teacher') AND name = N'Role_id' AND is_nullable = 1)
        ALTER TABLE dbo.Teacher ALTER COLUMN Role_id int NOT NULL;

    IF OBJECT_ID(N'dbo.FK_Teacher_Roles', N'F') IS NULL
        ALTER TABLE dbo.Teacher WITH CHECK ADD CONSTRAINT FK_Teacher_Roles
            FOREIGN KEY (Role_id) REFERENCES dbo.Roles(Role_id);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Teacher') AND name = N'IX_Teacher_RoleID')
        CREATE INDEX IX_Teacher_RoleID ON dbo.Teacher(Role_id);

    IF COL_LENGTH(N'dbo.Task', N'Createdby_user_id') IS NULL
        ALTER TABLE dbo.[Task] ADD Createdby_user_id int NULL;
    IF COL_LENGTH(N'dbo.Task', N'Completion_approvedby_user_id') IS NULL
        ALTER TABLE dbo.[Task] ADD Completion_approvedby_user_id int NULL;

    IF COL_LENGTH(N'dbo.Task', N'User_id') IS NOT NULL
        EXEC sys.sp_executesql N'UPDATE dbo.[Task] SET Createdby_user_id = User_id WHERE Createdby_user_id IS NULL;';

    IF EXISTS (SELECT 1 FROM dbo.[Task] WHERE Createdby_user_id IS NULL)
        THROW 51002, 'One or more tasks do not have a creator.', 1;
    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Task') AND name = N'Createdby_user_id' AND is_nullable = 1)
        ALTER TABLE dbo.[Task] ALTER COLUMN Createdby_user_id int NOT NULL;

    IF OBJECT_ID(N'dbo.FK_Task_CreatedByUser', N'F') IS NULL
        ALTER TABLE dbo.[Task] WITH CHECK ADD CONSTRAINT FK_Task_CreatedByUser
            FOREIGN KEY (Createdby_user_id) REFERENCES dbo.[User](User_id);
    IF OBJECT_ID(N'dbo.FK_Task_CompletionApprovedByUser', N'F') IS NULL
        ALTER TABLE dbo.[Task] WITH CHECK ADD CONSTRAINT FK_Task_CompletionApprovedByUser
            FOREIGN KEY (Completion_approvedby_user_id) REFERENCES dbo.[User](User_id);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Task') AND name = N'IX_Task_CreatedByUserID')
        CREATE INDEX IX_Task_CreatedByUserID ON dbo.[Task](Createdby_user_id);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Task') AND name = N'IX_Task_CompletionApprovedByUserID')
        CREATE INDEX IX_Task_CompletionApprovedByUserID ON dbo.[Task](Completion_approvedby_user_id);

    /* Normalize the legacy value used for returned work. */
    UPDATE dbo.TaskAssignment
    SET Completion_status = N'Needs Revision'
    WHERE Completion_status = N'Returned';

    IF OBJECT_ID(N'dbo.CK_TaskAssignment_CompletionStatus', N'C') IS NULL
        ALTER TABLE dbo.TaskAssignment WITH CHECK ADD CONSTRAINT CK_TaskAssignment_CompletionStatus
            CHECK (Completion_status IN (N'Pending', N'Acknowledged', N'For Validation', N'Needs Revision', N'Completed'));

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

/* Central role lookup. Procedures never accept a client-supplied role. */
CREATE OR ALTER PROCEDURE dbo.AssertActiveUserRole
    @Acting_user_id int,
    @AllowDirector bit = 0,
    @AllowStaff bit = 0
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Role_name nvarchar(50);
    SELECT @Role_name = r.Role_name
    FROM dbo.[User] u
    INNER JOIN dbo.Roles r ON r.Role_id = u.Role_id
    WHERE u.User_id = @Acting_user_id AND u.Is_active = 1;

    IF @Role_name IS NULL
        THROW 51100, 'The acting account does not exist or is disabled.', 1;
    IF NOT ((@AllowDirector = 1 AND @Role_name = N'Director') OR
            (@AllowStaff = 1 AND @Role_name = N'Staff'))
        THROW 51101, 'The acting account is not authorized for this action.', 1;
END;
GO

/* The proof merge normally runs before this script. Keep this script safe to
   rerun independently by ensuring the procedure's referenced column exists. */
IF COL_LENGTH(N'dbo.Subtask', N'Proof_validation_status') IS NULL
    ALTER TABLE dbo.Subtask ADD Proof_validation_status nvarchar(20) NULL;
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
    SELECT TOP (1) @Previous_status = Completion_status FROM dbo.TaskAssignment WITH (UPDLOCK, HOLDLOCK) WHERE Task_id = @Task_id;
    IF @Previous_status IS NULL THROW 51110, 'The task does not exist or has no assignment.', 1;
    IF EXISTS (SELECT 1 FROM dbo.TaskAssignment WHERE Task_id = @Task_id AND Completion_status <> N'For Validation')
        THROW 51111, 'Every assignment must be For Validation before final approval.', 1;
    IF EXISTS
    (
        SELECT 1 FROM dbo.Subtask s
        WHERE s.Task_id = @Task_id
          AND (s.Proof_validation_status IS NULL OR s.Proof_validation_status <> N'Approved')
    ) THROW 51112, 'Every subtask must have approved proof before final completion.', 1;

    UPDATE dbo.TaskAssignment SET Completion_status = N'Completed', Completed_at = GETDATE() WHERE Task_id = @Task_id;
    UPDATE dbo.[Task] SET Completion_approvedby_user_id = @Acting_user_id WHERE Task_id = @Task_id;
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
    IF NOT EXISTS (SELECT 1 FROM dbo.TaskAssignment WITH (UPDLOCK, HOLDLOCK) WHERE Task_id = @Task_id AND Completion_status = N'For Validation')
        THROW 51121, 'Only a task that is For Validation can be returned for revision.', 1;
    UPDATE dbo.TaskAssignment SET Completion_status = N'Needs Revision', Completed_at = NULL WHERE Task_id = @Task_id;
    COMMIT TRANSACTION;
END;
GO
/* dbo.ReopenTask was never called by the app. Retired here so replaying this
   script removes it from databases that already have it. */
DROP PROCEDURE IF EXISTS dbo.ReopenTask;
GO

/* Validation: all result sets should show healthy mappings and zero invalid rows. */
SELECT Role_id, Role_name FROM dbo.Roles ORDER BY Role_id;
SELECT r.Role_name, COUNT(*) AS AccountCount FROM dbo.[User] u JOIN dbo.Roles r ON r.Role_id = u.Role_id GROUP BY r.Role_name;
SELECT r.Role_name, COUNT(*) AS AccountCount FROM dbo.Teacher t JOIN dbo.Roles r ON r.Role_id = t.Role_id GROUP BY r.Role_name;
SELECT COUNT(*) AS TasksWithoutCreator FROM dbo.[Task] WHERE Createdby_user_id IS NULL;
SELECT Completion_status, COUNT(*) AS AssignmentCount FROM dbo.TaskAssignment GROUP BY Completion_status;
SELECT name AS InstalledProcedure FROM sys.procedures
WHERE name IN (N'AssertActiveUserRole', N'ApproveTaskCompletion', N'RequestTaskRevision')
ORDER BY name;
