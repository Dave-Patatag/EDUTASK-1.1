USE [EduTaskDB];
GO

IF OBJECT_ID(N'dbo.[User]', N'U') IS NULL
    THROW 50001, 'dbo.[User] was not found in EduTaskDB.', 1;
GO

IF COL_LENGTH(N'dbo.[User]', N'Username') IS NULL
    ALTER TABLE dbo.[User] ADD Username NVARCHAR(50) NULL;
GO

IF COL_LENGTH(N'dbo.[User]', N'Birthdate') IS NULL
    ALTER TABLE dbo.[User] ADD Birthdate DATE NULL;
GO

IF COL_LENGTH(N'dbo.[User]', N'ProfilePhotoPath') IS NULL
    ALTER TABLE dbo.[User] ADD ProfilePhotoPath NVARCHAR(500) NULL;
GO

UPDATE dbo.[User]
SET Username = CONCAT('@', LOWER(REPLACE(CONCAT(FirstName, LastName), ' ', '')))
WHERE UserID = 1
  AND NULLIF(LTRIM(RTRIM(Username)), '') IS NULL;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.[User] WHERE UserID = 1)
    THROW 50002, 'The fixed account UserID 1 does not exist. Add that user before running the app.', 1;
GO

SELECT UserID, FirstName, LastName, Email, ContactNumber,
       Username, Birthdate, ProfilePhotoPath
FROM dbo.[User]
WHERE UserID = 1;
GO
