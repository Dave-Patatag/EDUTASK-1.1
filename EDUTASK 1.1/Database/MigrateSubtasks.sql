IF OBJECT_ID(N'dbo.Subtask', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Subtask
    (
        Subtask_id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Task_id int NOT NULL,
        Title nvarchar(200) NOT NULL,
        CONSTRAINT FK_Subtask_Task FOREIGN KEY (Task_id)
            REFERENCES dbo.[Task](Task_id) ON DELETE CASCADE
    );
END;
