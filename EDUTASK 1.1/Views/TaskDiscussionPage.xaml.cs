using EDUTASK_1._1.Services;
using TaskCommentItem = EDUTASK_1._1.Models.TaskCommentItem;
using SubtaskDisplayItem = EDUTASK_1._1.Models.SubtaskDisplayItem;

namespace EDUTASK_1._1.Views;

public partial class TaskDiscussionPage : ContentPage
{
    private readonly DatabaseService _database = new();
    private readonly int _taskID;
    private readonly int _subtaskID;
    private readonly string _authorType;
    private readonly int _authorID;
    private readonly string _authorName;
    private readonly bool _isReadOnly;

    public TaskDiscussionPage(
        int taskID,
        int subtaskID,
        string authorType,
        int authorID,
        string authorName,
        bool isReadOnly = false)
    {
        InitializeComponent();
        _taskID = taskID;
        _subtaskID = subtaskID;
        _authorType = authorType;
        _authorID = authorID;
        _authorName = string.IsNullOrWhiteSpace(authorName) ? authorType : authorName;
        _isReadOnly = isReadOnly;
        CommentComposer.IsVisible = !_isReadOnly;
        ReadOnlyNotice.IsVisible = _isReadOnly;
        BackButton.IsVisible = true;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var taskTable = await _database.GetTaskByIDAsync(_taskID);
            TaskTitleLabel.Text = taskTable.Rows.Count == 0
                ? "Task"
                : taskTable.Rows[0]["Title"]?.ToString() ?? "Task";
            await _database.MarkTaskCommentsReadAsync(_subtaskID, _authorType, _authorID);
            var comments = await _database.GetTaskCommentsAsync(_taskID, _subtaskID);
            int returnSequence = 0;
            foreach (var comment in comments)
            {
                comment.IsMine =
                    comment.AuthorID == _authorID &&
                    string.Equals(comment.AuthorType, _authorType, StringComparison.OrdinalIgnoreCase);
                if (comment.IsProofReturn)
                    comment.ReturnSequence = ++returnSequence;
            }

            CommentsView.ItemsSource = comments;
        }
        catch
        {
            await UiAlertService.ShowAsync(this, "Discussion couldn't load", "We couldn't load the messages. Please try again.", "OK");
        }
        finally
        {
            CommentsRefreshView.IsRefreshing = false;
        }
    }

    private async void OnRefreshing(object sender, EventArgs e) => await LoadAsync();

    private async void OnBackClicked(object sender, EventArgs e) =>
        await Navigation.PopAsync();

    private async void OnCommentTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is not TaskCommentItem { IsProofReturn: true })
            return;
        try
        {
            List<SubtaskDisplayItem> subtasks = await _database.GetTaskSubtasksAsync(_taskID);
            SubtaskDisplayItem? subtask = subtasks.FirstOrDefault(item => item.SubtaskID == _subtaskID);
            if (subtask is null || !subtask.HasProofHistory)
            {
                await UiAlertService.ShowAsync(this, "History unavailable", "No proof submission history was found.");
                return;
            }
            await Navigation.PushModalAsync(new SubtaskProofHistoryPage(
                subtask,
                _authorType == "User" && subtask.IsProofPending,
                LoadAsync));
        }
        catch
        {
            await UiAlertService.ShowAsync(this, "History couldn't open", "We couldn't load the submission history. Please try again.");
        }
    }

    private async void OnSendClicked(object sender, EventArgs e)
    {
        if (_isReadOnly)
            return;

        string commentText = CommentEditor.Text?.Trim() ?? string.Empty;
        if (commentText.Length == 0)
            return;

        SendButton.IsEnabled = false;
        try
        {
            bool added = await _database.AddTaskCommentAsync(
                _taskID,
                _subtaskID,
                _authorType,
                _authorID,
                _authorName,
                commentText);
            if (!added)
                return;

            CommentEditor.Text = string.Empty;
            await LoadAsync();
        }
        catch (ArgumentException ex)
        {
            await UiAlertService.ShowAsync(this, "Message couldn't be sent", ex.Message, "OK");
        }
        catch
        {
            await UiAlertService.ShowAsync(this, "Message couldn't be sent", "We couldn't send your message. Please try again.", "OK");
        }
        finally
        {
            SendButton.IsEnabled = true;
        }
    }
}
