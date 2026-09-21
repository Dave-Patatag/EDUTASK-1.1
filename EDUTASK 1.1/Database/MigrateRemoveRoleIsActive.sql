/* Removes the unused role-level active flag. Account-level Is_active remains
   on dbo.User and dbo.Teacher. Safe to run repeatedly. */
USE EduTaskDB;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

CREATE OR ALTER PROCEDURE dbo.AssertActiveUserRole
    @Acting_user_id int,
    @AllowDirector bit = 0,
    @AllowStaff bit = 0
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Role_name nvarchar(50);
    SELECT @Role_name = r.Role_name
    FROM dbo.[User] u
    INNER JOIN dbo.Roles r ON r.Role_id = u.Role_id
    WHERE u.User_id = @Acting_user_id AND u.Is_active = 1;

    IF @Role_name IS NULL
        THROW 51100, 'The acting account does not exist or is disabled.', 1;
    IF NOT ((@AllowDirector = 1 AND @Role_name = N'Director') OR
            (@AllowStaff = 1 AND @Role_name = N'Staff'))
        THROW 51101, 'The acting account is not authorized for this action.', 1;
END;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    IF COL_LENGTH(N'dbo.Roles', N'Is_active') IS NOT NULL
    BEGIN
        DECLARE @DefaultConstraint sysname;
        SELECT @DefaultConstraint = dc.name
        FROM sys.default_constraints dc
        INNER JOIN sys.columns c
            ON c.object_id = dc.parent_object_id
            AND c.column_id = dc.parent_column_id
        WHERE c.object_id = OBJECT_ID(N'dbo.Roles')
          AND c.name = N'Is_active';

        IF @DefaultConstraint IS NOT NULL
        BEGIN
            DECLARE @DropDefault nvarchar(max) =
                N'ALTER TABLE dbo.Roles DROP CONSTRAINT ' + QUOTENAME(@DefaultConstraint);
            EXEC sys.sp_executesql @DropDefault;
        END;

        ALTER TABLE dbo.Roles DROP COLUMN Is_active;
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO
