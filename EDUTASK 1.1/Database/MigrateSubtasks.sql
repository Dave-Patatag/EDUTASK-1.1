IF OBJECT_ID(N'dbo.Subtask', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Subtask
    (
        SubtaskID int IDENTITY(1,1) NOT NULL PRIMARY KEY,
        TaskID int NOT NULL,
        Title nvarchar(200) NOT NULL,
        IsCompleted bit NOT NULL CONSTRAINT DF_Subtask_IsCompleted DEFAULT (0),
        CreatedAt datetime NOT NULL CONSTRAINT DF_Subtask_CreatedAt DEFAULT (GETDATE()),
        CONSTRAINT FK_Subtask_Task FOREIGN KEY (TaskID)
            REFERENCES dbo.[Task](TaskID) ON DELETE CASCADE
    );
END;
