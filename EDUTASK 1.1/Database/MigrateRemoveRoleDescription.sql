/* Removes unused role metadata. Safe to run repeatedly.
   MigrateRolesAndAuthorization.sql no longer creates or seeds this column. */
USE EduTaskDB;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF COL_LENGTH(N'dbo.Roles', N'Description') IS NOT NULL
        ALTER TABLE dbo.Roles DROP COLUMN Description;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
