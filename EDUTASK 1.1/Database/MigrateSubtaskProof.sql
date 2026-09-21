/* =====================================================================
   EduTaskDB â€” merge the current proof into dbo.Subtask

   dbo.ProofSubmission remains separate because a subtask can have
   many submission attempts. Existing current-proof data and history are
   preserved before dbo.SubtaskProof is dropped. Safe to run repeatedly.
   ===================================================================== */

USE EduTaskDB;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

-- Keep existing proof bytes while aligning the column name with the ERD.
-- This must be a separate batch so later Proof_file references compile safely.
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.Subtask', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Subtask
    (
        Subtask_id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Task_id int NOT NULL,
        Title nvarchar(200) NOT NULL,
        CONSTRAINT FK_Subtask_Task FOREIGN KEY (Task_id)
            REFERENCES dbo.[Task](Task_id) ON DELETE CASCADE
    );
END;

IF COL_LENGTH(N'dbo.Subtask', N'ProofImageData') IS NOT NULL
BEGIN
    IF OBJECT_ID(N'dbo.CK_Subtask_ProofFileSize', N'C') IS NOT NULL
        ALTER TABLE dbo.Subtask DROP CONSTRAINT CK_Subtask_ProofFileSize;
    IF OBJECT_ID(N'dbo.CK_Subtask_ProofFields', N'C') IS NOT NULL
        ALTER TABLE dbo.Subtask DROP CONSTRAINT CK_Subtask_ProofFields;

    IF COL_LENGTH(N'dbo.Subtask', N'Proof_file') IS NULL
        EXEC sys.sp_rename N'dbo.Subtask.ProofImageData', N'Proof_file', N'COLUMN';
    ELSE
    BEGIN
        EXEC sys.sp_executesql N'
            UPDATE dbo.Subtask
            SET Proof_file = ProofImageData
            WHERE Proof_file IS NULL AND ProofImageData IS NOT NULL;';
        EXEC sys.sp_executesql N'ALTER TABLE dbo.Subtask DROP COLUMN ProofImageData;';
    END;
END;
IF COL_LENGTH(N'dbo.Subtask', N'Proof_file') IS NULL
    ALTER TABLE dbo.Subtask ADD Proof_file varbinary(max) NULL;

COMMIT TRANSACTION;
GO

BEGIN TRY
    BEGIN TRANSACTION;

    IF COL_LENGTH(N'dbo.Subtask', N'Proof_file_name') IS NULL
        ALTER TABLE dbo.Subtask ADD Proof_file_name nvarchar(255) NULL;
    IF COL_LENGTH(N'dbo.Subtask', N'Proof_content_type') IS NULL
        ALTER TABLE dbo.Subtask ADD Proof_content_type nvarchar(50) NULL;
    IF COL_LENGTH(N'dbo.Subtask', N'Proof_validation_status') IS NULL
        ALTER TABLE dbo.Subtask ADD Proof_validation_status nvarchar(20) NULL;
    IF COL_LENGTH(N'dbo.Subtask', N'Proof_uploaded_at') IS NULL
        ALTER TABLE dbo.Subtask ADD Proof_uploaded_at datetime2 NULL;
    IF COL_LENGTH(N'dbo.Subtask', N'Proof_submittedby_teacher_id') IS NULL
        ALTER TABLE dbo.Subtask ADD Proof_submittedby_teacher_id int NULL;

    IF OBJECT_ID(N'dbo.ProofSubmission', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ProofSubmission
        (
            Submission_id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ProofSubmission PRIMARY KEY,
            Subtask_id int NOT NULL,
            Attempt_number int NOT NULL,
            File_name nvarchar(255) NOT NULL,
            File_type nvarchar(50) NOT NULL,
            Proof_file varbinary(max) NOT NULL,
            Proof_status nvarchar(20) NOT NULL,
            Submitted_at datetime2 NOT NULL CONSTRAINT DF_ProofSubmission_SubmittedAt DEFAULT (SYSDATETIME()),
            Submittedby_teacher_id int NULL,
            Reviewed_at datetime2 NULL,
            Reviewedby_user_id int NULL,
            Return_remarks nvarchar(500) NULL,
            CONSTRAINT FK_ProofSubmission_Subtask
                FOREIGN KEY (Subtask_id) REFERENCES dbo.Subtask(Subtask_id),
            CONSTRAINT FK_ProofSubmission_SubmittedByTeacher
                FOREIGN KEY (Submittedby_teacher_id) REFERENCES dbo.Teacher(Teacher_id),
            CONSTRAINT UQ_ProofSubmission_Attempt UNIQUE (Subtask_id, Attempt_number),
            CONSTRAINT CK_ProofSubmission_Status
                CHECK (Proof_status IN ('Pending', 'Returned', 'Approved')),
            CONSTRAINT CK_ProofSubmission_ContentType
                CHECK (File_type IN ('image/jpeg', 'image/png', 'application/pdf')),
            CONSTRAINT CK_ProofSubmission_FileSize
                CHECK (DATALENGTH(Proof_file) > 0 AND DATALENGTH(Proof_file) <= 20971520)
        );
    END;

    IF COL_LENGTH(N'dbo.ProofSubmission', N'Submittedby_teacher_id') IS NULL
        ALTER TABLE dbo.ProofSubmission ADD Submittedby_teacher_id int NULL;

    IF OBJECT_ID(N'dbo.SubtaskProofAttachment', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.SubtaskProofAttachment
        (
            Attachment_id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_SubtaskProofAttachment PRIMARY KEY,
            Subtask_id int NOT NULL,
            Sort_order tinyint NOT NULL,
            File_name nvarchar(255) NOT NULL,
            File_type nvarchar(50) NOT NULL,
            Proof_file varbinary(max) NOT NULL,
            Uploaded_at datetime2 NOT NULL CONSTRAINT DF_SubtaskProofAttachment_UploadedAt DEFAULT (SYSDATETIME()),
            CONSTRAINT FK_SubtaskProofAttachment_Subtask FOREIGN KEY (Subtask_id)
                REFERENCES dbo.Subtask(Subtask_id) ON DELETE CASCADE,
            CONSTRAINT UQ_SubtaskProofAttachment_Order UNIQUE (Subtask_id, Sort_order),
            CONSTRAINT CK_SubtaskProofAttachment_Order CHECK (Sort_order BETWEEN 1 AND 3),
            CONSTRAINT CK_SubtaskProofAttachment_ContentType CHECK
                (File_type IN ('image/jpeg', 'image/png', 'application/pdf')),
            CONSTRAINT CK_SubtaskProofAttachment_FileSize CHECK
                (DATALENGTH(Proof_file) > 0 AND DATALENGTH(Proof_file) <= 10485760)
        );
    END;

    IF OBJECT_ID(N'dbo.ProofAttachment', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ProofAttachment
        (
            Attachment_id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ProofAttachment PRIMARY KEY,
            Submission_id int NOT NULL,
            Sort_order tinyint NOT NULL,
            File_name nvarchar(255) NOT NULL,
            File_type nvarchar(50) NOT NULL,
            Proof_file varbinary(max) NOT NULL,
            CONSTRAINT FK_ProofAttachment_Submission FOREIGN KEY (Submission_id)
                REFERENCES dbo.ProofSubmission(Submission_id) ON DELETE CASCADE,
            CONSTRAINT UQ_ProofAttachment_Order UNIQUE (Submission_id, Sort_order),
            CONSTRAINT CK_ProofAttachment_Order CHECK (Sort_order BETWEEN 1 AND 3),
            CONSTRAINT CK_ProofAttachment_ContentType CHECK
                (File_type IN ('image/jpeg', 'image/png', 'application/pdf')),
            CONSTRAINT CK_ProofAttachment_FileSize CHECK
                (DATALENGTH(Proof_file) > 0 AND DATALENGTH(Proof_file) <= 10485760)
        );
    END;

    IF OBJECT_ID(N'dbo.SubtaskProof', N'U') IS NOT NULL
    BEGIN
        EXEC sys.sp_executesql N'
            INSERT INTO dbo.ProofSubmission
                (Subtask_id, Attempt_number, File_name, File_type, Proof_file, Proof_status,
                 Submitted_at, Submittedby_teacher_id, Reviewed_at, Reviewedby_user_id, Return_remarks)
            SELECT p.Subtask_id, 1, p.File_name, p.File_type, p.ImageData, p.ValidationStatus,
                   p.Uploaded_at, NULL, p.ReviewedAt, p.ReviewedByUserID, p.AdminRemarks
            FROM dbo.SubtaskProof p
            WHERE p.ValidationStatus IN (''Pending'', ''Returned'', ''Approved'')
              AND NOT EXISTS
                  (SELECT 1 FROM dbo.ProofSubmission h WHERE h.Subtask_id = p.Subtask_id);

            UPDATE s
            SET Proof_file = p.ImageData,
                Proof_file_name = p.File_name,
                Proof_content_type = p.File_type,
                Proof_validation_status = p.ValidationStatus,
                Proof_uploaded_at = p.Uploaded_at
            FROM dbo.Subtask s
            INNER JOIN dbo.SubtaskProof p ON p.Subtask_id = s.Subtask_id;';

        DROP TABLE dbo.SubtaskProof;
    END;

    INSERT INTO dbo.SubtaskProofAttachment
        (Subtask_id, Sort_order, File_name, File_type, Proof_file, Uploaded_at)
    SELECT s.Subtask_id, 1, s.Proof_file_name, s.Proof_content_type,
           s.Proof_file, ISNULL(s.Proof_uploaded_at, SYSDATETIME())
    FROM dbo.Subtask s
    WHERE s.Proof_file IS NOT NULL
      AND DATALENGTH(s.Proof_file) <= 10485760
      AND NOT EXISTS
          (SELECT 1 FROM dbo.SubtaskProofAttachment a WHERE a.Subtask_id = s.Subtask_id);

    INSERT INTO dbo.ProofAttachment
        (Submission_id, Sort_order, File_name, File_type, Proof_file)
    SELECT h.Submission_id, 1, h.File_name, h.File_type, h.Proof_file
    FROM dbo.ProofSubmission h
    WHERE DATALENGTH(h.Proof_file) <= 10485760
      AND NOT EXISTS
          (SELECT 1 FROM dbo.ProofAttachment a WHERE a.Submission_id = h.Submission_id);

    IF OBJECT_ID(N'dbo.CK_Subtask_ProofStatus', N'C') IS NULL
        EXEC sys.sp_executesql N'ALTER TABLE dbo.Subtask WITH CHECK ADD CONSTRAINT CK_Subtask_ProofStatus CHECK (Proof_validation_status IS NULL OR Proof_validation_status IN (''Draft'', ''Pending'', ''Approved'', ''Returned''));';
    IF OBJECT_ID(N'dbo.CK_Subtask_ProofContentType', N'C') IS NULL
        EXEC sys.sp_executesql N'ALTER TABLE dbo.Subtask WITH CHECK ADD CONSTRAINT CK_Subtask_ProofContentType CHECK (Proof_content_type IS NULL OR Proof_content_type IN (''image/jpeg'', ''image/png'', ''application/pdf''));';
    IF OBJECT_ID(N'dbo.CK_Subtask_ProofFileSize', N'C') IS NULL
        EXEC sys.sp_executesql N'ALTER TABLE dbo.Subtask WITH CHECK ADD CONSTRAINT CK_Subtask_ProofFileSize CHECK (Proof_file IS NULL OR (DATALENGTH(Proof_file) > 0 AND DATALENGTH(Proof_file) <= 20971520));';
    IF OBJECT_ID(N'dbo.CK_Subtask_ProofFields', N'C') IS NULL
        EXEC sys.sp_executesql N'ALTER TABLE dbo.Subtask WITH CHECK ADD CONSTRAINT CK_Subtask_ProofFields CHECK ((Proof_validation_status IS NULL AND Proof_file IS NULL AND Proof_file_name IS NULL AND Proof_content_type IS NULL AND Proof_uploaded_at IS NULL) OR (Proof_validation_status IS NOT NULL AND Proof_file IS NOT NULL AND Proof_file_name IS NOT NULL AND Proof_content_type IS NOT NULL AND Proof_uploaded_at IS NOT NULL));';
    IF OBJECT_ID(N'dbo.FK_Subtask_ProofSubmittedByTeacher', N'F') IS NULL
        EXEC sys.sp_executesql N'ALTER TABLE dbo.Subtask WITH CHECK ADD CONSTRAINT FK_Subtask_ProofSubmittedByTeacher FOREIGN KEY (Proof_submittedby_teacher_id) REFERENCES dbo.Teacher(Teacher_id);';
    IF OBJECT_ID(N'dbo.FK_ProofSubmission_SubmittedByTeacher', N'F') IS NULL
        EXEC sys.sp_executesql N'ALTER TABLE dbo.ProofSubmission WITH CHECK ADD CONSTRAINT FK_ProofSubmission_SubmittedByTeacher FOREIGN KEY (Submittedby_teacher_id) REFERENCES dbo.Teacher(Teacher_id);';

    IF OBJECT_ID(N'dbo.CK_ProofSubmission_FileSize', N'C') IS NOT NULL
       AND OBJECT_DEFINITION(OBJECT_ID(N'dbo.CK_ProofSubmission_FileSize')) NOT LIKE N'%20971520%'
        ALTER TABLE dbo.ProofSubmission DROP CONSTRAINT CK_ProofSubmission_FileSize;
    IF OBJECT_ID(N'dbo.CK_ProofSubmission_FileSize', N'C') IS NULL
        ALTER TABLE dbo.ProofSubmission WITH CHECK ADD CONSTRAINT CK_ProofSubmission_FileSize
            CHECK (DATALENGTH(Proof_file) > 0 AND DATALENGTH(Proof_file) <= 20971520);

    IF OBJECT_ID(N'dbo.ApproveTaskCompletion', N'P') IS NOT NULL
        EXEC sys.sp_executesql N'
            ALTER PROCEDURE dbo.ApproveTaskCompletion
                @Task_id int,
                @Acting_user_id int
            AS
            BEGIN
                SET NOCOUNT ON; SET XACT_ABORT ON;
                EXEC dbo.AssertActiveUserRole @Acting_user_id, @AllowDirector = 1;
                BEGIN TRANSACTION;
                DECLARE @Previous_status nvarchar(50);
                SELECT TOP (1) @Previous_status = Completion_status
                FROM dbo.TaskAssignment WITH (UPDLOCK, HOLDLOCK)
                WHERE Task_id = @Task_id;
                IF @Previous_status IS NULL THROW 51110, ''The task does not exist or has no assignment.'', 1;
                IF EXISTS (SELECT 1 FROM dbo.TaskAssignment WHERE Task_id = @Task_id AND Completion_status <> N''For Validation'')
                    THROW 51111, ''Every assignment must be For Validation before final approval.'', 1;
                IF EXISTS
                (
                    SELECT 1 FROM dbo.Subtask s
                    WHERE s.Task_id = @Task_id
                      AND (s.Proof_validation_status IS NULL OR s.Proof_validation_status <> N''Approved'')
                ) THROW 51112, ''Every subtask must have approved proof before final completion.'', 1;
                UPDATE dbo.TaskAssignment
                SET Completion_status = N''Completed'', Completed_at = GETDATE()
                WHERE Task_id = @Task_id;
                UPDATE dbo.[Task]
                SET Completion_approvedby_user_id = @Acting_user_id
                WHERE Task_id = @Task_id;
                COMMIT TRANSACTION;
            END;';

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

SELECT OBJECT_ID(N'dbo.SubtaskProof', N'U') AS RemovedSubtaskProofObjectID;
SELECT name AS SubtaskProofColumn
FROM sys.columns
WHERE object_id = OBJECT_ID(N'dbo.Subtask')
  AND name LIKE N'Proof%'
ORDER BY column_id;
GO
