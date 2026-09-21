/* Removes the unused role creation timestamp and its default constraint.
   Safe to run repeatedly. Other entities' timestamps are unaffected. */
USE EduTaskDB;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF COL_LENGTH(N'dbo.Roles', N'Created_at') IS NOT NULL
    BEGIN
        DECLARE @DefaultConstraint sysname;
        SELECT @DefaultConstraint = dc.name
        FROM sys.default_constraints dc
        INNER JOIN sys.columns c
            ON c.object_id = dc.parent_object_id
            AND c.column_id = dc.parent_column_id
        WHERE c.object_id = OBJECT_ID(N'dbo.Roles')
          AND c.name = N'Created_at';

        IF @DefaultConstraint IS NOT NULL
        BEGIN
            DECLARE @DropDefault nvarchar(max) =
                N'ALTER TABLE dbo.Roles DROP CONSTRAINT ' + QUOTENAME(@DefaultConstraint);
            EXEC sys.sp_executesql @DropDefault;
        END;

        ALTER TABLE dbo.Roles DROP COLUMN Created_at;
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
