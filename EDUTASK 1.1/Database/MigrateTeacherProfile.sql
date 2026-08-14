USE [EduTaskDB];
GO

IF OBJECT_ID(N'dbo.Teacher', N'U') IS NULL
    THROW 50003, 'dbo.Teacher was not found in EduTaskDB.', 1;
GO

IF COL_LENGTH(N'dbo.Teacher', N'Username') IS NULL
    ALTER TABLE dbo.Teacher ADD Username NVARCHAR(50) NULL;
GO

IF COL_LENGTH(N'dbo.Teacher', N'Birthdate') IS NULL
    ALTER TABLE dbo.Teacher ADD Birthdate DATE NULL;
GO

IF COL_LENGTH(N'dbo.Teacher', N'ProfilePhotoPath') IS NULL
    ALTER TABLE dbo.Teacher ADD ProfilePhotoPath NVARCHAR(500) NULL;
GO

UPDATE dbo.Teacher
SET Username = CONCAT('@', LOWER(REPLACE(CONCAT(FirstName, LastName), ' ', '')))
WHERE NULLIF(LTRIM(RTRIM(Username)), '') IS NULL;
GO

SELECT TeacherID, FirstName, LastName, Email, ContactNumber,
       Username, Birthdate, ProfilePhotoPath
FROM dbo.Teacher
ORDER BY TeacherID;
GO
