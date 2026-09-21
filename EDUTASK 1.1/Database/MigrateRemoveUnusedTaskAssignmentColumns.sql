/* Removes legacy TaskAssignment columns that EduTask does not read or write.
   Safe to run repeatedly. */
USE EduTaskDB;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF COL_LENGTH(N'dbo.TaskAssignment', N'Updated_at') IS NOT NULL
    BEGIN
        DECLARE @UpdatedAtDefault sysname;
        SELECT @UpdatedAtDefault = dc.name
        FROM sys.default_constraints dc
        INNER JOIN sys.columns c
            ON c.object_id = dc.parent_object_id
            AND c.column_id = dc.parent_column_id
        WHERE c.object_id = OBJECT_ID(N'dbo.TaskAssignment')
          AND c.name = N'Updated_at';

        IF @UpdatedAtDefault IS NOT NULL
        BEGIN
            DECLARE @DropUpdatedAtDefault nvarchar(max) =
                N'ALTER TABLE dbo.TaskAssignment DROP CONSTRAINT ' + QUOTENAME(@UpdatedAtDefault);
            EXEC sys.sp_executesql @DropUpdatedAtDefault;
        END;

        ALTER TABLE dbo.TaskAssignment DROP COLUMN Updated_at;
    END;

    IF COL_LENGTH(N'dbo.TaskAssignment', N'StartedAt') IS NOT NULL
        ALTER TABLE dbo.TaskAssignment DROP COLUMN StartedAt;

    IF COL_LENGTH(N'dbo.TaskAssignment', N'CompletionNotes') IS NOT NULL
        ALTER TABLE dbo.TaskAssignment DROP COLUMN CompletionNotes;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
