/* =====================================================================
   EduTaskDB - rename submitted-proof tables and columns to the ERD terms.

   This is a rename-only migration. It preserves every row, identity value,
   foreign key, and the separate dbo.SubtaskProofAttachment draft table.
   Safe to run repeatedly.
   ===================================================================== */

USE EduTaskDB;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.SubtaskProofHistory', N'U') IS NOT NULL
       AND OBJECT_ID(N'dbo.ProofSubmission', N'U') IS NOT NULL
        THROW 51200, 'Both SubtaskProofHistory and ProofSubmission exist; automatic rename is unsafe.', 1;

    IF OBJECT_ID(N'dbo.SubtaskProofHistoryAttachment', N'U') IS NOT NULL
       AND OBJECT_ID(N'dbo.ProofAttachment', N'U') IS NOT NULL
        THROW 51201, 'Both SubtaskProofHistoryAttachment and ProofAttachment exist; automatic rename is unsafe.', 1;

    IF OBJECT_ID(N'dbo.SubtaskProofHistory', N'U') IS NOT NULL
        EXEC sys.sp_rename N'dbo.SubtaskProofHistory', N'ProofSubmission';

    IF OBJECT_ID(N'dbo.SubtaskProofHistoryAttachment', N'U') IS NOT NULL
        EXEC sys.sp_rename N'dbo.SubtaskProofHistoryAttachment', N'ProofAttachment';

    IF OBJECT_ID(N'dbo.ProofSubmission', N'U') IS NULL
        THROW 51202, 'ProofSubmission is missing.', 1;

    IF OBJECT_ID(N'dbo.ProofAttachment', N'U') IS NULL
        THROW 51203, 'ProofAttachment is missing.', 1;

    -- SQL Server blocks sp_rename when a CHECK constraint directly references
    -- the column. Recreate only those checks after the columns have new names.
    IF OBJECT_ID(N'dbo.CK_SubtaskProofHistory_Status', N'C') IS NOT NULL
        ALTER TABLE dbo.ProofSubmission DROP CONSTRAINT CK_SubtaskProofHistory_Status;
    IF OBJECT_ID(N'dbo.CK_SubtaskProofHistory_ContentType', N'C') IS NOT NULL
        ALTER TABLE dbo.ProofSubmission DROP CONSTRAINT CK_SubtaskProofHistory_ContentType;
    IF OBJECT_ID(N'dbo.CK_SubtaskProofHistory_FileSize', N'C') IS NOT NULL
        ALTER TABLE dbo.ProofSubmission DROP CONSTRAINT CK_SubtaskProofHistory_FileSize;
    IF OBJECT_ID(N'dbo.CK_SubtaskProofHistoryAttachment_Order', N'C') IS NOT NULL
        ALTER TABLE dbo.ProofAttachment DROP CONSTRAINT CK_SubtaskProofHistoryAttachment_Order;
    IF OBJECT_ID(N'dbo.CK_SubtaskProofHistoryAttachment_ContentType', N'C') IS NOT NULL
        ALTER TABLE dbo.ProofAttachment DROP CONSTRAINT CK_SubtaskProofHistoryAttachment_ContentType;
    IF OBJECT_ID(N'dbo.CK_SubtaskProofHistoryAttachment_FileSize', N'C') IS NOT NULL
        ALTER TABLE dbo.ProofAttachment DROP CONSTRAINT CK_SubtaskProofHistoryAttachment_FileSize;

    IF COL_LENGTH(N'dbo.ProofSubmission', N'HistoryID') IS NOT NULL
       AND COL_LENGTH(N'dbo.ProofSubmission', N'Submission_id') IS NULL
        EXEC sys.sp_rename N'dbo.ProofSubmission.HistoryID', N'Submission_id', N'COLUMN';
    IF COL_LENGTH(N'dbo.ProofSubmission', N'SubtaskID') IS NOT NULL
       AND COL_LENGTH(N'dbo.ProofSubmission', N'Subtask_id') IS NULL
        EXEC sys.sp_rename N'dbo.ProofSubmission.SubtaskID', N'Subtask_id', N'COLUMN';
    IF COL_LENGTH(N'dbo.ProofSubmission', N'AttemptNumber') IS NOT NULL
       AND COL_LENGTH(N'dbo.ProofSubmission', N'Attempt_number') IS NULL
        EXEC sys.sp_rename N'dbo.ProofSubmission.AttemptNumber', N'Attempt_number', N'COLUMN';
    IF COL_LENGTH(N'dbo.ProofSubmission', N'FileName') IS NOT NULL
       AND COL_LENGTH(N'dbo.ProofSubmission', N'File_name') IS NULL
        EXEC sys.sp_rename N'dbo.ProofSubmission.FileName', N'File_name', N'COLUMN';
    IF COL_LENGTH(N'dbo.ProofSubmission', N'ContentType') IS NOT NULL
       AND COL_LENGTH(N'dbo.ProofSubmission', N'File_type') IS NULL
        EXEC sys.sp_rename N'dbo.ProofSubmission.ContentType', N'File_type', N'COLUMN';
    IF COL_LENGTH(N'dbo.ProofSubmission', N'FileData') IS NOT NULL
       AND COL_LENGTH(N'dbo.ProofSubmission', N'Proof_file') IS NULL
        EXEC sys.sp_rename N'dbo.ProofSubmission.FileData', N'Proof_file', N'COLUMN';
    IF COL_LENGTH(N'dbo.ProofSubmission', N'ValidationStatus') IS NOT NULL
       AND COL_LENGTH(N'dbo.ProofSubmission', N'Proof_status') IS NULL
        EXEC sys.sp_rename N'dbo.ProofSubmission.ValidationStatus', N'Proof_status', N'COLUMN';
    IF COL_LENGTH(N'dbo.ProofSubmission', N'SubmittedAt') IS NOT NULL
       AND COL_LENGTH(N'dbo.ProofSubmission', N'Submitted_at') IS NULL
        EXEC sys.sp_rename N'dbo.ProofSubmission.SubmittedAt', N'Submitted_at', N'COLUMN';
    IF COL_LENGTH(N'dbo.ProofSubmission', N'SubmittedByTeacherID') IS NOT NULL
       AND COL_LENGTH(N'dbo.ProofSubmission', N'Submittedby_teacher_id') IS NULL
        EXEC sys.sp_rename N'dbo.ProofSubmission.SubmittedByTeacherID', N'Submittedby_teacher_id', N'COLUMN';
    IF COL_LENGTH(N'dbo.ProofSubmission', N'ReviewedAt') IS NOT NULL
       AND COL_LENGTH(N'dbo.ProofSubmission', N'Reviewed_at') IS NULL
        EXEC sys.sp_rename N'dbo.ProofSubmission.ReviewedAt', N'Reviewed_at', N'COLUMN';
    IF COL_LENGTH(N'dbo.ProofSubmission', N'ReviewedByUserID') IS NOT NULL
       AND COL_LENGTH(N'dbo.ProofSubmission', N'Reviewedby_user_id') IS NULL
        EXEC sys.sp_rename N'dbo.ProofSubmission.ReviewedByUserID', N'Reviewedby_user_id', N'COLUMN';
    IF COL_LENGTH(N'dbo.ProofSubmission', N'ReturnRemarks') IS NOT NULL
       AND COL_LENGTH(N'dbo.ProofSubmission', N'Return_remarks') IS NULL
        EXEC sys.sp_rename N'dbo.ProofSubmission.ReturnRemarks', N'Return_remarks', N'COLUMN';

    IF COL_LENGTH(N'dbo.ProofAttachment', N'AttachmentID') IS NOT NULL
       AND COL_LENGTH(N'dbo.ProofAttachment', N'Attachment_id') IS NULL
        EXEC sys.sp_rename N'dbo.ProofAttachment.AttachmentID', N'Attachment_id', N'COLUMN';
    IF COL_LENGTH(N'dbo.ProofAttachment', N'HistoryID') IS NOT NULL
       AND COL_LENGTH(N'dbo.ProofAttachment', N'Submission_id') IS NULL
        EXEC sys.sp_rename N'dbo.ProofAttachment.HistoryID', N'Submission_id', N'COLUMN';
    IF COL_LENGTH(N'dbo.ProofAttachment', N'SortOrder') IS NOT NULL
       AND COL_LENGTH(N'dbo.ProofAttachment', N'Sort_order') IS NULL
        EXEC sys.sp_rename N'dbo.ProofAttachment.SortOrder', N'Sort_order', N'COLUMN';
    IF COL_LENGTH(N'dbo.ProofAttachment', N'FileName') IS NOT NULL
       AND COL_LENGTH(N'dbo.ProofAttachment', N'File_name') IS NULL
        EXEC sys.sp_rename N'dbo.ProofAttachment.FileName', N'File_name', N'COLUMN';
    IF COL_LENGTH(N'dbo.ProofAttachment', N'ContentType') IS NOT NULL
       AND COL_LENGTH(N'dbo.ProofAttachment', N'File_type') IS NULL
        EXEC sys.sp_rename N'dbo.ProofAttachment.ContentType', N'File_type', N'COLUMN';
    IF COL_LENGTH(N'dbo.ProofAttachment', N'FileData') IS NOT NULL
       AND COL_LENGTH(N'dbo.ProofAttachment', N'Proof_file') IS NULL
        EXEC sys.sp_rename N'dbo.ProofAttachment.FileData', N'Proof_file', N'COLUMN';

    IF OBJECT_ID(N'dbo.CK_ProofSubmission_Status', N'C') IS NULL
        ALTER TABLE dbo.ProofSubmission WITH CHECK ADD CONSTRAINT CK_ProofSubmission_Status
            CHECK (Proof_status IN (N'Pending', N'Returned', N'Approved'));
    IF OBJECT_ID(N'dbo.CK_ProofSubmission_ContentType', N'C') IS NULL
        ALTER TABLE dbo.ProofSubmission WITH CHECK ADD CONSTRAINT CK_ProofSubmission_ContentType
            CHECK (File_type IN ('image/jpeg', 'image/png', 'application/pdf'));
    IF OBJECT_ID(N'dbo.CK_ProofSubmission_FileSize', N'C') IS NULL
        ALTER TABLE dbo.ProofSubmission WITH CHECK ADD CONSTRAINT CK_ProofSubmission_FileSize
            CHECK (DATALENGTH(Proof_file) > 0 AND DATALENGTH(Proof_file) <= 20971520);

    IF OBJECT_ID(N'dbo.CK_ProofAttachment_Order', N'C') IS NULL
        ALTER TABLE dbo.ProofAttachment WITH CHECK ADD CONSTRAINT CK_ProofAttachment_Order
            CHECK (Sort_order BETWEEN 1 AND 3);
    IF OBJECT_ID(N'dbo.CK_ProofAttachment_ContentType', N'C') IS NULL
        ALTER TABLE dbo.ProofAttachment WITH CHECK ADD CONSTRAINT CK_ProofAttachment_ContentType
            CHECK (File_type IN ('image/jpeg', 'image/png', 'application/pdf'));
    IF OBJECT_ID(N'dbo.CK_ProofAttachment_FileSize', N'C') IS NULL
        ALTER TABLE dbo.ProofAttachment WITH CHECK ADD CONSTRAINT CK_ProofAttachment_FileSize
            CHECK (DATALENGTH(Proof_file) > 0 AND DATALENGTH(Proof_file) <= 10485760);

    IF OBJECT_ID(N'dbo.PK_SubtaskProofHistory', N'PK') IS NOT NULL AND OBJECT_ID(N'dbo.PK_ProofSubmission', N'PK') IS NULL EXEC sys.sp_rename N'dbo.PK_SubtaskProofHistory', N'PK_ProofSubmission', N'OBJECT';
    IF OBJECT_ID(N'dbo.DF_SubtaskProofHistory_SubmittedAt', N'D') IS NOT NULL AND OBJECT_ID(N'dbo.DF_ProofSubmission_SubmittedAt', N'D') IS NULL EXEC sys.sp_rename N'dbo.DF_SubtaskProofHistory_SubmittedAt', N'DF_ProofSubmission_SubmittedAt', N'OBJECT';
    IF OBJECT_ID(N'dbo.FK_SubtaskProofHistory_Subtask', N'F') IS NOT NULL AND OBJECT_ID(N'dbo.FK_ProofSubmission_Subtask', N'F') IS NULL EXEC sys.sp_rename N'dbo.FK_SubtaskProofHistory_Subtask', N'FK_ProofSubmission_Subtask', N'OBJECT';
    IF OBJECT_ID(N'dbo.FK_SubtaskProofHistory_SubmittedByTeacher', N'F') IS NOT NULL AND OBJECT_ID(N'dbo.FK_ProofSubmission_SubmittedByTeacher', N'F') IS NULL EXEC sys.sp_rename N'dbo.FK_SubtaskProofHistory_SubmittedByTeacher', N'FK_ProofSubmission_SubmittedByTeacher', N'OBJECT';
    IF OBJECT_ID(N'dbo.UQ_SubtaskProofHistory_Attempt', N'UQ') IS NOT NULL AND OBJECT_ID(N'dbo.UQ_ProofSubmission_Attempt', N'UQ') IS NULL EXEC sys.sp_rename N'dbo.UQ_SubtaskProofHistory_Attempt', N'UQ_ProofSubmission_Attempt', N'OBJECT';

    IF OBJECT_ID(N'dbo.PK_SubtaskProofHistoryAttachment', N'PK') IS NOT NULL AND OBJECT_ID(N'dbo.PK_ProofAttachment', N'PK') IS NULL EXEC sys.sp_rename N'dbo.PK_SubtaskProofHistoryAttachment', N'PK_ProofAttachment', N'OBJECT';
    IF OBJECT_ID(N'dbo.FK_SubtaskProofHistoryAttachment_History', N'F') IS NOT NULL AND OBJECT_ID(N'dbo.FK_ProofAttachment_Submission', N'F') IS NULL EXEC sys.sp_rename N'dbo.FK_SubtaskProofHistoryAttachment_History', N'FK_ProofAttachment_Submission', N'OBJECT';
    IF OBJECT_ID(N'dbo.UQ_SubtaskProofHistoryAttachment_Order', N'UQ') IS NOT NULL AND OBJECT_ID(N'dbo.UQ_ProofAttachment_Order', N'UQ') IS NULL EXEC sys.sp_rename N'dbo.UQ_SubtaskProofHistoryAttachment_Order', N'UQ_ProofAttachment_Order', N'OBJECT';

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
GO

SELECT
    (SELECT COUNT(*) FROM dbo.ProofSubmission) AS SubmissionCount,
    (SELECT COUNT(*) FROM dbo.ProofAttachment) AS AttachmentCount;
GO
