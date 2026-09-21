USE [EduTaskDB];
GO

IF OBJECT_ID(N'dbo.Teacher', N'U') IS NULL
    THROW 50003, 'dbo.Teacher was not found in EduTaskDB.', 1;
GO

IF COL_LENGTH(N'dbo.Teacher', N'Username') IS NULL
    ALTER TABLE dbo.Teacher ADD Username NVARCHAR(50) NULL;
GO

IF COL_LENGTH(N'dbo.Teacher', N'Profile_photo') IS NULL
    ALTER TABLE dbo.Teacher ADD Profile_photo NVARCHAR(500) NULL;
GO

UPDATE dbo.Teacher
SET Username = CONCAT('@', LOWER(REPLACE(CONCAT(First_name, Last_name), ' ', '')))
WHERE NULLIF(LTRIM(RTRIM(Username)), '') IS NULL;
GO

SELECT Teacher_id, First_name, Last_name, Email, Contact_number,
       Username, Profile_photo
FROM dbo.Teacher
ORDER BY Teacher_id;
GO
