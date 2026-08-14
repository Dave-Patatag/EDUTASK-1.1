using Microsoft.Data.SqlClient;
using System.Data;
using EDUTASK_1._1.Models;
using SubtaskDraft = EDUTASK_1._1.Models.SubtaskDraft;
using TaskCommentItem = EDUTASK_1._1.Models.TaskCommentItem;
using SubtaskDisplayItem = EDUTASK_1._1.Models.SubtaskDisplayItem;

namespace EDUTASK_1._1.Services;

public class DatabaseService
{
    private const string ConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=EduTaskDB;Trusted_Connection=True;TrustServerCertificate=True;";

    public SqlConnection GetConnection() => new(ConnectionString);

    public async Task<User?> GetUserByIdAsync(
        int userID,
        CancellationToken cancellationToken = default)
    {
        const string query = """
            SELECT u.UserID, u.FirstName, u.LastName, u.Email, u.Password, u.ContactNumber,
                   u.AccountCreated, u.Username, u.Birthdate, u.ProfilePhotoPath,
                   u.RoleID, r.RoleName, u.IsActive
            FROM dbo.[User] u
            INNER JOIN dbo.Roles r ON r.RoleID = u.RoleID
            WHERE u.UserID = @UserID
            """;

        DataTable table = await ExecuteQueryAsync(
            query,
            [new SqlParameter("@UserID", SqlDbType.Int) { Value = userID }],
            cancellationToken);

        if (table.Rows.Count == 0)
            return null;

        DataRow row = table.Rows[0];
        return new User
        {
            UserID = row.Field<int>("UserID"),
            FirstName = row.Field<string>("FirstName") ?? string.Empty,
            LastName = row.Field<string>("LastName") ?? string.Empty,
            Email = row.Field<string>("Email") ?? string.Empty,
            Password = row.Field<string>("Password") ?? string.Empty,
            ContactNumber = row.Field<string>("ContactNumber") ?? string.Empty,
            AccountCreated = row.Field<DateTime>("AccountCreated"),
            Username = row.Field<string>("Username") ?? string.Empty,
            Birthdate = row.IsNull("Birthdate") ? null : row.Field<DateTime>("Birthdate"),
            ProfilePhotoPath = row.Field<string>("ProfilePhotoPath") ?? string.Empty,
            RoleID = row.Field<int>("RoleID"),
            RoleName = row.Field<string>("RoleName") ?? string.Empty,
            IsActive = row.Field<bool>("IsActive")
        };
    }

    public async Task<User?> GetActiveUserByRoleAsync(
        string roleName,
        CancellationToken cancellationToken = default)
    {
        const string query = """
            SELECT TOP (1) u.UserID
            FROM dbo.[User] u
            INNER JOIN dbo.Roles r ON r.RoleID = u.RoleID
            WHERE u.IsActive = 1 AND r.IsActive = 1 AND r.RoleName = @RoleName
            ORDER BY u.UserID
            """;
        object? result = await ExecuteScalarAsync(
            query,
            [new SqlParameter("@RoleName", SqlDbType.NVarChar, 50) { Value = roleName }],
            cancellationToken);
        return result is null or DBNull ? null : await GetUserByIdAsync(Convert.ToInt32(result), cancellationToken);
    }
    public async Task<bool> UpdateUserProfileAsync(
        int userID,
        string fullName,
        string contactNumber,
        string email,
        string username,
        DateTime birthdate,
        string? profilePhotoPath,
        CancellationToken cancellationToken = default)
    {
        string normalizedName = fullName.Trim();
        int lastSpace = normalizedName.LastIndexOf(' ');
        string firstName = lastSpace > 0 ? normalizedName[..lastSpace].Trim() : normalizedName;
        string lastName = lastSpace > 0 ? normalizedName[(lastSpace + 1)..].Trim() : string.Empty;

        const string query = """
            UPDATE dbo.[User]
            SET FirstName = @FirstName,
                LastName = @LastName,
                ContactNumber = @ContactNumber,
                Email = @Email,
                Username = @Username,
                Birthdate = @Birthdate,
                ProfilePhotoPath = @ProfilePhotoPath
            WHERE UserID = @UserID
            """;

        int rows = await ExecuteNonQueryAsync(
            query,
            [
                new SqlParameter("@FirstName", SqlDbType.NVarChar, 100) { Value = firstName },
                new SqlParameter("@LastName", SqlDbType.NVarChar, 100) { Value = lastName },
                new SqlParameter("@ContactNumber", SqlDbType.NVarChar, 30) { Value = contactNumber.Trim() },
                new SqlParameter("@Email", SqlDbType.NVarChar, 255) { Value = email.Trim() },
                new SqlParameter("@Username", SqlDbType.NVarChar, 50) { Value = username.Trim() },
                new SqlParameter("@Birthdate", SqlDbType.Date) { Value = birthdate.Date },
                new SqlParameter("@ProfilePhotoPath", SqlDbType.NVarChar, 500)
                {
                    Value = string.IsNullOrWhiteSpace(profilePhotoPath) ? DBNull.Value : profilePhotoPath
                },
                new SqlParameter("@UserID", SqlDbType.Int) { Value = userID }
            ],
            cancellationToken);

        return rows > 0;
    }

    public async Task<Teachers?> GetTeacherByIdAsync(
        int teacherID,
        CancellationToken cancellationToken = default)
    {
        const string query = """
            SELECT TeacherID, FirstName, LastName, Email, Password, ContactNumber,
                   AccountCreated, Username, Birthdate, ProfilePhotoPath
            FROM dbo.Teacher
            WHERE TeacherID = @TeacherID
            """;

        DataTable table = await ExecuteQueryAsync(
            query,
            [new SqlParameter("@TeacherID", SqlDbType.Int) { Value = teacherID }],
            cancellationToken);

        if (table.Rows.Count == 0)
            return null;

        DataRow row = table.Rows[0];
        return new Teachers
        {
            TeacherID = row.Field<int>("TeacherID"),
            FirstName = row.Field<string>("FirstName") ?? string.Empty,
            LastName = row.Field<string>("LastName") ?? string.Empty,
            Email = row.Field<string>("Email") ?? string.Empty,
            Password = row.Field<string>("Password") ?? string.Empty,
            ContactNumber = row.Field<string>("ContactNumber") ?? string.Empty,
            AccountCreated = row.Field<DateTime>("AccountCreated"),
            Username = row.Field<string>("Username") ?? string.Empty,
            Birthdate = row.IsNull("Birthdate") ? null : row.Field<DateTime>("Birthdate"),
            ProfilePhotoPath = row.Field<string>("ProfilePhotoPath") ?? string.Empty,
            RoleID = 0,
            RoleName = "Teacher",
            IsActive = true
        };
    }

    public async Task<bool> UpdateTeacherProfileAsync(
        int teacherID,
        string fullName,
        string contactNumber,
        string email,
        string username,
        DateTime birthdate,
        string? profilePhotoPath,
        CancellationToken cancellationToken = default)
    {
        string normalizedName = fullName.Trim();
        int lastSpace = normalizedName.LastIndexOf(' ');
        string firstName = lastSpace > 0 ? normalizedName[..lastSpace].Trim() : normalizedName;
        string lastName = lastSpace > 0 ? normalizedName[(lastSpace + 1)..].Trim() : string.Empty;

        const string query = """
            UPDATE dbo.Teacher
            SET FirstName = @FirstName,
                LastName = @LastName,
                ContactNumber = @ContactNumber,
                Email = @Email,
                Username = @Username,
                Birthdate = @Birthdate,
                ProfilePhotoPath = @ProfilePhotoPath
            WHERE TeacherID = @TeacherID
            """;

        int rows = await ExecuteNonQueryAsync(
            query,
            [
                new SqlParameter("@FirstName", SqlDbType.NVarChar, 100) { Value = firstName },
                new SqlParameter("@LastName", SqlDbType.NVarChar, 100) { Value = lastName },
                new SqlParameter("@ContactNumber", SqlDbType.NVarChar, 30) { Value = contactNumber.Trim() },
                new SqlParameter("@Email", SqlDbType.NVarChar, 255) { Value = email.Trim() },
                new SqlParameter("@Username", SqlDbType.NVarChar, 50) { Value = username.Trim() },
                new SqlParameter("@Birthdate", SqlDbType.Date) { Value = birthdate.Date },
                new SqlParameter("@ProfilePhotoPath", SqlDbType.NVarChar, 500)
                {
                    Value = string.IsNullOrWhiteSpace(profilePhotoPath) ? DBNull.Value : profilePhotoPath
                },
                new SqlParameter("@TeacherID", SqlDbType.Int) { Value = teacherID }
            ],
            cancellationToken);

        return rows > 0;
    }

    public async Task<DataTable> ExecuteQueryAsync(
        string query,
        IEnumerable<SqlParameter>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = GetConnection();
        await using var command = new SqlCommand(query, connection);
        AddParameters(command, parameters);

        await connection.OpenAsync(cancellationToken);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var table = new DataTable();
        table.Load(reader);
        return table;
    }

    public async Task<int> ExecuteNonQueryAsync(
        string query,
        IEnumerable<SqlParameter>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = GetConnection();
        await using var command = new SqlCommand(query, connection);
        AddParameters(command, parameters);

        await connection.OpenAsync(cancellationToken);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<object?> ExecuteScalarAsync(
        string query,
        IEnumerable<SqlParameter>? parameters = null,
        CancellationToken cancellationToken = default)
    {
        await using var connection = GetConnection();
        await using var command = new SqlCommand(query, connection);
        AddParameters(command, parameters);

        await connection.OpenAsync(cancellationToken);
        return await command.ExecuteScalarAsync(cancellationToken);
    }

    public async Task<DataTable> GetAllTeachersAsync(CancellationToken cancellationToken = default)
    {
        const string columnQuery = """
            SELECT COUNT(*)
            FROM sys.columns
            WHERE object_id = OBJECT_ID(@TableName)
              AND name = @ColumnName
            """;
        int accountStatusColumns = Convert.ToInt32(await ExecuteScalarAsync(
            columnQuery,
            [
                new SqlParameter("@TableName", SqlDbType.NVarChar, 128) { Value = "Teacher" },
                new SqlParameter("@ColumnName", SqlDbType.NVarChar, 128) { Value = "AccountStatus" }
            ],
            cancellationToken));
        if (accountStatusColumns > 0)
        {
            const string activeTeacherQuery = """
                SELECT TeacherID, FirstName, LastName
                FROM Teacher
                WHERE AccountStatus = @AccountStatus
                ORDER BY FirstName, LastName
                """;

            return await ExecuteQueryAsync(
                activeTeacherQuery,
                [new SqlParameter("@AccountStatus", SqlDbType.NVarChar, 20) { Value = "Active" }],
                cancellationToken);
        }

        // The currently attached EduTaskDB predates AccountStatus. Keep the app usable
        // without changing the user's database; once the column exists, the branch above
        // automatically enforces the Active-only filter.
        const string legacyTeacherQuery = """
            SELECT TeacherID, FirstName, LastName
            FROM Teacher
            ORDER BY FirstName, LastName
            """;
        return await ExecuteQueryAsync(legacyTeacherQuery, cancellationToken: cancellationToken);
    }

    public Task<DataTable> GetAllTasksWithTeachersAsync(CancellationToken cancellationToken = default)
    {
        const string query = """
            SELECT
                t.TaskID,
                t.Title,
                t.Description,
                t.CreatedAt,
                t.UserID,
                t.CreatedByUserID,
                ta.AssignmentID,
                ta.TeacherID,
                ta.Deadline,
                ta.Priority,
                ta.AssignedAt,
                ta.IsAcknowledged,
                ta.CompletionStatus,
                ta.CompletedAt,
                CONCAT(te.FirstName, ' ', te.LastName) AS TeacherName
            FROM [Task] t
            LEFT JOIN TaskAssignment ta ON t.TaskID = ta.TaskID
            LEFT JOIN Teacher te ON ta.TeacherID = te.TeacherID
            ORDER BY CASE WHEN ta.Deadline IS NULL THEN 1 ELSE 0 END, ta.Deadline, t.CreatedAt DESC
            """;

        return ExecuteQueryAsync(query, cancellationToken: cancellationToken);
    }

    public Task<DataTable> GetTaskByIDAsync(int taskID, CancellationToken cancellationToken = default)
    {
        const string query = """
            SELECT
                t.TaskID,
                t.Title,
                t.Description,
                t.CreatedAt,
                t.UserID,
                t.CreatedByUserID,
                t.isDailyRemind,
                ta.AssignmentID,
                ta.TeacherID,
                ta.Deadline,
                ta.Priority,
                ta.AssignedAt,
                ta.IsAcknowledged,
                ta.AcknowledgedAt,
                ta.CompletionStatus,
                ta.CompletedAt
            FROM [Task] t
            LEFT JOIN TaskAssignment ta ON t.TaskID = ta.TaskID
            WHERE t.TaskID = @TaskID
            """;

        return ExecuteQueryAsync(
            query,
            [new SqlParameter("@TaskID", SqlDbType.Int) { Value = taskID }],
            cancellationToken);
    }

    public Task<DataTable> GetTeacherTasksAsync(int teacherID, CancellationToken cancellationToken = default)
    {
        const string query = """
            SELECT
                t.TaskID,
                t.Title,
                t.Description,
                t.CreatedAt,
                ta.AssignmentID,
                ta.Deadline,
                ta.Priority,
                ta.AssignedAt,
                ta.IsAcknowledged,
                ta.CompletionStatus,
                ta.CompletedAt
            FROM [Task] t
            INNER JOIN TaskAssignment ta ON t.TaskID = ta.TaskID
            WHERE ta.TeacherID = @TeacherID
            ORDER BY ta.Deadline
            """;

        return ExecuteQueryAsync(
            query,
            [new SqlParameter("@TeacherID", SqlDbType.Int) { Value = teacherID }],
            cancellationToken);
    }

    public async Task<int> CreateTaskWithAssignmentsAsync(
        string title,
        string? description,
        int adminID,
        bool isDailyRemind,
        IReadOnlyCollection<int> teacherIDs,
        IReadOnlyCollection<SubtaskDraft> subtasks,
        DateTime deadline,
        string priority,
        bool createIndividualTasks,
        CancellationToken cancellationToken = default)
    {
        int[] distinctTeacherIDs = teacherIDs.Distinct().ToArray();
        if (distinctTeacherIDs.Length == 0)
            throw new ArgumentException("At least one teacher is required.", nameof(teacherIDs));
        foreach (int teacherID in distinctTeacherIDs)
            ValidateTaskAssignment(title, teacherID, priority);

        await using var connection = GetConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            const string ensureSubtaskTableQuery = """
                IF OBJECT_ID(N'dbo.Subtask', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.Subtask
                    (
                        SubtaskID int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                        TaskID int NOT NULL,
                        Title nvarchar(200) NOT NULL,
                        IsCompleted bit NOT NULL CONSTRAINT DF_Subtask_IsCompleted DEFAULT (0),
                        CreatedAt datetime NOT NULL CONSTRAINT DF_Subtask_CreatedAt DEFAULT (GETDATE()),
                        CONSTRAINT FK_Subtask_Task FOREIGN KEY (TaskID)
                            REFERENCES dbo.[Task](TaskID) ON DELETE CASCADE
                    );
                END
                """;
            await using (var schemaCommand = new SqlCommand(ensureSubtaskTableQuery, connection, transaction))
                await schemaCommand.ExecuteNonQueryAsync(cancellationToken);

            const string taskQuery = """
                INSERT INTO [Task] (Title, Description, CreatedAt, UserID, isDailyRemind)
                VALUES (@Title, @Description, GETDATE(), @UserID, @IsDailyRemind);
                SELECT CAST(SCOPE_IDENTITY() AS int);
                """;
            const string assignmentQuery = """
                INSERT INTO TaskAssignment
                    (TaskID, TeacherID, Deadline, Priority, AssignedAt, IsAcknowledged, CompletionStatus)
                VALUES
                    (@TaskID, @TeacherID, @Deadline, @Priority, GETDATE(), 0, 'Pending')
                """;
            const string subtaskQuery = """
                INSERT INTO dbo.Subtask (TaskID, Title, IsCompleted, CreatedAt)
                VALUES (@TaskID, @Title, @IsCompleted, GETDATE())
                """;

            int firstTaskID = 0;
            int taskCopyCount = createIndividualTasks ? distinctTeacherIDs.Length : 1;
            for (int copyIndex = 0; copyIndex < taskCopyCount; copyIndex++)
            {
                await using var taskCommand = new SqlCommand(taskQuery, connection, transaction);
                taskCommand.Parameters.Add("@Title", SqlDbType.NVarChar, 200).Value = title.Trim();
                taskCommand.Parameters.Add("@Description", SqlDbType.NVarChar, -1).Value = description?.Trim() ?? string.Empty;
                taskCommand.Parameters.Add("@UserID", SqlDbType.Int).Value = adminID;
                taskCommand.Parameters.Add("@IsDailyRemind", SqlDbType.Bit).Value = isDailyRemind;
                int taskID = Convert.ToInt32(await taskCommand.ExecuteScalarAsync(cancellationToken));
                if (firstTaskID == 0)
                    firstTaskID = taskID;

                IEnumerable<int> assignedTeacherIDs = createIndividualTasks
                    ? [distinctTeacherIDs[copyIndex]]
                    : distinctTeacherIDs;
                foreach (int teacherID in assignedTeacherIDs)
                {
                    await using var assignmentCommand = new SqlCommand(assignmentQuery, connection, transaction);
                    assignmentCommand.Parameters.Add("@TaskID", SqlDbType.Int).Value = taskID;
                    assignmentCommand.Parameters.Add("@TeacherID", SqlDbType.Int).Value = teacherID;
                    assignmentCommand.Parameters.Add("@Deadline", SqlDbType.DateTime).Value = deadline;
                    assignmentCommand.Parameters.Add("@Priority", SqlDbType.NVarChar, 10).Value = NormalizePriority(priority);
                    await assignmentCommand.ExecuteNonQueryAsync(cancellationToken);
                }

                foreach (SubtaskDraft subtask in subtasks)
                {
                    await using var subtaskCommand = new SqlCommand(subtaskQuery, connection, transaction);
                    subtaskCommand.Parameters.Add("@TaskID", SqlDbType.Int).Value = taskID;
                    subtaskCommand.Parameters.Add("@Title", SqlDbType.NVarChar, 200).Value = subtask.Title.Trim();
                    subtaskCommand.Parameters.Add("@IsCompleted", SqlDbType.Bit).Value = subtask.IsCompleted;
                    await subtaskCommand.ExecuteNonQueryAsync(cancellationToken);
                }
            }

            await transaction.CommitAsync(cancellationToken);
            return firstTaskID;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
    public async Task<bool> UpdateTaskAsync(
        int taskID,
        string title,
        string? description,
        bool isDailyRemind,
        CancellationToken cancellationToken = default)
    {
        ValidateTitle(title);

        const string query = """
            UPDATE [Task]
            SET Title = @Title,
                Description = @Description,
                isDailyRemind = @IsDailyRemind
            WHERE TaskID = @TaskID
            """;

        int rows = await ExecuteNonQueryAsync(
            query,
            [
                new SqlParameter("@Title", SqlDbType.NVarChar, 200) { Value = title.Trim() },
                new SqlParameter("@Description", SqlDbType.NVarChar, -1) { Value = description?.Trim() ?? string.Empty },
                new SqlParameter("@IsDailyRemind", SqlDbType.Bit) { Value = isDailyRemind },
                new SqlParameter("@TaskID", SqlDbType.Int) { Value = taskID }
            ],
            cancellationToken);

        return rows > 0;
    }

    public async Task<bool> UpdateTaskAssignmentAsync(
        int assignmentID,
        int teacherID,
        DateTime deadline,
        string priority,
        CancellationToken cancellationToken = default)
    {
        ValidateAssignment(teacherID, priority);

        const string query = """
            UPDATE TaskAssignment
            SET TeacherID = @TeacherID,
                Deadline = @Deadline,
                Priority = @Priority
            WHERE AssignmentID = @AssignmentID
            """;

        int rows = await ExecuteNonQueryAsync(
            query,
            [
                new SqlParameter("@TeacherID", SqlDbType.Int) { Value = teacherID },
                new SqlParameter("@Deadline", SqlDbType.DateTime) { Value = deadline },
                new SqlParameter("@Priority", SqlDbType.NVarChar, 10) { Value = NormalizePriority(priority) },
                new SqlParameter("@AssignmentID", SqlDbType.Int) { Value = assignmentID }
            ],
            cancellationToken);

        return rows > 0;
    }

    public async Task<bool> UpdateTaskWithAssignmentAsync(
        int taskID,
        int assignmentID,
        string title,
        string? description,
        bool isDailyRemind,
        int teacherID,
        DateTime deadline,
        DateTime originalDeadline,
        string priority,
        IReadOnlyCollection<SubtaskDraft> subtasks,
        CancellationToken cancellationToken = default)
    {
        ValidateTaskAssignment(title, teacherID, priority);

        await using var connection = GetConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            const string taskQuery = """
                UPDATE [Task]
                SET Title = @Title,
                    Description = @Description,
                    isDailyRemind = @IsDailyRemind
                WHERE TaskID = @TaskID
                """;
            await using var taskCommand = new SqlCommand(taskQuery, connection, transaction);
            taskCommand.Parameters.Add("@Title", SqlDbType.NVarChar, 200).Value = title.Trim();
            taskCommand.Parameters.Add("@Description", SqlDbType.NVarChar, -1).Value = description?.Trim() ?? string.Empty;
            taskCommand.Parameters.Add("@IsDailyRemind", SqlDbType.Bit).Value = isDailyRemind;
            taskCommand.Parameters.Add("@TaskID", SqlDbType.Int).Value = taskID;
            int taskRows = await taskCommand.ExecuteNonQueryAsync(cancellationToken);

            const string assignmentQuery = """
                UPDATE TaskAssignment
                SET TeacherID = @TeacherID,
                    Deadline = @Deadline,
                    Priority = @Priority
                WHERE AssignmentID = @AssignmentID
                  AND TaskID = @TaskID
                """;
            await using var assignmentCommand = new SqlCommand(assignmentQuery, connection, transaction);
            assignmentCommand.Parameters.Add("@TeacherID", SqlDbType.Int).Value = teacherID;
            assignmentCommand.Parameters.Add("@Deadline", SqlDbType.DateTime).Value = deadline;
            assignmentCommand.Parameters.Add("@Priority", SqlDbType.NVarChar, 10).Value = NormalizePriority(priority);
            assignmentCommand.Parameters.Add("@AssignmentID", SqlDbType.Int).Value = assignmentID;
            assignmentCommand.Parameters.Add("@TaskID", SqlDbType.Int).Value = taskID;
            int assignmentRows = await assignmentCommand.ExecuteNonQueryAsync(cancellationToken);

            if (taskRows == 0 || assignmentRows == 0)
                throw new InvalidOperationException("The task or assignment no longer exists.");

            int[] retainedIDs = subtasks.Where(s => s.SubtaskID.HasValue).Select(s => s.SubtaskID!.Value).Distinct().ToArray();
            var existingIDs = new List<int>();
            await using (var command = new SqlCommand("SELECT SubtaskID FROM dbo.Subtask WHERE TaskID = @TaskID", connection, transaction))
            {
                command.Parameters.Add("@TaskID", SqlDbType.Int).Value = taskID;
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken)) existingIDs.Add(reader.GetInt32(0));
            }
            foreach (int removedID in existingIDs.Except(retainedIDs))
            {
                await using var history = new SqlCommand(
                    "DELETE FROM dbo.SubtaskProofHistory WHERE SubtaskID = @SubtaskID", connection, transaction);
                history.Parameters.Add("@SubtaskID", SqlDbType.Int).Value = removedID;
                await history.ExecuteNonQueryAsync(cancellationToken);
                await using var proof = new SqlCommand("DELETE FROM dbo.SubtaskProof WHERE SubtaskID = @SubtaskID", connection, transaction);
                proof.Parameters.Add("@SubtaskID", SqlDbType.Int).Value = removedID;
                await proof.ExecuteNonQueryAsync(cancellationToken);
                await using var remove = new SqlCommand("DELETE FROM dbo.Subtask WHERE SubtaskID = @SubtaskID AND TaskID = @TaskID", connection, transaction);
                remove.Parameters.Add("@SubtaskID", SqlDbType.Int).Value = removedID;
                remove.Parameters.Add("@TaskID", SqlDbType.Int).Value = taskID;
                await remove.ExecuteNonQueryAsync(cancellationToken);
            }
            foreach (SubtaskDraft subtask in subtasks)
            {
                string sql = subtask.SubtaskID.HasValue
                    ? "UPDATE dbo.Subtask SET Title = @Title WHERE SubtaskID = @SubtaskID AND TaskID = @TaskID"
                    : "INSERT INTO dbo.Subtask (TaskID, Title, IsCompleted, CreatedAt) VALUES (@TaskID, @Title, 0, GETDATE())";
                await using var command = new SqlCommand(sql, connection, transaction);
                command.Parameters.Add("@TaskID", SqlDbType.Int).Value = taskID;
                command.Parameters.Add("@Title", SqlDbType.NVarChar, 200).Value = subtask.Title.Trim();
                if (subtask.SubtaskID.HasValue) command.Parameters.Add("@SubtaskID", SqlDbType.Int).Value = subtask.SubtaskID.Value;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<bool> AcknowledgeTaskAsync(int assignmentID, CancellationToken cancellationToken = default)
    {
        const string query = """
            UPDATE TaskAssignment
            SET IsAcknowledged = 1,
                AcknowledgedAt = GETDATE()
            WHERE AssignmentID = @AssignmentID
            """;

        return await ExecuteNonQueryAsync(
            query,
            [new SqlParameter("@AssignmentID", SqlDbType.Int) { Value = assignmentID }],
            cancellationToken) > 0;
    }

    public async Task<bool> ValidateTaskAsync(
        int assignmentID,
        int actingUserID,
        CancellationToken cancellationToken = default)
    {
        const string query = """
            SET XACT_ABORT ON;
            BEGIN TRANSACTION;
            DECLARE @TaskID int = (SELECT TaskID FROM dbo.TaskAssignment WHERE AssignmentID = @AssignmentID);
            IF @TaskID IS NULL THROW 51140, 'The assignment does not exist.', 1;

            -- Subtasks and their proofs are shared across every teacher assigned to this task.
            UPDATE dbo.TaskAssignment
            SET CompletionStatus = N'For Validation', CompletedAt = NULL
            WHERE TaskID = @TaskID AND CompletionStatus <> N'Completed';

            EXEC dbo.ApproveTaskCompletion @TaskID = @TaskID, @ActingUserID = @ActingUserID;
            COMMIT TRANSACTION;
            """;
        await ExecuteNonQueryAsync(query,
            [
                new SqlParameter("@AssignmentID", SqlDbType.Int) { Value = assignmentID },
                new SqlParameter("@ActingUserID", SqlDbType.Int) { Value = actingUserID }
            ], cancellationToken);
        return true;
    }

    public async Task<bool> RejectTaskCompletionAsync(
        int assignmentID,
        int actingUserID,
        string reason,
        CancellationToken cancellationToken = default)
    {
        const string query = """
            DECLARE @TaskID int = (SELECT TaskID FROM dbo.TaskAssignment WHERE AssignmentID = @AssignmentID);
            IF @TaskID IS NULL THROW 51140, 'The assignment does not exist.', 1;
            EXEC dbo.RequestTaskRevision @TaskID = @TaskID, @ActingUserID = @ActingUserID, @Reason = @Reason;
            """;
        await ExecuteNonQueryAsync(query,
            [
                new SqlParameter("@AssignmentID", SqlDbType.Int) { Value = assignmentID },
                new SqlParameter("@ActingUserID", SqlDbType.Int) { Value = actingUserID },
                new SqlParameter("@Reason", SqlDbType.NVarChar, 1000) { Value = reason }
            ], cancellationToken);
        return true;
    }
    public async Task<bool> DeleteTaskAsync(int taskID, int actingUserID, CancellationToken cancellationToken = default)
    {
        await using var connection = GetConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await using (var authorize = new SqlCommand("dbo.AssertActiveUserRole", connection, transaction))
            {
                authorize.CommandType = CommandType.StoredProcedure;
                authorize.Parameters.Add("@ActingUserID", SqlDbType.Int).Value = actingUserID;
                authorize.Parameters.Add("@AllowDirector", SqlDbType.Bit).Value = true;
                authorize.Parameters.Add("@AllowStaff", SqlDbType.Bit).Value = false;
                await authorize.ExecuteNonQueryAsync(cancellationToken);
            }
            const string deleteHistory = """
                DELETE h
                FROM dbo.SubtaskProofHistory h
                INNER JOIN dbo.Subtask s ON s.SubtaskID = h.SubtaskID
                WHERE s.TaskID = @TaskID
                """;
            await using var historyCommand = new SqlCommand(deleteHistory, connection, transaction);
            historyCommand.Parameters.Add("@TaskID", SqlDbType.Int).Value = taskID;
            await historyCommand.ExecuteNonQueryAsync(cancellationToken);

            const string deleteComments = "DELETE FROM dbo.TaskComment WHERE TaskID = @TaskID";
            await using var commentCommand = new SqlCommand(deleteComments, connection, transaction);
            commentCommand.Parameters.Add("@TaskID", SqlDbType.Int).Value = taskID;
            await commentCommand.ExecuteNonQueryAsync(cancellationToken);

            const string deleteAssignments = "DELETE FROM TaskAssignment WHERE TaskID = @TaskID";
            await using var assignmentCommand = new SqlCommand(deleteAssignments, connection, transaction);
            assignmentCommand.Parameters.Add("@TaskID", SqlDbType.Int).Value = taskID;
            await assignmentCommand.ExecuteNonQueryAsync(cancellationToken);

            const string deleteTask = "DELETE FROM [Task] WHERE TaskID = @TaskID";
            await using var taskCommand = new SqlCommand(deleteTask, connection, transaction);
            taskCommand.Parameters.Add("@TaskID", SqlDbType.Int).Value = taskID;
            int rows = await taskCommand.ExecuteNonQueryAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return rows > 0;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<List<SubtaskDisplayItem>> GetTaskSubtasksAsync(
        int taskID,
        CancellationToken cancellationToken = default)
    {
        const string query = """
            SELECT s.SubtaskID, s.Title, s.IsCompleted,
                   p.ProofID, p.FileName, p.ValidationStatus, p.UploadedAt, p.AdminRemarks
            FROM dbo.Subtask s
            LEFT JOIN dbo.SubtaskProof p ON p.SubtaskID = s.SubtaskID
            WHERE s.TaskID = @TaskID
            ORDER BY s.SubtaskID
            """;
        DataTable table = await ExecuteQueryAsync(
            query,
            [new SqlParameter("@TaskID", SqlDbType.Int) { Value = taskID }],
            cancellationToken);
        List<SubtaskDisplayItem> subtasks = table.AsEnumerable().Select(row => new SubtaskDisplayItem
        {
            SubtaskID = row.Field<int>("SubtaskID"),
            TaskID = taskID,
            Title = row.Field<string>("Title") ?? string.Empty,
            IsCompleted = row.Field<bool>("IsCompleted"),
            ProofID = row.IsNull("ProofID") ? null : row.Field<int>("ProofID"),
            ProofFileName = row.Field<string>("FileName"),
            ProofStatus = row.Field<string>("ValidationStatus"),
            ProofUploadedAt = row.IsNull("UploadedAt") ? null : row.Field<DateTime>("UploadedAt"),
            AdminRemarks = row.Field<string>("AdminRemarks")
        }).ToList();

        if (subtasks.Count == 0)
            return subtasks;

        const string historyQuery = """
            SELECT h.HistoryID, h.SubtaskID, h.AttemptNumber, h.FileName, h.ContentType,
                   h.ValidationStatus, h.SubmittedAt, h.ReviewedAt,
                   h.ReviewedByUserID, h.ReturnRemarks
            FROM dbo.SubtaskProofHistory h
            INNER JOIN dbo.Subtask s ON s.SubtaskID = h.SubtaskID
            WHERE s.TaskID = @TaskID
            ORDER BY h.SubtaskID, h.AttemptNumber DESC;
            """;
        DataTable historyTable = await ExecuteQueryAsync(
            historyQuery,
            [new SqlParameter("@TaskID", SqlDbType.Int) { Value = taskID }],
            cancellationToken);
        var historyBySubtask = historyTable.AsEnumerable()
            .GroupBy(row => row.Field<int>("SubtaskID"))
            .ToDictionary(group => group.Key, group => group.Select(row => new SubtaskProofHistoryItem
            {
                HistoryID = row.Field<int>("HistoryID"),
                AttemptNumber = row.Field<int>("AttemptNumber"),
                FileName = row.Field<string>("FileName") ?? string.Empty,
                ContentType = row.Field<string>("ContentType") ?? string.Empty,
                ValidationStatus = row.Field<string>("ValidationStatus") ?? string.Empty,
                SubmittedAt = row.Field<DateTime>("SubmittedAt"),
                ReviewedAt = row.IsNull("ReviewedAt") ? null : row.Field<DateTime>("ReviewedAt"),
                ReviewedByUserID = row.IsNull("ReviewedByUserID") ? null : row.Field<int>("ReviewedByUserID"),
                ReturnRemarks = row.Field<string>("ReturnRemarks")
            }).ToList());
        foreach (SubtaskDisplayItem subtask in subtasks)
        {
            if (historyBySubtask.TryGetValue(subtask.SubtaskID, out List<SubtaskProofHistoryItem>? history))
                subtask.ProofHistory.AddRange(history);
        }
        return subtasks;
    }

    private async System.Threading.Tasks.Task EnsureSubtaskProofFileConstraintsAsync(CancellationToken cancellationToken)
    {
        const string query = """
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
            """;

        await ExecuteNonQueryAsync(query, [], cancellationToken);
    }
    public async Task<bool> UploadSubtaskProofAsync(
        int subtaskID,
        PreparedProofImage image,
        CancellationToken cancellationToken = default)
    {
        if (image.Data.Length is 0 or > ProofImageService.MaximumBytes)
            throw new ArgumentException("Files must be between 1 byte and 20 MB.");
        if (image.ContentType is not ("image/jpeg" or "image/png" or "application/pdf"))
            throw new ArgumentException("Only JPEG, PNG, and PDF files are supported.");

        await EnsureSubtaskProofFileConstraintsAsync(cancellationToken);

        const string query = """
            SET XACT_ABORT ON;
            BEGIN TRANSACTION;

            DECLARE @WasReturned bit = CASE WHEN EXISTS
            (
                SELECT 1
                FROM dbo.SubtaskProof WITH (UPDLOCK, HOLDLOCK)
                WHERE SubtaskID = @SubtaskID
                  AND ValidationStatus = 'Returned'
            ) THEN 1 ELSE 0 END;

            IF EXISTS
            (
                SELECT 1
                FROM dbo.SubtaskProof
                WHERE SubtaskID = @SubtaskID
                  AND ValidationStatus IN ('Draft', 'Returned')
            )
            BEGIN
                UPDATE dbo.SubtaskProof
                SET ImageData = @ImageData,
                    FileName = @FileName,
                    ContentType = @ContentType,
                    ValidationStatus = 'Draft',
                    UploadedAt = SYSDATETIME(),
                    ReviewedAt = NULL,
                    ReviewedByUserID = NULL,
                    AdminRemarks = NULL
                WHERE SubtaskID = @SubtaskID;
            END
            ELSE IF NOT EXISTS (SELECT 1 FROM dbo.SubtaskProof WHERE SubtaskID = @SubtaskID)
            BEGIN
                INSERT INTO dbo.SubtaskProof (SubtaskID, ImageData, FileName, ContentType, ValidationStatus)
                VALUES (@SubtaskID, @ImageData, @FileName, @ContentType, 'Draft');
            END
            ELSE
            BEGIN
                ROLLBACK TRANSACTION;
                THROW 51000, 'An approved file cannot be replaced.', 1;
            END;

            IF @WasReturned = 1
            BEGIN
                UPDATE ta
                SET CompletionStatus = 'Returned',
                    CompletedAt = NULL
                FROM dbo.TaskAssignment ta
                INNER JOIN dbo.Subtask s ON s.TaskID = ta.TaskID
                WHERE s.SubtaskID = @SubtaskID
                  AND ta.CompletionStatus = 'For Validation';
            END;

            COMMIT TRANSACTION;
            """;

        return await ExecuteNonQueryAsync(query,
            [
                new SqlParameter("@SubtaskID", SqlDbType.Int) { Value = subtaskID },
                new SqlParameter("@ImageData", SqlDbType.VarBinary, -1) { Value = image.Data },
                new SqlParameter("@FileName", SqlDbType.NVarChar, 255) { Value = image.FileName },
                new SqlParameter("@ContentType", SqlDbType.NVarChar, 50) { Value = image.ContentType }
            ], cancellationToken) > 0;
    }

    public async Task<bool> ConfirmSubtaskProofAsync(
        int subtaskID,
        CancellationToken cancellationToken = default)
    {
        await EnsureSubtaskProofFileConstraintsAsync(cancellationToken);
        const string query = """
            SET XACT_ABORT ON;
            SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
            BEGIN TRANSACTION;
            IF NOT EXISTS (SELECT 1 FROM dbo.SubtaskProof WITH (UPDLOCK, HOLDLOCK)
                           WHERE SubtaskID = @SubtaskID AND ValidationStatus = 'Draft')
            BEGIN
                ROLLBACK TRANSACTION;
                SELECT CAST(0 AS bit);
                RETURN;
            END;
            DECLARE @SubmittedAt datetime2 = SYSDATETIME();
            DECLARE @AttemptNumber int;
            SELECT @AttemptNumber = ISNULL(MAX(AttemptNumber), 0) + 1
            FROM dbo.SubtaskProofHistory WITH (UPDLOCK, HOLDLOCK)
            WHERE SubtaskID = @SubtaskID;
            INSERT INTO dbo.SubtaskProofHistory
                (SubtaskID, AttemptNumber, FileName, ContentType, FileData, ValidationStatus, SubmittedAt)
            SELECT SubtaskID, @AttemptNumber, FileName, ContentType, ImageData, 'Pending', @SubmittedAt
            FROM dbo.SubtaskProof
            WHERE SubtaskID = @SubtaskID AND ValidationStatus = 'Draft';
            UPDATE dbo.SubtaskProof
            SET ValidationStatus = 'Pending', UploadedAt = @SubmittedAt
            WHERE SubtaskID = @SubtaskID AND ValidationStatus = 'Draft';
            -- Move the shared task to validation only after every shared subtask has
            -- a submitted or approved proof. All teacher assignments share this state.
            DECLARE @TaskID int = (SELECT TaskID FROM dbo.Subtask WHERE SubtaskID = @SubtaskID);
            IF @TaskID IS NOT NULL
               AND NOT EXISTS
               (
                   SELECT 1
                   FROM dbo.Subtask s
                   WHERE s.TaskID = @TaskID
                     AND NOT EXISTS
                     (
                         SELECT 1
                         FROM dbo.SubtaskProof p
                         WHERE p.SubtaskID = s.SubtaskID
                           AND p.ValidationStatus IN (N'Pending', N'Approved')
                     )
               )
            BEGIN
                UPDATE dbo.TaskAssignment
                SET CompletionStatus = N'For Validation', CompletedAt = NULL
                WHERE TaskID = @TaskID
                  AND CompletionStatus <> N'Completed';
            END;
            COMMIT TRANSACTION;
            SELECT CAST(1 AS bit);
            """;

        return Convert.ToBoolean(await ExecuteScalarAsync(query,
            [new SqlParameter("@SubtaskID", SqlDbType.Int) { Value = subtaskID }], cancellationToken));
    }
    public async Task<bool> RemoveSubtaskProofAsync(
        int subtaskID,
        CancellationToken cancellationToken = default)
    {
        const string query = """
            SET XACT_ABORT ON;
            BEGIN TRANSACTION;

            DECLARE @RemovedProofs int;

            DELETE FROM dbo.SubtaskProof
            WHERE SubtaskID = @SubtaskID
              AND ValidationStatus IN ('Draft', 'Returned');

            SET @RemovedProofs = @@ROWCOUNT;

            IF @RemovedProofs > 0
            BEGIN
                UPDATE ta
                SET CompletionStatus = 'Returned',
                    CompletedAt = NULL
                FROM dbo.TaskAssignment ta
                INNER JOIN dbo.Subtask s ON s.TaskID = ta.TaskID
                WHERE s.SubtaskID = @SubtaskID
                  AND ta.CompletionStatus = 'For Validation';
            END;

            COMMIT TRANSACTION;

            SELECT @RemovedProofs;
            """;
        int removedProofs = Convert.ToInt32(await ExecuteScalarAsync(query,
            [new SqlParameter("@SubtaskID", SqlDbType.Int) { Value = subtaskID }],
            cancellationToken));
        return removedProofs > 0;
    }
    public async Task<(byte[] Data, string ContentType, string FileName)?> GetSubtaskProofImageAsync(
        int subtaskID,
        CancellationToken cancellationToken = default)
    {
        const string query = """
            SELECT ImageData, ContentType, FileName
            FROM dbo.SubtaskProof
            WHERE SubtaskID = @SubtaskID
            """;
        DataTable table = await ExecuteQueryAsync(query,
            [new SqlParameter("@SubtaskID", SqlDbType.Int) { Value = subtaskID }], cancellationToken);
        if (table.Rows.Count == 0)
            return null;
        DataRow row = table.Rows[0];
        return ((byte[])row["ImageData"], row.Field<string>("ContentType") ?? "image/jpeg", row.Field<string>("FileName") ?? "proof.jpg");
    }

    public async Task<List<SubtaskProofHistoryItem>> GetSubtaskProofHistoryAsync(
        int subtaskID,
        CancellationToken cancellationToken = default)
    {
        await EnsureSubtaskProofFileConstraintsAsync(cancellationToken);
        const string query = """
            SELECT HistoryID, AttemptNumber, FileName, ContentType, ValidationStatus,
                   SubmittedAt, ReviewedAt, ReviewedByUserID, ReturnRemarks
            FROM dbo.SubtaskProofHistory
            WHERE SubtaskID = @SubtaskID
            ORDER BY AttemptNumber DESC;
            """;
        DataTable table = await ExecuteQueryAsync(query,
            [new SqlParameter("@SubtaskID", SqlDbType.Int) { Value = subtaskID }], cancellationToken);
        return table.AsEnumerable().Select(row => new SubtaskProofHistoryItem
        {
            HistoryID = row.Field<int>("HistoryID"),
            AttemptNumber = row.Field<int>("AttemptNumber"),
            FileName = row.Field<string>("FileName") ?? string.Empty,
            ContentType = row.Field<string>("ContentType") ?? string.Empty,
            ValidationStatus = row.Field<string>("ValidationStatus") ?? string.Empty,
            SubmittedAt = row.Field<DateTime>("SubmittedAt"),
            ReviewedAt = row.IsNull("ReviewedAt") ? null : row.Field<DateTime>("ReviewedAt"),
            ReviewedByUserID = row.IsNull("ReviewedByUserID") ? null : row.Field<int>("ReviewedByUserID"),
            ReturnRemarks = row.Field<string>("ReturnRemarks")
        }).ToList();
    }

    public async Task<(byte[] Data, string ContentType, string FileName)?> GetSubtaskProofHistoryFileAsync(
        int historyID,
        CancellationToken cancellationToken = default)
    {
        await EnsureSubtaskProofFileConstraintsAsync(cancellationToken);
        const string query = """
            SELECT FileData, ContentType, FileName
            FROM dbo.SubtaskProofHistory
            WHERE HistoryID = @HistoryID;
            """;
        DataTable table = await ExecuteQueryAsync(query,
            [new SqlParameter("@HistoryID", SqlDbType.Int) { Value = historyID }], cancellationToken);
        if (table.Rows.Count == 0)
            return null;
        DataRow row = table.Rows[0];
        return ((byte[])row["FileData"], row.Field<string>("ContentType") ?? "image/jpeg",
            row.Field<string>("FileName") ?? "proof.jpg");
    }

    public async Task<bool> ReviewSubtaskProofAsync(
        int subtaskID,
        bool approve,
        int reviewedByUserID,
        string? adminRemarks,
        CancellationToken cancellationToken = default)
    {
        string remarks = adminRemarks?.Trim() ?? string.Empty;
        if (!approve && remarks.Length == 0)
            throw new ArgumentException("Add a note explaining what needs to be changed.");
        if (remarks.Length > 500)
            throw new ArgumentException("Admin remarks cannot exceed 500 characters.");

        await EnsureSubtaskProofFileConstraintsAsync(cancellationToken);
        await using var connection = GetConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await using (var authorize = new SqlCommand("dbo.AssertActiveUserRole", connection, transaction))
            {
                authorize.CommandType = CommandType.StoredProcedure;
                authorize.Parameters.Add("@ActingUserID", SqlDbType.Int).Value = reviewedByUserID;
                authorize.Parameters.Add("@AllowDirector", SqlDbType.Bit).Value = true;
                authorize.Parameters.Add("@AllowStaff", SqlDbType.Bit).Value = true;
                await authorize.ExecuteNonQueryAsync(cancellationToken);
            }
            const string proofQuery = """
                UPDATE dbo.SubtaskProof
                SET ValidationStatus = @Status,
                    ReviewedAt = SYSDATETIME(),
                    ReviewedByUserID = @ReviewedByUserID,
                    AdminRemarks = @AdminRemarks
                WHERE SubtaskID = @SubtaskID
                  AND ValidationStatus = 'Pending'
                """;
            await using var proofCommand = new SqlCommand(proofQuery, connection, transaction);
            proofCommand.Parameters.Add("@Status", SqlDbType.NVarChar, 20).Value = approve ? "Approved" : "Returned";
            proofCommand.Parameters.Add("@ReviewedByUserID", SqlDbType.Int).Value = reviewedByUserID;
            proofCommand.Parameters.Add("@AdminRemarks", SqlDbType.NVarChar, 500).Value = remarks.Length == 0 ? DBNull.Value : remarks;
            proofCommand.Parameters.Add("@SubtaskID", SqlDbType.Int).Value = subtaskID;
            if (await proofCommand.ExecuteNonQueryAsync(cancellationToken) == 0)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return false;
            }

            const string historyQuery = """
                UPDATE dbo.SubtaskProofHistory
                SET ValidationStatus = @Status,
                    ReviewedAt = SYSDATETIME(),
                    ReviewedByUserID = @ReviewedByUserID,
                    ReturnRemarks = @AdminRemarks
                WHERE SubtaskID = @SubtaskID
                  AND ValidationStatus = 'Pending';
                """;
            await using var historyCommand = new SqlCommand(historyQuery, connection, transaction);
            historyCommand.Parameters.Add("@Status", SqlDbType.NVarChar, 20).Value = approve ? "Approved" : "Returned";
            historyCommand.Parameters.Add("@ReviewedByUserID", SqlDbType.Int).Value = reviewedByUserID;
            historyCommand.Parameters.Add("@AdminRemarks", SqlDbType.NVarChar, 500).Value =
                remarks.Length == 0 ? DBNull.Value : remarks;
            historyCommand.Parameters.Add("@SubtaskID", SqlDbType.Int).Value = subtaskID;
            if (await historyCommand.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new InvalidOperationException("The pending proof history attempt could not be updated.");

            const string subtaskQuery = """
                UPDATE dbo.Subtask
                SET IsCompleted = @IsCompleted,
                    CompletedAt = CASE WHEN @IsCompleted = 1 THEN GETDATE() ELSE NULL END
                WHERE SubtaskID = @SubtaskID
                """;
            await using var subtaskCommand = new SqlCommand(subtaskQuery, connection, transaction);
            subtaskCommand.Parameters.Add("@IsCompleted", SqlDbType.Bit).Value = approve;
            subtaskCommand.Parameters.Add("@SubtaskID", SqlDbType.Int).Value = subtaskID;
            await subtaskCommand.ExecuteNonQueryAsync(cancellationToken);

            if (!approve)
            {
                const string assignmentQuery = """
                    UPDATE ta
                    SET CompletionStatus = 'Returned',
                        CompletedAt = NULL
                    FROM dbo.TaskAssignment ta
                    INNER JOIN dbo.Subtask s ON s.TaskID = ta.TaskID
                    WHERE s.SubtaskID = @SubtaskID
                      AND ta.CompletionStatus = 'For Validation'
                    """;
                await using var assignmentCommand = new SqlCommand(assignmentQuery, connection, transaction);
                assignmentCommand.Parameters.Add("@SubtaskID", SqlDbType.Int).Value = subtaskID;
                await assignmentCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
    public async Task<List<TaskCommentItem>> GetTaskCommentsAsync(
        int taskID,
        int subtaskID,
        CancellationToken cancellationToken = default)
    {
        await EnsureTaskCommentTableAsync(cancellationToken);
        const string query = """
            SELECT CommentID, AuthorID, AuthorName, AuthorType, CommentText, MessageType, CreatedAt
            FROM dbo.TaskComment
            WHERE TaskID = @TaskID
              AND SubtaskID = @SubtaskID
            ORDER BY CreatedAt, CommentID
            """;
        DataTable table = await ExecuteQueryAsync(
            query,
            [
                new SqlParameter("@TaskID", SqlDbType.Int) { Value = taskID },
                new SqlParameter("@SubtaskID", SqlDbType.Int) { Value = subtaskID }
            ],
            cancellationToken);
        return table.AsEnumerable().Select(row => new TaskCommentItem
        {
            CommentID = row.Field<int>("CommentID"),
            AuthorID = row.Field<int>("AuthorID"),
            AuthorName = row.Field<string>("AuthorName") ?? "Unknown",
            AuthorType = row.Field<string>("AuthorType") ?? "User",
            CommentText = row.Field<string>("CommentText") ?? string.Empty,
            MessageType = row.Field<string>("MessageType") ?? "Comment",
            CreatedAt = row.Field<DateTime>("CreatedAt")
        }).ToList();
    }

    public async Task<bool> AddTaskCommentAsync(
        int taskID,
        int subtaskID,
        string authorType,
        int authorID,
        string authorName,
        string commentText,
        string messageType = "Comment",
        CancellationToken cancellationToken = default)
    {
        string message = commentText.Trim();
        if (message.Length == 0)
            throw new ArgumentException("Write a comment or note first.");
        if (message.Length > 1000)
            throw new ArgumentException("Comments cannot exceed 1,000 characters.");
        if (authorType is not ("User" or "Teacher"))
            throw new ArgumentException("Invalid comment author.");

        await EnsureTaskCommentTableAsync(cancellationToken);
        const string query = """
            INSERT INTO dbo.TaskComment
                (TaskID, SubtaskID, AuthorType, AuthorID, AuthorName, CommentText, MessageType, CreatedAt)
            VALUES
                (@TaskID, @SubtaskID, @AuthorType, @AuthorID, @AuthorName, @CommentText, @MessageType, GETDATE())
            """;
        return await ExecuteNonQueryAsync(
            query,
            [
                new SqlParameter("@TaskID", SqlDbType.Int) { Value = taskID },
                new SqlParameter("@SubtaskID", SqlDbType.Int) { Value = subtaskID },
                new SqlParameter("@AuthorType", SqlDbType.NVarChar, 10) { Value = authorType },
                new SqlParameter("@AuthorID", SqlDbType.Int) { Value = authorID },
                new SqlParameter("@AuthorName", SqlDbType.NVarChar, 120) { Value = authorName.Trim() },
                new SqlParameter("@CommentText", SqlDbType.NVarChar, 1000) { Value = message },
                new SqlParameter("@MessageType", SqlDbType.NVarChar, 20) { Value = messageType }
            ],
            cancellationToken) > 0;
    }

    public async System.Threading.Tasks.Task<int> GetUnreadTaskCommentCountAsync(
        int subtaskID,
        string readerType,
        int readerID,
        CancellationToken cancellationToken = default)
    {
        await EnsureTaskCommentReadTableAsync(cancellationToken);
        const string query = """
            SELECT COUNT(*)
            FROM dbo.TaskComment AS comment
            LEFT JOIN dbo.TaskCommentRead AS receipt
              ON receipt.SubtaskID = comment.SubtaskID
             AND receipt.ReaderType = @ReaderType
             AND receipt.ReaderID = @ReaderID
            WHERE comment.SubtaskID = @SubtaskID
              AND comment.CommentID > ISNULL(receipt.LastReadCommentID, 0)
              AND NOT (comment.AuthorType = @ReaderType AND comment.AuthorID = @ReaderID)
            """;
        object? result = await ExecuteScalarAsync(
            query,
            [
                new SqlParameter("@SubtaskID", SqlDbType.Int) { Value = subtaskID },
                new SqlParameter("@ReaderType", SqlDbType.NVarChar, 10) { Value = readerType },
                new SqlParameter("@ReaderID", SqlDbType.Int) { Value = readerID }
            ],
            cancellationToken);
        return Convert.ToInt32(result ?? 0);
    }

    public async System.Threading.Tasks.Task MarkTaskCommentsReadAsync(
        int subtaskID,
        string readerType,
        int readerID,
        CancellationToken cancellationToken = default)
    {
        await EnsureTaskCommentReadTableAsync(cancellationToken);
        const string query = """
            DECLARE @LastReadCommentID int = ISNULL(
                (SELECT MAX(CommentID) FROM dbo.TaskComment WHERE SubtaskID = @SubtaskID), 0);

            MERGE dbo.TaskCommentRead AS target
            USING (SELECT @SubtaskID AS SubtaskID, @ReaderType AS ReaderType, @ReaderID AS ReaderID) AS source
               ON target.SubtaskID = source.SubtaskID
              AND target.ReaderType = source.ReaderType
              AND target.ReaderID = source.ReaderID
            WHEN MATCHED THEN
                UPDATE SET LastReadCommentID = @LastReadCommentID, ReadAt = GETDATE()
            WHEN NOT MATCHED THEN
                INSERT (SubtaskID, ReaderType, ReaderID, LastReadCommentID, ReadAt)
                VALUES (@SubtaskID, @ReaderType, @ReaderID, @LastReadCommentID, GETDATE());
            """;
        await ExecuteNonQueryAsync(
            query,
            [
                new SqlParameter("@SubtaskID", SqlDbType.Int) { Value = subtaskID },
                new SqlParameter("@ReaderType", SqlDbType.NVarChar, 10) { Value = readerType },
                new SqlParameter("@ReaderID", SqlDbType.Int) { Value = readerID }
            ],
            cancellationToken);
    }

    private async System.Threading.Tasks.Task EnsureTaskCommentReadTableAsync(CancellationToken cancellationToken)
    {
        await EnsureTaskCommentTableAsync(cancellationToken);
        const string query = """
            IF OBJECT_ID(N'dbo.TaskCommentRead', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TaskCommentRead
                (
                    SubtaskID int NOT NULL,
                    ReaderType nvarchar(10) NOT NULL,
                    ReaderID int NOT NULL,
                    LastReadCommentID int NOT NULL CONSTRAINT DF_TaskCommentRead_LastRead DEFAULT (0),
                    ReadAt datetime NOT NULL CONSTRAINT DF_TaskCommentRead_ReadAt DEFAULT (GETDATE()),
                    CONSTRAINT PK_TaskCommentRead PRIMARY KEY (SubtaskID, ReaderType, ReaderID),
                    CONSTRAINT FK_TaskCommentRead_Subtask FOREIGN KEY (SubtaskID)
                        REFERENCES dbo.Subtask(SubtaskID) ON DELETE CASCADE
                );
            END
            """;
        await ExecuteNonQueryAsync(query, cancellationToken: cancellationToken);
    }
    private async System.Threading.Tasks.Task EnsureTaskCommentTableAsync(CancellationToken cancellationToken)
    {
        const string query = """
            IF OBJECT_ID(N'dbo.TaskComment', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.TaskComment
                (
                    CommentID int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    TaskID int NOT NULL,
                    SubtaskID int NULL,
                    AuthorType nvarchar(10) NOT NULL,
                    AuthorID int NOT NULL,
                    AuthorName nvarchar(120) NOT NULL,
                    CommentText nvarchar(1000) NOT NULL,
                    CreatedAt datetime NOT NULL CONSTRAINT DF_TaskComment_CreatedAt DEFAULT (GETDATE()),
                    CONSTRAINT FK_TaskComment_Task FOREIGN KEY (TaskID)
                        REFERENCES dbo.[Task](TaskID) ON DELETE CASCADE
                );
            END

            IF COL_LENGTH(N'dbo.TaskComment', N'MessageType') IS NULL
                ALTER TABLE dbo.TaskComment ADD MessageType nvarchar(20) NOT NULL CONSTRAINT DF_TaskComment_MessageType DEFAULT ('Comment');

            IF COL_LENGTH(N'dbo.TaskComment', N'SubtaskID') IS NULL
                ALTER TABLE dbo.TaskComment ADD SubtaskID int NULL;

            IF OBJECT_ID(N'dbo.FK_TaskComment_Subtask', N'F') IS NULL
                ALTER TABLE dbo.TaskComment ADD CONSTRAINT FK_TaskComment_Subtask
                    FOREIGN KEY (SubtaskID) REFERENCES dbo.Subtask(SubtaskID);
            """;
        await ExecuteNonQueryAsync(query, cancellationToken: cancellationToken);
    }

    private static void AddParameters(SqlCommand command, IEnumerable<SqlParameter>? parameters)
    {
        if (parameters is not null)
            command.Parameters.AddRange(parameters.ToArray());
    }

    private static void ValidateTaskAssignment(
        string title,
        int teacherID,
        string priority)
    {
        ValidateTitle(title);
        ValidateAssignment(teacherID, priority);
    }

    private static void ValidateTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Please complete the required fields.");
        if (title.Trim().Length > 200)
            throw new ArgumentException("Keep the title under 200 characters.");
    }

    private static void ValidateAssignment(
        int teacherID,
        string priority)
    {
        if (teacherID <= 0)
            throw new ArgumentException("Please complete the required fields.");
        _ = NormalizePriority(priority);
    }

    private static string NormalizePriority(string priority)
    {
        if (string.IsNullOrWhiteSpace(priority))
            throw new ArgumentException("Please complete the required fields.");

        string normalized = char.ToUpperInvariant(priority[0]) + priority[1..].ToLowerInvariant();
        if (normalized is not ("Low" or "Medium" or "High"))
            throw new ArgumentException("Please complete the required fields.");

        return normalized;
    }

}
