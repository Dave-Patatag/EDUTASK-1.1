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

    public async Task<AuthenticationResult> AuthenticateAsync(string username, string password)
    {
        await EnsureAuthenticationSchemaAsync();
        const string query = """
            SELECT u.UserID AS AccountID, N'User' AS AccountType, u.Password, u.Username AS StoredUsername,
                   u.IsActive, u.DisabledAt, r.RoleName, r.IsActive AS IsRoleActive
            FROM dbo.[User] u
            INNER JOIN dbo.Roles r ON r.RoleID = u.RoleID
            WHERE u.Username = @Username
            UNION ALL
            SELECT t.TeacherID, N'Teacher', t.Password, t.Username,
                   t.IsActive, t.DisabledAt, r.RoleName, r.IsActive
            FROM dbo.Teacher t
            INNER JOIN dbo.Roles r ON r.RoleID = t.RoleID
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
        string role = row.Field<string>("RoleName") ?? string.Empty;
        if (!row.Field<bool>("IsRoleActive"))
            return new(false, "This account role is currently unavailable.", role, null, null);
        if (!row.Field<bool>("IsActive"))
        {
            string message = row.IsNull("DisabledAt")
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
                (FirstName, LastName, Email, Password, ContactNumber, AccountCreated, Username,
                 ProfilePhotoPath, RoleID, IsActive,
                 SecurityQuestion1, SecurityAnswerHash1, SecurityQuestion2, SecurityAnswerHash2)
            VALUES
                (@FirstName, @LastName, @Email, @Password, N'', GETDATE(), @Username,
                 NULL, (SELECT RoleID FROM dbo.Roles WHERE RoleName=@Role), 0,
                 @Question1, @Answer1, @Question2, @Answer2)
            """;
        await ExecuteNonQueryAsync(insert,
        [
            new("@FirstName", SqlDbType.NVarChar, 100) { Value = firstName },
            new("@LastName", SqlDbType.NVarChar, 100) { Value = lastName },
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
            SELECT u.UserID AS AccountID, u.Username, u.Email, r.RoleName AS AccountRole,
                   u.SecurityQuestion1, u.SecurityQuestion2
            FROM dbo.[User] u
            INNER JOIN dbo.Roles r ON r.RoleID=u.RoleID
            WHERE (u.Username=@Identifier OR u.Email=@Identifier) AND u.IsActive=1

            UNION ALL

            SELECT t.TeacherID AS AccountID, t.Username, t.Email, N'Teacher' AS AccountRole,
                   t.SecurityQuestion1, t.SecurityQuestion2
            FROM dbo.Teacher t
            WHERE (t.Username=@Identifier OR t.Email=@Identifier) AND t.IsActive=1
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
        string question1 = row.Field<string>("SecurityQuestion1") ?? string.Empty;
        string question2 = row.Field<string>("SecurityQuestion2") ?? string.Empty;
        return role.Length == 0 || question1.Length == 0 || question2.Length == 0
            ? (false, 0, string.Empty, string.Empty, string.Empty)
            : (true, row.Field<int>("AccountID"), role, question1, question2);
    }

    public async Task<bool> VerifyPasswordRecoveryAnswersAsync(
        int accountId, string role, string answer1, string answer2)
    {
        await EnsureAuthenticationSchemaAsync();
        string table = role == "Teacher" ? "dbo.Teacher" : "dbo.[User]";
        string idColumn = role == "Teacher" ? "TeacherID" : "UserID";
        DataTable result = await ExecuteQueryAsync(
            $"SELECT SecurityAnswerHash1, SecurityAnswerHash2 FROM {table} WHERE {idColumn}=@AccountID AND IsActive=1",
            [new("@AccountID", SqlDbType.Int) { Value = accountId }]);

        if (result.Rows.Count == 0)
            return false;

        DataRow row = result.Rows[0];
        string hash1 = row.Field<string>("SecurityAnswerHash1") ?? string.Empty;
        string hash2 = row.Field<string>("SecurityAnswerHash2") ?? string.Empty;
        return CredentialHashService.Verify(CredentialHashService.NormalizeAnswer(answer1), hash1) &&
               CredentialHashService.Verify(CredentialHashService.NormalizeAnswer(answer2), hash2);
    }

    public async Task<(bool Success, string Message)> ResetPasswordAsync(
        int accountId, string role, string newPassword)
    {
        await EnsureAuthenticationSchemaAsync();
        string table = role == "Teacher" ? "dbo.Teacher" : "dbo.[User]";
        string idColumn = role == "Teacher" ? "TeacherID" : "UserID";
        object? storedValue = await ExecuteScalarAsync(
            $"SELECT Password FROM {table} WHERE {idColumn}=@AccountID AND IsActive=1",
            [new("@AccountID", SqlDbType.Int) { Value = accountId }]);

        string storedPassword = storedValue?.ToString() ?? string.Empty;
        if (storedPassword.Length == 0)
            return (false, "Password could not be updated.");
        if (CredentialHashService.Verify(newPassword, storedPassword))
            return (false, "Choose a password you have not used here.");

        await ExecuteNonQueryAsync(
            $"UPDATE {table} SET Password=@Password WHERE {idColumn}=@AccountID AND IsActive=1",
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
            SELECT u.UserID AccountID, N'Staff' AccountType,
                   CONCAT(u.FirstName, N' ', u.LastName) FullName, u.Email, u.AccountCreated RequestedAt
            FROM dbo.[User] u INNER JOIN dbo.Roles r ON r.RoleID=u.RoleID
            WHERE u.IsActive=0 AND u.DisabledAt IS NULL AND r.RoleName=N'Staff'
            UNION ALL
            SELECT t.TeacherID, N'Teacher', CONCAT(t.FirstName, N' ', t.LastName), t.Email, t.AccountCreated
            FROM dbo.Teacher t WHERE t.IsActive=0 AND t.DisabledAt IS NULL
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
    /// for approval, which are also IsActive=0 — DisabledAt is what tells the
    /// two apart.
    /// </summary>
    public Task<List<DirectoryAccountItem>> GetDisabledDirectoryAsync(bool includeStaff = true) =>
        GetDirectoryAsync(includeStaff, disabled: true);

    private async Task<List<DirectoryAccountItem>> GetDirectoryAsync(bool includeStaff, bool disabled)
    {
        await EnsureAuthenticationSchemaAsync();

        // Not user input: one of two fixed fragments chosen by the caller's flag.
        string state = disabled
            ? "IsActive=0 AND DisabledAt IS NOT NULL"
            : "IsActive=1";

        string query = includeStaff
            ? $"""
              SELECT u.UserID AccountID, N'Staff' AccountType,
                     CONCAT(u.FirstName, N' ', u.LastName) FullName,
                     u.Username, u.Email, u.ContactNumber
              FROM dbo.[User] u INNER JOIN dbo.Roles r ON r.RoleID=u.RoleID
              WHERE r.RoleName=N'Staff' AND u.{state}
              UNION ALL
              SELECT t.TeacherID, N'Teacher', CONCAT(t.FirstName, N' ', t.LastName),
                     t.Username, t.Email, t.ContactNumber
              FROM dbo.Teacher t WHERE t.{state}
              ORDER BY AccountType, FullName
              """
            : $"""
              SELECT t.TeacherID AccountID, N'Teacher' AccountType,
                     CONCAT(t.FirstName, N' ', t.LastName) FullName,
                     t.Username, t.Email, t.ContactNumber
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
            row.Field<string>("ContactNumber") ?? string.Empty,
            disabled)).ToList();
    }

    public async Task<bool> ApproveAccountAsync(int accountID, string accountType)
    {
        if (!UserSessionService.IsDirector)
            throw new UnauthorizedAccessException("Only a Director can approve accounts.");

        await EnsureAuthenticationSchemaAsync();

        string table = accountType == "Teacher" ? "dbo.Teacher" : "dbo.[User]";
        string idColumn = accountType == "Teacher" ? "TeacherID" : "UserID";
        return await ExecuteNonQueryAsync(
            $"UPDATE {table} SET IsActive=1, DisabledAt=NULL WHERE {idColumn}=@AccountID AND IsActive=0",
            [new("@AccountID", SqlDbType.Int) { Value = accountID }]) == 1;
    }

    /// <summary>
    /// Switches an account off or back on.
    ///
    /// Disabling stamps DisabledAt, which is the only thing separating a
    /// switched-off account from one that has never been approved: both sit at
    /// IsActive=0, and without the stamp a disabled account would reappear in
    /// the register requests queue.
    /// </summary>
    public async Task<bool> SetAccountDisabledAsync(int accountID, string accountType, bool disabled)
    {
        if (!UserSessionService.IsDirector)
            throw new UnauthorizedAccessException("Only a Director can disable accounts.");

        await EnsureAuthenticationSchemaAsync();

        string table = accountType == "Teacher" ? "dbo.Teacher" : "dbo.[User]";
        string idColumn = accountType == "Teacher" ? "TeacherID" : "UserID";

        string update = disabled
            ? $"UPDATE {table} SET IsActive=0, DisabledAt=SYSUTCDATETIME() WHERE {idColumn}=@AccountID AND IsActive=1"
            : $"UPDATE {table} SET IsActive=1, DisabledAt=NULL WHERE {idColumn}=@AccountID AND DisabledAt IS NOT NULL";

        return await ExecuteNonQueryAsync(
            update, [new("@AccountID", SqlDbType.Int) { Value = accountID }]) == 1;
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

            DECLARE @PreviousRoleID int;
            DECLARE @DirectorRoleID int;

            SELECT @PreviousRoleID = u.RoleID
            FROM dbo.[User] u WITH (UPDLOCK, HOLDLOCK)
            INNER JOIN dbo.Roles r ON r.RoleID = u.RoleID
            WHERE u.UserID = @StaffUserID
              AND u.IsActive = 1
              AND u.DisabledAt IS NULL
              AND r.RoleName = N'Staff'
              AND r.IsActive = 1;

            SELECT @DirectorRoleID = RoleID
            FROM dbo.Roles
            WHERE RoleName = N'Director' AND IsActive = 1;

            IF @PreviousRoleID IS NULL OR @DirectorRoleID IS NULL OR NOT EXISTS
            (
                SELECT 1
                FROM dbo.[User] actingUser
                INNER JOIN dbo.Roles actingRole ON actingRole.RoleID = actingUser.RoleID
                WHERE actingUser.UserID = @ActingUserID
                  AND actingUser.IsActive = 1
                  AND actingUser.DisabledAt IS NULL
                  AND actingRole.RoleName = N'Director'
                  AND actingRole.IsActive = 1
            )
            BEGIN
                ROLLBACK TRANSACTION;
                SELECT CAST(0 AS bit);
                RETURN;
            END;

            UPDATE dbo.[User]
            SET RoleID = @DirectorRoleID
            WHERE UserID = @StaffUserID AND RoleID = @PreviousRoleID;

            INSERT INTO dbo.AccountRoleChangeLog
                (UserID, PreviousRoleID, NewRoleID, ChangedByUserID, ChangedAt)
            VALUES
                (@StaffUserID, @PreviousRoleID, @DirectorRoleID, @ActingUserID, SYSUTCDATETIME());

            COMMIT TRANSACTION;
            SELECT CAST(1 AS bit);
            """;

        object? result = await ExecuteScalarAsync(query,
        [
            new SqlParameter("@StaffUserID", SqlDbType.Int) { Value = staffUserID },
            new SqlParameter("@ActingUserID", SqlDbType.Int) { Value = UserSessionService.CurrentUserId }
        ]);
        return Convert.ToBoolean(result ?? false);
    }

    private async System.Threading.Tasks.Task EnsureAuthenticationSchemaAsync()
    {
        const string query = """
            IF COL_LENGTH(N'dbo.User', N'SecurityQuestion1') IS NULL ALTER TABLE dbo.[User] ADD SecurityQuestion1 nvarchar(200) NULL;
            IF COL_LENGTH(N'dbo.User', N'SecurityAnswerHash1') IS NULL ALTER TABLE dbo.[User] ADD SecurityAnswerHash1 nvarchar(500) NULL;
            IF COL_LENGTH(N'dbo.User', N'SecurityQuestion2') IS NULL ALTER TABLE dbo.[User] ADD SecurityQuestion2 nvarchar(200) NULL;
            IF COL_LENGTH(N'dbo.User', N'SecurityAnswerHash2') IS NULL ALTER TABLE dbo.[User] ADD SecurityAnswerHash2 nvarchar(500) NULL;
            IF COL_LENGTH(N'dbo.Teacher', N'SecurityQuestion1') IS NULL ALTER TABLE dbo.Teacher ADD SecurityQuestion1 nvarchar(200) NULL;
            IF COL_LENGTH(N'dbo.Teacher', N'SecurityAnswerHash1') IS NULL ALTER TABLE dbo.Teacher ADD SecurityAnswerHash1 nvarchar(500) NULL;
            IF COL_LENGTH(N'dbo.Teacher', N'SecurityQuestion2') IS NULL ALTER TABLE dbo.Teacher ADD SecurityQuestion2 nvarchar(200) NULL;
            IF COL_LENGTH(N'dbo.Teacher', N'SecurityAnswerHash2') IS NULL ALTER TABLE dbo.Teacher ADD SecurityAnswerHash2 nvarchar(500) NULL;
            IF COL_LENGTH(N'dbo.User', N'DisabledAt') IS NULL ALTER TABLE dbo.[User] ADD DisabledAt datetime2 NULL;
            IF COL_LENGTH(N'dbo.Teacher', N'DisabledAt') IS NULL ALTER TABLE dbo.Teacher ADD DisabledAt datetime2 NULL;

            IF COL_LENGTH(N'dbo.User', N'Bio') IS NULL ALTER TABLE dbo.[User] ADD Bio nvarchar(300) NULL;
            IF COL_LENGTH(N'dbo.Teacher', N'Bio') IS NULL ALTER TABLE dbo.Teacher ADD Bio nvarchar(300) NULL;

            IF OBJECT_ID(N'dbo.AccountRoleChangeLog', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.AccountRoleChangeLog
                (
                    RoleChangeID bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_AccountRoleChangeLog PRIMARY KEY,
                    UserID int NOT NULL,
                    PreviousRoleID int NOT NULL,
                    NewRoleID int NOT NULL,
                    ChangedByUserID int NOT NULL,
                    ChangedAt datetime2 NOT NULL CONSTRAINT DF_AccountRoleChangeLog_ChangedAt DEFAULT (SYSUTCDATETIME()),
                    CONSTRAINT FK_AccountRoleChangeLog_User FOREIGN KEY (UserID) REFERENCES dbo.[User](UserID),
                    CONSTRAINT FK_AccountRoleChangeLog_PreviousRole FOREIGN KEY (PreviousRoleID) REFERENCES dbo.Roles(RoleID),
                    CONSTRAINT FK_AccountRoleChangeLog_NewRole FOREIGN KEY (NewRoleID) REFERENCES dbo.Roles(RoleID),
                    CONSTRAINT FK_AccountRoleChangeLog_ChangedBy FOREIGN KEY (ChangedByUserID) REFERENCES dbo.[User](UserID)
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
            SELECT u.UserID, u.FirstName, u.LastName, u.Email, u.Password, u.ContactNumber,
                   u.AccountCreated, u.Username, u.ProfilePhotoPath,
                   u.RoleID, r.RoleName, u.IsActive, u.Bio
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
            ProfilePhotoPath = row.Field<string>("ProfilePhotoPath") ?? string.Empty,
            RoleID = row.Field<int>("RoleID"),
            RoleName = row.Field<string>("RoleName") ?? string.Empty,
            IsActive = row.Field<bool>("IsActive"),
            Bio = row.Field<string>("Bio") ?? string.Empty
        };
    }

    public async Task<bool> UpdateUserProfileAsync(
        int userID,
        string fullName,
        string contactNumber,
        string email,
        string username,
        string? profilePhotoPath,
        string? bio,
        CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticationSchemaAsync();

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
                ProfilePhotoPath = @ProfilePhotoPath,
                Bio = @Bio
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
                new SqlParameter("@ProfilePhotoPath", SqlDbType.NVarChar, 500)
                {
                    Value = string.IsNullOrWhiteSpace(profilePhotoPath) ? DBNull.Value : profilePhotoPath
                },
                new SqlParameter("@Bio", SqlDbType.NVarChar, 300)
                {
                    Value = string.IsNullOrWhiteSpace(bio) ? DBNull.Value : bio.Trim()
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
        await EnsureAuthenticationSchemaAsync();

        const string query = """
            SELECT TeacherID, FirstName, LastName, Email, Password, ContactNumber,
                   AccountCreated, Username, ProfilePhotoPath, RoleID, IsActive, Bio
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
            ProfilePhotoPath = row.Field<string>("ProfilePhotoPath") ?? string.Empty,
            RoleID = row.IsNull("RoleID") ? 0 : row.Field<int>("RoleID"),
            RoleName = "Teacher",
            IsActive = row.Field<bool>("IsActive"),
            Bio = row.Field<string>("Bio") ?? string.Empty
        };
    }

    public async Task<bool> UpdateTeacherProfileAsync(
        int teacherID,
        string fullName,
        string contactNumber,
        string email,
        string username,
        string? profilePhotoPath,
        string? bio,
        CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticationSchemaAsync();

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
                ProfilePhotoPath = @ProfilePhotoPath,
                Bio = @Bio
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
                new SqlParameter("@ProfilePhotoPath", SqlDbType.NVarChar, 500)
                {
                    Value = string.IsNullOrWhiteSpace(profilePhotoPath) ? DBNull.Value : profilePhotoPath
                },
                new SqlParameter("@Bio", SqlDbType.NVarChar, 300)
                {
                    Value = string.IsNullOrWhiteSpace(bio) ? DBNull.Value : bio.Trim()
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
                new SqlParameter("@ColumnName", SqlDbType.NVarChar, 128) { Value = "IsActive" }
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
            conditions.Add("IsActive = 1");

        // The currently attached EduTaskDB may predate AccountStatus/IsActive. Keep the app
        // usable without changing the user's database; once a column exists, its filter is
        // automatically enforced so pending/inactive teachers no longer appear here.
        string whereClause = conditions.Count > 0 ? $"WHERE {string.Join(" AND ", conditions)}" : string.Empty;
        string query = $"""
            SELECT TeacherID, FirstName, LastName
            FROM Teacher
            {whereClause}
            ORDER BY FirstName, LastName
            """;
        return await ExecuteQueryAsync(query, parameters, cancellationToken);
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
                ta.CompletedAt,
                CONCAT(te.FirstName, ' ', te.LastName) AS TeacherName,
                te.ProfilePhotoPath AS TeacherProfilePhotoPath,
                CONCAT(creator.FirstName, ' ', creator.LastName) AS CreatorName,
                creator.ProfilePhotoPath AS CreatorProfilePhotoPath
            FROM [Task] t
            LEFT JOIN TaskAssignment ta ON t.TaskID = ta.TaskID
            LEFT JOIN Teacher te ON ta.TeacherID = te.TeacherID
            LEFT JOIN dbo.[User] creator ON creator.UserID = COALESCE(t.CreatedByUserID, t.UserID)
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

    public async Task<ProfileTaskSummary> GetUserProfileTaskSummaryAsync(
        int userID,
        CancellationToken cancellationToken = default)
    {
        const string query = """
            WITH UserTasks AS
            (
                SELECT
                    t.TaskID,
                    CASE WHEN COUNT(ta.AssignmentID) > 0
                              AND SUM(CASE WHEN ta.CompletionStatus = N'Completed' THEN 1 ELSE 0 END) = COUNT(ta.AssignmentID)
                         THEN 1 ELSE 0 END AS IsCompleted,
                    CASE WHEN SUM(CASE WHEN ta.CompletionStatus <> N'Completed'
                                            AND ta.Deadline < CAST(GETDATE() AS date)
                                       THEN 1 ELSE 0 END) > 0
                         THEN 1 ELSE 0 END AS IsOverdue
                FROM dbo.[Task] t
                LEFT JOIN dbo.TaskAssignment ta ON ta.TaskID = t.TaskID
                WHERE COALESCE(t.CreatedByUserID, t.UserID) = @UserID
                GROUP BY t.TaskID
            )
            SELECT
                COUNT(*) AS TotalCount,
                COALESCE(SUM(CASE WHEN IsCompleted = 0 AND IsOverdue = 0 THEN 1 ELSE 0 END), 0) AS PendingCount,
                COALESCE(SUM(CASE WHEN IsCompleted = 0 AND IsOverdue = 1 THEN 1 ELSE 0 END), 0) AS OverdueCount,
                COALESCE(SUM(IsCompleted), 0) AS CompletedCount
            FROM UserTasks;
            """;

        DataTable table = await ExecuteQueryAsync(
            query,
            [new SqlParameter("@UserID", SqlDbType.Int) { Value = userID }],
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
                COALESCE(SUM(CASE WHEN ta.CompletionStatus <> N'Completed'
                                        AND (ta.Deadline IS NULL OR ta.Deadline >= CAST(GETDATE() AS date))
                                  THEN 1 ELSE 0 END), 0) AS PendingCount,
                COALESCE(SUM(CASE WHEN ta.CompletionStatus <> N'Completed'
                                        AND ta.Deadline < CAST(GETDATE() AS date)
                                  THEN 1 ELSE 0 END), 0) AS OverdueCount,
                COALESCE(SUM(CASE WHEN ta.CompletionStatus = N'Completed' THEN 1 ELSE 0 END), 0) AS CompletedCount
            FROM dbo.TaskAssignment ta
            WHERE ta.TeacherID = @TeacherID;
            """;

        DataTable table = await ExecuteQueryAsync(
            query,
            [new SqlParameter("@TeacherID", SqlDbType.Int) { Value = teacherID }],
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
                AcknowledgedAt = GETDATE(),
                CompletionStatus = CASE
                    WHEN CompletionStatus = N'Pending' THEN N'Acknowledged'
                    ELSE CompletionStatus
                END
            WHERE AssignmentID = @AssignmentID
              AND IsAcknowledged = 0
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
                SET CompletionStatus = 'Needs Revision',
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
                SET CompletionStatus = 'Needs Revision',
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
                    SET CompletionStatus = 'Needs Revision',
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
            SELECT c.CommentID, c.AuthorID, c.AuthorName, c.AuthorType, c.CommentText, c.MessageType, c.CreatedAt,
                   COALESCE(
                       CASE WHEN c.AuthorType = N'Teacher' THEN teacher.ProfilePhotoPath END,
                       CASE WHEN c.AuthorType = N'User' THEN appUser.ProfilePhotoPath END,
                       N'') AS AuthorProfilePhotoPath,
                   COALESCE(
                       CASE WHEN c.AuthorType = N'Teacher' THEN N'Teacher' END,
                       userRole.RoleName,
                       c.AuthorType) AS AuthorRoleName
            FROM dbo.TaskComment c
            LEFT JOIN dbo.Teacher teacher
                ON c.AuthorType = N'Teacher' AND teacher.TeacherID = c.AuthorID
            LEFT JOIN dbo.[User] appUser
                ON c.AuthorType = N'User' AND appUser.UserID = c.AuthorID
            LEFT JOIN dbo.Roles userRole ON userRole.RoleID = appUser.RoleID
            WHERE c.TaskID = @TaskID
              AND c.SubtaskID = @SubtaskID
            ORDER BY c.CreatedAt, c.CommentID
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
            AuthorRoleName = row.Field<string>("AuthorRoleName") ?? "User",
            AuthorProfilePhotoPath = row.Field<string>("AuthorProfilePhotoPath") ?? string.Empty,
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

    public async System.Threading.Tasks.Task<Dictionary<int, int>> GetUnreadTaskCommentCountsAsync(
        IEnumerable<int> subtaskIDs,
        string readerType,
        int readerID,
        CancellationToken cancellationToken = default)
    {
        int[] ids = subtaskIDs.Distinct().ToArray();
        Dictionary<int, int> counts = ids.ToDictionary(id => id, _ => 0);
        if (ids.Length == 0)
            return counts;

        await EnsureTaskCommentReadTableAsync(cancellationToken);

        List<SqlParameter> parameters =
        [
            new SqlParameter("@ReaderType", SqlDbType.NVarChar, 10) { Value = readerType },
            new SqlParameter("@ReaderID", SqlDbType.Int) { Value = readerID }
        ];
        string[] paramNames = new string[ids.Length];
        for (int i = 0; i < ids.Length; i++)
        {
            paramNames[i] = $"@Subtask{i}";
            parameters.Add(new SqlParameter(paramNames[i], SqlDbType.Int) { Value = ids[i] });
        }

        string query = $"""
            SELECT comment.SubtaskID, COUNT(*) AS UnreadCount
            FROM dbo.TaskComment AS comment
            LEFT JOIN dbo.TaskCommentRead AS receipt
              ON receipt.SubtaskID = comment.SubtaskID
             AND receipt.ReaderType = @ReaderType
             AND receipt.ReaderID = @ReaderID
            WHERE comment.SubtaskID IN ({string.Join(",", paramNames)})
              AND comment.CommentID > ISNULL(receipt.LastReadCommentID, 0)
              AND NOT (comment.AuthorType = @ReaderType AND comment.AuthorID = @ReaderID)
            GROUP BY comment.SubtaskID
            """;

        DataTable table = await ExecuteQueryAsync(query, parameters, cancellationToken);
        foreach (DataRow row in table.Rows)
            counts[Convert.ToInt32(row["SubtaskID"])] = Convert.ToInt32(row["UnreadCount"]);
        return counts;
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

    private static volatile bool _taskCommentReadTableEnsured;
    private static volatile bool _taskCommentTableEnsured;

    private async System.Threading.Tasks.Task EnsureTaskCommentReadTableAsync(CancellationToken cancellationToken)
    {
        if (_taskCommentReadTableEnsured)
            return;
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
        _taskCommentReadTableEnsured = true;
    }
    private async System.Threading.Tasks.Task EnsureTaskCommentTableAsync(CancellationToken cancellationToken)
    {
        if (_taskCommentTableEnsured)
            return;
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
        _taskCommentTableEnsured = true;
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
