/* =====================================================================
   EduTaskDB — merge notification read/hidden state

   Combines dbo.NotificationRead and dbo.NotificationHidden into one
   dbo.NotificationState row per recipient and generated notification.
   Existing Read_at and Hidden_at timestamps are preserved.
   Safe to run repeatedly.
   ===================================================================== */

USE EduTaskDB;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF COL_LENGTH(N'dbo.NotificationState', N'NtificationKey') IS NOT NULL
       AND COL_LENGTH(N'dbo.NotificationState', N'Notification_key') IS NULL
        EXEC sys.sp_rename N'dbo.NotificationState.NtificationKey', N'Notification_key', N'COLUMN';

    IF OBJECT_ID(N'dbo.NotificationState', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.NotificationState
        (
            Recipient_type nvarchar(10) NOT NULL,
            Recipient_id int NOT NULL,
            Notification_key nvarchar(100) NOT NULL,
            Read_at datetime2 NULL,
            Hidden_at datetime2 NULL,
            CONSTRAINT PK_NotificationState
                PRIMARY KEY (Recipient_type, Recipient_id, Notification_key),
            CONSTRAINT CK_NotificationState_HasState
                CHECK (Read_at IS NOT NULL OR Hidden_at IS NOT NULL)
        );
    END;

    IF OBJECT_ID(N'dbo.NotificationRead', N'U') IS NOT NULL
    BEGIN
        EXEC sys.sp_executesql N'
            MERGE dbo.NotificationState WITH (HOLDLOCK) AS target
            USING dbo.NotificationRead AS source
              ON target.Recipient_type = source.Recipient_type
             AND target.Recipient_id = source.Recipient_id
             AND target.Notification_key = source.Notification_key
            WHEN MATCHED AND target.Read_at IS NULL THEN
                UPDATE SET Read_at = source.Read_at
            WHEN NOT MATCHED THEN
                INSERT (Recipient_type, Recipient_id, Notification_key, Read_at, Hidden_at)
                VALUES (source.Recipient_type, source.Recipient_id, source.Notification_key, source.Read_at, NULL);';

        DROP TABLE dbo.NotificationRead;
    END;

    IF OBJECT_ID(N'dbo.NotificationHidden', N'U') IS NOT NULL
    BEGIN
        EXEC sys.sp_executesql N'
            MERGE dbo.NotificationState WITH (HOLDLOCK) AS target
            USING dbo.NotificationHidden AS source
              ON target.Recipient_type = source.Recipient_type
             AND target.Recipient_id = source.Recipient_id
             AND target.Notification_key = source.Notification_key
            WHEN MATCHED AND target.Hidden_at IS NULL THEN
                UPDATE SET Hidden_at = source.Hidden_at
            WHEN NOT MATCHED THEN
                INSERT (Recipient_type, Recipient_id, Notification_key, Read_at, Hidden_at)
                VALUES (source.Recipient_type, source.Recipient_id, source.Notification_key, NULL, source.Hidden_at);';

        DROP TABLE dbo.NotificationHidden;
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

SELECT
    OBJECT_ID(N'dbo.NotificationState', N'U') AS NotificationStateObjectID,
    OBJECT_ID(N'dbo.NotificationRead', N'U') AS RemovedNotificationReadObjectID,
    OBJECT_ID(N'dbo.NotificationHidden', N'U') AS RemovedNotificationHiddenObjectID;
GO
