USE [EduTaskDB];
GO

IF OBJECT_ID(N'dbo.[User]', N'U') IS NULL
    THROW 50001, 'dbo.[User] was not found in EduTaskDB.', 1;
GO

IF COL_LENGTH(N'dbo.[User]', N'Username') IS NULL
    ALTER TABLE dbo.[User] ADD Username NVARCHAR(50) NULL;
GO

IF COL_LENGTH(N'dbo.[User]', N'Profile_photo') IS NULL
    ALTER TABLE dbo.[User] ADD Profile_photo NVARCHAR(500) NULL;
GO

UPDATE dbo.[User]
SET Username = CONCAT('@', LOWER(REPLACE(CONCAT(First_name, Last_name), ' ', '')))
WHERE User_id = 1
  AND NULLIF(LTRIM(RTRIM(Username)), '') IS NULL;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.[User] WHERE User_id = 1)
    THROW 50002, 'The fixed account User_id 1 does not exist. Add that user before running the app.', 1;
GO

SELECT User_id, First_name, Last_name, Email, Contact_number,
       Username, Profile_photo
FROM dbo.[User]
WHERE User_id = 1;
GO
