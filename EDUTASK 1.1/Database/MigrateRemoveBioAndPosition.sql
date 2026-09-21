SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF COL_LENGTH(N'dbo.User', N'Bio') IS NOT NULL
        ALTER TABLE dbo.[User] DROP COLUMN Bio;
    IF COL_LENGTH(N'dbo.User', N'Position') IS NOT NULL
        ALTER TABLE dbo.[User] DROP COLUMN Position;
    IF COL_LENGTH(N'dbo.Teacher', N'Bio') IS NOT NULL
        ALTER TABLE dbo.Teacher DROP COLUMN Bio;
    IF COL_LENGTH(N'dbo.Teacher', N'Position') IS NOT NULL
        ALTER TABLE dbo.Teacher DROP COLUMN Position;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT t.name AS Table_name, c.name AS Remaining_column
FROM sys.tables t
JOIN sys.columns c ON c.object_id=t.object_id
WHERE t.name IN (N'User',N'Teacher')
  AND c.name IN (N'Bio',N'Position');
