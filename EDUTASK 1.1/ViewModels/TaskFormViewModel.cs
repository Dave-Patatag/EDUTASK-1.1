using EDUTASK_1._1.Services;
using System.Data;
using TeacherOption = EDUTASK_1._1.Models.TeacherOption;
using SubtaskDraft = EDUTASK_1._1.Models.SubtaskDraft;
using TaskEditData = EDUTASK_1._1.Models.TaskEditData;

namespace EDUTASK_1._1.ViewModels;

public sealed class TaskFormViewModel
{
    private readonly DatabaseService _database;
    private readonly Page _page;

    public TaskFormViewModel(Page page, DatabaseService? database = null)
    {
        _page = page;
        _database = database ?? new DatabaseService();
    }

    public async Task<List<TeacherOption>> LoadActiveTeachersAsync()
    {
        try
        {
            DataTable table = await _database.GetAllTeachersAsync();
            return table.AsEnumerable()
                .Select(row => new TeacherOption
                {
                    TeacherID = row.Field<int>("TeacherID"),
                    FirstName = row.Field<string>("FirstName") ?? string.Empty,
                    LastName = row.Field<string>("LastName") ?? string.Empty
                })
                .ToList();
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Unable to load active teachers", ex);
            return [];
        }
    }

    public async Task<TaskEditData?> LoadTaskAsync(int taskID)
    {
        try
        {
            DataTable table = await _database.GetTaskByIDAsync(taskID);
            if (table.Rows.Count == 0)
            {
                await UiAlertService.ShowAsync(_page, "Task not found", "This task may have been deleted. Return to the dashboard and refresh the list.", "OK");
                return null;
            }

            DataRow row = table.Rows[0];
            return new TaskEditData
            {
                TaskID = row.Field<int>("TaskID"),
                CreatedByUserID = row.IsNull("CreatedByUserID") ? row.Field<int>("UserID") : row.Field<int>("CreatedByUserID"),
                AssignmentID = row.IsNull("AssignmentID") ? null : row.Field<int>("AssignmentID"),
                Title = row.Field<string>("Title") ?? string.Empty,
                Description = row.Field<string>("Description") ?? string.Empty,
                IsDailyRemind = !row.IsNull("isDailyRemind") && row.Field<bool>("isDailyRemind"),
                TeacherID = row.IsNull("TeacherID") ? null : row.Field<int>("TeacherID"),
                Deadline = row.IsNull("Deadline") ? DateTime.Today.AddDays(1).AddHours(17) : row.Field<DateTime>("Deadline"),
                Priority = row.Field<string>("Priority") ?? string.Empty,
                CompletionStatus = row.Field<string>("CompletionStatus") ?? "Pending",
                CompletedAt = row.IsNull("CompletedAt") ? null : row.Field<DateTime>("CompletedAt")
            };
        }
        catch (Exception ex)
        {
            await ShowErrorAsync("Unable to load task details", ex);
            return null;
        }
    }

    public async Task<bool> CreateAsync(
        string title,
        string? description,
        bool isDailyRemind,
        IReadOnlyCollection<TeacherOption> teachers,
        IReadOnlyCollection<SubtaskDraft> subtasks,
        DateTime deadline,
        string priority,
        bool createIndividualTasks)
    {
        try
        {
            if (teachers.Count == 0)
                throw new ArgumentException("Choose a teacher to continue.");

            List<SubtaskDraft> validSubtasks = subtasks
                .Where(subtask => !string.IsNullOrWhiteSpace(subtask.Title))
                .ToList();
            if (validSubtasks.Any(subtask => subtask.Title.Trim().Length > 200))
                throw new ArgumentException("Subtask titles cannot exceed 200 characters.");

            Validate(title, teachers.First(), priority);

            await _database.CreateTaskWithAssignmentsAsync(
                title,
                description,
                adminID: UserSessionService.CurrentUserId,
                isDailyRemind,
                teachers.Select(teacher => teacher.TeacherID).ToList(),
                validSubtasks,
                deadline,
                priority,
                createIndividualTasks);
            return true;
        }        catch (Exception ex)
        {
            await ShowSaveErrorAsync(ex, isUpdate: false);
            return false;
        }
    }

    public async Task<bool> UpdateAsync(
        TaskEditData task,
        string title,
        string? description,
        bool isDailyRemind,
        TeacherOption? teacher,
        DateTime deadline,
        DateTime originalDeadline,
        string priority,
        IReadOnlyCollection<SubtaskDraft> subtasks)
    {
        try
        {
            Validate(title, teacher, priority);
            if (UserSessionService.IsStaff && task.CompletionStatus != "Pending")
                throw new UnauthorizedAccessException("Staff can edit pending tasks only.");
            List<SubtaskDraft> validSubtasks = subtasks.Where(s => !string.IsNullOrWhiteSpace(s.Title)).ToList();
            if (validSubtasks.Any(s => s.Title.Trim().Length > 200))
                throw new ArgumentException("Subtask titles cannot exceed 200 characters.");

            if (!task.AssignmentID.HasValue)
                throw new InvalidOperationException("This task has no assignment to update.");

            TeacherOption selectedTeacher = teacher!;

            bool updated = await _database.UpdateTaskWithAssignmentAsync(
                task.TaskID,
                task.AssignmentID.Value,
                title,
                description,
                isDailyRemind,
                selectedTeacher.TeacherID,
                deadline,
                originalDeadline,
                priority,
                validSubtasks);

            if (!updated)
                throw new InvalidOperationException("The task could not be updated because it was changed or removed.");

            return true;
        }
        catch (Exception ex)
        {
            await ShowSaveErrorAsync(ex, isUpdate: true);
            return false;
        }
    }

    public async Task<bool> DeleteAsync(int taskID, string title)
    {
        try
        {
            if (!UserSessionService.CanDeleteTasks)
                throw new UnauthorizedAccessException("Only a Director can delete tasks.");
            bool confirmed = await UiAlertService.ConfirmAsync(_page, "Delete task", $"Delete '{title}'? This task and its files will be permanently removed.", "Delete", "Cancel");
            if (!confirmed)
                return false;

            bool deleted = await _database.DeleteTaskAsync(taskID, UserSessionService.CurrentUserId);
            if (!deleted)
                throw new InvalidOperationException("The task no longer exists.");

            return true;
        }
        catch (Exception ex)
        {
            string message = ex is InvalidOperationException
                ? ex.Message
                : "We couldn't delete this task. Please try again.";
            await UiAlertService.ShowAsync(_page, "Task couldn't be deleted", message, "OK");
            return false;
        }
    }

    private static void Validate(
        string title,
        TeacherOption? teacher,
        string priority)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Please complete the required fields.");
        if (title.Trim().Length > 200)
            throw new ArgumentException("Title cannot exceed 200 characters.");
        if (teacher is null)
            throw new ArgumentException("Please complete the required fields.");
        if (priority is not ("Low" or "Medium" or "High"))
            throw new ArgumentException("Please complete the required fields.");
    }

    private Task ShowErrorAsync(string title, Exception exception)
    {
        string message = exception is ArgumentException or UnauthorizedAccessException or InvalidOperationException
            ? exception.Message
            : "Please check the SQL Server LocalDB connection and try again.";
        return UiAlertService.ShowAsync(_page, title, message, "OK");
    }

    private Task ShowSaveErrorAsync(Exception exception, bool isUpdate)
    {
        string message = exception is ArgumentException or UnauthorizedAccessException
            ? RemoveParameterDetails(exception.Message)
            : isUpdate
                ? "Your changes weren't saved. Please try again."
                : "The task wasn't created. Please try again.";

        string title = exception is UnauthorizedAccessException
            ? "Task is read-only"
            : exception is ArgumentException ? "Check task details" : "Couldn't save task";
        return UiAlertService.ShowAsync(_page, title, message, "OK");
    }

    private static string RemoveParameterDetails(string message)
    {
        int parameterStart = message.IndexOf(" (Parameter", StringComparison.Ordinal);
        return parameterStart >= 0 ? message[..parameterStart] : message;
    }
}




