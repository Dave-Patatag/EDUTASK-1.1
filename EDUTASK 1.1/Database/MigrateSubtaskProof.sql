IF OBJECT_ID(N'dbo.SubtaskProof', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SubtaskProof
    (
        ProofID int IDENTITY(1,1) NOT NULL CONSTRAINT PK_SubtaskProof PRIMARY KEY,
        SubtaskID int NOT NULL,
        ImageData varbinary(max) NOT NULL,
        FileName nvarchar(255) NOT NULL,
        ContentType nvarchar(50) NOT NULL,
        ValidationStatus nvarchar(20) NOT NULL CONSTRAINT DF_SubtaskProof_ValidationStatus DEFAULT ('Pending'),
        UploadedAt datetime2 NOT NULL CONSTRAINT DF_SubtaskProof_UploadedAt DEFAULT (SYSDATETIME()),
        ReviewedAt datetime2 NULL,
        ReviewedByUserID int NULL,
        AdminRemarks nvarchar(500) NULL,
        CONSTRAINT UQ_SubtaskProof_SubtaskID UNIQUE (SubtaskID),
        CONSTRAINT FK_SubtaskProof_Subtask FOREIGN KEY (SubtaskID) REFERENCES dbo.Subtask(SubtaskID),
        CONSTRAINT CK_SubtaskProof_Status CHECK (ValidationStatus IN ('Draft', 'Pending', 'Approved', 'Returned')),
        CONSTRAINT CK_SubtaskProof_ContentType CHECK (ContentType IN ('image/jpeg', 'image/png', 'application/pdf')),
        CONSTRAINT CK_SubtaskProof_ImageSize CHECK (DATALENGTH(ImageData) > 0 AND DATALENGTH(ImageData) <= 20971520)
    );
END;

IF OBJECT_ID(N'dbo.SubtaskProofHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SubtaskProofHistory
    (
        HistoryID int IDENTITY(1,1) NOT NULL CONSTRAINT PK_SubtaskProofHistory PRIMARY KEY,
        SubtaskID int NOT NULL,
        AttemptNumber int NOT NULL,
        FileName nvarchar(255) NOT NULL,
        ContentType nvarchar(50) NOT NULL,
        FileData varbinary(max) NOT NULL,
        ValidationStatus nvarchar(20) NOT NULL,
        SubmittedAt datetime2 NOT NULL CONSTRAINT DF_SubtaskProofHistory_SubmittedAt DEFAULT (SYSDATETIME()),
        ReviewedAt datetime2 NULL,
        ReviewedByUserID int NULL,
        ReturnRemarks nvarchar(500) NULL,
        CONSTRAINT FK_SubtaskProofHistory_Subtask FOREIGN KEY (SubtaskID) REFERENCES dbo.Subtask(SubtaskID),
        CONSTRAINT UQ_SubtaskProofHistory_Attempt UNIQUE (SubtaskID, AttemptNumber),
        CONSTRAINT CK_SubtaskProofHistory_Status CHECK (ValidationStatus IN ('Pending', 'Returned', 'Approved')),
        CONSTRAINT CK_SubtaskProofHistory_ContentType CHECK (ContentType IN ('image/jpeg', 'image/png', 'application/pdf')),
        CONSTRAINT CK_SubtaskProofHistory_FileSize CHECK (DATALENGTH(FileData) > 0 AND DATALENGTH(FileData) <= 20971520)
    );
END;

-- Preserve a current submitted proof when this migration is applied to an existing database.
IF OBJECT_ID(N'dbo.SubtaskProofHistory', N'U') IS NOT NULL
BEGIN
    IF OBJECT_ID(N'dbo.CK_SubtaskProofHistory_FileSize', N'C') IS NOT NULL
       AND OBJECT_DEFINITION(OBJECT_ID(N'dbo.CK_SubtaskProofHistory_FileSize')) NOT LIKE N'%20971520%'
        ALTER TABLE dbo.SubtaskProofHistory DROP CONSTRAINT CK_SubtaskProofHistory_FileSize;

    IF OBJECT_ID(N'dbo.CK_SubtaskProofHistory_FileSize', N'C') IS NULL
        ALTER TABLE dbo.SubtaskProofHistory WITH CHECK ADD CONSTRAINT CK_SubtaskProofHistory_FileSize
            CHECK (DATALENGTH(FileData) > 0 AND DATALENGTH(FileData) <= 20971520);

    INSERT INTO dbo.SubtaskProofHistory
        (SubtaskID, AttemptNumber, FileName, ContentType, FileData, ValidationStatus,
         SubmittedAt, ReviewedAt, ReviewedByUserID, ReturnRemarks)
    SELECT p.SubtaskID, 1, p.FileName, p.ContentType, p.ImageData, p.ValidationStatus,
           p.UploadedAt, p.ReviewedAt, p.ReviewedByUserID, p.AdminRemarks
    FROM dbo.SubtaskProof p
    WHERE p.ValidationStatus IN ('Pending', 'Returned', 'Approved')
      AND NOT EXISTS
          (SELECT 1 FROM dbo.SubtaskProofHistory h WHERE h.SubtaskID = p.SubtaskID);
END;
IF OBJECT_ID(N'dbo.SubtaskProof', N'U') IS NOT NULL
BEGIN
    IF OBJECT_ID(N'dbo.CK_SubtaskProof_Status', N'C') IS NOT NULL
       AND OBJECT_DEFINITION(OBJECT_ID(N'dbo.CK_SubtaskProof_Status')) NOT LIKE N'%Draft%'
        ALTER TABLE dbo.SubtaskProof DROP CONSTRAINT CK_SubtaskProof_Status;

    IF OBJECT_ID(N'dbo.CK_SubtaskProof_Status', N'C') IS NULL
        ALTER TABLE dbo.SubtaskProof WITH CHECK ADD CONSTRAINT CK_SubtaskProof_Status
            CHECK (ValidationStatus IN ('Draft', 'Pending', 'Approved', 'Returned'));
    IF OBJECT_ID(N'dbo.CK_SubtaskProof_ContentType', N'C') IS NOT NULL
       AND OBJECT_DEFINITION(OBJECT_ID(N'dbo.CK_SubtaskProof_ContentType')) NOT LIKE N'%application/pdf%'
        ALTER TABLE dbo.SubtaskProof DROP CONSTRAINT CK_SubtaskProof_ContentType;

    IF OBJECT_ID(N'dbo.CK_SubtaskProof_ContentType', N'C') IS NULL
        ALTER TABLE dbo.SubtaskProof WITH CHECK ADD CONSTRAINT CK_SubtaskProof_ContentType
            CHECK (ContentType IN ('image/jpeg', 'image/png', 'application/pdf'));

    IF OBJECT_ID(N'dbo.CK_SubtaskProof_ImageSize', N'C') IS NOT NULL
       AND OBJECT_DEFINITION(OBJECT_ID(N'dbo.CK_SubtaskProof_ImageSize')) NOT LIKE N'%20971520%'
        ALTER TABLE dbo.SubtaskProof DROP CONSTRAINT CK_SubtaskProof_ImageSize;

    IF OBJECT_ID(N'dbo.CK_SubtaskProof_ImageSize', N'C') IS NULL
        ALTER TABLE dbo.SubtaskProof WITH CHECK ADD CONSTRAINT CK_SubtaskProof_ImageSize
            CHECK (DATALENGTH(ImageData) > 0 AND DATALENGTH(ImageData) <= 20971520);
END;
