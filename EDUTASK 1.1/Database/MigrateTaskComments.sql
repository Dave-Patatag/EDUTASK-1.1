IF OBJECT_ID(N'dbo.TaskComment', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TaskComment
    (
        CommentID int IDENTITY(1,1) NOT NULL PRIMARY KEY,
        TaskID int NOT NULL,
        AuthorType nvarchar(10) NOT NULL,
        AuthorID int NOT NULL,
        AuthorName nvarchar(120) NOT NULL,
        CommentText nvarchar(1000) NOT NULL,
        CreatedAt datetime NOT NULL CONSTRAINT DF_TaskComment_CreatedAt DEFAULT (GETDATE()),
        CONSTRAINT FK_TaskComment_Task FOREIGN KEY (TaskID)
            REFERENCES dbo.[Task](TaskID) ON DELETE CASCADE
    );
END;
