/*
   EduTaskDB column naming migration
   - Keeps entity/table names unchanged.
   - Renames multiword attributes to the agreed format: First_letter_uppercase + underscores.
   - Preserves data and relationships because sp_rename keeps each column's internal ID.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

BEGIN TRY
    BEGIN TRANSACTION;

    /* Drop checks that SQL Server will not allow through a referenced-column rename. */
    IF OBJECT_ID(N'dbo.CK_NotificationState_HasState', N'C') IS NOT NULL
        ALTER TABLE dbo.NotificationState DROP CONSTRAINT CK_NotificationState_HasState;
    IF OBJECT_ID(N'dbo.CK_Subtask_ProofContentType', N'C') IS NOT NULL
        ALTER TABLE dbo.Subtask DROP CONSTRAINT CK_Subtask_ProofContentType;
    IF OBJECT_ID(N'dbo.CK_Subtask_ProofFields', N'C') IS NOT NULL
        ALTER TABLE dbo.Subtask DROP CONSTRAINT CK_Subtask_ProofFields;
    IF OBJECT_ID(N'dbo.CK_Subtask_ProofFileSize', N'C') IS NOT NULL
        ALTER TABLE dbo.Subtask DROP CONSTRAINT CK_Subtask_ProofFileSize;
    IF OBJECT_ID(N'dbo.CK_Subtask_ProofStatus', N'C') IS NOT NULL
        ALTER TABLE dbo.Subtask DROP CONSTRAINT CK_Subtask_ProofStatus;
    IF OBJECT_ID(N'dbo.CK_SubtaskProofAttachment_ContentType', N'C') IS NOT NULL
        ALTER TABLE dbo.SubtaskProofAttachment DROP CONSTRAINT CK_SubtaskProofAttachment_ContentType;
    IF OBJECT_ID(N'dbo.CK_SubtaskProofAttachment_FileSize', N'C') IS NOT NULL
        ALTER TABLE dbo.SubtaskProofAttachment DROP CONSTRAINT CK_SubtaskProofAttachment_FileSize;
    IF OBJECT_ID(N'dbo.CK_SubtaskProofAttachment_Order', N'C') IS NOT NULL
        ALTER TABLE dbo.SubtaskProofAttachment DROP CONSTRAINT CK_SubtaskProofAttachment_Order;
    IF OBJECT_ID(N'dbo.CK_TaskActivityLog_ActionType', N'C') IS NOT NULL
        ALTER TABLE dbo.TaskActivityLog DROP CONSTRAINT CK_TaskActivityLog_ActionType;
    IF OBJECT_ID(N'dbo.CK_TaskActivityLog_OneActor', N'C') IS NOT NULL
        ALTER TABLE dbo.TaskActivityLog DROP CONSTRAINT CK_TaskActivityLog_OneActor;
    IF OBJECT_ID(N'dbo.CK_TaskAssignment_CompletionStatus', N'C') IS NOT NULL
        ALTER TABLE dbo.TaskAssignment DROP CONSTRAINT CK_TaskAssignment_CompletionStatus;
    IF OBJECT_ID(N'dbo.CK_TaskDiscussion_ExactlyOneAuthor', N'C') IS NOT NULL
        ALTER TABLE dbo.TaskDiscussion DROP CONSTRAINT CK_TaskDiscussion_ExactlyOneAuthor;

    /* Non-constraint indexes block sp_rename; recreate them after the rename. */
    IF OBJECT_ID(N'dbo.FK_TaskDiscussion_Task_Subtask', N'F') IS NOT NULL ALTER TABLE dbo.TaskDiscussion DROP CONSTRAINT FK_TaskDiscussion_Task_Subtask;
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Subtask') AND name=N'IX_SubTask_TaskID') DROP INDEX IX_SubTask_TaskID ON dbo.Subtask;
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Subtask') AND name=N'UQ_Subtask_Task_Subtask') DROP INDEX UQ_Subtask_Task_Subtask ON dbo.Subtask;
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Task') AND name=N'IX_Task_CompletionApprovedByUserID') DROP INDEX IX_Task_CompletionApprovedByUserID ON dbo.[Task];
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Task') AND name=N'IX_Task_CreatedByUserID') DROP INDEX IX_Task_CreatedByUserID ON dbo.[Task];
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.TaskActivityLog') AND name=N'IX_TaskActivityLog_Task_CreatedAt') DROP INDEX IX_TaskActivityLog_Task_CreatedAt ON dbo.TaskActivityLog;
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.TaskActivityLog') AND name=N'IX_TaskActivityLog_Teacher_CreatedAt') DROP INDEX IX_TaskActivityLog_Teacher_CreatedAt ON dbo.TaskActivityLog;
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.TaskActivityLog') AND name=N'IX_TaskActivityLog_User_CreatedAt') DROP INDEX IX_TaskActivityLog_User_CreatedAt ON dbo.TaskActivityLog;
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.TaskAssignment') AND name=N'IX_TaskAssignment_Task_Teacher') DROP INDEX IX_TaskAssignment_Task_Teacher ON dbo.TaskAssignment;
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.TaskDiscussion') AND name=N'IX_TaskDiscussion_Task_Subtask') DROP INDEX IX_TaskDiscussion_Task_Subtask ON dbo.TaskDiscussion;
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Teacher') AND name=N'IX_Teacher_RoleID') DROP INDEX IX_Teacher_RoleID ON dbo.Teacher;
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.[User]') AND name=N'IX_User_RoleID') DROP INDEX IX_User_RoleID ON dbo.[User];

    /* App-owned modules contain textual column references and are recreated below. */
    IF OBJECT_ID(N'dbo.TR_TaskDiscussion_RequireSubtask', N'TR') IS NOT NULL
        DROP TRIGGER dbo.TR_TaskDiscussion_RequireSubtask;
    IF OBJECT_ID(N'dbo.ApproveTaskCompletion', N'P') IS NOT NULL
        DROP PROCEDURE dbo.ApproveTaskCompletion;
    IF OBJECT_ID(N'dbo.RequestTaskRevision', N'P') IS NOT NULL
        DROP PROCEDURE dbo.RequestTaskRevision;
    IF OBJECT_ID(N'dbo.AssertActiveUserRole', N'P') IS NOT NULL
        DROP PROCEDURE dbo.AssertActiveUserRole;

    /* AccountRoleChangeLog */
    IF COL_LENGTH(N'dbo.AccountRoleChangeLog', N'RoleChangeID') IS NOT NULL EXEC sys.sp_rename N'dbo.AccountRoleChangeLog.RoleChangeID', N'Role_change_id', N'COLUMN';
    IF COL_LENGTH(N'dbo.AccountRoleChangeLog', N'UserID') IS NOT NULL EXEC sys.sp_rename N'dbo.AccountRoleChangeLog.UserID', N'User_id', N'COLUMN';
    IF COL_LENGTH(N'dbo.AccountRoleChangeLog', N'PreviousRoleID') IS NOT NULL EXEC sys.sp_rename N'dbo.AccountRoleChangeLog.PreviousRoleID', N'Previous_role_id', N'COLUMN';
    IF COL_LENGTH(N'dbo.AccountRoleChangeLog', N'NewRoleID') IS NOT NULL EXEC sys.sp_rename N'dbo.AccountRoleChangeLog.NewRoleID', N'New_role_id', N'COLUMN';
    IF COL_LENGTH(N'dbo.AccountRoleChangeLog', N'ChangedByUserID') IS NOT NULL EXEC sys.sp_rename N'dbo.AccountRoleChangeLog.ChangedByUserID', N'Changedby_user_id', N'COLUMN';
    IF COL_LENGTH(N'dbo.AccountRoleChangeLog', N'ChangedAt') IS NOT NULL EXEC sys.sp_rename N'dbo.AccountRoleChangeLog.ChangedAt', N'Changed_at', N'COLUMN';

    /* NotificationState */
    IF COL_LENGTH(N'dbo.NotificationState', N'RecipientType') IS NOT NULL EXEC sys.sp_rename N'dbo.NotificationState.RecipientType', N'Recipient_type', N'COLUMN';
    IF COL_LENGTH(N'dbo.NotificationState', N'RecipientID') IS NOT NULL EXEC sys.sp_rename N'dbo.NotificationState.RecipientID', N'Recipient_id', N'COLUMN';
    IF COL_LENGTH(N'dbo.NotificationState', N'NotificationKey') IS NOT NULL EXEC sys.sp_rename N'dbo.NotificationState.NotificationKey', N'Notification_key', N'COLUMN';
    IF COL_LENGTH(N'dbo.NotificationState', N'ReadAt') IS NOT NULL EXEC sys.sp_rename N'dbo.NotificationState.ReadAt', N'Read_at', N'COLUMN';
    IF COL_LENGTH(N'dbo.NotificationState', N'HiddenAt') IS NOT NULL EXEC sys.sp_rename N'dbo.NotificationState.HiddenAt', N'Hidden_at', N'COLUMN';

    /* Roles */
    IF COL_LENGTH(N'dbo.Roles', N'RoleID') IS NOT NULL EXEC sys.sp_rename N'dbo.Roles.RoleID', N'Role_id', N'COLUMN';
    IF COL_LENGTH(N'dbo.Roles', N'RoleName') IS NOT NULL EXEC sys.sp_rename N'dbo.Roles.RoleName', N'Role_name', N'COLUMN';

    /* Subtask */
    IF COL_LENGTH(N'dbo.Subtask', N'SubTaskID') IS NOT NULL EXEC sys.sp_rename N'dbo.Subtask.SubTaskID', N'Subtask_id', N'COLUMN';
    IF COL_LENGTH(N'dbo.Subtask', N'TaskID') IS NOT NULL EXEC sys.sp_rename N'dbo.Subtask.TaskID', N'Task_id', N'COLUMN';
    IF COL_LENGTH(N'dbo.Subtask', N'IsCompleted') IS NOT NULL EXEC sys.sp_rename N'dbo.Subtask.IsCompleted', N'Is_completed', N'COLUMN';
    IF COL_LENGTH(N'dbo.Subtask', N'CreatedAt') IS NOT NULL EXEC sys.sp_rename N'dbo.Subtask.CreatedAt', N'Created_at', N'COLUMN';
    IF COL_LENGTH(N'dbo.Subtask', N'CompletedAt') IS NOT NULL EXEC sys.sp_rename N'dbo.Subtask.CompletedAt', N'Completed_at', N'COLUMN';
    IF COL_LENGTH(N'dbo.Subtask', N'ProofFileName') IS NOT NULL EXEC sys.sp_rename N'dbo.Subtask.ProofFileName', N'Proof_file_name', N'COLUMN';
    IF COL_LENGTH(N'dbo.Subtask', N'ProofContentType') IS NOT NULL EXEC sys.sp_rename N'dbo.Subtask.ProofContentType', N'Proof_content_type', N'COLUMN';
    IF COL_LENGTH(N'dbo.Subtask', N'ProofValidationStatus') IS NOT NULL EXEC sys.sp_rename N'dbo.Subtask.ProofValidationStatus', N'Proof_validation_status', N'COLUMN';
    IF COL_LENGTH(N'dbo.Subtask', N'ProofUploadedAt') IS NOT NULL EXEC sys.sp_rename N'dbo.Subtask.ProofUploadedAt', N'Proof_uploaded_at', N'COLUMN';
    IF COL_LENGTH(N'dbo.Subtask', N'ProofReviewedAt') IS NOT NULL EXEC sys.sp_rename N'dbo.Subtask.ProofReviewedAt', N'Proof_reviewed_at', N'COLUMN';
    IF COL_LENGTH(N'dbo.Subtask', N'ProofReviewedByUserID') IS NOT NULL EXEC sys.sp_rename N'dbo.Subtask.ProofReviewedByUserID', N'Proof_reviewedby_user_id', N'COLUMN';
    IF COL_LENGTH(N'dbo.Subtask', N'ProofSubmittedByTeacherID') IS NOT NULL EXEC sys.sp_rename N'dbo.Subtask.ProofSubmittedByTeacherID', N'Proof_submittedby_teacher_id', N'COLUMN';
    IF COL_LENGTH(N'dbo.Subtask', N'ProofAdminRemarks') IS NOT NULL EXEC sys.sp_rename N'dbo.Subtask.ProofAdminRemarks', N'Proof_admin_remarks', N'COLUMN';
    IF COL_LENGTH(N'dbo.Subtask', N'ProofFile') IS NOT NULL EXEC sys.sp_rename N'dbo.Subtask.ProofFile', N'Proof_file', N'COLUMN';

    /* Current/draft proof attachments */
    IF COL_LENGTH(N'dbo.SubtaskProofAttachment', N'AttachmentID') IS NOT NULL EXEC sys.sp_rename N'dbo.SubtaskProofAttachment.AttachmentID', N'Attachment_id', N'COLUMN';
    IF COL_LENGTH(N'dbo.SubtaskProofAttachment', N'SubtaskID') IS NOT NULL EXEC sys.sp_rename N'dbo.SubtaskProofAttachment.SubtaskID', N'Subtask_id', N'COLUMN';
    IF COL_LENGTH(N'dbo.SubtaskProofAttachment', N'SortOrder') IS NOT NULL EXEC sys.sp_rename N'dbo.SubtaskProofAttachment.SortOrder', N'Sort_order', N'COLUMN';
    IF COL_LENGTH(N'dbo.SubtaskProofAttachment', N'FileName') IS NOT NULL EXEC sys.sp_rename N'dbo.SubtaskProofAttachment.FileName', N'File_name', N'COLUMN';
    IF COL_LENGTH(N'dbo.SubtaskProofAttachment', N'ContentType') IS NOT NULL EXEC sys.sp_rename N'dbo.SubtaskProofAttachment.ContentType', N'File_type', N'COLUMN';
    IF COL_LENGTH(N'dbo.SubtaskProofAttachment', N'FileData') IS NOT NULL EXEC sys.sp_rename N'dbo.SubtaskProofAttachment.FileData', N'Proof_file', N'COLUMN';
    IF COL_LENGTH(N'dbo.SubtaskProofAttachment', N'UploadedAt') IS NOT NULL EXEC sys.sp_rename N'dbo.SubtaskProofAttachment.UploadedAt', N'Uploaded_at', N'COLUMN';

    /* Task */
    IF COL_LENGTH(N'dbo.Task', N'TaskID') IS NOT NULL EXEC sys.sp_rename N'dbo.Task.TaskID', N'Task_id', N'COLUMN';
    IF COL_LENGTH(N'dbo.Task', N'CreatedAt') IS NOT NULL EXEC sys.sp_rename N'dbo.Task.CreatedAt', N'Created_at', N'COLUMN';
    IF COL_LENGTH(N'dbo.Task', N'UserID') IS NOT NULL EXEC sys.sp_rename N'dbo.Task.UserID', N'User_id', N'COLUMN';
    IF COL_LENGTH(N'dbo.Task', N'isDailyRemind') IS NOT NULL EXEC sys.sp_rename N'dbo.Task.isDailyRemind', N'Is_daily_remind', N'COLUMN';
    IF COL_LENGTH(N'dbo.Task', N'UpdatedAt') IS NOT NULL EXEC sys.sp_rename N'dbo.Task.UpdatedAt', N'Updated_at', N'COLUMN';
    IF COL_LENGTH(N'dbo.Task', N'CreatedByUserID') IS NOT NULL EXEC sys.sp_rename N'dbo.Task.CreatedByUserID', N'Createdby_user_id', N'COLUMN';
    IF COL_LENGTH(N'dbo.Task', N'LastModifiedByUserID') IS NOT NULL EXEC sys.sp_rename N'dbo.Task.LastModifiedByUserID', N'Lastmodifiedby_user_id', N'COLUMN';
    IF COL_LENGTH(N'dbo.Task', N'CompletionApprovedByUserID') IS NOT NULL EXEC sys.sp_rename N'dbo.Task.CompletionApprovedByUserID', N'Completion_approvedby_user_id', N'COLUMN';
    IF COL_LENGTH(N'dbo.Task', N'CompletionApprovedAt') IS NOT NULL EXEC sys.sp_rename N'dbo.Task.CompletionApprovedAt', N'Completion_approved_at', N'COLUMN';
    IF COL_LENGTH(N'dbo.Task', N'RevisionRequestedByUserID') IS NOT NULL EXEC sys.sp_rename N'dbo.Task.RevisionRequestedByUserID', N'Revision_requestedby_user_id', N'COLUMN';
    IF COL_LENGTH(N'dbo.Task', N'RevisionRequestedAt') IS NOT NULL EXEC sys.sp_rename N'dbo.Task.RevisionRequestedAt', N'Revision_requested_at', N'COLUMN';
    IF COL_LENGTH(N'dbo.Task', N'RevisionReason') IS NOT NULL EXEC sys.sp_rename N'dbo.Task.RevisionReason', N'Revision_reason', N'COLUMN';

    /* TaskActivityLog */
    IF COL_LENGTH(N'dbo.TaskActivityLog', N'ActivityLogID') IS NOT NULL EXEC sys.sp_rename N'dbo.TaskActivityLog.ActivityLogID', N'Activity_log_id', N'COLUMN';
    IF COL_LENGTH(N'dbo.TaskActivityLog', N'TaskID') IS NOT NULL EXEC sys.sp_rename N'dbo.TaskActivityLog.TaskID', N'Task_id', N'COLUMN';
    IF COL_LENGTH(N'dbo.TaskActivityLog', N'PerformedByUserID') IS NOT NULL EXEC sys.sp_rename N'dbo.TaskActivityLog.PerformedByUserID', N'Performedby_user_id', N'COLUMN';
    IF COL_LENGTH(N'dbo.TaskActivityLog', N'PerformedByTeacherID') IS NOT NULL EXEC sys.sp_rename N'dbo.TaskActivityLog.PerformedByTeacherID', N'Performedby_teacher_id', N'COLUMN';
    IF COL_LENGTH(N'dbo.TaskActivityLog', N'ActionType') IS NOT NULL EXEC sys.sp_rename N'dbo.TaskActivityLog.ActionType', N'Action_type', N'COLUMN';
    IF COL_LENGTH(N'dbo.TaskActivityLog', N'PreviousStatus') IS NOT NULL EXEC sys.sp_rename N'dbo.TaskActivityLog.PreviousStatus', N'Previous_status', N'COLUMN';
    IF COL_LENGTH(N'dbo.TaskActivityLog', N'NewStatus') IS NOT NULL EXEC sys.sp_rename N'dbo.TaskActivityLog.NewStatus', N'New_status', N'COLUMN';
    IF COL_LENGTH(N'dbo.TaskActivityLog', N'CreatedAt') IS NOT NULL EXEC sys.sp_rename N'dbo.TaskActivityLog.CreatedAt', N'Created_at', N'COLUMN';

    /* TaskAssignment */
    IF COL_LENGTH(N'dbo.TaskAssignment', N'AssignmentID') IS NOT NULL EXEC sys.sp_rename N'dbo.TaskAssignment.AssignmentID', N'Assignment_id', N'COLUMN';
    IF COL_LENGTH(N'dbo.TaskAssignment', N'TaskID') IS NOT NULL EXEC sys.sp_rename N'dbo.TaskAssignment.TaskID', N'Task_id', N'COLUMN';
    IF COL_LENGTH(N'dbo.TaskAssignment', N'TeacherID') IS NOT NULL EXEC sys.sp_rename N'dbo.TaskAssignment.TeacherID', N'Teacher_id', N'COLUMN';
    IF COL_LENGTH(N'dbo.TaskAssignment', N'AssignedAt') IS NOT NULL EXEC sys.sp_rename N'dbo.TaskAssignment.AssignedAt', N'Assigned_at', N'COLUMN';
    IF COL_LENGTH(N'dbo.TaskAssignment', N'IsAcknowledged') IS NOT NULL EXEC sys.sp_rename N'dbo.TaskAssignment.IsAcknowledged', N'Is_acknowledged', N'COLUMN';
    IF COL_LENGTH(N'dbo.TaskAssignment', N'AcknowledgedAt') IS NOT NULL EXEC sys.sp_rename N'dbo.TaskAssignment.AcknowledgedAt', N'Acknowledged_at', N'COLUMN';
    IF COL_LENGTH(N'dbo.TaskAssignment', N'CompletionStatus') IS NOT NULL EXEC sys.sp_rename N'dbo.TaskAssignment.CompletionStatus', N'Completion_status', N'COLUMN';
    IF COL_LENGTH(N'dbo.TaskAssignment', N'CompletedAt') IS NOT NULL EXEC sys.sp_rename N'dbo.TaskAssignment.CompletedAt', N'Completed_at', N'COLUMN';

    /* TaskDiscussion and read state */
    IF COL_LENGTH(N'dbo.TaskDiscussion', N'DiscussionID') IS NOT NULL EXEC sys.sp_rename N'dbo.TaskDiscussion.DiscussionID', N'Discussion_id', N'COLUMN';
    IF COL_LENGTH(N'dbo.TaskDiscussion', N'TaskID') IS NOT NULL EXEC sys.sp_rename N'dbo.TaskDiscussion.TaskID', N'Task_id', N'COLUMN';
    IF COL_LENGTH(N'dbo.TaskDiscussion', N'MessageText') IS NOT NULL EXEC sys.sp_rename N'dbo.TaskDiscussion.MessageText', N'Message_text', N'COLUMN';
    IF COL_LENGTH(N'dbo.TaskDiscussion', N'CreatedAt') IS NOT NULL EXEC sys.sp_rename N'dbo.TaskDiscussion.CreatedAt', N'Created_at', N'COLUMN';
    IF COL_LENGTH(N'dbo.TaskDiscussion', N'SubtaskID') IS NOT NULL EXEC sys.sp_rename N'dbo.TaskDiscussion.SubtaskID', N'Subtask_id', N'COLUMN';
    IF COL_LENGTH(N'dbo.TaskDiscussion', N'MessageType') IS NOT NULL EXEC sys.sp_rename N'dbo.TaskDiscussion.MessageType', N'Message_type', N'COLUMN';
    IF COL_LENGTH(N'dbo.TaskDiscussion', N'UserID') IS NOT NULL EXEC sys.sp_rename N'dbo.TaskDiscussion.UserID', N'User_id', N'COLUMN';
    IF COL_LENGTH(N'dbo.TaskDiscussion', N'TeacherID') IS NOT NULL EXEC sys.sp_rename N'dbo.TaskDiscussion.TeacherID', N'Teacher_id', N'COLUMN';

    IF COL_LENGTH(N'dbo.TaskDiscussionRead', N'SubtaskID') IS NOT NULL EXEC sys.sp_rename N'dbo.TaskDiscussionRead.SubtaskID', N'Subtask_id', N'COLUMN';
    IF COL_LENGTH(N'dbo.TaskDiscussionRead', N'ReaderType') IS NOT NULL EXEC sys.sp_rename N'dbo.TaskDiscussionRead.ReaderType', N'Reader_type', N'COLUMN';
    IF COL_LENGTH(N'dbo.TaskDiscussionRead', N'ReaderID') IS NOT NULL EXEC sys.sp_rename N'dbo.TaskDiscussionRead.ReaderID', N'Reader_id', N'COLUMN';
    IF COL_LENGTH(N'dbo.TaskDiscussionRead', N'LastReadDiscussionID') IS NOT NULL EXEC sys.sp_rename N'dbo.TaskDiscussionRead.LastReadDiscussionID', N'Lastread_discussion_id', N'COLUMN';
    IF COL_LENGTH(N'dbo.TaskDiscussionRead', N'ReadAt') IS NOT NULL EXEC sys.sp_rename N'dbo.TaskDiscussionRead.ReadAt', N'Read_at', N'COLUMN';

    /* Teacher and User */
    IF COL_LENGTH(N'dbo.Teacher', N'TeacherID') IS NOT NULL EXEC sys.sp_rename N'dbo.Teacher.TeacherID', N'Teacher_id', N'COLUMN';
    IF COL_LENGTH(N'dbo.Teacher', N'FirstName') IS NOT NULL EXEC sys.sp_rename N'dbo.Teacher.FirstName', N'First_name', N'COLUMN';
    IF COL_LENGTH(N'dbo.Teacher', N'LastName') IS NOT NULL EXEC sys.sp_rename N'dbo.Teacher.LastName', N'Last_name', N'COLUMN';
    IF COL_LENGTH(N'dbo.Teacher', N'AccountCreated') IS NOT NULL EXEC sys.sp_rename N'dbo.Teacher.AccountCreated', N'Account_created', N'COLUMN';
    IF COL_LENGTH(N'dbo.Teacher', N'ContactNumber') IS NOT NULL EXEC sys.sp_rename N'dbo.Teacher.ContactNumber', N'Contact_number', N'COLUMN';
    IF COL_LENGTH(N'dbo.Teacher', N'ProfilePhotoPath') IS NOT NULL EXEC sys.sp_rename N'dbo.Teacher.ProfilePhotoPath', N'Profile_photo', N'COLUMN';
    IF COL_LENGTH(N'dbo.Teacher', N'RoleID') IS NOT NULL EXEC sys.sp_rename N'dbo.Teacher.RoleID', N'Role_id', N'COLUMN';
    IF COL_LENGTH(N'dbo.Teacher', N'IsActive') IS NOT NULL EXEC sys.sp_rename N'dbo.Teacher.IsActive', N'Is_active', N'COLUMN';
    IF COL_LENGTH(N'dbo.Teacher', N'SecurityQuestion1') IS NOT NULL EXEC sys.sp_rename N'dbo.Teacher.SecurityQuestion1', N'Security_question1', N'COLUMN';
    IF COL_LENGTH(N'dbo.Teacher', N'SecurityAnswerHash1') IS NOT NULL EXEC sys.sp_rename N'dbo.Teacher.SecurityAnswerHash1', N'Security_answer_hash1', N'COLUMN';
    IF COL_LENGTH(N'dbo.Teacher', N'SecurityQuestion2') IS NOT NULL EXEC sys.sp_rename N'dbo.Teacher.SecurityQuestion2', N'Security_question2', N'COLUMN';
    IF COL_LENGTH(N'dbo.Teacher', N'SecurityAnswerHash2') IS NOT NULL EXEC sys.sp_rename N'dbo.Teacher.SecurityAnswerHash2', N'Security_answer_hash2', N'COLUMN';
    IF COL_LENGTH(N'dbo.Teacher', N'DisabledAt') IS NOT NULL EXEC sys.sp_rename N'dbo.Teacher.DisabledAt', N'Disabled_at', N'COLUMN';

    IF COL_LENGTH(N'dbo.[User]', N'UserID') IS NOT NULL EXEC sys.sp_rename N'dbo.[User].UserID', N'User_id', N'COLUMN';
    IF COL_LENGTH(N'dbo.[User]', N'FirstName') IS NOT NULL EXEC sys.sp_rename N'dbo.[User].FirstName', N'First_name', N'COLUMN';
    IF COL_LENGTH(N'dbo.[User]', N'LastName') IS NOT NULL EXEC sys.sp_rename N'dbo.[User].LastName', N'Last_name', N'COLUMN';
    IF COL_LENGTH(N'dbo.[User]', N'AccountCreated') IS NOT NULL EXEC sys.sp_rename N'dbo.[User].AccountCreated', N'Account_created', N'COLUMN';
    IF COL_LENGTH(N'dbo.[User]', N'ContactNumber') IS NOT NULL EXEC sys.sp_rename N'dbo.[User].ContactNumber', N'Contact_number', N'COLUMN';
    IF COL_LENGTH(N'dbo.[User]', N'ProfilePhotoPath') IS NOT NULL EXEC sys.sp_rename N'dbo.[User].ProfilePhotoPath', N'Profile_photo', N'COLUMN';
    IF COL_LENGTH(N'dbo.[User]', N'RoleID') IS NOT NULL EXEC sys.sp_rename N'dbo.[User].RoleID', N'Role_id', N'COLUMN';
    IF COL_LENGTH(N'dbo.[User]', N'IsActive') IS NOT NULL EXEC sys.sp_rename N'dbo.[User].IsActive', N'Is_active', N'COLUMN';
    IF COL_LENGTH(N'dbo.[User]', N'SecurityQuestion1') IS NOT NULL EXEC sys.sp_rename N'dbo.[User].SecurityQuestion1', N'Security_question1', N'COLUMN';
    IF COL_LENGTH(N'dbo.[User]', N'SecurityAnswerHash1') IS NOT NULL EXEC sys.sp_rename N'dbo.[User].SecurityAnswerHash1', N'Security_answer_hash1', N'COLUMN';
    IF COL_LENGTH(N'dbo.[User]', N'SecurityQuestion2') IS NOT NULL EXEC sys.sp_rename N'dbo.[User].SecurityQuestion2', N'Security_question2', N'COLUMN';
    IF COL_LENGTH(N'dbo.[User]', N'SecurityAnswerHash2') IS NOT NULL EXEC sys.sp_rename N'dbo.[User].SecurityAnswerHash2', N'Security_answer_hash2', N'COLUMN';
    IF COL_LENGTH(N'dbo.[User]', N'DisabledAt') IS NOT NULL EXEC sys.sp_rename N'dbo.[User].DisabledAt', N'Disabled_at', N'COLUMN';

    /* Restore the performance and integrity indexes using the new names. */
    IF OBJECT_ID(N'dbo.Subtask', N'U') IS NOT NULL
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Subtask') AND name=N'IX_SubTask_TaskID') EXEC(N'CREATE INDEX IX_SubTask_TaskID ON dbo.Subtask(Task_id);');
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Subtask') AND name=N'UQ_Subtask_Task_Subtask') EXEC(N'CREATE UNIQUE INDEX UQ_Subtask_Task_Subtask ON dbo.Subtask(Task_id,Subtask_id);');
    END;
    IF OBJECT_ID(N'dbo.Task', N'U') IS NOT NULL
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Task') AND name=N'IX_Task_CompletionApprovedByUserID') EXEC(N'CREATE INDEX IX_Task_CompletionApprovedByUserID ON dbo.[Task](Completion_approvedby_user_id);');
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Task') AND name=N'IX_Task_CreatedByUserID') EXEC(N'CREATE INDEX IX_Task_CreatedByUserID ON dbo.[Task](Createdby_user_id);');
    END;
    IF OBJECT_ID(N'dbo.TaskActivityLog', N'U') IS NOT NULL
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.TaskActivityLog') AND name=N'IX_TaskActivityLog_Task_CreatedAt') EXEC(N'CREATE INDEX IX_TaskActivityLog_Task_CreatedAt ON dbo.TaskActivityLog(Task_id,Created_at DESC);');
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.TaskActivityLog') AND name=N'IX_TaskActivityLog_Teacher_CreatedAt') EXEC(N'CREATE INDEX IX_TaskActivityLog_Teacher_CreatedAt ON dbo.TaskActivityLog(Performedby_teacher_id,Created_at DESC) WHERE Performedby_teacher_id IS NOT NULL;');
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.TaskActivityLog') AND name=N'IX_TaskActivityLog_User_CreatedAt') EXEC(N'CREATE INDEX IX_TaskActivityLog_User_CreatedAt ON dbo.TaskActivityLog(Performedby_user_id,Created_at DESC) WHERE Performedby_user_id IS NOT NULL;');
    END;
    IF OBJECT_ID(N'dbo.TaskAssignment', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.TaskAssignment') AND name=N'IX_TaskAssignment_Task_Teacher') EXEC(N'CREATE INDEX IX_TaskAssignment_Task_Teacher ON dbo.TaskAssignment(Task_id,Teacher_id);');
    IF OBJECT_ID(N'dbo.TaskDiscussion', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.TaskDiscussion', N'Task_id') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.TaskDiscussion') AND name=N'IX_TaskDiscussion_Task_Subtask') EXEC(N'CREATE INDEX IX_TaskDiscussion_Task_Subtask ON dbo.TaskDiscussion(Task_id,Subtask_id);');
    IF OBJECT_ID(N'dbo.TaskDiscussion', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.TaskDiscussion', N'Task_id') IS NOT NULL AND OBJECT_ID(N'dbo.FK_TaskDiscussion_Task_Subtask', N'F') IS NULL EXEC(N'ALTER TABLE dbo.TaskDiscussion WITH CHECK ADD CONSTRAINT FK_TaskDiscussion_Task_Subtask FOREIGN KEY (Task_id,Subtask_id) REFERENCES dbo.Subtask(Task_id,Subtask_id);');
    IF OBJECT_ID(N'dbo.Teacher', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.Teacher') AND name=N'IX_Teacher_RoleID') EXEC(N'CREATE INDEX IX_Teacher_RoleID ON dbo.Teacher(Role_id);');
    IF OBJECT_ID(N'dbo.[User]', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'dbo.[User]') AND name=N'IX_User_RoleID') EXEC(N'CREATE INDEX IX_User_RoleID ON dbo.[User](Role_id);');

    /* Restore checks against the renamed attributes. */
    IF OBJECT_ID(N'dbo.NotificationState', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.CK_NotificationState_HasState', N'C') IS NULL
        EXEC(N'ALTER TABLE dbo.NotificationState WITH CHECK ADD CONSTRAINT CK_NotificationState_HasState CHECK (Read_at IS NOT NULL OR Hidden_at IS NOT NULL);');
    IF OBJECT_ID(N'dbo.Subtask', N'U') IS NOT NULL
    BEGIN
        EXEC(N'ALTER TABLE dbo.Subtask WITH CHECK ADD CONSTRAINT CK_Subtask_ProofContentType CHECK (Proof_content_type IS NULL OR Proof_content_type IN (''image/jpeg'',''image/png'',''application/pdf''));');
        EXEC(N'ALTER TABLE dbo.Subtask WITH CHECK ADD CONSTRAINT CK_Subtask_ProofFileSize CHECK (Proof_file IS NULL OR (DATALENGTH(Proof_file)>0 AND DATALENGTH(Proof_file)<=20971520));');
        EXEC(N'ALTER TABLE dbo.Subtask WITH CHECK ADD CONSTRAINT CK_Subtask_ProofStatus CHECK (Proof_validation_status IS NULL OR Proof_validation_status IN (N''Draft'',N''Pending'',N''Approved'',N''Returned''));');
        EXEC(N'ALTER TABLE dbo.Subtask WITH CHECK ADD CONSTRAINT CK_Subtask_ProofFields CHECK
        ((Proof_validation_status IS NULL AND Proof_file IS NULL AND Proof_file_name IS NULL AND Proof_content_type IS NULL AND Proof_uploaded_at IS NULL)
         OR (Proof_validation_status IS NOT NULL AND Proof_file IS NOT NULL AND Proof_file_name IS NOT NULL AND Proof_content_type IS NOT NULL AND Proof_uploaded_at IS NOT NULL));');
    END;
    IF OBJECT_ID(N'dbo.SubtaskProofAttachment', N'U') IS NOT NULL
    BEGIN
        EXEC(N'ALTER TABLE dbo.SubtaskProofAttachment WITH CHECK ADD CONSTRAINT CK_SubtaskProofAttachment_ContentType CHECK (File_type IN (''image/jpeg'',''image/png'',''application/pdf''));');
        EXEC(N'ALTER TABLE dbo.SubtaskProofAttachment WITH CHECK ADD CONSTRAINT CK_SubtaskProofAttachment_FileSize CHECK (DATALENGTH(Proof_file)>0 AND DATALENGTH(Proof_file)<=10485760);');
        EXEC(N'ALTER TABLE dbo.SubtaskProofAttachment WITH CHECK ADD CONSTRAINT CK_SubtaskProofAttachment_Order CHECK (Sort_order BETWEEN 1 AND 3);');
    END;
    IF OBJECT_ID(N'dbo.TaskActivityLog', N'U') IS NOT NULL
    BEGIN
        EXEC(N'ALTER TABLE dbo.TaskActivityLog WITH CHECK ADD CONSTRAINT CK_TaskActivityLog_ActionType CHECK (Action_type IN (N''TaskCreated'',N''TaskEdited'',N''TeacherAssigned'',N''TeacherUnassigned'',N''TaskAcknowledged'',N''ProofSubmitted'',N''RevisionRequested'',N''CompletionApproved'',N''TaskReopened'',N''TaskDeleted''));');
        EXEC(N'ALTER TABLE dbo.TaskActivityLog WITH CHECK ADD CONSTRAINT CK_TaskActivityLog_OneActor CHECK ((Performedby_user_id IS NOT NULL AND Performedby_teacher_id IS NULL) OR (Performedby_user_id IS NULL AND Performedby_teacher_id IS NOT NULL));');
    END;
    IF OBJECT_ID(N'dbo.TaskAssignment', N'U') IS NOT NULL AND OBJECT_ID(N'dbo.CK_TaskAssignment_CompletionStatus', N'C') IS NULL
        EXEC(N'ALTER TABLE dbo.TaskAssignment WITH CHECK ADD CONSTRAINT CK_TaskAssignment_CompletionStatus CHECK (Completion_status IN (N''Pending'',N''Acknowledged'',N''For Validation'',N''Needs Revision'',N''Completed''));');
    IF OBJECT_ID(N'dbo.TaskDiscussion', N'U') IS NOT NULL
       AND COL_LENGTH(N'dbo.TaskDiscussion', N'User_id') IS NOT NULL
       AND COL_LENGTH(N'dbo.TaskDiscussion', N'Teacher_id') IS NOT NULL
       AND OBJECT_ID(N'dbo.CK_TaskDiscussion_ExactlyOneAuthor', N'C') IS NULL
        EXEC(N'ALTER TABLE dbo.TaskDiscussion WITH CHECK ADD CONSTRAINT CK_TaskDiscussion_ExactlyOneAuthor CHECK ((User_id IS NOT NULL AND Teacher_id IS NULL) OR (User_id IS NULL AND Teacher_id IS NOT NULL));');

    EXEC(N'CREATE PROCEDURE dbo.AssertActiveUserRole
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
        IF @Role_name IS NULL THROW 51100, ''The acting account does not exist or is disabled.'', 1;
        IF NOT ((@AllowDirector = 1 AND @Role_name = N''Director'') OR (@AllowStaff = 1 AND @Role_name = N''Staff''))
            THROW 51101, ''The acting account is not authorized for this action.'', 1;
    END;');

    EXEC(N'CREATE PROCEDURE dbo.ApproveTaskCompletion
        @Task_id int,
        @Acting_user_id int
    AS
    BEGIN
        SET NOCOUNT ON; SET XACT_ABORT ON;
        EXEC dbo.AssertActiveUserRole @Acting_user_id, @AllowDirector = 1;
        BEGIN TRANSACTION;
        DECLARE @Previous_status nvarchar(50);
        SELECT TOP (1) @Previous_status = Completion_status FROM dbo.TaskAssignment WITH (UPDLOCK,HOLDLOCK) WHERE Task_id=@Task_id;
        IF @Previous_status IS NULL THROW 51110, ''The task does not exist or has no assignment.'', 1;
        IF EXISTS (SELECT 1 FROM dbo.TaskAssignment WHERE Task_id=@Task_id AND Completion_status<>N''For Validation'') THROW 51111, ''Every assignment must be For Validation before final approval.'', 1;
        IF EXISTS (SELECT 1 FROM dbo.Subtask WHERE Task_id=@Task_id AND (Proof_validation_status IS NULL OR Proof_validation_status<>N''Approved'')) THROW 51112, ''Every subtask must have approved proof before final completion.'', 1;
        UPDATE dbo.TaskAssignment SET Completion_status=N''Completed'', Completed_at=GETDATE() WHERE Task_id=@Task_id;
        UPDATE dbo.[Task] SET Completion_approvedby_user_id=@Acting_user_id WHERE Task_id=@Task_id;
        COMMIT TRANSACTION;
    END;');

    EXEC(N'CREATE PROCEDURE dbo.RequestTaskRevision
        @Task_id int,
        @Acting_user_id int,
        @Reason nvarchar(1000)
    AS
    BEGIN
        SET NOCOUNT ON; SET XACT_ABORT ON;
        IF NULLIF(LTRIM(RTRIM(@Reason)),N'''') IS NULL THROW 51120, ''A revision reason is required.'', 1;
        EXEC dbo.AssertActiveUserRole @Acting_user_id, @AllowDirector=1, @AllowStaff=1;
        BEGIN TRANSACTION;
        IF NOT EXISTS (SELECT 1 FROM dbo.TaskAssignment WITH (UPDLOCK,HOLDLOCK) WHERE Task_id=@Task_id AND Completion_status=N''For Validation'') THROW 51121, ''Only a task that is For Validation can be returned for revision.'', 1;
        UPDATE dbo.TaskAssignment SET Completion_status=N''Needs Revision'', Completed_at=NULL WHERE Task_id=@Task_id;
        UPDATE dbo.[Task] SET Revision_requestedby_user_id=@Acting_user_id, Revision_requested_at=SYSUTCDATETIME(), Revision_reason=@Reason, Lastmodifiedby_user_id=@Acting_user_id, Updated_at=SYSUTCDATETIME() WHERE Task_id=@Task_id;
        INSERT dbo.TaskActivityLog(Task_id,Performedby_user_id,Action_type,Previous_status,New_status,Details) VALUES (@Task_id,@Acting_user_id,N''RevisionRequested'',N''For Validation'',N''Needs Revision'',@Reason);
        COMMIT TRANSACTION;
    END;');

    EXEC(N'CREATE TRIGGER dbo.TR_TaskDiscussion_RequireSubtask ON dbo.TaskDiscussion AFTER INSERT,UPDATE AS
    BEGIN
        SET NOCOUNT ON;
        IF EXISTS (SELECT 1 FROM inserted WHERE Subtask_id IS NULL) OR EXISTS (SELECT 1 FROM deleted WHERE Subtask_id IS NULL)
            THROW 50005, ''New discussion messages require a subtask. Previous task discussions are read-only.'', 1;
    END;');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT t.name AS Table_name, c.column_id, c.name AS Column_name
FROM sys.tables t
JOIN sys.columns c ON c.object_id=t.object_id
WHERE t.is_ms_shipped=0 AND t.name<>N'sysdiagrams'
ORDER BY t.name,c.column_id;
