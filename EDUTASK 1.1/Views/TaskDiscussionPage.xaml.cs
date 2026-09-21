using EDUTASK_1._1.Services;
using EDUTASK_1._1.Views.Base;
using System.Data;
using TaskDiscussionItem = EDUTASK_1._1.Models.TaskDiscussionItem;
using SubtaskDisplayItem = EDUTASK_1._1.Models.SubtaskDisplayItem;

namespace EDUTASK_1._1.Views;

public partial class TaskDiscussionPage : EduTaskPage
{
    private readonly DatabaseService _database = new();
    private readonly int _taskID;
    private readonly int? _subtaskID;
    private readonly int? _userID;
    private readonly int? _teacherID;
    private readonly string _senderType;
    private readonly int _senderID;
    private readonly string _readerType;
    private readonly int _readerID;
    private readonly bool _isReadOnly;

    public TaskDiscussionPage(
        int taskID,
        int? subtaskID,
        int? userID,
        int? teacherID,
        bool isReadOnly = false)
    {
        if (userID.HasValue == teacherID.HasValue
            || (userID.HasValue && userID.Value <= 0)
            || (teacherID.HasValue && teacherID.Value <= 0))
            throw new ArgumentException("A discussion participant must be either a user or a teacher.");

        InitializeComponent();
        _taskID = taskID;
        _subtaskID = subtaskID;
        _userID = userID;
        _teacherID = teacherID;
        _readerType = teacherID.HasValue ? "Teacher" : "User";
        _readerID = teacherID ?? userID!.Value;
        _senderType = _readerType;
        _senderID = _readerID;
        _isReadOnly = isReadOnly || !subtaskID.HasValue;
        DiscussionComposer.IsVisible = !_isReadOnly;
        ReadOnlyNotice.IsVisible = _isReadOnly;
        if (!subtaskID.HasValue)
            ReadOnlyNoticeLabel.Text = "Previous task discussion is read-only.";
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
            string[] teacherNames = taskTable.AsEnumerable()
                .Select(row => row["TeacherName"]?.ToString()?.Trim())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Cast<string>()
                .ToArray();
            bool teacherView = _teacherID.HasValue;
            DataRow? firstTaskRow = taskTable.Rows.Count > 0 ? taskTable.Rows[0] : null;
            string participantName;

            if (teacherView)
            {
                participantName = firstTaskRow?["CreatorName"]?.ToString()?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(participantName))
                    participantName = "Director / Staff";
            }
            else
            {
                participantName = teacherNames.Length switch
                {
                    0 => "No teacher assigned",
                    1 => teacherNames[0],
                    _ => $"{teacherNames[0]} +{teacherNames.Length - 1}"
                };
            }

            ParticipantNameLabel.Text = _subtaskID.HasValue ? participantName : "Previous task discussion";
            List<TaskDiscussionItem> discussions;
            if (_subtaskID is int subtaskID)
            {
                await _database.MarkTaskDiscussionsReadAsync(subtaskID, _readerType, _readerID);
                discussions = await _database.GetTaskDiscussionsAsync(_taskID, subtaskID);
            }
            else
            {
                discussions = await _database.GetPreviousTaskDiscussionsAsync(_taskID);
            }
            int returnSequence = 0;
            foreach (var discussion in discussions)
            {
                discussion.IsMine = discussion.Sender_type == _senderType && discussion.Sender_id == _senderID;
                if (discussion.IsProofReturn)
                    discussion.ReturnSequence = ++returnSequence;
            }

            DiscussionView.ItemsSource = discussions;
        }
        catch
        {
            await UiAlertService.ShowAsync(this, "Discussion couldn't load", "We couldn't load the messages. Please try again.", "OK");
        }
        finally
        {
            DiscussionRefreshView.IsRefreshing = false;
        }
    }

    private async void OnRefreshing(object sender, EventArgs e) => await LoadAsync();

    private async void OnBackClicked(object sender, EventArgs e) =>
        await Navigation.PopModalAsync(false);

    private async void OnDiscussionTapped(object sender, TappedEventArgs e)
    {
        if (!_subtaskID.HasValue || e.Parameter is not TaskDiscussionItem { IsProofReturn: true })
            return;
        try
        {
            List<SubtaskDisplayItem> subtasks = await _database.GetTaskSubtasksAsync(_taskID);
            SubtaskDisplayItem? subtask = subtasks.FirstOrDefault(item => item.Subtask_id == _subtaskID);
            if (subtask is null || !subtask.HasProofHistory)
            {
                await UiAlertService.ShowAsync(this, "History unavailable", "No proof submission history was found.");
                return;
            }
            await Navigation.PushModalAsync(new SubtaskProofHistoryPage(
                subtask,
                _userID.HasValue && subtask.IsProofPending,
                LoadAsync));
        }
        catch
        {
            await UiAlertService.ShowAsync(this, "History couldn't open", "We couldn't load the submission history. Please try again.");
        }
    }

    private async void OnSendClicked(object sender, EventArgs e)
    {
        if (_isReadOnly || !_subtaskID.HasValue)
            return;

        string messageText = DiscussionEditor.Text?.Trim() ?? string.Empty;
        if (messageText.Length == 0)
            return;

        SendButton.IsEnabled = false;
        try
        {
            bool added = await _database.AddTaskDiscussionAsync(
                _taskID,
                _subtaskID.Value,
                _senderType,
                _senderID,
                messageText);
            if (!added)
                return;

            DiscussionEditor.Text = string.Empty;
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
