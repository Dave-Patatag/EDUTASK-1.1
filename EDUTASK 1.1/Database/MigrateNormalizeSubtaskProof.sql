/*
   Normalizes proof storage to match the ERD:
     Subtask          = Subtask_id, Task_id, Title
     ProofSubmission  = one draft/submission attempt and its review state
     ProofAttachment  = one-to-many files for a submission

   Legacy proof columns and the separate draft-attachment table are migrated
   before they are removed. Safe to run repeatedly.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @SchemaLockResult int;
    EXEC @SchemaLockResult = sys.sp_getapplock
        @Resource = N'EduTask.NormalizeSubtaskProof',
        @LockMode = N'Exclusive',
        @LockOwner = N'Transaction',
        @LockTimeout = 15000;
    IF @SchemaLockResult < 0
        THROW 51300, 'The proof schema could not be locked for migration.', 1;

    IF OBJECT_ID(N'dbo.Subtask', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Subtask
        (
            Subtask_id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Subtask PRIMARY KEY,
            Task_id int NOT NULL,
            Title nvarchar(200) NOT NULL,
            CONSTRAINT FK_Subtask_Task FOREIGN KEY (Task_id)
                REFERENCES dbo.[Task](Task_id) ON DELETE CASCADE
        );
    END;

    IF OBJECT_ID(N'dbo.SubtaskProofHistory', N'U') IS NOT NULL
       AND OBJECT_ID(N'dbo.ProofSubmission', N'U') IS NULL
        EXEC sys.sp_rename N'dbo.SubtaskProofHistory', N'ProofSubmission';

    IF OBJECT_ID(N'dbo.SubtaskProofHistoryAttachment', N'U') IS NOT NULL
       AND OBJECT_ID(N'dbo.ProofAttachment', N'U') IS NULL
        EXEC sys.sp_rename N'dbo.SubtaskProofHistoryAttachment', N'ProofAttachment';

    IF OBJECT_ID(N'dbo.ProofSubmission', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ProofSubmission
        (
            Submission_id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ProofSubmission PRIMARY KEY,
            Subtask_id int NOT NULL,
            Attempt_number int NOT NULL,
            Proof_status nvarchar(20) NOT NULL,
            Submitted_at datetime2 NOT NULL CONSTRAINT DF_ProofSubmission_SubmittedAt DEFAULT (SYSDATETIME()),
            Submittedby_teacher_id int NULL,
            Reviewed_at datetime2 NULL,
            Reviewedby_user_id int NULL,
            Return_remarks nvarchar(500) NULL,
            CONSTRAINT FK_ProofSubmission_Subtask FOREIGN KEY (Subtask_id)
                REFERENCES dbo.Subtask(Subtask_id) ON DELETE CASCADE,
            CONSTRAINT FK_ProofSubmission_SubmittedByTeacher FOREIGN KEY (Submittedby_teacher_id)
                REFERENCES dbo.Teacher(Teacher_id),
            CONSTRAINT FK_ProofSubmission_ReviewedByUser FOREIGN KEY (Reviewedby_user_id)
                REFERENCES dbo.[User](User_id),
            CONSTRAINT UQ_ProofSubmission_Attempt UNIQUE (Subtask_id, Attempt_number)
        );
    END;

    IF OBJECT_ID(N'dbo.ProofAttachment', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.ProofAttachment
        (
            Submission_id int NOT NULL,
            Sort_order tinyint NOT NULL,
            File_name nvarchar(255) NOT NULL,
            File_type nvarchar(50) NOT NULL,
            Proof_file varbinary(max) NOT NULL,
            CONSTRAINT PK_ProofAttachment PRIMARY KEY (Submission_id, Sort_order),
            CONSTRAINT FK_ProofAttachment_Submission FOREIGN KEY (Submission_id)
                REFERENCES dbo.ProofSubmission(Submission_id) ON DELETE CASCADE
        );
    END;

    /* ProofAttachment is identified by its owner and position. Convert older
       surrogate-key versions to the weak-entity composite primary key. */
    IF COL_LENGTH(N'dbo.ProofAttachment', N'Attachment_id') IS NOT NULL
    BEGIN
        DECLARE @AttachmentPrimaryKey sysname;
        DECLARE @DropAttachmentPrimaryKeySql nvarchar(max);
        SELECT @AttachmentPrimaryKey = kc.name
        FROM sys.key_constraints kc
        INNER JOIN sys.index_columns ic
            ON ic.object_id = kc.parent_object_id
           AND ic.index_id = kc.unique_index_id
        INNER JOIN sys.columns c
            ON c.object_id = ic.object_id
           AND c.column_id = ic.column_id
        WHERE kc.parent_object_id = OBJECT_ID(N'dbo.ProofAttachment')
          AND kc.type = N'PK'
          AND c.name = N'Attachment_id';

        IF @AttachmentPrimaryKey IS NOT NULL
        BEGIN
            SET @DropAttachmentPrimaryKeySql = N'ALTER TABLE dbo.ProofAttachment DROP CONSTRAINT '
                + QUOTENAME(@AttachmentPrimaryKey) + N';';
            EXEC sys.sp_executesql @DropAttachmentPrimaryKeySql;
        END;

        ALTER TABLE dbo.ProofAttachment DROP COLUMN Attachment_id;
    END;

    IF OBJECT_ID(N'dbo.UQ_ProofAttachment_Order', N'UQ') IS NOT NULL
        ALTER TABLE dbo.ProofAttachment DROP CONSTRAINT UQ_ProofAttachment_Order;
    IF OBJECT_ID(N'dbo.UQ_SubtaskProofHistoryAttachment_Order', N'UQ') IS NOT NULL
        ALTER TABLE dbo.ProofAttachment DROP CONSTRAINT UQ_SubtaskProofHistoryAttachment_Order;

    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.key_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.ProofAttachment')
          AND type = N'PK'
    )
        ALTER TABLE dbo.ProofAttachment ADD CONSTRAINT PK_ProofAttachment
            PRIMARY KEY (Submission_id, Sort_order);

    /* Permit a submission row to represent the pre-confirmation draft. */
    DECLARE @ConstraintName sysname;
    DECLARE @Sql nvarchar(max);
    DECLARE ConstraintCursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT cc.name
        FROM sys.check_constraints cc
        WHERE cc.parent_object_id = OBJECT_ID(N'dbo.ProofSubmission')
          AND cc.definition LIKE N'%Proof_status%';
    OPEN ConstraintCursor;
    FETCH NEXT FROM ConstraintCursor INTO @ConstraintName;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @Sql = N'ALTER TABLE dbo.ProofSubmission DROP CONSTRAINT ' + QUOTENAME(@ConstraintName) + N';';
        EXEC sys.sp_executesql @Sql;
        FETCH NEXT FROM ConstraintCursor INTO @ConstraintName;
    END;
    CLOSE ConstraintCursor;
    DEALLOCATE ConstraintCursor;

    IF OBJECT_ID(N'dbo.CK_ProofSubmission_Status', N'C') IS NULL
        ALTER TABLE dbo.ProofSubmission WITH CHECK ADD CONSTRAINT CK_ProofSubmission_Status
            CHECK (Proof_status IN (N'Draft', N'Pending', N'Returned', N'Approved'));
    IF OBJECT_ID(N'dbo.CK_ProofAttachment_Order', N'C') IS NULL
        ALTER TABLE dbo.ProofAttachment WITH CHECK ADD CONSTRAINT CK_ProofAttachment_Order
            CHECK (Sort_order BETWEEN 1 AND 3);
    IF OBJECT_ID(N'dbo.CK_ProofAttachment_ContentType', N'C') IS NULL
        ALTER TABLE dbo.ProofAttachment WITH CHECK ADD CONSTRAINT CK_ProofAttachment_ContentType
            CHECK (File_type IN ('image/jpeg', 'image/png', 'application/pdf'));
    IF OBJECT_ID(N'dbo.CK_ProofAttachment_FileSize', N'C') IS NULL
        ALTER TABLE dbo.ProofAttachment WITH CHECK ADD CONSTRAINT CK_ProofAttachment_FileSize
            CHECK (DATALENGTH(Proof_file) > 0 AND DATALENGTH(Proof_file) <= 10485760);
    IF OBJECT_ID(N'dbo.FK_ProofSubmission_SubmittedByTeacher', N'F') IS NULL
        ALTER TABLE dbo.ProofSubmission WITH CHECK ADD CONSTRAINT FK_ProofSubmission_SubmittedByTeacher
            FOREIGN KEY (Submittedby_teacher_id) REFERENCES dbo.Teacher(Teacher_id);
    IF OBJECT_ID(N'dbo.FK_ProofSubmission_ReviewedByUser', N'F') IS NULL
        ALTER TABLE dbo.ProofSubmission WITH CHECK ADD CONSTRAINT FK_ProofSubmission_ReviewedByUser
            FOREIGN KEY (Reviewedby_user_id) REFERENCES dbo.[User](User_id);

    /* Preserve a legacy current proof that has no submission row yet. */
    IF COL_LENGTH(N'dbo.Subtask', N'Proof_validation_status') IS NOT NULL
       AND COL_LENGTH(N'dbo.Subtask', N'Proof_file') IS NOT NULL
    BEGIN
        EXEC sys.sp_executesql N'
            INSERT INTO dbo.ProofSubmission
                (Subtask_id, Attempt_number, Proof_status, Submitted_at, Submittedby_teacher_id,
                 Reviewed_at, Reviewedby_user_id, Return_remarks)
            SELECT s.Subtask_id,
                   ISNULL((SELECT MAX(x.Attempt_number) FROM dbo.ProofSubmission x
                           WHERE x.Subtask_id = s.Subtask_id), 0) + 1,
                   s.Proof_validation_status, ISNULL(s.Proof_uploaded_at, SYSDATETIME()),
                   s.Proof_submittedby_teacher_id, s.Proof_reviewed_at,
                   s.Proof_reviewedby_user_id, s.Proof_admin_remarks
            FROM dbo.Subtask s
            OUTER APPLY
            (
                SELECT TOP (1) h.Proof_status
                FROM dbo.ProofSubmission h
                WHERE h.Subtask_id = s.Subtask_id
                ORDER BY h.Attempt_number DESC, h.Submission_id DESC
            ) latest
            WHERE s.Proof_validation_status IS NOT NULL
              AND s.Proof_file IS NOT NULL
              AND (latest.Proof_status IS NULL
                   OR (s.Proof_validation_status = N''Draft'' AND latest.Proof_status <> N''Draft''));

            INSERT INTO dbo.ProofAttachment
                (Submission_id, Sort_order, File_name, File_type, Proof_file)
            SELECT latestSubmission.Submission_id, 1, s.Proof_file_name,
                   s.Proof_content_type, s.Proof_file
            FROM dbo.Subtask s
            CROSS APPLY
            (
                SELECT TOP (1) h.Submission_id
                FROM dbo.ProofSubmission h
                WHERE h.Subtask_id = s.Subtask_id
                ORDER BY h.Attempt_number DESC, h.Submission_id DESC
            ) latestSubmission
            WHERE s.Proof_file IS NOT NULL
              AND NOT EXISTS
                  (SELECT 1 FROM dbo.ProofAttachment a
                   WHERE a.Submission_id = latestSubmission.Submission_id);';
    END;

    /* If only the legacy draft table has data, create its submission first. */
    IF OBJECT_ID(N'dbo.SubtaskProofAttachment', N'U') IS NOT NULL
    BEGIN
        EXEC sys.sp_executesql N'
            INSERT INTO dbo.ProofSubmission
                (Subtask_id, Attempt_number, Proof_status, Submitted_at, Submittedby_teacher_id)
            SELECT a.Subtask_id, 1, N''Draft'', a.Uploaded_at, NULL
            FROM dbo.SubtaskProofAttachment a
            WHERE a.Sort_order = 1
              AND NOT EXISTS
                  (SELECT 1 FROM dbo.ProofSubmission h WHERE h.Subtask_id = a.Subtask_id);';

        /* Move all legacy draft/current files to the latest submission. */
        EXEC sys.sp_executesql N'
            INSERT INTO dbo.ProofAttachment
                (Submission_id, Sort_order, File_name, File_type, Proof_file)
            SELECT target.Submission_id, a.Sort_order, a.File_name, a.File_type, a.Proof_file
            FROM dbo.SubtaskProofAttachment a
            CROSS APPLY
            (
                SELECT TOP (1) h.Submission_id
                FROM dbo.ProofSubmission h
                WHERE h.Subtask_id = a.Subtask_id
                ORDER BY h.Attempt_number DESC, h.Submission_id DESC
            ) target
            WHERE NOT EXISTS
            (
                SELECT 1 FROM dbo.ProofAttachment pa
                WHERE pa.Submission_id = target.Submission_id
                  AND pa.Sort_order = a.Sort_order
            );';
        DROP TABLE dbo.SubtaskProofAttachment;
    END;

    /* Move the legacy primary file into the attachment table before dropping
       the duplicated ProofSubmission columns. Dynamic SQL keeps this migration
       repeatable after those columns no longer exist. */
    IF COL_LENGTH(N'dbo.ProofSubmission', N'File_name') IS NOT NULL
       AND COL_LENGTH(N'dbo.ProofSubmission', N'File_type') IS NOT NULL
       AND COL_LENGTH(N'dbo.ProofSubmission', N'Proof_file') IS NOT NULL
        EXEC sys.sp_executesql N'
            INSERT INTO dbo.ProofAttachment
                (Submission_id, Sort_order, File_name, File_type, Proof_file)
            SELECT h.Submission_id, 1, h.File_name, h.File_type, h.Proof_file
            FROM dbo.ProofSubmission h
            WHERE NOT EXISTS
            (
                SELECT 1 FROM dbo.ProofAttachment a
                WHERE a.Submission_id = h.Submission_id
            );';

    IF EXISTS
    (
        SELECT 1 FROM dbo.ProofSubmission s
        WHERE NOT EXISTS
            (SELECT 1 FROM dbo.ProofAttachment a WHERE a.Submission_id=s.Submission_id)
    )
        THROW 51501, 'A proof submission has no attachment; file columns cannot be removed safely.', 1;

    IF OBJECT_ID(N'dbo.CK_ProofSubmission_ContentType', N'C') IS NOT NULL
        ALTER TABLE dbo.ProofSubmission DROP CONSTRAINT CK_ProofSubmission_ContentType;
    IF OBJECT_ID(N'dbo.CK_ProofSubmission_FileSize', N'C') IS NOT NULL
        ALTER TABLE dbo.ProofSubmission DROP CONSTRAINT CK_ProofSubmission_FileSize;
    IF COL_LENGTH(N'dbo.ProofSubmission', N'File_name') IS NOT NULL
        ALTER TABLE dbo.ProofSubmission DROP COLUMN File_name;
    IF COL_LENGTH(N'dbo.ProofSubmission', N'File_type') IS NOT NULL
        ALTER TABLE dbo.ProofSubmission DROP COLUMN File_type;
    IF COL_LENGTH(N'dbo.ProofSubmission', N'Proof_file') IS NOT NULL
        ALTER TABLE dbo.ProofSubmission DROP COLUMN Proof_file;

    /* Remove every dependency on the legacy Subtask columns. */
    DECLARE DependencyCursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT DISTINCT o.name
        FROM
        (
            SELECT fk.name
            FROM sys.foreign_keys fk
            INNER JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
            WHERE fk.parent_object_id = OBJECT_ID(N'dbo.Subtask')
              AND COL_NAME(fkc.parent_object_id, fkc.parent_column_id) IN
                  (N'Is_completed', N'Created_at', N'Completed_at', N'Proof_file_name',
                   N'Proof_content_type', N'Proof_validation_status', N'Proof_uploaded_at',
                   N'Proof_reviewed_at', N'Proof_reviewedby_user_id',
                   N'Proof_submittedby_teacher_id', N'Proof_admin_remarks', N'Proof_file')
            UNION
            SELECT cc.name
            FROM sys.check_constraints cc
            WHERE cc.parent_object_id = OBJECT_ID(N'dbo.Subtask')
            UNION
            SELECT dc.name
            FROM sys.default_constraints dc
            INNER JOIN sys.columns c ON c.object_id = dc.parent_object_id
                AND c.column_id = dc.parent_column_id
            WHERE dc.parent_object_id = OBJECT_ID(N'dbo.Subtask')
              AND c.name IN
                  (N'Is_completed', N'Created_at', N'Completed_at', N'Proof_file_name',
                   N'Proof_content_type', N'Proof_validation_status', N'Proof_uploaded_at',
                   N'Proof_reviewed_at', N'Proof_reviewedby_user_id',
                   N'Proof_submittedby_teacher_id', N'Proof_admin_remarks', N'Proof_file')
        ) o;
    OPEN DependencyCursor;
    FETCH NEXT FROM DependencyCursor INTO @ConstraintName;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @Sql = N'ALTER TABLE dbo.Subtask DROP CONSTRAINT ' + QUOTENAME(@ConstraintName) + N';';
        EXEC sys.sp_executesql @Sql;
        FETCH NEXT FROM DependencyCursor INTO @ConstraintName;
    END;
    CLOSE DependencyCursor;
    DEALLOCATE DependencyCursor;

    DECLARE @ColumnName sysname;
    DECLARE ColumnCursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT name
        FROM sys.columns
        WHERE object_id = OBJECT_ID(N'dbo.Subtask')
          AND name IN
              (N'Is_completed', N'Created_at', N'Completed_at', N'Proof_file_name',
               N'Proof_content_type', N'Proof_validation_status', N'Proof_uploaded_at',
               N'Proof_reviewed_at', N'Proof_reviewedby_user_id',
               N'Proof_submittedby_teacher_id', N'Proof_admin_remarks', N'Proof_file');
    OPEN ColumnCursor;
    FETCH NEXT FROM ColumnCursor INTO @ColumnName;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @Sql = N'ALTER TABLE dbo.Subtask DROP COLUMN ' + QUOTENAME(@ColumnName) + N';';
        EXEC sys.sp_executesql @Sql;
        FETCH NEXT FROM ColumnCursor INTO @ColumnName;
    END;
    CLOSE ColumnCursor;
    DEALLOCATE ColumnCursor;

    EXEC sys.sp_executesql N'
        CREATE OR ALTER PROCEDURE dbo.ApproveTaskCompletion
            @Task_id int,
            @Acting_user_id int
        AS
        BEGIN
            SET NOCOUNT ON; SET XACT_ABORT ON;
            EXEC dbo.AssertActiveUserRole @Acting_user_id, @AllowDirector = 1;
            BEGIN TRANSACTION;
            IF NOT EXISTS (SELECT 1 FROM dbo.TaskAssignment WITH (UPDLOCK, HOLDLOCK) WHERE Task_id = @Task_id)
                THROW 51110, ''The task does not exist or has no assignment.'', 1;
            IF EXISTS (SELECT 1 FROM dbo.TaskAssignment WHERE Task_id = @Task_id AND Completion_status <> N''For Validation'')
                THROW 51111, ''Every assignment must be For Validation before final approval.'', 1;
            IF EXISTS
            (
                SELECT 1
                FROM dbo.Subtask s
                OUTER APPLY
                (
                    SELECT TOP (1) h.Proof_status
                    FROM dbo.ProofSubmission h
                    WHERE h.Subtask_id = s.Subtask_id
                    ORDER BY h.Attempt_number DESC, h.Submission_id DESC
                ) latest
                WHERE s.Task_id = @Task_id
                  AND ISNULL(latest.Proof_status, N'''') <> N''Approved''
            ) THROW 51112, ''Every subtask must have approved proof before final completion.'', 1;
            UPDATE dbo.TaskAssignment
            SET Completion_status = N''Completed'', Completed_at = GETDATE()
            WHERE Task_id = @Task_id;
            UPDATE dbo.[Task]
            SET Approvedby_user_id = @Acting_user_id
            WHERE Task_id = @Task_id;
            COMMIT TRANSACTION;
        END;';

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF CURSOR_STATUS('local', 'ConstraintCursor') >= 0 CLOSE ConstraintCursor;
    IF CURSOR_STATUS('local', 'ConstraintCursor') >= -1 DEALLOCATE ConstraintCursor;
    IF CURSOR_STATUS('local', 'DependencyCursor') >= 0 CLOSE DependencyCursor;
    IF CURSOR_STATUS('local', 'DependencyCursor') >= -1 DEALLOCATE DependencyCursor;
    IF CURSOR_STATUS('local', 'ColumnCursor') >= 0 CLOSE ColumnCursor;
    IF CURSOR_STATUS('local', 'ColumnCursor') >= -1 DEALLOCATE ColumnCursor;
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
