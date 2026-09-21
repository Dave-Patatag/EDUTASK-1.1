/* Adds the nullable timestamp used for meaningful task-edit display and
   teacher notifications. New tasks remain NULL until a user-visible edit. */
USE EduTaskDB;
GO

SET NOCOUNT ON;

IF COL_LENGTH(N'dbo.Task', N'Updated_at') IS NULL
    ALTER TABLE dbo.[Task] ADD Updated_at datetime2 NULL;
GO

SELECT Task_id, Created_at, Updated_at
FROM dbo.[Task]
ORDER BY Task_id;
GO
