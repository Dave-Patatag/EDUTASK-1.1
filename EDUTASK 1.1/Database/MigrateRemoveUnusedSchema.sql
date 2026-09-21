/* =====================================================================
   Removes schema elements that are not used by EduTask.

   Run this after MigrateRolesAndAuthorization.sql so its stored
   procedures no longer write to dbo.TaskActivityLog.
   Safe to run repeatedly.
   ===================================================================== */

USE EduTaskDB;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    DROP TABLE IF EXISTS dbo.TaskActivityLog;

    IF COL_LENGTH(N'dbo.User', N'Birthdate') IS NOT NULL
        ALTER TABLE dbo.[User] DROP COLUMN Birthdate;
    IF COL_LENGTH(N'dbo.User', N'Position') IS NOT NULL
        ALTER TABLE dbo.[User] DROP COLUMN Position;

    IF COL_LENGTH(N'dbo.Teacher', N'Birthdate') IS NOT NULL
        ALTER TABLE dbo.Teacher DROP COLUMN Birthdate;
    IF COL_LENGTH(N'dbo.Teacher', N'Position') IS NOT NULL
        ALTER TABLE dbo.Teacher DROP COLUMN Position;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

SELECT name AS RemainingUnusedTable
FROM sys.tables
WHERE name = N'TaskActivityLog';

SELECT OBJECT_NAME(object_id) AS TableName, name AS RemainingUnusedColumn
FROM sys.columns
WHERE (object_id = OBJECT_ID(N'dbo.User') OR object_id = OBJECT_ID(N'dbo.Teacher'))
  AND name IN (N'Birthdate', N'Position');
GO
