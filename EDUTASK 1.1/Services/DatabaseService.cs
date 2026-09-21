using Microsoft.Data.SqlClient;
using System.Data;
using EDUTASK_1._1.Models;
using SubtaskDraft = EDUTASK_1._1.Models.SubtaskDraft;
using TaskDiscussionItem = EDUTASK_1._1.Models.TaskDiscussionItem;
using SubtaskDisplayItem = EDUTASK_1._1.Models.SubtaskDisplayItem;

namespace EDUTASK_1._1.Services;

public class DatabaseService
{
    private const string ConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=EduTaskDB;Trusted_Connection=True;TrustServerCertificate=True;";

    public SqlConnection GetConnection() => new(ConnectionString);

    private static readonly SemaphoreSlim ConsistentColumnNamesGate = new(1, 1);
    private static volatile bool _consistentColumnNamesEnsured;

    internal async Task EnsureConsistentColumnNamesAsync(CancellationToken cancellationToken = default)
    {
        if (_consistentColumnNamesEnsured)
            return;

        await ConsistentColumnNamesGate.WaitAsync(cancellationToken);
        try
        {
            if (_consistentColumnNamesEnsured)
                return;

#if !ANDROID
            object? oldSchemaMarker = await ExecuteScalarAsync(
                "SELECT CASE WHEN COL_LENGTH(N'dbo.Roles', N'RoleID') IS NOT NULL THEN 1 ELSE 0 END",
                cancellationToken: cancellationToken);
            if (Convert.ToInt32(oldSchemaMarker ?? 0) == 1)
            {
                await using Stream? stream = typeof(DatabaseService).Assembly
                    .GetManifestResourceStream("EDUTASK.ConsistentColumnNamesMigration.sql");
                if (stream is null)
                    throw new InvalidOperationException("The consistent-column-name migration is missing from the application package.");

                using var reader = new StreamReader(stream);
                string migration = await reader.ReadToEndAsync(cancellationToken);
                await ExecuteNonQueryAsync(migration, cancellationToken: cancellationToken);
            }
#endif

            _consistentColumnNamesEnsured = true;
        }
        finally
        {
            ConsistentColumnNamesGate.Release();
        }
    }

    private static readonly SemaphoreSlim CleanConsistentSchemaGate = new(1, 1);
    private static volatile bool _cleanConsistentSchemaEnsured;

    internal async Task EnsureCleanConsistentSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (_cleanConsistentSchemaEnsured)
            return;

        await CleanConsistentSchemaGate.WaitAsync(cancellationToken);
        try
        {
            if (_cleanConsistentSchemaEnsured)
                return;

            await EnsureConsistentColumnNamesAsync(cancellationToken);
            using Stream migration = typeof(DatabaseService).Assembly.GetManifestResourceStream(
                "EDUTASK.CleanConsistentSchemaMigration.sql")
                ?? throw new InvalidOperationException("The clean consistent-schema migration is missing.");
            using var reader = new StreamReader(migration);
            string query = await reader.ReadToEndAsync(cancellationToken);
            await ExecuteNonQueryAsync(query, cancellationToken: cancellationToken);
            _cleanConsistentSchemaEnsured = true;
        }
        finally
        {
            CleanConsistentSchemaGate.Release();
        }
    }

    public async Task<AuthenticationResult> AuthenticateAsync(string username, string password)
    {
        await EnsureAuthenticationSchemaAsync();
        const string query = """
            SELECT u.User_id AS AccountID, N'User' AS AccountType, u.Password, u.Username AS StoredUsername,
                   u.Is_active, u.Disabled_at, r.Role_name
            FROM dbo.[User] u
            INNER JOIN dbo.Roles r ON r.Role_id = u.Role_id
            WHERE u.Username = @Username
            UNION ALL
            SELECT t.Teacher_id, N'Teacher', t.Password, t.Username,
                   t.Is_active, t.Disabled_at, r.Role_name
            FROM dbo.Teacher t
            INNER JOIN dbo.Roles r ON r.Role_id = t.Role_id
            WHERE t.Username = @Username
            """;
        string normalizedUsername = username.Trim();
        DataTable table = await ExecuteQueryAsync(query,
        [
            new SqlParameter("@Username", SqlDbType.NVarChar, 100) { Value = normalizedUsername }
        ]);

        // SQL Server's default collation is commonly case-insensitive. Require
        // the typed username to match the stored value exactly so @ADMiN does
        // not authenticate an account registered as @admin.
        List<DataRow> exactMatches = table.AsEnumerable()
            .Where(row => string.Equals(
                row.Field<string>("StoredUsername"),
                normalizedUsername,
                StringComparison.Ordinal))
            .ToList();

        if (exactMatches.Count > 1)
            return new(false, "This username is assigned to multiple accounts. Contact the Director.", string.Empty, null, null);
        if (exactMatches.Count == 0 ||
            !CredentialHashService.Verify(password, exactMatches[0].Field<string>("Password") ?? string.Empty))
            return new(false, "The username or password is incorrect.", string.Empty, null, null);

        DataRow row = exactMatches[0];
        string role = row.Field<string>("Role_name") ?? string.Empty;
        if (!row.Field<bool>("Is_active"))
        {
            string message = row.IsNull("Disabled_at")
                ? "Your account is waiting for Director approval."
                : "Your account has been disabled. Contact the Director.";
            return new(false, message, role, null, null);
        }

        int id = row.Field<int>("AccountID");
        bool isTeacherAccount = row.Field<string>("AccountType") == "Teacher";
        return isTeacherAccount
            ? new(true, string.Empty, role, null, await GetTeacherByIdAsync(id))
            : new(true, string.Empty, role, await GetUserByIdAsync(id), null);
    }

    public async Task<(bool Success, string Message)> RegisterAccountAsync(
        string role, string fullName, string username, string email, string password,
        string question1, string answer1, string question2, string answer2)
    {
        await EnsureAuthenticationSchemaAsync();
        if (role is not ("Staff" or "Teacher"))
            return (false, "Only Staff and Teacher accounts can be requested.");
        if (!FormFieldValidation.IsValidUsername(username.Trim()))
            return (false, "Enter a valid username.");
        if (question1 == question2)
            return (false, "Choose two different security questions.");
        string normalizedName = fullName.Trim();
        int split = normalizedName.LastIndexOf(' ');
        string firstName = split > 0 ? normalizedName[..split].Trim() : normalizedName;
        string lastName = split > 0 ? normalizedName[(split + 1)..].Trim() : string.Empty;
        string normalizedUsername = username.Trim();
        string table = role == "Teacher" ? "dbo.Teacher" : "dbo.[User]";
        DataTable duplicateCheck = await ExecuteQueryAsync(
            """
            SELECT
                (SELECT COUNT(1) FROM dbo.Teacher WHERE Email=@Email) +
                (SELECT COUNT(1) FROM dbo.[User] WHERE Email=@Email) AS EmailCount,
                (SELECT COUNT(1) FROM dbo.Teacher WHERE Username=@Username) +
                (SELECT COUNT(1) FROM dbo.[User] WHERE Username=@Username) AS UsernameCount
            """,
            [new("@Email", SqlDbType.NVarChar, 255) { Value = email.Trim() },
             new("@Username", SqlDbType.NVarChar, 100) { Value = normalizedUsername }]);
        DataRow duplicateRow = duplicateCheck.Rows[0];
        if (Convert.ToInt32(duplicateRow["UsernameCount"]) > 0)
            return (false, "This username is already taken.");
        if (Convert.ToInt32(duplicateRow["EmailCount"]) > 0)
            return (false, "This email is already registered.");
        string insert = $"""
            INSERT INTO {table}
                (First_name, Last_name, Email, Password, Contact_number, Account_created, Username,
                 Profile_photo, Role_id, Is_active,
                 Security_question_1, Security_answer_1, Security_question_2, Security_answer_2)
            VALUES
                (@First_name, @Last_name, @Email, @Password, N'', GETDATE(), @Username,
                 NULL, (SELECT Role_id FROM dbo.Roles WHERE Role_name=@Role), 0,
                 @Question1, @Answer1, @Question2, @Answer2)
            """;
        await ExecuteNonQueryAsync(insert,
        [
            new("@First_name", SqlDbType.NVarChar, 100) { Value = firstName },
            new("@Last_name", SqlDbType.NVarChar, 100) { Value = lastName },
            new("@Email", SqlDbType.NVarChar, 255) { Value = email.Trim() },
            new("@Password", SqlDbType.NVarChar, 500) { Value = CredentialHashService.Hash(password) },
            new("@Username", SqlDbType.NVarChar, 100) { Value = normalizedUsername },
            new("@Role", SqlDbType.NVarChar, 50) { Value = role },
            new("@Question1", SqlDbType.NVarChar, 200) { Value = question1 },
            new("@Answer1", SqlDbType.NVarChar, 500) { Value = CredentialHashService.Hash(CredentialHashService.NormalizeAnswer(answer1)) },
            new("@Question2", SqlDbType.NVarChar, 200) { Value = question2 },
            new("@Answer2", SqlDbType.NVarChar, 500) { Value = CredentialHashService.Hash(CredentialHashService.NormalizeAnswer(answer2)) }
        ]);
        return (true, "Your account request was submitted and is waiting for Director approval.");
    }

    public async Task<(bool Success, int AccountId, string Role, string Question1, string Question2)> GetPasswordRecoveryChallengeAsync(
        string identifier)
    {
        await EnsureAuthenticationSchemaAsync();
        string normalizedIdentifier = identifier.Trim();
        const string query = """
            SELECT u.User_id AS AccountID, u.Username, u.Email, r.Role_name AS AccountRole,
                   u.Security_question_1, u.Security_question_2
            FROM dbo.[User] u
            INNER JOIN dbo.Roles r ON r.Role_id=u.Role_id
            WHERE (u.Username=@Identifier OR u.Email=@Identifier) AND u.Is_active=1

            UNION ALL

            SELECT t.Teacher_id AS AccountID, t.Username, t.Email, N'Teacher' AS AccountRole,
                   t.Security_question_1, t.Security_question_2
            FROM dbo.Teacher t
            WHERE (t.Username=@Identifier OR t.Email=@Identifier) AND t.Is_active=1
            """;

        DataTable table = await ExecuteQueryAsync(query,
        [
            new("@Identifier", SqlDbType.NVarChar, 255) { Value = normalizedIdentifier }
        ]);

        DataRow[] matches = table.AsEnumerable()
            .Where(row =>
                string.Equals(row.Field<string>("Username"), normalizedIdentifier, StringComparison.Ordinal) ||
                string.Equals(row.Field<string>("Email"), normalizedIdentifier, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (matches.Length != 1)
            return (false, 0, string.Empty, string.Empty, string.Empty);

        DataRow row = matches[0];
        string role = row.Field<string>("AccountRole") ?? string.Empty;
        string question1 = row.Field<string>("Security_question_1") ?? string.Empty;
        string question2 = row.Field<string>("Security_question_2") ?? string.Empty;
        return role.Length == 0 || question1.Length == 0 || question2.Length == 0
            ? (false, 0, string.Empty, string.Empty, string.Empty)
            : (true, row.Field<int>("AccountID"), role, question1, question2);
    }

    public async Task<bool> VerifyPasswordRecoveryAnswersAsync(
        int accountId, string role, string answer1, string answer2)
    {
        await EnsureAuthenticationSchemaAsync();
        string table = role == "Teacher" ? "dbo.Teacher" : "dbo.[User]";
        string idColumn = role == "Teacher" ? "Teacher_id" : "User_id";
        DataTable result = await ExecuteQueryAsync(
            $"SELECT Security_answer_1, Security_answer_2 FROM {table} WHERE {idColumn}=@AccountID AND Is_active=1",
            [new("@AccountID", SqlDbType.Int) { Value = accountId }]);

        if (result.Rows.Count == 0)
            return false;

        DataRow row = result.Rows[0];
        string hash1 = row.Field<string>("Security_answer_1") ?? string.Empty;
        string hash2 = row.Field<string>("Security_answer_2") ?? string.Empty;
        return CredentialHashService.Verify(CredentialHashService.NormalizeAnswer(answer1), hash1) &&
               CredentialHashService.Verify(CredentialHashService.NormalizeAnswer(answer2), hash2);
    }

    public async Task<(bool Success, string Message)> ResetPasswordAsync(
        int accountId, string role, string newPassword)
    {
        await EnsureAuthenticationSchemaAsync();
        string table = role == "Teacher" ? "dbo.Teacher" : "dbo.[User]";
        string idColumn = role == "Teacher" ? "Teacher_id" : "User_id";
        object? storedValue = await ExecuteScalarAsync(
            $"SELECT Password FROM {table} WHERE {idColumn}=@AccountID AND Is_active=1",
            [new("@AccountID", SqlDbType.Int) { Value = accountId }]);

        string storedPassword = storedValue?.ToString() ?? string.Empty;
        if (storedPassword.Length == 0)
            return (false, "Password could not be updated.");
        if (CredentialHashService.Verify(newPassword, storedPassword))
            return (false, "Choose a password you have not used here.");

        await ExecuteNonQueryAsync(
            $"UPDATE {table} SET Password=@Password WHERE {idColumn}=@AccountID AND Is_active=1",
            [
                new("@Password", SqlDbType.NVarChar, 500) { Value = CredentialHashService.Hash(newPassword) },
                new("@AccountID", SqlDbType.Int) { Value = accountId }
            ]);
        return (true, "Password updated successfully.");
    }
    public async Task<List<PendingAccountItem>> GetPendingAccountsAsync()
    {
        await EnsureAuthenticationSchemaAsync();
        const string query = """
            SELECT u.User_id AccountID, N'Staff' AccountType,
                   CONCAT(u.First_name, N' ', u.Last_name) FullName, u.Email, u.Account_created RequestedAt
            FROM dbo.[User] u INNER JOIN dbo.Roles r ON r.Role_id=u.Role_id
            WHERE u.Is_active=0 AND u.Disabled_at IS NULL AND r.Role_name=N'Staff'
            UNION ALL
            SELECT t.Teacher_id, N'Teacher', CONCAT(t.First_name, N' ', t.Last_name), t.Email, t.Account_created
            FROM dbo.Teacher t WHERE t.Is_active=0 AND t.Disabled_at IS NULL
            ORDER BY RequestedAt
            """;
        DataTable table = await ExecuteQueryAsync(query);
        return table.AsEnumerable().Select(row => new PendingAccountItem(
            row.Field<int>("AccountID"),
            row.Field<string>("AccountType") ?? string.Empty,
            row.Field<string>("FullName") ?? string.Empty,
            row.Field<string>("Email") ?? string.Empty,
            row.Field<DateTime>("RequestedAt"))).ToList();
    }

    public Task<List<DirectoryAccountItem>> GetActiveDirectoryAsync(bool includeStaff = true) =>
        GetDirectoryAsync(includeStaff, disabled: false);

    /// <summary>
    /// Accounts a Director has switched off. Distinct from the accounts waiting
    /// for approval, which are also Is_active=0 â€” Disabled_at is what tells the
    /// two apart.
    /// </summary>
    public Task<List<DirectoryAccountItem>> GetDisabledDirectoryAsync(bool includeStaff = true) =>
        GetDirectoryAsync(includeStaff, disabled: true);

    private async Task<List<DirectoryAccountItem>> GetDirectoryAsync(bool includeStaff, bool disabled)
    {
        await EnsureAuthenticationSchemaAsync();

        // Not user input: one of two fixed fragments chosen by the caller's flag.
        string state = disabled
            ? "Is_active=0 AND Disabled_at IS NOT NULL"
            : "Is_active=1";

        string query = includeStaff
            ? $"""
              SELECT u.User_id AccountID, N'Staff' AccountType,
                     CONCAT(u.First_name, N' ', u.Last_name) FullName,
                     u.Username, u.Email, u.Contact_number
              FROM dbo.[User] u INNER JOIN dbo.Roles r ON r.Role_id=u.Role_id
              WHERE r.Role_name=N'Staff' AND u.{state}
              UNION ALL
              SELECT t.Teacher_id, N'Teacher', CONCAT(t.First_name, N' ', t.Last_name),
                     t.Username, t.Email, t.Contact_number
              FROM dbo.Teacher t WHERE t.{state}
              ORDER BY AccountType, FullName
              """
            : $"""
              SELECT t.Teacher_id AccountID, N'Teacher' AccountType,
                     CONCAT(t.First_name, N' ', t.Last_name) FullName,
                     t.Username, t.Email, t.Contact_number
              FROM dbo.Teacher t WHERE t.{state}
              ORDER BY FullName
              """;

        DataTable table = await ExecuteQueryAsync(query);
        return table.AsEnumerable().Select(row => new DirectoryAccountItem(
            row.Field<int>("AccountID"),
            row.Field<string>("AccountType") ?? string.Empty,
            row.Field<string>("FullName") ?? string.Empty,
            row.Field<string>("Username") ?? string.Empty,
            row.Field<string>("Email") ?? string.Empty,
            row.Field<string>("Contact_number") ?? string.Empty,
            disabled)).ToList();
    }

    public async Task<bool> ApproveAccountAsync(int accountID, string accountType)
    {
        if (!UserSessionService.IsDirector)
            throw new UnauthorizedAccessException("Only a Director can approve accounts.");

        await EnsureAuthenticationSchemaAsync();

        string table = accountType == "Teacher" ? "dbo.Teacher" : "dbo.[User]";
        string idColumn = accountType == "Teacher" ? "Teacher_id" : "User_id";
        return await ExecuteNonQueryAsync(
            $"UPDATE {table} SET Is_active=1, Disabled_at=NULL WHERE {idColumn}=@AccountID AND Is_active=0",
            [new("@AccountID", SqlDbType.Int) { Value = accountID }]) == 1;
    }

    /// <summary>
    /// Switches an account off or back on.
    ///
    /// Disabling stamps Disabled_at, which is the only thing separating a
    /// switched-off account from one that has never been approved: both sit at
    /// Is_active=0, and without the stamp a disabled account would reappear in
    /// the approval requests queue.
    /// </summary>
    public async Task<bool> SetAccountDisabledAsync(int accountID, string accountType, bool disabled)
    {
        if (!UserSessionService.IsDirector)
            throw new UnauthorizedAccessException("Only a Director can disable accounts.");

        await EnsureAuthenticationSchemaAsync();

        string table = accountType == "Teacher" ? "dbo.Teacher" : "dbo.[User]";
        string idColumn = accountType == "Teacher" ? "Teacher_id" : "User_id";

        string update;
        if (disabled && string.Equals(accountType, "Teacher", StringComparison.OrdinalIgnoreCase))
        {
            // Repeat the unfinished-task check in the UPDATE itself. The UI checks
            // first to explain the block, while this guard prevents a concurrent
            // assignment from being added between that check and this write.
            update = """
                UPDATE dbo.Teacher
                SET Is_active=0, Disabled_at=SYSUTCDATETIME()
                WHERE Teacher_id=@AccountID
                  AND Is_active=1
                  AND NOT EXISTS
                  (
                      SELECT 1
                      FROM dbo.TaskAssignment ta
                      WHERE ta.Teacher_id=@AccountID
                        AND ISNULL(ta.Completion_status, N'Pending') <> N'Completed'
                  )
                """;
        }
        else
        {
            update = disabled
                ? $"UPDATE {table} SET Is_active=0, Disabled_at=SYSUTCDATETIME() WHERE {idColumn}=@AccountID AND Is_active=1"
                : $"UPDATE {table} SET Is_active=1, Disabled_at=NULL WHERE {idColumn}=@AccountID AND Disabled_at IS NOT NULL";
        }

        return await ExecuteNonQueryAsync(
            update, [new("@AccountID", SqlDbType.Int) { Value = accountID }]) == 1;
    }

    public async Task<int> GetTeacherUnfinishedTaskCountAsync(
        int teacherID,
        CancellationToken cancellationToken = default)
    {
        const string query = """
            SELECT COUNT(DISTINCT ta.Task_id)
            FROM dbo.TaskAssignment ta
            WHERE ta.Teacher_id = @Teacher_id
              AND ISNULL(ta.Completion_status, N'Pending') <> N'Completed'
            """;

        object? count = await ExecuteScalarAsync(
            query,
            [new SqlParameter("@Teacher_id", SqlDbType.Int) { Value = teacherID }],
            cancellationToken);
        return Convert.ToInt32(count);
    }

    /// <summary>
    /// Promotes an active Staff user to Director. Both the signed-in session and
    /// the database record of the acting user must still identify a Director;
    /// the client cannot grant this permission by merely supplying a role name.
    /// </summary>
    public async Task<bool> PromoteStaffToDirectorAsync(int staffUserID)
    {
        if (!UserSessionService.IsDirector)
            throw new UnauthorizedAccessException("Only a Director can promote Staff accounts.");

        await EnsureAuthenticationSchemaAsync();

        const string query = """
            SET NOCOUNT ON;
            SET XACT_ABORT ON;
            BEGIN TRANSACTION;

            DECLARE @Previous_role_id int;
            DECLARE @DirectorRoleID int;

            SELECT @Previous_role_id = u.Role_id
            FROM dbo.[User] u WITH (UPDLOCK, HOLDLOCK)
            INNER JOIN dbo.Roles r ON r.Role_id = u.Role_id
            WHERE u.User_id = @StaffUserID
              AND u.Is_active = 1
              AND u.Disabled_at IS NULL
              AND r.Role_name = N'Staff';

            SELECT @DirectorRoleID = Role_id
            FROM dbo.Roles
            WHERE Role_name = N'Director';

            IF @Previous_role_id IS NULL OR @DirectorRoleID IS NULL OR NOT EXISTS
            (
                SELECT 1
                FROM dbo.[User] actingUser
                INNER JOIN dbo.Roles actingRole ON actingRole.Role_id = actingUser.Role_id
                WHERE actingUser.User_id = @Acting_user_id
                  AND actingUser.Is_active = 1
                  AND actingUser.Disabled_at IS NULL
                  AND actingRole.Role_name = N'Director'
            )
            BEGIN
                ROLLBACK TRANSACTION;
                SELECT CAST(0 AS bit);
                RETURN;
            END;

            UPDATE dbo.[User]
            SET Role_id = @DirectorRoleID
            WHERE User_id = @StaffUserID AND Role_id = @Previous_role_id;

            INSERT INTO dbo.Promotion
                (Promoted_user_id, Promotedby_user_id, Promoted_at)
            VALUES
                (@StaffUserID, @Acting_user_id, SYSUTCDATETIME());

            COMMIT TRANSACTION;
            SELECT CAST(1 AS bit);
            """;

        object? result = await ExecuteScalarAsync(query,
        [
            new SqlParameter("@StaffUserID", SqlDbType.Int) { Value = staffUserID },
            new SqlParameter("@Acting_user_id", SqlDbType.Int) { Value = UserSessionService.CurrentUserId }
        ]);
        return Convert.ToBoolean(result ?? false);
    }

    private async System.Threading.Tasks.Task EnsureAuthenticationSchemaAsync()
    {
        await EnsureCleanConsistentSchemaAsync();

        const string query = """
            IF COL_LENGTH(N'dbo.User', N'Security_question_1') IS NULL ALTER TABLE dbo.[User] ADD Security_question_1 nvarchar(200) NULL;
            IF COL_LENGTH(N'dbo.User', N'Security_answer_1') IS NULL ALTER TABLE dbo.[User] ADD Security_answer_1 nvarchar(500) NULL;
            IF COL_LENGTH(N'dbo.User', N'Security_question_2') IS NULL ALTER TABLE dbo.[User] ADD Security_question_2 nvarchar(200) NULL;
            IF COL_LENGTH(N'dbo.User', N'Security_answer_2') IS NULL ALTER TABLE dbo.[User] ADD Security_answer_2 nvarchar(500) NULL;
            IF COL_LENGTH(N'dbo.Teacher', N'Security_question_1') IS NULL ALTER TABLE dbo.Teacher ADD Security_question_1 nvarchar(200) NULL;
            IF COL_LENGTH(N'dbo.Teacher', N'Security_answer_1') IS NULL ALTER TABLE dbo.Teacher ADD Security_answer_1 nvarchar(500) NULL;
            IF COL_LENGTH(N'dbo.Teacher', N'Security_question_2') IS NULL ALTER TABLE dbo.Teacher ADD Security_question_2 nvarchar(200) NULL;
            IF COL_LENGTH(N'dbo.Teacher', N'Security_answer_2') IS NULL ALTER TABLE dbo.Teacher ADD Security_answer_2 nvarchar(500) NULL;
            IF COL_LENGTH(N'dbo.User', N'Disabled_at') IS NULL ALTER TABLE dbo.[User] ADD Disabled_at datetime2 NULL;
            IF COL_LENGTH(N'dbo.Teacher', N'Disabled_at') IS NULL ALTER TABLE dbo.Teacher ADD Disabled_at datetime2 NULL;

            IF COL_LENGTH(N'dbo.User', N'Bio') IS NOT NULL ALTER TABLE dbo.[User] DROP COLUMN Bio;
            IF COL_LENGTH(N'dbo.User', N'Position') IS NOT NULL ALTER TABLE dbo.[User] DROP COLUMN Position;
            IF COL_LENGTH(N'dbo.Teacher', N'Bio') IS NOT NULL ALTER TABLE dbo.Teacher DROP COLUMN Bio;
            IF COL_LENGTH(N'dbo.Teacher', N'Position') IS NOT NULL ALTER TABLE dbo.Teacher DROP COLUMN Position;

            IF OBJECT_ID(N'dbo.Promotion', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.Promotion
                (
                    Promotion_id bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_Promotion PRIMARY KEY,
                    Promoted_user_id int NOT NULL,
                    Promotedby_user_id int NOT NULL,
                    Promoted_at datetime2 NOT NULL CONSTRAINT DF_Promotion_PromotedAt DEFAULT (SYSUTCDATETIME()),
                    CONSTRAINT FK_Promotion_PromotedUser FOREIGN KEY (Promoted_user_id) REFERENCES dbo.[User](User_id),
                    CONSTRAINT FK_Promotion_PromotedBy FOREIGN KEY (Promotedby_user_id) REFERENCES dbo.[User](User_id)
                );
            END;
            """;
        await ExecuteNonQueryAsync(query);
    }

    public async Task<User?> GetUserByIdAsync(
        int userID,
        CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticationSchemaAsync();

        const string query = """
            SELECT u.User_id, u.First_name, u.Last_name, u.Email, u.Contact_number,
                   u.Account_created, u.Username, u.Profile_photo,
                   r.Role_name, u.Is_active
            FROM dbo.[User] u
            INNER JOIN dbo.Roles r ON r.Role_id = u.Role_id
            WHERE u.User_id = @User_id
            """;

        DataTable table = await ExecuteQueryAsync(
            query,
            [new SqlParameter("@User_id", SqlDbType.Int) { Value = userID }],
            cancellationToken);

        if (table.Rows.Count == 0)
            return null;

        DataRow row = table.Rows[0];
        return new User
        {
            User_id = row.Field<int>("User_id"),
            First_name = row.Field<string>("First_name") ?? string.Empty,
            Last_name = row.Field<string>("Last_name") ?? string.Empty,
            Email = row.Field<string>("Email") ?? string.Empty,
            Contact_number = row.Field<string>("Contact_number") ?? string.Empty,
            Account_created = row.Field<DateTime>("Account_created"),
            Username = row.Field<string>("Username") ?? string.Empty,
            Profile_photo = row.Field<string>("Profile_photo") ?? string.Empty,
            Role_name = row.Field<string>("Role_name") ?? string.Empty,
            Is_active = row.Field<bool>("Is_active")
        };
    }

    public async Task<bool> UpdateUserProfileAsync(
        int userID,
        string fullName,
        string contactNumber,
        string email,
        string username,
        string? profilePhotoPath,
        CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticationSchemaAsync();

        string normalizedName = fullName.Trim();
        int lastSpace = normalizedName.LastIndexOf(' ');
        string firstName = lastSpace > 0 ? normalizedName[..lastSpace].Trim() : normalizedName;
        string lastName = lastSpace > 0 ? normalizedName[(lastSpace + 1)..].Trim() : string.Empty;

        const string query = """
            UPDATE dbo.[User]
            SET First_name = @First_name,
                Last_name = @Last_name,
                Contact_number = @Contact_number,
                Email = @Email,
                Username = @Username,
                Profile_photo = @Profile_photo
            WHERE User_id = @User_id
            """;

        int rows = await ExecuteNonQueryAsync(
            query,
            [
                new SqlParameter("@First_name", SqlDbType.NVarChar, 100) { Value = firstName },
                new SqlParameter("@Last_name", SqlDbType.NVarChar, 100) { Value = lastName },
                new SqlParameter("@Contact_number", SqlDbType.NVarChar, 30) { Value = contactNumber.Trim() },
                new SqlParameter("@Email", SqlDbType.NVarChar, 255) { Value = email.Trim() },
                new SqlParameter("@Username", SqlDbType.NVarChar, 50) { Value = username.Trim() },
                new SqlParameter("@Profile_photo", SqlDbType.NVarChar, 500)
                {
                    Value = string.IsNullOrWhiteSpace(profilePhotoPath) ? DBNull.Value : profilePhotoPath
                },
                new SqlParameter("@User_id", SqlDbType.Int) { Value = userID }
            ],
            cancellationToken);

        return rows > 0;
    }

    public async Task<Teachers?> GetTeacherByIdAsync(
        int teacherID,
        CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticationSchemaAsync();

        const string query = """
            SELECT Teacher_id, First_name, Last_name, Email, Contact_number,
                   Account_created, Username, Profile_photo, Is_active
            FROM dbo.Teacher
            WHERE Teacher_id = @Teacher_id
            """;

        DataTable table = await ExecuteQueryAsync(
            query,
            [new SqlParameter("@Teacher_id", SqlDbType.Int) { Value = teacherID }],
            cancellationToken);

        if (table.Rows.Count == 0)
            return null;

        DataRow row = table.Rows[0];
        return new Teachers
        {
            Teacher_id = row.Field<int>("Teacher_id"),
            First_name = row.Field<string>("First_name") ?? string.Empty,
            Last_name = row.Field<string>("Last_name") ?? string.Empty,
            Email = row.Field<string>("Email") ?? string.Empty,
            Contact_number = row.Field<string>("Contact_number") ?? string.Empty,
            Account_created = row.Field<DateTime>("Account_created"),
            Username = row.Field<string>("Username") ?? string.Empty,
            Profile_photo = row.Field<string>("Profile_photo") ?? string.Empty,
            Role_name = "Teacher",
            Is_active = row.Field<bool>("Is_active")
        };
    }

    public async Task<bool> UpdateTeacherProfileAsync(
        int teacherID,
        string fullName,
        string contactNumber,
        string email,
        string username,
        string? profilePhotoPath,
        CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticationSchemaAsync();

        string normalizedName = fullName.Trim();
        int lastSpace = normalizedName.LastIndexOf(' ');
        string firstName = lastSpace > 0 ? normalizedName[..lastSpace].Trim() : normalizedName;
        string lastName = lastSpace > 0 ? normalizedName[(lastSpace + 1)..].Trim() : string.Empty;

        const string query = """
            UPDATE dbo.Teacher
            SET First_name = @First_name,
                Last_name = @Last_name,
                Contact_number = @Contact_number,
                Email = @Email,
                Username = @Username,
                Profile_photo = @Profile_photo
            WHERE Teacher_id = @Teacher_id
            """;

        int rows = await ExecuteNonQueryAsync(
            query,
            [
                new SqlParameter("@First_name", SqlDbType.NVarChar, 100) { Value = firstName },
                new SqlParameter("@Last_name", SqlDbType.NVarChar, 100) { Value = lastName },
                new SqlParameter("@Contact_number", SqlDbType.NVarChar, 30) { Value = contactNumber.Trim() },
                new SqlParameter("@Email", SqlDbType.NVarChar, 255) { Value = email.Trim() },
                new SqlParameter("@Username", SqlDbType.NVarChar, 50) { Value = username.Trim() },
                new SqlParameter("@Profile_photo", SqlDbType.NVarChar, 500)
                {
                    Value = string.IsNullOrWhiteSpace(profilePhotoPath) ? DBNull.Value : profilePhotoPath
                },
                new SqlParameter("@Teacher_id", SqlDbType.Int) { Value = teacherID }
            ],
            cancellationToken);

        return rows > 0;
    }

    public async Task<DataTable> ExecuteQueryAsync(
        string query,
        IEnumerable<SqlParameter>? parameters = null,
        CancellationToken cancellationToken = default)
    {
#if ANDROID
        return await RemoteDatabaseGateway.QueryAsync(query, parameters, cancellationToken);
#else
        await using var connection = GetConnection();
        await using var command = new SqlCommand(query, connection);
        AddParameters(command, parameters);

        await connection.OpenAsync(cancellationToken);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var table = new DataTable();
        table.Load(reader);
        return table;
#endif
    }

    public async Task<int> ExecuteNonQueryAsync(
        string query,
        IEnumerable<SqlParameter>? parameters = null,
        CancellationToken cancellationToken = default)
    {
#if ANDROID
        return await RemoteDatabaseGateway.NonQueryAsync(query, parameters, cancellationToken);
#else
        await using var connection = GetConnection();
        await using var command = new SqlCommand(query, connection);
        AddParameters(command, parameters);

        await connection.OpenAsync(cancellationToken);
        return await command.ExecuteNonQueryAsync(cancellationToken);
#endif
    }

    public async Task<object?> ExecuteScalarAsync(
        string query,
        IEnumerable<SqlParameter>? parameters = null,
        CancellationToken cancellationToken = default)
    {
#if ANDROID
        return await RemoteDatabaseGateway.ScalarAsync(query, parameters, cancellationToken);
#else
        await using var connection = GetConnection();
        await using var command = new SqlCommand(query, connection);
        AddParameters(command, parameters);

        await connection.OpenAsync(cancellationToken);
        return await command.ExecuteScalarAsync(cancellationToken);
#endif
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
        int isActiveColumns = Convert.ToInt32(await ExecuteScalarAsync(
            columnQuery,
            [
                new SqlParameter("@TableName", SqlDbType.NVarChar, 128) { Value = "Teacher" },
                new SqlParameter("@ColumnName", SqlDbType.NVarChar, 128) { Value = "Is_active" }
            ],
            cancellationToken));

        List<string> conditions = [];
        List<SqlParameter> parameters = [];
        if (accountStatusColumns > 0)
        {
            conditions.Add("AccountStatus = @AccountStatus");
            parameters.Add(new SqlParameter("@AccountStatus", SqlDbType.NVarChar, 20) { Value = "Active" });
        }
        if (isActiveColumns > 0)
            conditions.Add("Is_active = 1");

        // The currently attached EduTaskDB may predate AccountStatus/Is_active. Keep the app
        // usable without changing the user's database; once a column exists, its filter is
        // automatically enforced so pending/inactive teachers no longer appear here.
        string whereClause = conditions.Count > 0 ? $"WHERE {string.Join(" AND ", conditions)}" : string.Empty;
        string query = $"""
            SELECT Teacher_id, First_name, Last_name
            FROM Teacher
            {whereClause}
            ORDER BY First_name, Last_name
            """;
        return await ExecuteQueryAsync(query, parameters, cancellationToken);
    }

    private static readonly SemaphoreSlim TaskOwnershipSchemaGate = new(1, 1);
    private static volatile bool _taskOwnershipSchemaEnsured;

    /// <summary>
    /// Prepares every schema used by dashboard actions before the dashboard is
    /// shown. Individual operations retain their own guards as a fallback.
    /// </summary>
    public async Task PrepareOperationalSchemaAsync(CancellationToken cancellationToken = default)
    {
        await EnsureCleanConsistentSchemaAsync(cancellationToken);
        await EnsureTaskOwnershipSchemaAsync(cancellationToken);
        await EnsureSubtaskProofSchemaAsync(cancellationToken);
        await EnsureTaskDiscussionTableAsync(cancellationToken);
        await NotificationDatabaseExtensions.EnsureNotificationStateAsync(this, cancellationToken);
    }

    /// <summary>
    /// Older EduTask databases stored the creator only in Task.User_id. Bring
    /// those rows forward before any task screen reads Createdby_user_id so one
    /// legacy task cannot prevent the entire task feed from loading.
    /// </summary>
    internal async Task EnsureTaskOwnershipSchemaAsync(CancellationToken cancellationToken)
    {
        await EnsureCleanConsistentSchemaAsync(cancellationToken);

        if (_taskOwnershipSchemaEnsured)
            return;

        await TaskOwnershipSchemaGate.WaitAsync(cancellationToken);
        try
        {
            if (_taskOwnershipSchemaEnsured)
                return;

            const string query = """
                IF COL_LENGTH(N'dbo.Task', N'Createdby_user_id') IS NULL
                    ALTER TABLE dbo.[Task] ADD Createdby_user_id int NULL;

                IF COL_LENGTH(N'dbo.Task', N'Updated_at') IS NULL
                    ALTER TABLE dbo.[Task] ADD Updated_at datetime2 NULL;

                IF COL_LENGTH(N'dbo.Task', N'User_id') IS NOT NULL
                    EXEC sys.sp_executesql N'
                        UPDATE dbo.[Task]
                        SET Createdby_user_id = User_id
                        WHERE Createdby_user_id IS NULL;';

                IF EXISTS (SELECT 1 FROM dbo.[Task] WHERE Createdby_user_id IS NULL)
                    THROW 51002, 'One or more tasks do not have a creator.', 1;
                """;

            await ExecuteNonQueryAsync(query, cancellationToken: cancellationToken);
            _taskOwnershipSchemaEnsured = true;
        }
        finally
        {
            TaskOwnershipSchemaGate.Release();
        }
    }

    public async Task<DataTable> GetAllTasksWithTeachersAsync(CancellationToken cancellationToken = default)
    {
        await EnsureTaskOwnershipSchemaAsync(cancellationToken);

        const string query = """
            SELECT
                t.Task_id,
                t.Title,
                t.Description,
                t.Created_at,
                t.Updated_at,
                t.Createdby_user_id,
                ta.Assignment_id,
                ta.Teacher_id,
                ta.Deadline,
                ta.Priority,
                ta.Assigned_at,
                ta.Is_acknowledged,
                ta.Completion_status,
                ta.Completed_at,
                CONCAT(te.First_name, ' ', te.Last_name) AS TeacherName
            FROM [Task] t
            LEFT JOIN TaskAssignment ta ON t.Task_id = ta.Task_id
            LEFT JOIN Teacher te ON ta.Teacher_id = te.Teacher_id
            ORDER BY CASE WHEN ta.Deadline IS NULL THEN 1 ELSE 0 END, ta.Deadline, t.Created_at DESC
            """;

        return await ExecuteQueryAsync(query, cancellationToken: cancellationToken);
    }

    public async Task<DataTable> GetTaskByIDAsync(int taskID, CancellationToken cancellationToken = default)
    {
        await EnsureTaskOwnershipSchemaAsync(cancellationToken);

        const string query = """
            SELECT
                t.Task_id,
                t.Title,
                t.Description,
                t.Created_at,
                t.Updated_at,
                t.Createdby_user_id,
                t.Is_daily_remind,
                ta.Assignment_id,
                ta.Teacher_id,
                ta.Deadline,
                ta.Priority,
                ta.Assigned_at,
                ta.Is_acknowledged,
                ta.Acknowledged_at,
                ta.Completion_status,
                ta.Completed_at,
                CONCAT(te.First_name, ' ', te.Last_name) AS TeacherName,
                te.Profile_photo AS TeacherProfilePhotoPath,
                CONCAT(creator.First_name, ' ', creator.Last_name) AS CreatorName,
                creator.Profile_photo AS CreatorProfilePhotoPath
            FROM [Task] t
            LEFT JOIN TaskAssignment ta ON t.Task_id = ta.Task_id
            LEFT JOIN Teacher te ON ta.Teacher_id = te.Teacher_id
            LEFT JOIN dbo.[User] creator ON creator.User_id = t.Createdby_user_id
            WHERE t.Task_id = @Task_id
            """;

        return await ExecuteQueryAsync(
            query,
            [new SqlParameter("@Task_id", SqlDbType.Int) { Value = taskID }],
            cancellationToken);
    }

    public async Task<DataTable> GetTeacherTasksAsync(int teacherID, CancellationToken cancellationToken = default)
    {
        await EnsureTaskOwnershipSchemaAsync(cancellationToken);

        const string query = """
            SELECT
                t.Task_id,
                t.Title,
                t.Description,
                t.Created_at,
                ta.Assignment_id,
                ta.Deadline,
                ta.Priority,
                ta.Assigned_at,
                ta.Is_acknowledged,
                ta.Completion_status,
                ta.Completed_at
            FROM [Task] t
            INNER JOIN TaskAssignment ta ON t.Task_id = ta.Task_id
            WHERE ta.Teacher_id = @Teacher_id
            ORDER BY ta.Deadline
            """;

        return await ExecuteQueryAsync(
            query,
            [new SqlParameter("@Teacher_id", SqlDbType.Int) { Value = teacherID }],
            cancellationToken);
    }

    public async Task<ProfileTaskSummary> GetUserProfileTaskSummaryAsync(
        int userID,
        CancellationToken cancellationToken = default)
    {
        await EnsureTaskOwnershipSchemaAsync(cancellationToken);

        const string query = """
            WITH UserTasks AS
            (
                SELECT
                    t.Task_id,
                    CASE WHEN COUNT(ta.Assignment_id) > 0
                              AND SUM(CASE WHEN ta.Completion_status = N'Completed' THEN 1 ELSE 0 END) = COUNT(ta.Assignment_id)
                         THEN 1 ELSE 0 END AS Is_completed,
                    CASE WHEN SUM(CASE WHEN ta.Completion_status <> N'Completed'
                                            AND ta.Deadline < CAST(GETDATE() AS date)
                                       THEN 1 ELSE 0 END) > 0
                         THEN 1 ELSE 0 END AS IsOverdue
                FROM dbo.[Task] t
                LEFT JOIN dbo.TaskAssignment ta ON ta.Task_id = t.Task_id
                WHERE t.Createdby_user_id = @User_id
                GROUP BY t.Task_id
            )
            SELECT
                COUNT(*) AS TotalCount,
                COALESCE(SUM(CASE WHEN Is_completed = 0 AND IsOverdue = 0 THEN 1 ELSE 0 END), 0) AS PendingCount,
                COALESCE(SUM(CASE WHEN Is_completed = 0 AND IsOverdue = 1 THEN 1 ELSE 0 END), 0) AS OverdueCount,
                COALESCE(SUM(Is_completed), 0) AS CompletedCount
            FROM UserTasks;
            """;

        DataTable table = await ExecuteQueryAsync(
            query,
            [new SqlParameter("@User_id", SqlDbType.Int) { Value = userID }],
            cancellationToken);
        return ReadProfileTaskSummary(table);
    }

    public async Task<ProfileTaskSummary> GetTeacherProfileTaskSummaryAsync(
        int teacherID,
        CancellationToken cancellationToken = default)
    {
        const string query = """
            SELECT
                COUNT(*) AS TotalCount,
                COALESCE(SUM(CASE WHEN ta.Completion_status <> N'Completed'
                                        AND (ta.Deadline IS NULL OR ta.Deadline >= CAST(GETDATE() AS date))
                                  THEN 1 ELSE 0 END), 0) AS PendingCount,
                COALESCE(SUM(CASE WHEN ta.Completion_status <> N'Completed'
                                        AND ta.Deadline < CAST(GETDATE() AS date)
                                  THEN 1 ELSE 0 END), 0) AS OverdueCount,
                COALESCE(SUM(CASE WHEN ta.Completion_status = N'Completed' THEN 1 ELSE 0 END), 0) AS CompletedCount
            FROM dbo.TaskAssignment ta
            WHERE ta.Teacher_id = @Teacher_id;
            """;

        DataTable table = await ExecuteQueryAsync(
            query,
            [new SqlParameter("@Teacher_id", SqlDbType.Int) { Value = teacherID }],
            cancellationToken);
        return ReadProfileTaskSummary(table);
    }

    private static ProfileTaskSummary ReadProfileTaskSummary(DataTable table)
    {
        if (table.Rows.Count == 0)
            return new ProfileTaskSummary(0, 0, 0, 0);

        DataRow row = table.Rows[0];
        return new ProfileTaskSummary(
            Convert.ToInt32(row["TotalCount"]),
            Convert.ToInt32(row["PendingCount"]),
            Convert.ToInt32(row["OverdueCount"]),
            Convert.ToInt32(row["CompletedCount"]));
    }

    public async Task<int> CreateTaskWithAssignmentsAsync(
        string title,
        string? description,
        int adminID,
        bool Is_daily_remind,
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

        await EnsureTaskOwnershipSchemaAsync(cancellationToken);

        await using var connection = GetConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            foreach (int teacherID in distinctTeacherIDs)
                await EnsureTeacherActiveForAssignmentAsync(
                    connection, transaction, teacherID, cancellationToken);

            const string ensureSubtaskTableQuery = """
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
                END
                """;
            await using (var schemaCommand = new SqlCommand(ensureSubtaskTableQuery, connection, transaction))
                await schemaCommand.ExecuteNonQueryAsync(cancellationToken);

            const string taskQuery = """
                INSERT INTO [Task] (Title, Description, Created_at, Updated_at, Createdby_user_id, Is_daily_remind)
                VALUES (@Title, @Description, GETDATE(), NULL, @User_id, @IsDailyRemind);
                SELECT CAST(SCOPE_IDENTITY() AS int);
                """;
            const string assignmentQuery = """
                INSERT INTO TaskAssignment
                    (Task_id, Teacher_id, Deadline, Priority, Assigned_at, Is_acknowledged, Completion_status)
                VALUES
                    (@Task_id, @Teacher_id, @Deadline, @Priority, GETDATE(), 0, 'Pending')
                """;
            const string subtaskQuery = """
                INSERT INTO dbo.Subtask (Task_id, Title)
                VALUES (@Task_id, @Title)
                """;

            int firstTaskID = 0;
            int taskCopyCount = createIndividualTasks ? distinctTeacherIDs.Length : 1;
            for (int copyIndex = 0; copyIndex < taskCopyCount; copyIndex++)
            {
                await using var taskCommand = new SqlCommand(taskQuery, connection, transaction);
                taskCommand.Parameters.Add("@Title", SqlDbType.NVarChar, 200).Value = title.Trim();
                taskCommand.Parameters.Add("@Description", SqlDbType.NVarChar, -1).Value = description?.Trim() ?? string.Empty;
                taskCommand.Parameters.Add("@User_id", SqlDbType.Int).Value = adminID;
                taskCommand.Parameters.Add("@IsDailyRemind", SqlDbType.Bit).Value = Is_daily_remind;
                int taskID = Convert.ToInt32(await taskCommand.ExecuteScalarAsync(cancellationToken));
                if (firstTaskID == 0)
                    firstTaskID = taskID;

                IEnumerable<int> assignedTeacherIDs = createIndividualTasks
                    ? [distinctTeacherIDs[copyIndex]]
                    : distinctTeacherIDs;
                foreach (int teacherID in assignedTeacherIDs)
                {
                    await using var assignmentCommand = new SqlCommand(assignmentQuery, connection, transaction);
                    assignmentCommand.Parameters.Add("@Task_id", SqlDbType.Int).Value = taskID;
                    assignmentCommand.Parameters.Add("@Teacher_id", SqlDbType.Int).Value = teacherID;
                    assignmentCommand.Parameters.Add("@Deadline", SqlDbType.DateTime).Value = deadline;
                    assignmentCommand.Parameters.Add("@Priority", SqlDbType.NVarChar, 10).Value = NormalizePriority(priority);
                    await assignmentCommand.ExecuteNonQueryAsync(cancellationToken);
                }

                foreach (SubtaskDraft subtask in subtasks)
                {
                    await using var subtaskCommand = new SqlCommand(subtaskQuery, connection, transaction);
                    subtaskCommand.Parameters.Add("@Task_id", SqlDbType.Int).Value = taskID;
                    subtaskCommand.Parameters.Add("@Title", SqlDbType.NVarChar, 200).Value = subtask.Title.Trim();
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

    public async Task<bool> UpdateTaskWithAssignmentAsync(
        int taskID,
        int assignmentID,
        string title,
        string? description,
        bool Is_daily_remind,
        int teacherID,
        DateTime deadline,
        DateTime originalDeadline,
        string priority,
        IReadOnlyCollection<SubtaskDraft> subtasks,
        CancellationToken cancellationToken = default)
    {
        ValidateTaskAssignment(title, teacherID, priority);
        if (subtasks.Any(s => string.IsNullOrWhiteSpace(s.Title)))
            throw new ArgumentException("Every subtask must have a title.");
        await EnsureTaskOwnershipSchemaAsync(cancellationToken);
        await EnsureTaskDiscussionTableAsync(cancellationToken);

        string normalizedTitle = title.Trim();
        string normalizedDescription = description?.Trim() ?? string.Empty;
        string normalizedPriority = NormalizePriority(priority);

        await using var connection = GetConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await EnsureTeacherActiveForAssignmentAsync(
                connection, transaction, teacherID, cancellationToken);

            bool hasMeaningfulChanges;
            const string snapshotQuery = """
                SELECT t.Title, t.Description, t.Is_daily_remind,
                       ta.Teacher_id, ta.Deadline, ta.Priority
                FROM dbo.[Task] t WITH (UPDLOCK, HOLDLOCK)
                INNER JOIN dbo.TaskAssignment ta WITH (UPDLOCK, HOLDLOCK)
                    ON ta.Task_id = t.Task_id
                WHERE t.Task_id = @Task_id AND ta.Assignment_id = @Assignment_id
                """;
            await using (var snapshotCommand = new SqlCommand(snapshotQuery, connection, transaction))
            {
                snapshotCommand.Parameters.Add("@Task_id", SqlDbType.Int).Value = taskID;
                snapshotCommand.Parameters.Add("@Assignment_id", SqlDbType.Int).Value = assignmentID;
                await using var reader = await snapshotCommand.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                    throw new InvalidOperationException("The task or assignment no longer exists.");

                string currentTitle = reader.GetString(0);
                string currentDescription = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                bool currentDailyRemind = !reader.IsDBNull(2) && reader.GetBoolean(2);
                int currentTeacherID = reader.GetInt32(3);
                DateTime currentDeadline = reader.GetDateTime(4);
                string currentPriority = reader.IsDBNull(5) ? string.Empty : reader.GetString(5);

                hasMeaningfulChanges =
                    !string.Equals(currentTitle, normalizedTitle, StringComparison.Ordinal) ||
                    !string.Equals(currentDescription, normalizedDescription, StringComparison.Ordinal) ||
                    currentDailyRemind != Is_daily_remind ||
                    currentTeacherID != teacherID ||
                    currentDeadline != deadline ||
                    !string.Equals(currentPriority, normalizedPriority, StringComparison.Ordinal);
            }

            var existingSubtasks = new Dictionary<int, string>();
            await using (var subtaskSnapshotCommand = new SqlCommand(
                "SELECT Subtask_id, Title FROM dbo.Subtask WITH (UPDLOCK, HOLDLOCK) WHERE Task_id = @Task_id",
                connection, transaction))
            {
                subtaskSnapshotCommand.Parameters.Add("@Task_id", SqlDbType.Int).Value = taskID;
                await using var reader = await subtaskSnapshotCommand.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                    existingSubtasks[reader.GetInt32(0)] = reader.GetString(1);
            }

            Dictionary<int, string> retainedSubtasks = subtasks
                .Where(subtask => subtask.Subtask_id.HasValue)
                .ToDictionary(subtask => subtask.Subtask_id!.Value, subtask => subtask.Title.Trim());
            hasMeaningfulChanges |=
                subtasks.Any(subtask => !subtask.Subtask_id.HasValue) ||
                existingSubtasks.Count != retainedSubtasks.Count ||
                retainedSubtasks.Any(subtask =>
                    !existingSubtasks.TryGetValue(subtask.Key, out string? existingTitle) ||
                    !string.Equals(existingTitle, subtask.Value, StringComparison.Ordinal));

            const string taskQuery = """
                UPDATE [Task]
                SET Title = @Title,
                    Description = @Description,
                    Is_daily_remind = @IsDailyRemind
                WHERE Task_id = @Task_id
                """;
            await using var taskCommand = new SqlCommand(taskQuery, connection, transaction);
            taskCommand.Parameters.Add("@Title", SqlDbType.NVarChar, 200).Value = normalizedTitle;
            taskCommand.Parameters.Add("@Description", SqlDbType.NVarChar, -1).Value = normalizedDescription;
            taskCommand.Parameters.Add("@IsDailyRemind", SqlDbType.Bit).Value = Is_daily_remind;
            taskCommand.Parameters.Add("@Task_id", SqlDbType.Int).Value = taskID;
            int taskRows = await taskCommand.ExecuteNonQueryAsync(cancellationToken);

            const string assignmentQuery = """
                UPDATE TaskAssignment
                SET Teacher_id = @Teacher_id,
                    Deadline = @Deadline,
                    Priority = @Priority
                WHERE Assignment_id = @Assignment_id
                  AND Task_id = @Task_id
                """;
            await using var assignmentCommand = new SqlCommand(assignmentQuery, connection, transaction);
            assignmentCommand.Parameters.Add("@Teacher_id", SqlDbType.Int).Value = teacherID;
            assignmentCommand.Parameters.Add("@Deadline", SqlDbType.DateTime).Value = deadline;
            assignmentCommand.Parameters.Add("@Priority", SqlDbType.NVarChar, 10).Value = normalizedPriority;
            assignmentCommand.Parameters.Add("@Assignment_id", SqlDbType.Int).Value = assignmentID;
            assignmentCommand.Parameters.Add("@Task_id", SqlDbType.Int).Value = taskID;
            int assignmentRows = await assignmentCommand.ExecuteNonQueryAsync(cancellationToken);

            if (taskRows == 0 || assignmentRows == 0)
                throw new InvalidOperationException("The task or assignment no longer exists.");

            int[] retainedIDs = subtasks.Where(s => s.Subtask_id.HasValue).Select(s => s.Subtask_id!.Value).Distinct().ToArray();
            var existingIDs = new List<int>();
            await using (var command = new SqlCommand("SELECT Subtask_id FROM dbo.Subtask WHERE Task_id = @Task_id", connection, transaction))
            {
                command.Parameters.Add("@Task_id", SqlDbType.Int).Value = taskID;
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken)) existingIDs.Add(reader.GetInt32(0));
            }
            foreach (int removedID in existingIDs.Except(retainedIDs))
            {
                // Lock the subtask until commit so a new message cannot race its removal.
                await using var guard = new SqlCommand("""
                    SELECT CAST(CASE WHEN latest.Proof_status = N'Approved' THEN 1 ELSE 0 END AS bit)
                    FROM dbo.Subtask s WITH (XLOCK, HOLDLOCK)
                    OUTER APPLY
                    (
                        SELECT TOP (1) h.Proof_status
                        FROM dbo.ProofSubmission h
                        WHERE h.Subtask_id = s.Subtask_id
                        ORDER BY h.Attempt_number DESC, h.Submission_id DESC
                    ) latest
                    WHERE s.Subtask_id = @Subtask_id AND s.Task_id = @Task_id
                    """, connection, transaction);
                guard.Parameters.Add("@Subtask_id", SqlDbType.Int).Value = removedID;
                guard.Parameters.Add("@Task_id", SqlDbType.Int).Value = taskID;
                if (await guard.ExecuteScalarAsync(cancellationToken) is true)
                    throw new InvalidOperationException("Completed or approved subtasks cannot be removed. Reload the task and try again.");
                await using var discussions = new SqlCommand(
                    "DELETE FROM dbo.TaskDiscussion WHERE Subtask_id = @Subtask_id", connection, transaction);
                discussions.Parameters.Add("@Subtask_id", SqlDbType.Int).Value = removedID;
                await discussions.ExecuteNonQueryAsync(cancellationToken);
                await using var history = new SqlCommand(
                    "DELETE FROM dbo.ProofSubmission WHERE Subtask_id = @Subtask_id", connection, transaction);
                history.Parameters.Add("@Subtask_id", SqlDbType.Int).Value = removedID;
                await history.ExecuteNonQueryAsync(cancellationToken);
                await using var remove = new SqlCommand("DELETE FROM dbo.Subtask WHERE Subtask_id = @Subtask_id AND Task_id = @Task_id", connection, transaction);
                remove.Parameters.Add("@Subtask_id", SqlDbType.Int).Value = removedID;
                remove.Parameters.Add("@Task_id", SqlDbType.Int).Value = taskID;
                await remove.ExecuteNonQueryAsync(cancellationToken);
            }
            foreach (SubtaskDraft subtask in subtasks)
            {
                string sql = subtask.Subtask_id.HasValue
                    ? "UPDATE dbo.Subtask SET Title = @Title WHERE Subtask_id = @Subtask_id AND Task_id = @Task_id"
                    : "INSERT INTO dbo.Subtask (Task_id, Title) VALUES (@Task_id, @Title)";
                await using var command = new SqlCommand(sql, connection, transaction);
                command.Parameters.Add("@Task_id", SqlDbType.Int).Value = taskID;
                command.Parameters.Add("@Title", SqlDbType.NVarChar, 200).Value = subtask.Title.Trim();
                if (subtask.Subtask_id.HasValue) command.Parameters.Add("@Subtask_id", SqlDbType.Int).Value = subtask.Subtask_id.Value;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            if (hasMeaningfulChanges)
            {
                await using var updatedAtCommand = new SqlCommand(
                    "UPDATE dbo.[Task] SET Updated_at = SYSDATETIME() WHERE Task_id = @Task_id",
                    connection, transaction);
                updatedAtCommand.Parameters.Add("@Task_id", SqlDbType.Int).Value = taskID;
                await updatedAtCommand.ExecuteNonQueryAsync(cancellationToken);
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
            SET Is_acknowledged = 1,
                Acknowledged_at = GETDATE(),
                Completion_status = CASE
                    WHEN Completion_status = N'Pending' THEN N'Acknowledged'
                    ELSE Completion_status
                END
            WHERE Assignment_id = @Assignment_id
              AND Is_acknowledged = 0
            """;

        return await ExecuteNonQueryAsync(
            query,
            [new SqlParameter("@Assignment_id", SqlDbType.Int) { Value = assignmentID }],
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
            DECLARE @Task_id int = (SELECT Task_id FROM dbo.TaskAssignment WHERE Assignment_id = @Assignment_id);
            IF @Task_id IS NULL THROW 51140, 'The assignment does not exist.', 1;

            -- Subtasks and their proofs are shared across every teacher assigned to this task.
            UPDATE dbo.TaskAssignment
            SET Completion_status = N'For Validation', Completed_at = NULL
            WHERE Task_id = @Task_id AND Completion_status <> N'Completed';

            EXEC dbo.ApproveTaskCompletion @Task_id = @Task_id, @Acting_user_id = @Acting_user_id;
            COMMIT TRANSACTION;
            """;
        await ExecuteNonQueryAsync(query,
            [
                new SqlParameter("@Assignment_id", SqlDbType.Int) { Value = assignmentID },
                new SqlParameter("@Acting_user_id", SqlDbType.Int) { Value = actingUserID }
            ], cancellationToken);
        return true;
    }

    public async Task<bool> RejectTaskCompletionAsync(
        int assignmentID,
        int actingUserID,
        CancellationToken cancellationToken = default)
    {
        const string query = """
            DECLARE @Task_id int = (SELECT Task_id FROM dbo.TaskAssignment WHERE Assignment_id = @Assignment_id);
            IF @Task_id IS NULL THROW 51140, 'The assignment does not exist.', 1;
            EXEC dbo.RequestTaskRevision @Task_id = @Task_id, @Acting_user_id = @Acting_user_id;
            """;
        await ExecuteNonQueryAsync(query,
            [
                new SqlParameter("@Assignment_id", SqlDbType.Int) { Value = assignmentID },
                new SqlParameter("@Acting_user_id", SqlDbType.Int) { Value = actingUserID }
            ], cancellationToken);
        return true;
    }
    public async Task<bool> DeleteTaskAsync(int taskID, int actingUserID, CancellationToken cancellationToken = default)
    {
        await EnsureTaskDiscussionTableAsync(cancellationToken);
        await using var connection = GetConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await using (var authorize = new SqlCommand("dbo.AssertActiveUserRole", connection, transaction))
            {
                authorize.CommandType = CommandType.StoredProcedure;
                authorize.Parameters.Add("@Acting_user_id", SqlDbType.Int).Value = actingUserID;
                authorize.Parameters.Add("@AllowDirector", SqlDbType.Bit).Value = true;
                authorize.Parameters.Add("@AllowStaff", SqlDbType.Bit).Value = false;
                await authorize.ExecuteNonQueryAsync(cancellationToken);
            }
            const string deleteHistory = """
                DELETE h
                FROM dbo.ProofSubmission h
                INNER JOIN dbo.Subtask s ON s.Subtask_id = h.Subtask_id
                WHERE s.Task_id = @Task_id
                """;
            await using var historyCommand = new SqlCommand(deleteHistory, connection, transaction);
            historyCommand.Parameters.Add("@Task_id", SqlDbType.Int).Value = taskID;
            await historyCommand.ExecuteNonQueryAsync(cancellationToken);

            const string deleteDiscussions = "DELETE d FROM dbo.TaskDiscussion d INNER JOIN dbo.Subtask s ON s.Subtask_id = d.Subtask_id WHERE s.Task_id = @Task_id";
            await using var discussionCommand = new SqlCommand(deleteDiscussions, connection, transaction);
            discussionCommand.Parameters.Add("@Task_id", SqlDbType.Int).Value = taskID;
            await discussionCommand.ExecuteNonQueryAsync(cancellationToken);

            const string deleteAssignments = "DELETE FROM TaskAssignment WHERE Task_id = @Task_id";
            await using var assignmentCommand = new SqlCommand(deleteAssignments, connection, transaction);
            assignmentCommand.Parameters.Add("@Task_id", SqlDbType.Int).Value = taskID;
            await assignmentCommand.ExecuteNonQueryAsync(cancellationToken);

            const string deleteTask = "DELETE FROM [Task] WHERE Task_id = @Task_id";
            await using var taskCommand = new SqlCommand(deleteTask, connection, transaction);
            taskCommand.Parameters.Add("@Task_id", SqlDbType.Int).Value = taskID;
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
        await EnsureSubtaskProofSchemaAsync(cancellationToken);
        const string query = """
            SELECT s.Subtask_id, s.Title,
                   primaryFile.File_name AS Proof_file_name,
                   latest.Proof_status AS Proof_validation_status,
                   latest.Submitted_at AS Proof_uploaded_at,
                   latest.Submittedby_teacher_id AS Proof_submittedby_teacher_id,
                   (SELECT COUNT(*) FROM dbo.ProofAttachment a
                    WHERE a.Submission_id = latest.Submission_id) AS ProofFileCount
            FROM dbo.Subtask s
            OUTER APPLY
            (
                SELECT TOP (1) h.Submission_id, h.Proof_status,
                       h.Submitted_at, h.Submittedby_teacher_id
                FROM dbo.ProofSubmission h
                WHERE h.Subtask_id = s.Subtask_id
                ORDER BY h.Attempt_number DESC, h.Submission_id DESC
            ) latest
            OUTER APPLY
            (
                SELECT TOP (1) a.File_name
                FROM dbo.ProofAttachment a
                WHERE a.Submission_id = latest.Submission_id
                ORDER BY a.Sort_order
            ) primaryFile
            WHERE s.Task_id = @Task_id
            ORDER BY s.Subtask_id
            """;
        DataTable table = await ExecuteQueryAsync(
            query,
            [new SqlParameter("@Task_id", SqlDbType.Int) { Value = taskID }],
            cancellationToken);
        List<SubtaskDisplayItem> subtasks = table.AsEnumerable().Select(row => new SubtaskDisplayItem
        {
            Subtask_id = row.Field<int>("Subtask_id"),
            Task_id = taskID,
            Title = row.Field<string>("Title") ?? string.Empty,
            Proof_file_name = row.Field<string>("Proof_file_name"),
            ProofFileCount = Math.Max(row.Field<int>("ProofFileCount"),
                string.IsNullOrWhiteSpace(row.Field<string>("Proof_file_name")) ? 0 : 1),
            ProofStatus = row.Field<string>("Proof_validation_status"),
            Proof_uploaded_at = row.IsNull("Proof_uploaded_at") ? null : row.Field<DateTime>("Proof_uploaded_at"),
            Proof_submittedby_teacher_id = row.IsNull("Proof_submittedby_teacher_id")
                ? null
                : row.Field<int>("Proof_submittedby_teacher_id")
        }).ToList();

        if (subtasks.Count == 0)
            return subtasks;

        const string historyQuery = """
            SELECT h.Submission_id AS SubmissionID, h.Subtask_id AS Subtask_id,
                   h.Attempt_number AS AttemptNumber, primaryFile.File_name AS File_name,
                   primaryFile.File_type AS File_type, h.Proof_status AS ValidationStatus,
                   h.Submitted_at AS SubmittedAt, h.Reviewed_at AS ReviewedAt,
                   h.Reviewedby_user_id AS ReviewedByUserID, h.Return_remarks AS ReturnRemarks,
                   (SELECT COUNT(*) FROM dbo.ProofAttachment a
                    WHERE a.Submission_id = h.Submission_id) AS FileCount
            FROM dbo.ProofSubmission h
            INNER JOIN dbo.Subtask s ON s.Subtask_id = h.Subtask_id
            OUTER APPLY
            (
                SELECT TOP (1) a.File_name, a.File_type
                FROM dbo.ProofAttachment a
                WHERE a.Submission_id = h.Submission_id
                ORDER BY a.Sort_order
            ) primaryFile
            WHERE s.Task_id = @Task_id
              AND h.Proof_status <> N'Draft'
            ORDER BY h.Subtask_id, h.Attempt_number DESC;
            """;
        DataTable historyTable = await ExecuteQueryAsync(
            historyQuery,
            [new SqlParameter("@Task_id", SqlDbType.Int) { Value = taskID }],
            cancellationToken);
        var historyBySubtask = historyTable.AsEnumerable()
            .GroupBy(row => row.Field<int>("Subtask_id"))
            .ToDictionary(group => group.Key, group => group.Select(row => new ProofSubmissionItem
            {
                SubmissionID = row.Field<int>("SubmissionID"),
                AttemptNumber = row.Field<int>("AttemptNumber"),
                File_name = row.Field<string>("File_name") ?? string.Empty,
                File_type = row.Field<string>("File_type") ?? string.Empty,
                FileCount = Math.Max(1, row.Field<int>("FileCount")),
                ValidationStatus = row.Field<string>("ValidationStatus") ?? string.Empty,
                SubmittedAt = row.Field<DateTime>("SubmittedAt"),
                ReviewedAt = row.IsNull("ReviewedAt") ? null : row.Field<DateTime>("ReviewedAt"),
                ReviewedByUserID = row.IsNull("ReviewedByUserID") ? null : row.Field<int>("ReviewedByUserID"),
                ReturnRemarks = row.Field<string>("ReturnRemarks")
            }).ToList());
        foreach (SubtaskDisplayItem subtask in subtasks)
        {
            if (historyBySubtask.TryGetValue(subtask.Subtask_id, out List<ProofSubmissionItem>? history))
                subtask.ProofHistory.AddRange(history);
        }
        return subtasks;
    }

    public async Task<int> GetPendingProofReviewCountAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureSubtaskProofSchemaAsync(cancellationToken);
        object? count = await ExecuteScalarAsync(
            "SELECT COUNT(*) FROM dbo.ProofSubmission WHERE Proof_status = N'Pending'",
            cancellationToken: cancellationToken);
        return Convert.ToInt32(count);
    }

    private static readonly SemaphoreSlim SubtaskProofSchemaGate = new(1, 1);
    private static volatile bool _subtaskProofSchemaEnsured;

    internal async System.Threading.Tasks.Task EnsureSubtaskProofSchemaAsync(CancellationToken cancellationToken)
    {
        await EnsureCleanConsistentSchemaAsync(cancellationToken);

        if (_subtaskProofSchemaEnsured)
            return;

        await SubtaskProofSchemaGate.WaitAsync(cancellationToken);
        try
        {
            if (_subtaskProofSchemaEnsured)
                return;

            using (Stream migration = typeof(DatabaseService).Assembly.GetManifestResourceStream(
                       "EDUTASK.NormalizeSubtaskProofMigration.sql")
                   ?? throw new InvalidOperationException("The normalized proof schema migration is missing."))
            using (var reader = new StreamReader(migration))
            {
                string normalizedSchemaMigration = await reader.ReadToEndAsync(cancellationToken);
                await ExecuteNonQueryAsync(normalizedSchemaMigration, cancellationToken: cancellationToken);
            }

            _subtaskProofSchemaEnsured = true;
        }
        finally
        {
            SubtaskProofSchemaGate.Release();
        }
    }
    public Task<bool> UploadSubtaskProofAsync(
        int subtaskID,
        int teacherID,
        PreparedProofImage image,
        CancellationToken cancellationToken = default) =>
        UploadSubtaskProofsAsync(subtaskID, teacherID, [image], cancellationToken);

    public async Task<bool> UploadSubtaskProofsAsync(
        int subtaskID,
        int teacherID,
        IReadOnlyList<PreparedProofImage> files,
        CancellationToken cancellationToken = default)
    {
        if (files.Count < 1 || files.Count > ProofImageService.MaximumFiles)
            throw new ArgumentException("Select between one and three proof files.");
        if (files.Any(file => file.Data.Length is 0 or > ProofImageService.MaximumBytes))
            throw new ArgumentException("Each file must be between 1 byte and 10 MB.");
        if (files.Any(file => file.File_type is not ("image/jpeg" or "image/png" or "application/pdf")))
            throw new ArgumentException("Only JPEG, PNG, and PDF files are supported.");

        PreparedProofImage firstFile = files[0];

        await EnsureSubtaskProofSchemaAsync(cancellationToken);

        const string query = """
            SET XACT_ABORT ON;
            SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
            BEGIN TRANSACTION;

            DECLARE @Task_id int;
            DECLARE @CurrentSubmissionID int;
            DECLARE @CurrentStatus nvarchar(20);
            DECLARE @CurrentTeacherID int;
            SELECT @Task_id = Task_id
            FROM dbo.Subtask WITH (UPDLOCK, HOLDLOCK)
            WHERE Subtask_id = @Subtask_id;

            SELECT TOP (1)
                   @CurrentSubmissionID = Submission_id,
                   @CurrentStatus = Proof_status,
                   @CurrentTeacherID = Submittedby_teacher_id
            FROM dbo.ProofSubmission WITH (UPDLOCK, HOLDLOCK)
            WHERE Subtask_id = @Subtask_id
            ORDER BY Attempt_number DESC, Submission_id DESC;

            IF @Task_id IS NULL OR NOT EXISTS
            (
                SELECT 1 FROM dbo.TaskAssignment
                WHERE Task_id = @Task_id AND Teacher_id = @Teacher_id
            )
            BEGIN
                ROLLBACK TRANSACTION;
                THROW 51001, 'Only an assigned teacher can upload proof for this shared subtask.', 1;
            END

            DECLARE @WasReturned bit = CASE WHEN @CurrentStatus = N'Returned' THEN 1 ELSE 0 END;

            IF @CurrentStatus IN (N'Pending', N'Approved')
               OR (@CurrentStatus IN (N'Draft', N'Returned')
                   AND @CurrentTeacherID IS NOT NULL
                   AND @CurrentTeacherID <> @Teacher_id)
            BEGIN
                ROLLBACK TRANSACTION;
                THROW 51000, 'Another teacher already owns or submitted proof for this shared subtask.', 1;
            END;

            IF @CurrentStatus = N'Draft'
            BEGIN
                UPDATE dbo.ProofSubmission
                SET Submitted_at = SYSDATETIME(),
                    Submittedby_teacher_id = @Teacher_id,
                    Reviewed_at = NULL,
                    Reviewedby_user_id = NULL,
                    Return_remarks = NULL
                WHERE Submission_id = @CurrentSubmissionID;
            END
            ELSE
            BEGIN
                DECLARE @AttemptNumber int;
                SELECT @AttemptNumber = ISNULL(MAX(Attempt_number), 0) + 1
                FROM dbo.ProofSubmission WITH (UPDLOCK, HOLDLOCK)
                WHERE Subtask_id = @Subtask_id;

                INSERT INTO dbo.ProofSubmission
                    (Subtask_id, Attempt_number, Proof_status, Submitted_at, Submittedby_teacher_id)
                VALUES
                    (@Subtask_id, @AttemptNumber, N'Draft', SYSDATETIME(), @Teacher_id);
                SET @CurrentSubmissionID = CONVERT(int, SCOPE_IDENTITY());
            END;

            DELETE FROM dbo.ProofAttachment WHERE Submission_id = @CurrentSubmissionID;
            INSERT INTO dbo.ProofAttachment
                (Submission_id, Sort_order, File_name, File_type, Proof_file)
            SELECT @CurrentSubmissionID, attachment.Sort_order, attachment.File_name,
                   attachment.File_type, attachment.Proof_file
            FROM
            (
                VALUES
                    (CAST(1 AS tinyint), @File_name, @File_type, @Proof_file),
                    (CAST(2 AS tinyint), @FileName2, @ContentType2, @FileData2),
                    (CAST(3 AS tinyint), @FileName3, @ContentType3, @FileData3)
            ) attachment (Sort_order, File_name, File_type, Proof_file)
            WHERE attachment.Sort_order <= @FileCount;

            IF @WasReturned = 1
            BEGIN
                UPDATE ta
                SET Completion_status = 'Needs Revision',
                    Completed_at = NULL
                FROM dbo.TaskAssignment ta
                INNER JOIN dbo.Subtask s ON s.Task_id = ta.Task_id
                WHERE s.Subtask_id = @Subtask_id
                  AND ta.Completion_status = 'For Validation';
            END;

            COMMIT TRANSACTION;
            """;

        return await ExecuteNonQueryAsync(query,
            [
                new SqlParameter("@Subtask_id", SqlDbType.Int) { Value = subtaskID },
                new SqlParameter("@Teacher_id", SqlDbType.Int) { Value = teacherID },
                new SqlParameter("@Proof_file", SqlDbType.VarBinary, -1) { Value = firstFile.Data },
                new SqlParameter("@File_name", SqlDbType.NVarChar, 255) { Value = firstFile.File_name },
                new SqlParameter("@File_type", SqlDbType.NVarChar, 50) { Value = firstFile.File_type },
                new SqlParameter("@FileCount", SqlDbType.TinyInt) { Value = files.Count },
                CreateProofFileParameter("@FileName2", SqlDbType.NVarChar, 255, files, 1, file => file.File_name),
                CreateProofFileParameter("@ContentType2", SqlDbType.NVarChar, 50, files, 1, file => file.File_type),
                CreateProofFileParameter("@FileData2", SqlDbType.VarBinary, -1, files, 1, file => file.Data),
                CreateProofFileParameter("@FileName3", SqlDbType.NVarChar, 255, files, 2, file => file.File_name),
                CreateProofFileParameter("@ContentType3", SqlDbType.NVarChar, 50, files, 2, file => file.File_type),
                CreateProofFileParameter("@FileData3", SqlDbType.VarBinary, -1, files, 2, file => file.Data)
            ], cancellationToken) > 0;
    }

    private static SqlParameter CreateProofFileParameter<T>(
        string name,
        SqlDbType type,
        int size,
        IReadOnlyList<PreparedProofImage> files,
        int index,
        Func<PreparedProofImage, T> selector) =>
        new(name, type, size)
        {
            Value = index < files.Count ? selector(files[index])! : DBNull.Value
        };

    public async Task<bool> ConfirmSubtaskProofAsync(
        int subtaskID,
        int teacherID,
        CancellationToken cancellationToken = default)
    {
        await EnsureSubtaskProofSchemaAsync(cancellationToken);
        const string query = """
            SET XACT_ABORT ON;
            SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
            BEGIN TRANSACTION;
            DECLARE @Task_id int;
            DECLARE @ProofTeacherID int;
            DECLARE @SubmissionID int;
            SELECT TOP (1)
                   @Task_id = s.Task_id,
                   @SubmissionID = h.Submission_id,
                   @ProofTeacherID = h.Submittedby_teacher_id
            FROM dbo.Subtask s WITH (UPDLOCK, HOLDLOCK)
            INNER JOIN dbo.ProofSubmission h WITH (UPDLOCK, HOLDLOCK)
                ON h.Subtask_id = s.Subtask_id
            WHERE s.Subtask_id = @Subtask_id
              AND h.Proof_status = N'Draft'
            ORDER BY h.Attempt_number DESC, h.Submission_id DESC;

            IF @Task_id IS NULL
               OR (@ProofTeacherID IS NOT NULL AND @ProofTeacherID <> @Teacher_id)
               OR NOT EXISTS
                  (SELECT 1 FROM dbo.TaskAssignment WHERE Task_id = @Task_id AND Teacher_id = @Teacher_id)
            BEGIN
                ROLLBACK TRANSACTION;
                SELECT CAST(0 AS bit);
                RETURN;
            END;
            UPDATE dbo.ProofSubmission
            SET Proof_status = N'Pending',
                Submitted_at = SYSDATETIME()
            WHERE Submission_id = @SubmissionID
              AND Proof_status = N'Draft';
            -- Move the shared task to validation only after every shared subtask has
            -- a submitted or approved proof. All teacher assignments share this state.
            IF @Task_id IS NOT NULL
               AND NOT EXISTS
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
                      AND ISNULL(latest.Proof_status, N'') NOT IN (N'Pending', N'Approved')
               )
            BEGIN
                UPDATE dbo.TaskAssignment
                SET Completion_status = N'For Validation', Completed_at = NULL
                WHERE Task_id = @Task_id
                  AND Completion_status <> N'Completed';
            END;
            COMMIT TRANSACTION;
            SELECT CAST(1 AS bit);
            """;

        return Convert.ToBoolean(await ExecuteScalarAsync(query,
            [
                new SqlParameter("@Subtask_id", SqlDbType.Int) { Value = subtaskID },
                new SqlParameter("@Teacher_id", SqlDbType.Int) { Value = teacherID }
            ], cancellationToken));
    }
    public async Task<bool> RemoveSubtaskProofAsync(
        int subtaskID,
        int teacherID,
        CancellationToken cancellationToken = default)
    {
        await EnsureSubtaskProofSchemaAsync(cancellationToken);
        const string query = """
            SET XACT_ABORT ON;
            BEGIN TRANSACTION;

            DECLARE @RemovedProofs int = 0;
            DECLARE @DraftSubmissionID int;
            DECLARE @Task_id int;

            SELECT TOP (1)
                   @DraftSubmissionID = h.Submission_id,
                   @Task_id = s.Task_id
            FROM dbo.Subtask s WITH (UPDLOCK, HOLDLOCK)
            INNER JOIN dbo.ProofSubmission h WITH (UPDLOCK, HOLDLOCK)
                ON h.Subtask_id = s.Subtask_id
            WHERE s.Subtask_id = @Subtask_id
              AND h.Proof_status = N'Draft'
              AND (h.Submittedby_teacher_id IS NULL OR h.Submittedby_teacher_id = @Teacher_id)
              AND EXISTS
                  (SELECT 1 FROM dbo.TaskAssignment ta
                   WHERE ta.Task_id = s.Task_id AND ta.Teacher_id = @Teacher_id)
            ORDER BY h.Attempt_number DESC, h.Submission_id DESC;

            IF @DraftSubmissionID IS NOT NULL
            BEGIN
                DELETE FROM dbo.ProofSubmission WHERE Submission_id = @DraftSubmissionID;
                SET @RemovedProofs = @@ROWCOUNT;
            END;

            IF @RemovedProofs > 0
            BEGIN
                UPDATE ta
                SET Completion_status = 'Needs Revision',
                    Completed_at = NULL
                FROM dbo.TaskAssignment ta
                WHERE ta.Task_id = @Task_id
                  AND ta.Completion_status = 'For Validation';
            END;

            COMMIT TRANSACTION;

            SELECT @RemovedProofs;
            """;
        int removedProofs = Convert.ToInt32(await ExecuteScalarAsync(query,
            [
                new SqlParameter("@Subtask_id", SqlDbType.Int) { Value = subtaskID },
                new SqlParameter("@Teacher_id", SqlDbType.Int) { Value = teacherID }
            ],
            cancellationToken));
        return removedProofs > 0;
    }
    public async Task<(byte[] Data, string File_type, string File_name)?> GetSubtaskProofImageAsync(
        int subtaskID,
        CancellationToken cancellationToken = default)
    {
        List<PreparedProofImage> files = await GetSubtaskProofFilesAsync(subtaskID, cancellationToken);
        PreparedProofImage? file = files.FirstOrDefault();
        return file is null ? null : (file.Data, file.File_type, file.File_name);
    }

    public async Task<List<PreparedProofImage>> GetSubtaskProofFilesAsync(
        int subtaskID,
        CancellationToken cancellationToken = default)
    {
        await EnsureSubtaskProofSchemaAsync(cancellationToken);
        const string query = """
            SELECT a.Proof_file, a.File_type, a.File_name
            FROM dbo.ProofAttachment a
            INNER JOIN
            (
                SELECT TOP (1) h.Submission_id
                FROM dbo.ProofSubmission h
                WHERE h.Subtask_id = @Subtask_id
                  AND h.Proof_status = N'Draft'
                ORDER BY h.Attempt_number DESC, h.Submission_id DESC
            ) currentDraft ON currentDraft.Submission_id = a.Submission_id
            ORDER BY a.Sort_order;
            """;
        DataTable table = await ExecuteQueryAsync(query,
            [new SqlParameter("@Subtask_id", SqlDbType.Int) { Value = subtaskID }], cancellationToken);
        return table.AsEnumerable().Select(row => new PreparedProofImage
        {
            Data = (byte[])row["Proof_file"],
            File_type = row.Field<string>("File_type") ?? "image/jpeg",
            File_name = row.Field<string>("File_name") ?? "proof.jpg"
        }).ToList();
    }

    public async Task<(byte[] Data, string File_type, string File_name)?> GetProofSubmissionFileAsync(
        int submissionID,
        CancellationToken cancellationToken = default)
    {
        List<PreparedProofImage> files = await GetProofSubmissionFilesAsync(submissionID, cancellationToken);
        PreparedProofImage? file = files.FirstOrDefault();
        return file is null ? null : (file.Data, file.File_type, file.File_name);
    }

    public async Task<List<PreparedProofImage>> GetProofSubmissionFilesAsync(
        int submissionID,
        CancellationToken cancellationToken = default)
    {
        await EnsureSubtaskProofSchemaAsync(cancellationToken);
        const string query = """
            SELECT Proof_file, File_type, File_name
            FROM dbo.ProofAttachment
            WHERE Submission_id = @SubmissionID
            ORDER BY Sort_order;
            """;
        DataTable table = await ExecuteQueryAsync(query,
            [new SqlParameter("@SubmissionID", SqlDbType.Int) { Value = submissionID }], cancellationToken);
        return table.AsEnumerable().Select(row => new PreparedProofImage
        {
            Data = (byte[])row["Proof_file"],
            File_type = row.Field<string>("File_type") ?? "image/jpeg",
            File_name = row.Field<string>("File_name") ?? "proof.jpg"
        }).ToList();
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

        await EnsureSubtaskProofSchemaAsync(cancellationToken);
        await using var connection = GetConnection();
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            await using (var authorize = new SqlCommand("dbo.AssertActiveUserRole", connection, transaction))
            {
                authorize.CommandType = CommandType.StoredProcedure;
                authorize.Parameters.Add("@Acting_user_id", SqlDbType.Int).Value = reviewedByUserID;
                authorize.Parameters.Add("@AllowDirector", SqlDbType.Bit).Value = true;
                authorize.Parameters.Add("@AllowStaff", SqlDbType.Bit).Value = true;
                await authorize.ExecuteNonQueryAsync(cancellationToken);
            }
            const string historyQuery = """
                UPDATE dbo.ProofSubmission
                SET Proof_status = @Status,
                    Reviewed_at = SYSDATETIME(),
                    Reviewedby_user_id = @ReviewedByUserID,
                    Return_remarks = @AdminRemarks
                WHERE Subtask_id = @Subtask_id
                  AND Proof_status = N'Pending'
                  AND Submission_id =
                  (
                      SELECT TOP (1) Submission_id
                      FROM dbo.ProofSubmission
                      WHERE Subtask_id = @Subtask_id AND Proof_status = N'Pending'
                      ORDER BY Attempt_number DESC, Submission_id DESC
                  );
                """;
            await using var historyCommand = new SqlCommand(historyQuery, connection, transaction);
            historyCommand.Parameters.Add("@Status", SqlDbType.NVarChar, 20).Value = approve ? "Approved" : "Returned";
            historyCommand.Parameters.Add("@ReviewedByUserID", SqlDbType.Int).Value = reviewedByUserID;
            historyCommand.Parameters.Add("@AdminRemarks", SqlDbType.NVarChar, 500).Value =
                remarks.Length == 0 ? DBNull.Value : remarks;
            historyCommand.Parameters.Add("@Subtask_id", SqlDbType.Int).Value = subtaskID;
            if (await historyCommand.ExecuteNonQueryAsync(cancellationToken) != 1)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return false;
            }

            if (!approve)
            {
                const string assignmentQuery = """
                    UPDATE ta
                    SET Completion_status = 'Needs Revision',
                        Completed_at = NULL
                    FROM dbo.TaskAssignment ta
                    INNER JOIN dbo.Subtask s ON s.Task_id = ta.Task_id
                    WHERE s.Subtask_id = @Subtask_id
                      AND ta.Completion_status = 'For Validation'
                    """;
                await using var assignmentCommand = new SqlCommand(assignmentQuery, connection, transaction);
                assignmentCommand.Parameters.Add("@Subtask_id", SqlDbType.Int).Value = subtaskID;
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
    public async Task<HashSet<int>> GetTasksWithPreviousDiscussionsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureTaskDiscussionTableAsync(cancellationToken);
        return [];
    }

    public Task<List<TaskDiscussionItem>> GetTaskDiscussionsAsync(
        int taskID,
        int subtaskID,
        CancellationToken cancellationToken = default) =>
        LoadTaskDiscussionsAsync(taskID, subtaskID, cancellationToken);

    public Task<List<TaskDiscussionItem>> GetPreviousTaskDiscussionsAsync(
        int taskID,
        CancellationToken cancellationToken = default) =>
        LoadTaskDiscussionsAsync(taskID, null, cancellationToken);

    private async Task<List<TaskDiscussionItem>> LoadTaskDiscussionsAsync(
        int taskID,
        int? subtaskID,
        CancellationToken cancellationToken)
    {
        await EnsureTaskDiscussionTableAsync(cancellationToken);
        const string query = """
            SELECT c.Discussion_id, c.Sender_id, c.Sender_type,
                   COALESCE(
                       NULLIF(LTRIM(RTRIM(CONCAT(teacher.First_name, N' ', teacher.Last_name))), N''),
                       NULLIF(LTRIM(RTRIM(CONCAT(appUser.First_name, N' ', appUser.Last_name))), N''),
                       N'Unknown') AS AuthorName,
                   c.Message_text, c.Message_type, c.Created_at,
                   COALESCE(teacher.Profile_photo, appUser.Profile_photo, N'') AS AuthorProfilePhotoPath,
                   COALESCE(CASE WHEN c.Sender_type = N'Teacher' THEN N'Teacher' END,
                            userRole.Role_name, N'User') AS AuthorRoleName
            FROM dbo.TaskDiscussion c
            LEFT JOIN dbo.Teacher teacher ON c.Sender_type = N'Teacher' AND teacher.Teacher_id = c.Sender_id
            LEFT JOIN dbo.[User] appUser ON c.Sender_type = N'User' AND appUser.User_id = c.Sender_id
            LEFT JOIN dbo.Roles userRole ON userRole.Role_id = appUser.Role_id
            INNER JOIN dbo.Subtask subtask ON subtask.Subtask_id = c.Subtask_id
            WHERE subtask.Task_id = @Task_id
              AND c.Subtask_id = @Subtask_id
            ORDER BY c.Created_at, c.Discussion_id
            """;
        DataTable table = await ExecuteQueryAsync(
            query,
            [
                new SqlParameter("@Task_id", SqlDbType.Int) { Value = taskID },
                new SqlParameter("@Subtask_id", SqlDbType.Int) { Value = (object?)subtaskID ?? DBNull.Value }
            ],
            cancellationToken);
        return table.AsEnumerable().Select(row => new TaskDiscussionItem
        {
            Discussion_id = row.Field<int>("Discussion_id"),
            Sender_id = row.Field<int>("Sender_id"),
            Sender_type = row.Field<string>("Sender_type") ?? string.Empty,
            AuthorName = row.Field<string>("AuthorName") ?? "Unknown",
            AuthorRoleName = row.Field<string>("AuthorRoleName") ?? "User",
            AuthorProfilePhotoPath = row.Field<string>("AuthorProfilePhotoPath") ?? string.Empty,
            Message_text = row.Field<string>("Message_text") ?? string.Empty,
            Message_type = row.Field<string>("Message_type") ?? "Discussion",
            Created_at = row.Field<DateTime>("Created_at")
        }).ToList();
    }

    public async Task<bool> AddTaskDiscussionAsync(
        int taskID,
        int subtaskID,
        string senderType,
        int senderID,
        string messageText,
        string messageType = "Discussion",
        CancellationToken cancellationToken = default)
    {
        string message = messageText.Trim();
        if (message.Length == 0)
            throw new ArgumentException("Write a discussion message first.");
        if (message.Length > 1000)
            throw new ArgumentException("Discussion messages cannot exceed 1,000 characters.");
        if ((senderType != "User" && senderType != "Teacher") || senderID <= 0)
            throw new ArgumentException("Invalid discussion author.");
        if (taskID <= 0 || subtaskID <= 0)
            throw new ArgumentException("Select a valid subtask before sending a message.");

        await EnsureTaskDiscussionTableAsync(cancellationToken);
        const string query = """
            INSERT INTO dbo.TaskDiscussion
                (Subtask_id, Sender_id, Sender_type, Message_text, Message_type, Created_at)
            SELECT @Subtask_id, @Sender_id, @Sender_type, @Message_text, @Message_type, GETDATE()
            FROM dbo.Subtask WITH (HOLDLOCK)
            WHERE Task_id = @Task_id AND Subtask_id = @Subtask_id
              AND ((@Sender_type = N'User' AND EXISTS
                        (SELECT 1 FROM dbo.[User] WHERE User_id = @Sender_id))
                   OR (@Sender_type = N'Teacher' AND EXISTS
                        (SELECT 1 FROM dbo.Teacher WHERE Teacher_id = @Sender_id)))
            """;
        int added = await ExecuteNonQueryAsync(
            query,
            [
                new SqlParameter("@Task_id", SqlDbType.Int) { Value = taskID },
                new SqlParameter("@Subtask_id", SqlDbType.Int) { Value = subtaskID },
                new SqlParameter("@Sender_id", SqlDbType.Int) { Value = senderID },
                new SqlParameter("@Sender_type", SqlDbType.NVarChar, 10) { Value = senderType },
                new SqlParameter("@Message_text", SqlDbType.NVarChar, 1000) { Value = message },
                new SqlParameter("@Message_type", SqlDbType.NVarChar, 20) { Value = messageType }
            ],
            cancellationToken);
        if (added == 0)
            throw new ArgumentException("This subtask no longer belongs to this task or has been removed. Reopen the task and try again.");
        return added > 0;
    }

    public async System.Threading.Tasks.Task<Dictionary<int, int>> GetUnreadTaskDiscussionCountsAsync(
        IEnumerable<int> subtaskIDs,
        string readerType,
        int readerID,
        CancellationToken cancellationToken = default)
    {
        int[] ids = subtaskIDs.Distinct().ToArray();
        Dictionary<int, int> counts = ids.ToDictionary(id => id, _ => 0);
        if (ids.Length == 0)
            return counts;

        await EnsureTaskDiscussionTableAsync(cancellationToken);

        List<SqlParameter> parameters =
        [
            new SqlParameter("@Reader_type", SqlDbType.NVarChar, 10) { Value = readerType },
            new SqlParameter("@Reader_id", SqlDbType.Int) { Value = readerID }
        ];
        string[] paramNames = new string[ids.Length];
        for (int i = 0; i < ids.Length; i++)
        {
            paramNames[i] = $"@Subtask{i}";
            parameters.Add(new SqlParameter(paramNames[i], SqlDbType.Int) { Value = ids[i] });
        }

        string query = $"""
            SELECT discussion.Subtask_id, COUNT(*) AS UnreadCount
            FROM dbo.TaskDiscussion AS discussion
            LEFT JOIN dbo.TaskDiscussionRead AS receipt
              ON receipt.Subtask_id = discussion.Subtask_id
             AND receipt.Reader_type = @Reader_type
             AND receipt.Reader_id = @Reader_id
            WHERE discussion.Subtask_id IN ({string.Join(",", paramNames)})
              AND discussion.Discussion_id > ISNULL(receipt.Lastread_discussion_id, 0)
              AND NOT (discussion.Sender_type = @Reader_type AND discussion.Sender_id = @Reader_id)
            GROUP BY discussion.Subtask_id
            """;

        DataTable table = await ExecuteQueryAsync(query, parameters, cancellationToken);
        foreach (DataRow row in table.Rows)
            counts[Convert.ToInt32(row["Subtask_id"])] = Convert.ToInt32(row["UnreadCount"]);
        return counts;
    }

    public async System.Threading.Tasks.Task MarkTaskDiscussionsReadAsync(
        int subtaskID,
        string readerType,
        int readerID,
        CancellationToken cancellationToken = default)
    {
        await EnsureTaskDiscussionTableAsync(cancellationToken);
        const string query = """
            DECLARE @Lastread_discussion_id int = ISNULL(
                (SELECT MAX(Discussion_id) FROM dbo.TaskDiscussion WHERE Subtask_id = @Subtask_id), 0);

            MERGE dbo.TaskDiscussionRead AS target
            USING (SELECT @Subtask_id AS Subtask_id, @Reader_type AS Reader_type, @Reader_id AS Reader_id) AS source
               ON target.Subtask_id = source.Subtask_id
              AND target.Reader_type = source.Reader_type
              AND target.Reader_id = source.Reader_id
            WHEN MATCHED THEN
                UPDATE SET Lastread_discussion_id = @Lastread_discussion_id, Read_at = GETDATE()
            WHEN NOT MATCHED THEN
                INSERT (Subtask_id, Reader_type, Reader_id, Lastread_discussion_id, Read_at)
                VALUES (@Subtask_id, @Reader_type, @Reader_id, @Lastread_discussion_id, GETDATE());
            """;
        await ExecuteNonQueryAsync(
            query,
            [
                new SqlParameter("@Subtask_id", SqlDbType.Int) { Value = subtaskID },
                new SqlParameter("@Reader_type", SqlDbType.NVarChar, 10) { Value = readerType },
                new SqlParameter("@Reader_id", SqlDbType.Int) { Value = readerID }
            ],
            cancellationToken);
    }

    private static readonly SemaphoreSlim TaskDiscussionSchemaGate = new(1, 1);
    private static volatile bool _taskDiscussionTableEnsured;

    internal async System.Threading.Tasks.Task EnsureTaskDiscussionTableAsync(CancellationToken cancellationToken)
    {
        if (_taskDiscussionTableEnsured)
            return;

        await TaskDiscussionSchemaGate.WaitAsync(cancellationToken);
        try
        {
            if (_taskDiscussionTableEnsured)
                return;

            using Stream migration = typeof(DatabaseService).Assembly.GetManifestResourceStream(
                "EDUTASK.TaskDiscussionMigration.sql")
                ?? throw new InvalidOperationException("The discussion schema migration is missing.");
            using var reader = new StreamReader(migration);
            string query = await reader.ReadToEndAsync(cancellationToken);
            await ExecuteNonQueryAsync(query, cancellationToken: cancellationToken);
            _taskDiscussionTableEnsured = true;
        }
        finally
        {
            TaskDiscussionSchemaGate.Release();
        }
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

    private static async Task EnsureTeacherActiveForAssignmentAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int teacherID,
        CancellationToken cancellationToken)
    {
        const string query = """
            SELECT COUNT(*)
            FROM dbo.Teacher WITH (UPDLOCK, HOLDLOCK)
            WHERE Teacher_id = @Teacher_id
              AND Is_active = 1
            """;

        await using var command = new SqlCommand(query, connection, transaction);
        command.Parameters.Add("@Teacher_id", SqlDbType.Int).Value = teacherID;
        int activeTeachers = Convert.ToInt32(
            await command.ExecuteScalarAsync(cancellationToken));
        if (activeTeachers != 1)
            throw new ArgumentException(
                "The selected teacher is no longer active. Refresh the teacher list and choose an active account.",
                nameof(teacherID));
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
