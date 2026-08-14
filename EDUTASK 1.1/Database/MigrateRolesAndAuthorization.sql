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
            RoleID int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Roles PRIMARY KEY,
            RoleName nvarchar(50) NOT NULL,
            Description nvarchar(250) NULL,
            IsActive bit NOT NULL CONSTRAINT DF_Roles_IsActive DEFAULT (1),
            CreatedAt datetime2 NOT NULL CONSTRAINT DF_Roles_CreatedAt DEFAULT (SYSUTCDATETIME()),
            CONSTRAINT UQ_Roles_RoleName UNIQUE (RoleName)
        );
    END;

    MERGE dbo.Roles WITH (HOLDLOCK) AS target
    USING (VALUES
        (N'Director', N'Full administrative and final approval authority.'),
        (N'Staff',    N'Creates and monitors tasks without final authority.'),
        (N'Teacher',  N'Works on assigned tasks and submits proof for validation.')
    ) AS source(RoleName, Description)
    ON target.RoleName = source.RoleName
    WHEN MATCHED THEN
        UPDATE SET Description = source.Description, IsActive = 1
    WHEN NOT MATCHED THEN
        INSERT (RoleName, Description) VALUES (source.RoleName, source.Description);

    IF COL_LENGTH(N'dbo.User', N'RoleID') IS NULL
        ALTER TABLE dbo.[User] ADD RoleID int NULL;
    IF COL_LENGTH(N'dbo.User', N'IsActive') IS NULL
        ALTER TABLE dbo.[User] ADD IsActive bit NOT NULL
            CONSTRAINT DF_User_IsActive DEFAULT (1) WITH VALUES;

    EXEC sys.sp_executesql N'UPDATE dbo.[User] SET RoleID = (SELECT RoleID FROM dbo.Roles WHERE RoleName = N''Director'') WHERE RoleID IS NULL;';

    EXEC sys.sp_executesql N'IF EXISTS (SELECT 1 FROM dbo.[User] WHERE RoleID IS NULL) THROW 51000, ''One or more User accounts could not be assigned a role.'', 1;';

    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.User') AND name = N'RoleID' AND is_nullable = 1)
        ALTER TABLE dbo.[User] ALTER COLUMN RoleID int NOT NULL;

    IF OBJECT_ID(N'dbo.FK_User_Roles', N'F') IS NULL
        ALTER TABLE dbo.[User] WITH CHECK ADD CONSTRAINT FK_User_Roles
            FOREIGN KEY (RoleID) REFERENCES dbo.Roles(RoleID);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.User') AND name = N'IX_User_RoleID')
        CREATE INDEX IX_User_RoleID ON dbo.[User](RoleID);

    IF COL_LENGTH(N'dbo.Teacher', N'RoleID') IS NULL
        ALTER TABLE dbo.Teacher ADD RoleID int NULL;
    IF COL_LENGTH(N'dbo.Teacher', N'IsActive') IS NULL
        ALTER TABLE dbo.Teacher ADD IsActive bit NOT NULL
            CONSTRAINT DF_Teacher_IsActive DEFAULT (1) WITH VALUES;

    EXEC sys.sp_executesql N'UPDATE dbo.Teacher SET RoleID = (SELECT RoleID FROM dbo.Roles WHERE RoleName = N''Teacher'') WHERE RoleID IS NULL;';

    EXEC sys.sp_executesql N'IF EXISTS (SELECT 1 FROM dbo.Teacher WHERE RoleID IS NULL) THROW 51001, ''One or more Teacher accounts could not be assigned a role.'', 1;';

    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Teacher') AND name = N'RoleID' AND is_nullable = 1)
        ALTER TABLE dbo.Teacher ALTER COLUMN RoleID int NOT NULL;

    IF OBJECT_ID(N'dbo.FK_Teacher_Roles', N'F') IS NULL
        ALTER TABLE dbo.Teacher WITH CHECK ADD CONSTRAINT FK_Teacher_Roles
            FOREIGN KEY (RoleID) REFERENCES dbo.Roles(RoleID);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Teacher') AND name = N'IX_Teacher_RoleID')
        CREATE INDEX IX_Teacher_RoleID ON dbo.Teacher(RoleID);

    /* Task.UserID is retained for compatibility and remains the original owner. */
    IF COL_LENGTH(N'dbo.Task', N'CreatedByUserID') IS NULL
        ALTER TABLE dbo.[Task] ADD CreatedByUserID int NULL;
    IF COL_LENGTH(N'dbo.Task', N'LastModifiedByUserID') IS NULL
        ALTER TABLE dbo.[Task] ADD LastModifiedByUserID int NULL;
    IF COL_LENGTH(N'dbo.Task', N'CompletionApprovedByUserID') IS NULL
        ALTER TABLE dbo.[Task] ADD CompletionApprovedByUserID int NULL;
    IF COL_LENGTH(N'dbo.Task', N'CompletionApprovedAt') IS NULL
        ALTER TABLE dbo.[Task] ADD CompletionApprovedAt datetime2 NULL;
    IF COL_LENGTH(N'dbo.Task', N'RevisionRequestedByUserID') IS NULL
        ALTER TABLE dbo.[Task] ADD RevisionRequestedByUserID int NULL;
    IF COL_LENGTH(N'dbo.Task', N'RevisionRequestedAt') IS NULL
        ALTER TABLE dbo.[Task] ADD RevisionRequestedAt datetime2 NULL;
    IF COL_LENGTH(N'dbo.Task', N'RevisionReason') IS NULL
        ALTER TABLE dbo.[Task] ADD RevisionReason nvarchar(1000) NULL;

    EXEC sys.sp_executesql N'UPDATE dbo.[Task] SET CreatedByUserID = UserID WHERE CreatedByUserID IS NULL;';

    IF OBJECT_ID(N'dbo.FK_Task_CreatedByUser', N'F') IS NULL
        ALTER TABLE dbo.[Task] WITH CHECK ADD CONSTRAINT FK_Task_CreatedByUser
            FOREIGN KEY (CreatedByUserID) REFERENCES dbo.[User](UserID);
    IF OBJECT_ID(N'dbo.FK_Task_LastModifiedByUser', N'F') IS NULL
        ALTER TABLE dbo.[Task] WITH CHECK ADD CONSTRAINT FK_Task_LastModifiedByUser
            FOREIGN KEY (LastModifiedByUserID) REFERENCES dbo.[User](UserID);
    IF OBJECT_ID(N'dbo.FK_Task_CompletionApprovedByUser', N'F') IS NULL
        ALTER TABLE dbo.[Task] WITH CHECK ADD CONSTRAINT FK_Task_CompletionApprovedByUser
            FOREIGN KEY (CompletionApprovedByUserID) REFERENCES dbo.[User](UserID);
    IF OBJECT_ID(N'dbo.FK_Task_RevisionRequestedByUser', N'F') IS NULL
        ALTER TABLE dbo.[Task] WITH CHECK ADD CONSTRAINT FK_Task_RevisionRequestedByUser
            FOREIGN KEY (RevisionRequestedByUserID) REFERENCES dbo.[User](UserID);

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Task') AND name = N'IX_Task_CreatedByUserID')
        CREATE INDEX IX_Task_CreatedByUserID ON dbo.[Task](CreatedByUserID);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Task') AND name = N'IX_Task_CompletionApprovedByUserID')
        CREATE INDEX IX_Task_CompletionApprovedByUserID ON dbo.[Task](CompletionApprovedByUserID);

    /* Normalize the legacy value used for returned work. */
    UPDATE dbo.TaskAssignment
    SET CompletionStatus = N'Needs Revision'
    WHERE CompletionStatus = N'Returned';

    IF OBJECT_ID(N'dbo.CK_TaskAssignment_CompletionStatus', N'C') IS NULL
        ALTER TABLE dbo.TaskAssignment WITH CHECK ADD CONSTRAINT CK_TaskAssignment_CompletionStatus
            CHECK (CompletionStatus IN (N'Pending', N'Acknowledged', N'For Validation', N'Needs Revision', N'Completed'));

    IF OBJECT_ID(N'dbo.TaskActivityLog', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.TaskActivityLog
        (
            ActivityLogID bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_TaskActivityLog PRIMARY KEY,
            TaskID int NOT NULL,
            PerformedByUserID int NULL,
            PerformedByTeacherID int NULL,
            ActionType nvarchar(50) NOT NULL,
            PreviousStatus nvarchar(50) NULL,
            NewStatus nvarchar(50) NULL,
            Details nvarchar(1000) NULL,
            CreatedAt datetime2 NOT NULL CONSTRAINT DF_TaskActivityLog_CreatedAt DEFAULT (SYSUTCDATETIME()),
            CONSTRAINT FK_TaskActivityLog_Task FOREIGN KEY (TaskID) REFERENCES dbo.[Task](TaskID),
            CONSTRAINT FK_TaskActivityLog_User FOREIGN KEY (PerformedByUserID) REFERENCES dbo.[User](UserID),
            CONSTRAINT FK_TaskActivityLog_Teacher FOREIGN KEY (PerformedByTeacherID) REFERENCES dbo.Teacher(TeacherID),
            CONSTRAINT CK_TaskActivityLog_OneActor CHECK
                ((PerformedByUserID IS NOT NULL AND PerformedByTeacherID IS NULL)
                 OR (PerformedByUserID IS NULL AND PerformedByTeacherID IS NOT NULL)),
            CONSTRAINT CK_TaskActivityLog_ActionType CHECK (ActionType IN
                (N'TaskCreated', N'TaskEdited', N'TeacherAssigned', N'TeacherUnassigned',
                 N'TaskAcknowledged', N'ProofSubmitted', N'RevisionRequested',
                 N'CompletionApproved', N'TaskReopened', N'TaskDeleted'))
        );
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.TaskActivityLog') AND name = N'IX_TaskActivityLog_Task_CreatedAt')
        CREATE INDEX IX_TaskActivityLog_Task_CreatedAt ON dbo.TaskActivityLog(TaskID, CreatedAt DESC);
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.TaskActivityLog') AND name = N'IX_TaskActivityLog_User_CreatedAt')
        CREATE INDEX IX_TaskActivityLog_User_CreatedAt ON dbo.TaskActivityLog(PerformedByUserID, CreatedAt DESC)
            WHERE PerformedByUserID IS NOT NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.TaskActivityLog') AND name = N'IX_TaskActivityLog_Teacher_CreatedAt')
        CREATE INDEX IX_TaskActivityLog_Teacher_CreatedAt ON dbo.TaskActivityLog(PerformedByTeacherID, CreatedAt DESC)
            WHERE PerformedByTeacherID IS NOT NULL;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

/* Central role lookup. Procedures never accept a client-supplied role. */
CREATE OR ALTER PROCEDURE dbo.AssertActiveUserRole
    @ActingUserID int,
    @AllowDirector bit = 0,
    @AllowStaff bit = 0
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @RoleName nvarchar(50);
    SELECT @RoleName = r.RoleName
    FROM dbo.[User] u
    INNER JOIN dbo.Roles r ON r.RoleID = u.RoleID
    WHERE u.UserID = @ActingUserID AND u.IsActive = 1 AND r.IsActive = 1;

    IF @RoleName IS NULL
        THROW 51100, 'The acting account does not exist or is disabled.', 1;
    IF NOT ((@AllowDirector = 1 AND @RoleName = N'Director') OR
            (@AllowStaff = 1 AND @RoleName = N'Staff'))
        THROW 51101, 'The acting account is not authorized for this action.', 1;
END;
GO

CREATE OR ALTER PROCEDURE dbo.ApproveTaskCompletion
    @TaskID int,
    @ActingUserID int
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    EXEC dbo.AssertActiveUserRole @ActingUserID, @AllowDirector = 1;
    BEGIN TRANSACTION;
    DECLARE @PreviousStatus nvarchar(50);
    SELECT TOP (1) @PreviousStatus = CompletionStatus FROM dbo.TaskAssignment WITH (UPDLOCK, HOLDLOCK) WHERE TaskID = @TaskID;
    IF @PreviousStatus IS NULL THROW 51110, 'The task does not exist or has no assignment.', 1;
    IF EXISTS (SELECT 1 FROM dbo.TaskAssignment WHERE TaskID = @TaskID AND CompletionStatus <> N'For Validation')
        THROW 51111, 'Every assignment must be For Validation before final approval.', 1;
    IF EXISTS
    (
        SELECT 1 FROM dbo.Subtask s
        WHERE s.TaskID = @TaskID AND NOT EXISTS
            (SELECT 1 FROM dbo.SubtaskProof p WHERE p.SubtaskID = s.SubtaskID AND p.ValidationStatus = N'Approved')
    ) THROW 51112, 'Every subtask must have approved proof before final completion.', 1;

    UPDATE dbo.TaskAssignment SET CompletionStatus = N'Completed', CompletedAt = GETDATE() WHERE TaskID = @TaskID;
    UPDATE dbo.[Task] SET CompletionApprovedByUserID = @ActingUserID,
        CompletionApprovedAt = SYSUTCDATETIME(), LastModifiedByUserID = @ActingUserID,
        UpdatedAt = SYSUTCDATETIME() WHERE TaskID = @TaskID;
    INSERT dbo.TaskActivityLog(TaskID, PerformedByUserID, ActionType, PreviousStatus, NewStatus)
        VALUES (@TaskID, @ActingUserID, N'CompletionApproved', @PreviousStatus, N'Completed');
    COMMIT TRANSACTION;
END;
GO

CREATE OR ALTER PROCEDURE dbo.RequestTaskRevision
    @TaskID int,
    @ActingUserID int,
    @Reason nvarchar(1000)
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    IF NULLIF(LTRIM(RTRIM(@Reason)), N'') IS NULL THROW 51120, 'A revision reason is required.', 1;
    EXEC dbo.AssertActiveUserRole @ActingUserID, @AllowDirector = 1, @AllowStaff = 1;
    BEGIN TRANSACTION;
    IF NOT EXISTS (SELECT 1 FROM dbo.TaskAssignment WITH (UPDLOCK, HOLDLOCK) WHERE TaskID = @TaskID AND CompletionStatus = N'For Validation')
        THROW 51121, 'Only a task that is For Validation can be returned for revision.', 1;
    UPDATE dbo.TaskAssignment SET CompletionStatus = N'Needs Revision', CompletedAt = NULL WHERE TaskID = @TaskID;
    UPDATE dbo.[Task] SET RevisionRequestedByUserID = @ActingUserID,
        RevisionRequestedAt = SYSUTCDATETIME(), RevisionReason = @Reason,
        LastModifiedByUserID = @ActingUserID, UpdatedAt = SYSUTCDATETIME() WHERE TaskID = @TaskID;
    INSERT dbo.TaskActivityLog(TaskID, PerformedByUserID, ActionType, PreviousStatus, NewStatus, Details)
        VALUES (@TaskID, @ActingUserID, N'RevisionRequested', N'For Validation', N'Needs Revision', @Reason);
    COMMIT TRANSACTION;
END;
GO

CREATE OR ALTER PROCEDURE dbo.ReopenTask
    @TaskID int,
    @ActingUserID int,
    @Reason nvarchar(1000) = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    EXEC dbo.AssertActiveUserRole @ActingUserID, @AllowDirector = 1;
    BEGIN TRANSACTION;
    IF NOT EXISTS (SELECT 1 FROM dbo.TaskAssignment WITH (UPDLOCK, HOLDLOCK) WHERE TaskID = @TaskID AND CompletionStatus = N'Completed')
        THROW 51130, 'Only a completed task can be reopened.', 1;
    UPDATE dbo.TaskAssignment SET CompletionStatus = N'Needs Revision', CompletedAt = NULL WHERE TaskID = @TaskID;
    UPDATE dbo.[Task] SET CompletionApprovedByUserID = NULL, CompletionApprovedAt = NULL,
        RevisionRequestedByUserID = @ActingUserID, RevisionRequestedAt = SYSUTCDATETIME(),
        RevisionReason = @Reason, LastModifiedByUserID = @ActingUserID,
        UpdatedAt = SYSUTCDATETIME() WHERE TaskID = @TaskID;
    INSERT dbo.TaskActivityLog(TaskID, PerformedByUserID, ActionType, PreviousStatus, NewStatus, Details)
        VALUES (@TaskID, @ActingUserID, N'TaskReopened', N'Completed', N'Needs Revision', @Reason);
    COMMIT TRANSACTION;
END;
GO

/* Validation: all result sets should show healthy mappings and zero invalid rows. */
SELECT RoleID, RoleName, IsActive FROM dbo.Roles ORDER BY RoleID;
SELECT r.RoleName, COUNT(*) AS AccountCount FROM dbo.[User] u JOIN dbo.Roles r ON r.RoleID = u.RoleID GROUP BY r.RoleName;
SELECT r.RoleName, COUNT(*) AS AccountCount FROM dbo.Teacher t JOIN dbo.Roles r ON r.RoleID = t.RoleID GROUP BY r.RoleName;
SELECT COUNT(*) AS TasksWithoutCreator FROM dbo.[Task] WHERE CreatedByUserID IS NULL;
SELECT CompletionStatus, COUNT(*) AS AssignmentCount FROM dbo.TaskAssignment GROUP BY CompletionStatus;
SELECT name AS InstalledProcedure FROM sys.procedures
WHERE name IN (N'AssertActiveUserRole', N'ApproveTaskCompletion', N'RequestTaskRevision', N'ReopenTask')
ORDER BY name;
