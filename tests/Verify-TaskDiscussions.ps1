param([string]$Server = '(localdb)\MSSQLLocalDB')

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$migration = Get-Content -LiteralPath (Join-Path $projectRoot 'EDUTASK 1.1\Database\MigrateTaskDiscussions.sql') -Raw
$service = Get-Content -LiteralPath (Join-Path $projectRoot 'EDUTASK 1.1\Services\DatabaseService.cs') -Raw
$updateMethod = ($service -split 'public async Task<bool> UpdateTaskWithAssignmentAsync\(')[1] -split 'public async Task<bool> AcknowledgeTaskAsync\('
$deleteStatements = [regex]::Matches($updateMethod[0], '"(DELETE FROM dbo\.[^"]+)"') | ForEach-Object { $_.Groups[1].Value }
if ($deleteStatements.Count -ne 3) { throw 'Expected the three subtask cleanup statements from the save method.' }
$addMethod = ($service -split 'public async Task<bool> AddTaskDiscussionAsync\(')[1]
$insertSql = [regex]::Match($addMethod, '(?s)const string query = """(.*?)""";').Groups[1].Value
$readMethod = ($service -split 'private async Task<List<TaskDiscussionItem>> LoadTaskDiscussionsAsync\(')[1]
$readSql = [regex]::Match($readMethod, '(?s)const string query = """(.*?)""";').Groups[1].Value

function Invoke-TestSql([string]$Sql, [hashtable]$Parameters = @{}, [switch]$Scalar) {
    $command = $script:connection.CreateCommand()
    $command.CommandText = $Sql
    try {
        foreach ($key in $Parameters.Keys) {
            $value = $Parameters[$key]
            $parameter = $command.Parameters.AddWithValue('@' + $key, $(if ($null -eq $value) { [DBNull]::Value } else { $value }))
            if ($key -in @('TaskID', 'SubtaskID')) { $parameter.SqlDbType = [System.Data.SqlDbType]::Int }
        }
        if ($Scalar) { return $command.ExecuteScalar() }
        return $command.ExecuteNonQuery()
    }
    finally { $command.Dispose() }
}

function Assert-Sql([string]$Sql, [string]$Message) {
    if ((Invoke-TestSql $Sql -Scalar) -ne 1) { throw $Message }
}

function Assert-Rejected([string]$Sql, [int]$ErrorNumber) {
    try { $null = Invoke-TestSql $Sql }
    catch {
        $errorObject = $_.Exception
        while ($errorObject.InnerException) { $errorObject = $errorObject.InnerException }
        if ($errorObject -is [System.Data.SqlClient.SqlException] -and
            $ErrorNumber -in @($errorObject.Errors | ForEach-Object Number)) { return }
        throw
    }
    throw "Expected SQL error $ErrorNumber."
}

foreach ($scenario in @('Fresh', 'Legacy')) {
    $databaseName = 'EduTaskDiscussionTest_' + [Guid]::NewGuid().ToString('N')
    $builder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new()
    $builder['Data Source'] = $Server
    $builder['Initial Catalog'] = 'master'
    $builder['Integrated Security'] = $true
    $builder['TrustServerCertificate'] = $true
    $script:connection = [System.Data.SqlClient.SqlConnection]::new($builder.ConnectionString)
    $created = $false
    try {
        $connection.Open()
        $null = Invoke-TestSql "CREATE DATABASE [$databaseName]"
        $created = $true
        $connection.ChangeDatabase($databaseName)
        $null = Invoke-TestSql @'
CREATE TABLE dbo.[Task] (TaskID int PRIMARY KEY, Title nvarchar(200));
CREATE TABLE dbo.Subtask (SubtaskID int PRIMARY KEY, TaskID int NOT NULL,
    Title nvarchar(200), IsCompleted bit NOT NULL DEFAULT 0,
    FOREIGN KEY (TaskID) REFERENCES dbo.[Task](TaskID) ON DELETE CASCADE);
CREATE TABLE dbo.SubtaskProofHistory (SubtaskID int REFERENCES dbo.Subtask(SubtaskID));
CREATE TABLE dbo.Teacher (TeacherID int PRIMARY KEY, FirstName nvarchar(50), LastName nvarchar(50), ProfilePhotoPath nvarchar(200));
CREATE TABLE dbo.Roles (RoleID int PRIMARY KEY, RoleName nvarchar(20));
CREATE TABLE dbo.[User] (UserID int PRIMARY KEY, FirstName nvarchar(50), LastName nvarchar(50), ProfilePhotoPath nvarchar(200), RoleID int);
INSERT dbo.Roles VALUES (1, 'Director');
INSERT dbo.[User](UserID, FirstName, LastName, RoleID) VALUES (1, 'Test', 'User', 1);
INSERT dbo.Teacher(TeacherID, FirstName, LastName) VALUES (1, 'Test', 'Teacher');
INSERT dbo.[Task] VALUES (1, 'Task one'), (2, 'Task two');
INSERT dbo.Subtask(SubtaskID, TaskID, Title) VALUES (11, 1, 'First'), (12, 1, 'Second'), (21, 2, 'Other task');
'@
        if ($scenario -eq 'Legacy') {
            $null = Invoke-TestSql @'
CREATE TABLE dbo.TaskComment (
    CommentID int IDENTITY PRIMARY KEY, TaskID int NOT NULL REFERENCES dbo.[Task](TaskID) ON DELETE CASCADE,
    AuthorType nvarchar(10) NOT NULL, AuthorID int NOT NULL, AuthorName nvarchar(120) NOT NULL,
    CommentText nvarchar(1000) NOT NULL, CreatedAt datetime NOT NULL DEFAULT GETDATE());
INSERT dbo.TaskComment(TaskID, AuthorType, AuthorID, AuthorName, CommentText)
VALUES (1, 'User', 1, 'Tester', 'Preserve this previous discussion');
'@
        }
        $null = Invoke-TestSql $migration
        $null = Invoke-TestSql $migration # Idempotency, including trigger replacement.
        if ($scenario -eq 'Legacy') {
            Assert-Sql "SELECT COUNT(*) FROM dbo.TaskDiscussion WHERE DiscussionID = 1 AND SubtaskID IS NULL AND MessageText = 'Preserve this previous discussion'" 'Legacy message was not preserved.'
            Assert-Rejected "UPDATE dbo.TaskDiscussion SET MessageText = 'Changed' WHERE SubtaskID IS NULL" 50005
            Assert-Rejected 'UPDATE dbo.TaskDiscussion SET SubtaskID = 11 WHERE SubtaskID IS NULL' 50005
            $legacyID = Invoke-TestSql $readSql @{ TaskID = 1; SubtaskID = $null } -Scalar
            if ($legacyID -ne 1) { throw 'The actual previous-discussion query did not return the legacy message.' }
        }
        Assert-Rejected "INSERT dbo.TaskDiscussion(TaskID, SubtaskID, UserID, MessageText) VALUES (1, NULL, 1, 'Invalid new message')" 50005
        Assert-Rejected "INSERT dbo.TaskDiscussion(TaskID, SubtaskID, UserID, MessageText) VALUES (1, 21, 1, 'Wrong task')" 547
        Assert-Rejected "INSERT dbo.TaskDiscussion(TaskID, SubtaskID, UserID, TeacherID, MessageText) VALUES (1, 11, 1, 1, 'Two authors')" 547

        $parameters = @{ TaskID = 1; SubtaskID = 11; UserID = 1; TeacherID = $null; MessageText = 'Keep this message'; MessageType = 'Discussion' }
        if ((Invoke-TestSql $insertSql $parameters) -ne 1) { throw 'The actual send query did not report one inserted message.' }
        $parameters.SubtaskID = 21
        if ((Invoke-TestSql $insertSql $parameters) -ne 0) { throw 'The send query accepted a mismatched subtask.' }
        $parameters.SubtaskID = 999
        if ((Invoke-TestSql $insertSql $parameters) -ne 0) { throw 'The send query accepted a missing subtask.' }
        Assert-Rejected 'UPDATE dbo.TaskDiscussion SET TaskID = 2 WHERE SubtaskID = 11' 547
        $null = Invoke-TestSql "UPDATE dbo.Subtask SET Title = 'Renamed' WHERE SubtaskID = 11; INSERT dbo.Subtask(SubtaskID, TaskID, Title) VALUES (13, 1, 'Added');"
        Assert-Sql 'SELECT COUNT(*) FROM dbo.TaskDiscussion WHERE TaskID = 1 AND SubtaskID = 11' 'Renaming or adding a subtask changed its existing discussion.'
        if ((Invoke-TestSql 'SELECT COUNT(*) FROM dbo.TaskDiscussion WHERE SubtaskID = 13' -Scalar) -ne 0) { throw 'New subtask did not start empty.' }
        $messageID = Invoke-TestSql $readSql @{ TaskID = 1; SubtaskID = 11 } -Scalar
        if ($null -eq $messageID -or $messageID -is [DBNull]) { throw 'The actual subtask-discussion query returned no message.' }
        $null = Invoke-TestSql "INSERT dbo.SubtaskProofHistory VALUES (11); INSERT dbo.TaskDiscussionRead(SubtaskID, ReaderType, ReaderID) VALUES (11, 'User', 1);"
        Assert-Rejected 'DELETE dbo.Subtask WHERE SubtaskID = 11' 547
        $cleanup = $deleteStatements -join ";`n"
        $null = Invoke-TestSql "BEGIN TRANSACTION; $cleanup; ROLLBACK TRANSACTION;" @{ TaskID = 1; SubtaskID = 11 }
        Assert-Sql 'SELECT COUNT(*) FROM dbo.TaskDiscussion WHERE SubtaskID = 11' 'Rollback lost discussion messages.'
        Assert-Sql 'SELECT COUNT(*) FROM dbo.SubtaskProofHistory WHERE SubtaskID = 11' 'Rollback lost proof history.'
        Assert-Sql 'SELECT COUNT(*) FROM dbo.TaskDiscussionRead WHERE SubtaskID = 11' 'Rollback lost read receipts.'
        $null = Invoke-TestSql "BEGIN TRANSACTION; $cleanup; COMMIT TRANSACTION;" @{ TaskID = 1; SubtaskID = 11 }
        Assert-Sql 'SELECT CASE WHEN NOT EXISTS (SELECT 1 FROM dbo.Subtask WHERE SubtaskID = 11) AND NOT EXISTS (SELECT 1 FROM dbo.TaskDiscussion WHERE SubtaskID = 11) AND NOT EXISTS (SELECT 1 FROM dbo.TaskDiscussionRead WHERE SubtaskID = 11) AND NOT EXISTS (SELECT 1 FROM dbo.SubtaskProofHistory WHERE SubtaskID = 11) THEN 1 ELSE 0 END' 'Subtask cleanup was incomplete.'
        if ($scenario -eq 'Legacy') {
            Assert-Sql 'SELECT COUNT(*) FROM dbo.TaskDiscussion WHERE TaskID = 1 AND SubtaskID IS NULL' 'Removing a subtask lost legacy task messages.'
        }
        # Pre-existing mismatches must halt the migration without silently moving messages.
        $null = Invoke-TestSql 'ALTER TABLE dbo.TaskDiscussion DROP CONSTRAINT FK_TaskDiscussion_Task_Subtask;'
        $null = Invoke-TestSql "INSERT dbo.TaskDiscussion(TaskID, SubtaskID, UserID, MessageText) VALUES (1, 21, 1, 'Existing mismatch');"
        Assert-Rejected $migration 50004
        Assert-Sql "SELECT COUNT(*) FROM dbo.TaskDiscussion WHERE TaskID = 1 AND SubtaskID = 21 AND MessageText = 'Existing mismatch'" 'Migration changed a mismatched record.'
        Write-Output "PASS: $scenario schema, repeated migration, message queries, ownership validation, rename/add, cleanup, rollback, and mismatch audit."
    }
    finally {
        if ($created) {
            $connection.ChangeDatabase('master')
            # Only remove the uniquely named database created by this iteration.
            if ($databaseName -notmatch '^EduTaskDiscussionTest_[a-f0-9]{32}$') { throw 'Unexpected test database name.' }
            $null = Invoke-TestSql "DROP DATABASE [$databaseName]"
        }
        $connection.Dispose()
    }
}
