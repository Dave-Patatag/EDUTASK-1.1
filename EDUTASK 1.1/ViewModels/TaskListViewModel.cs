using EDUTASK_1._1.Services;
using System.Collections.ObjectModel;
using System.Data;
using System.Windows.Input;
using AdministratorTaskItem = EDUTASK_1._1.Models.AdministratorTaskItem;

namespace EDUTASK_1._1.ViewModels;

public sealed class TaskListViewModel
{
    private readonly DatabaseService _database;
    private readonly Page _page;

    public TaskListViewModel(Page page, DatabaseService? database = null)
    {
        _page = page;
        _database = database ?? new DatabaseService();
    }

    public ObservableCollection<AdministratorTaskItem> Tasks { get; } = [];
    public ICommand? RefreshCommand { get; set; }

    public async Task LoadAsync(bool? completedFilter = null)
    {
        try
        {
            DataTable table = await _database.GetAllTasksWithTeachersAsync();
            Tasks.Clear();

            foreach (DataRow row in table.Rows)
            {
                string completion = row.Field<string>("CompletionStatus") ?? "Pending";
                if (completedFilter == true && completion != "Completed")
                    continue;
                if (completedFilter == false && completion == "Completed")
                    continue;

                bool acknowledged = !row.IsNull("IsAcknowledged") && row.Field<bool>("IsAcknowledged");
                DateTime? deadline = row.IsNull("Deadline") ? null : row.Field<DateTime>("Deadline");
                string priority = row.Field<string>("Priority") ?? "Unassigned";
                string status = completion == "Completed" ? "Completed" : acknowledged ? "Acknowledged" : "Pending";

                Tasks.Add(new AdministratorTaskItem
                {
                    TaskID = row.Field<int>("TaskID"),
                    Title = row.Field<string>("Title") ?? "Untitled task",
                    TeacherName = string.IsNullOrWhiteSpace(row.Field<string>("TeacherName"))
                        ? "Unassigned"
                        : row.Field<string>("TeacherName")!,
                    DeadlineDisplay = deadline?.ToString("MMM dd, yyyy") ?? "No deadline",
                    Priority = priority,
                    Status = status,
                    PriorityColor = PriorityColor(priority),
                    StatusColor = status == "Completed" ? Colors.Green : acknowledged ? Colors.Blue : Colors.Orange
                });
            }
        }
        catch (Exception ex)
        {
            await UiAlertService.ShowAsync(_page, 
                "Unable to load tasks",
                "Tasks could not be loaded. Please check the SQL Server LocalDB connection and try again.",
                "OK");
            System.Diagnostics.Debug.WriteLine(ex);
        }
    }

    public async Task<bool> DeleteAsync(AdministratorTaskItem task)
    {
        try
        {
            bool confirmed = await UiAlertService.ConfirmAsync(_page, "Delete task", $"Delete '{task.Title}'? This task and its files will be permanently removed.", "Delete", "Cancel");
            if (!confirmed)
                return false;

            bool deleted = await _database.DeleteTaskAsync(task.TaskID, UserSessionService.CurrentUserId);
            if (!deleted)
                throw new InvalidOperationException("The task no longer exists.");

            await LoadAsync();
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

    private static Color PriorityColor(string priority) => priority switch
    {
        "High" => Colors.Red,
        "Medium" => Colors.Orange,
        "Low" => Colors.Blue,
        _ => Colors.Gray
    };
}
