Task discussions stay attached to their subtask when its title changes. New
subtasks start without messages. Removing an existing subtask asks for confirmation;
its discussion, proof history, and read receipts are removed only when the task
is saved. Cancelling the editor leaves the database unchanged. Existing subtasks
cannot be removed by clearing their titles, and completed subtasks are checked
again during saving.

Tasks with legacy messages whose `SubtaskID` is NULL display a **Previous task
discussion** button on both teacher and director/staff dashboard cards. This view
is read-only and is available even when the task has no subtasks. It does not
create a subtask read receipt or combine legacy messages with a subtask discussion.

The embedded `Database/MigrateTaskDiscussions.sql` migration runs automatically
before discussion access or task editing in a newly started app process. It adds
a composite foreign key for `(TaskID, SubtaskID)` and a trigger that rejects new
messages without a subtask and updates to legacy messages. Existing legacy
messages and identities remain intact. Task deletion can still remove them.

If existing discussion records refer to a subtask belonging to a different task,
the migration raises error 50004 and rolls back. It does not guess where those
messages belong. Inspect the affected rows before correcting their ownership:

```sql
SELECT d.DiscussionID, d.TaskID AS DiscussionTaskID,
       d.SubtaskID, s.TaskID AS SubtaskTaskID
FROM dbo.TaskDiscussion AS d
JOIN dbo.Subtask AS s ON s.SubtaskID = d.SubtaskID
WHERE d.TaskID <> s.TaskID;
```

Run the database regression checks from the repository root:

```powershell
powershell -NoProfile -File tests\Verify-TaskDiscussions.ps1
```

The script requires Windows authentication and permission to create databases on
`(localdb)\MSSQLLocalDB` (override with `-Server`). It creates uniquely named test
databases and removes them in `finally`; it never opens the application database.
It exercises the actual migration and the SQL extracted from the service for
message insertion, message loading, and subtask cleanup. It checks fresh and
legacy schemas, repeated migration, rejected invalid writes, legacy preservation,
rename/add behavior, cleanup, transaction rollback, and pre-existing mismatches.

UI checks to perform in the app:

- Remove an unfinished subtask with messages: Cancel keeps it; Remove only stages
  the change; saving removes its discussion and proof history.
- Clear an existing subtask title: saving requests a title or explicit removal.
- Open Previous task discussion on each dashboard: only old task-level messages
  appear, with a read-only notice and no composer.
- Rename a subtask and add another: the existing discussion remains and the new
  subtask has no messages.
