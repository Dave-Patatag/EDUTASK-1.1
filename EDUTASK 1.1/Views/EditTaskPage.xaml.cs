using EDUTASK_1._1.Services;
using EDUTASK_1._1.ViewModels;
using Microsoft.Maui.Controls.Shapes;
using System.Collections.ObjectModel;
using SubtaskDraft = EDUTASK_1._1.Models.SubtaskDraft;
using TeacherOption = EDUTASK_1._1.Models.TeacherOption;
using TaskEditData = EDUTASK_1._1.Models.TaskEditData;

namespace EDUTASK_1._1.Views;

public partial class EditTaskPage : ContentPage
{
    private readonly int _taskID;
    private readonly TaskFormViewModel _formViewModel;
    private readonly bool _isReadOnly;
    private TaskEditData? _task;
    private DateTime? _originalDeadline;
    private bool _loaded;
    private string _selectedPriority = string.Empty;
    public ObservableCollection<SubtaskDraft> EditableSubtasks { get; } = [];

    public EditTaskPage(int taskID, bool isReadOnly = false)
    {
        InitializeComponent();
        _taskID = taskID;
        _isReadOnly = isReadOnly;
        _formViewModel = new TaskFormViewModel(this);
        BindingContext = this;
        ApplyReadOnlyMode();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_loaded)
            return;

        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        SetBusy(true);
        try
        {
            List<TeacherOption> teachers = await _formViewModel.LoadActiveTeachersAsync();
            TeacherPicker.ItemsSource = teachers;

            _task = await _formViewModel.LoadTaskAsync(_taskID);
            if (_task is null)
                return;

            _originalDeadline = _task.Deadline;
            TitleEntry.Text = _task.Title;
DescriptionEditor.Text = _task.Description;
            SummaryTitleLabel.Text = _task.Title;
            SummaryDescriptionLabel.Text = string.IsNullOrWhiteSpace(_task.Description)
                ? "No description provided"
                : _task.Description;
            SummaryDueDateLabel.Text = _task.Deadline.ToString("MMM d, yyyy");
            SummaryPriorityLabel.Text = string.IsNullOrWhiteSpace(_task.Priority) ? "None" : _task.Priority;
            SummaryReminderLabel.Text = _task.IsDailyRemind ? "On" : "Off";
            DailyRemindSwitch.IsToggled = _task.IsDailyRemind;
            DueDatePicker.Date = _task.Deadline.Date;
            DueDateDisplayLabel.Text = _task.Deadline.ToString("MMM dd, yyyy");
            DueDateLabel.Text = _task.Deadline.ToString("MMM d, yyyy");
            CompletionInfo.IsVisible = _isReadOnly &&
                string.Equals(_task.CompletionStatus, "Completed", StringComparison.OrdinalIgnoreCase);
            CompletedAtLabel.Text = _task.CompletedAt.HasValue
                ? $"Completed {_task.CompletedAt.Value:MMM d, yyyy 'at' h:mm tt}"
                : "Completion confirmed";

            var subtasks = await new DatabaseService().GetTaskSubtasksAsync(_taskID);
            EditableSubtasks.Clear();
            foreach (var subtask in subtasks)
                EditableSubtasks.Add(new SubtaskDraft { SubtaskID = subtask.SubtaskID, Title = subtask.Title, IsCompleted = subtask.IsCompleted });
            UpdateSubtaskVisibility();
            SubtasksList.Children.Clear();
            foreach (var subtask in subtasks)
            {
                var marker = new Label
                {
                    Text = subtask.IsCompleted ? "✓" : "○",
                    TextColor = subtask.IsCompleted ? Color.FromArgb("#24743A") : Color.FromArgb("#7B8794"),
                    FontSize = 18,
                    VerticalOptions = LayoutOptions.Center
                };
                var title = new Label
                {
                    Text = subtask.Title,
                    TextColor = Color.FromArgb("#2C3E50"),
                    TextDecorations = subtask.IsCompleted ? TextDecorations.Strikethrough : TextDecorations.None,
                    VerticalOptions = LayoutOptions.Center
                };
                var row = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) }, ColumnSpacing = 10 };
                row.Add(marker);
                row.Add(title, 1);
                SubtasksList.Children.Add(new Border
                {
                    BackgroundColor = subtask.IsCompleted ? Color.FromArgb("#F0F8F2") : Colors.White,
                    Stroke = Color.FromArgb("#DCE3E8"),
                    StrokeShape = new RoundRectangle { CornerRadius = 8 },
                    Padding = new Thickness(12, 9),
                    Content = row
                });
            }
            SubtasksSection.IsVisible = subtasks.Count > 0;
            SelectPriority(_task.Priority);
TeacherPicker.SelectedItem = teachers.FirstOrDefault(t => t.TeacherID == _task.TeacherID);
            SummaryTeacherLabel.Text = TeacherPicker.SelectedItem is TeacherOption selectedTeacher
                ? selectedTeacher.DisplayName
                : "Not assigned";
            _loaded = true;
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void OnAddSubtaskClicked(object sender, EventArgs e)
    {
        EditableSubtasks.Add(new SubtaskDraft());
        UpdateSubtaskVisibility();
    }

    private async void OnRemoveSubtaskClicked(object sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: SubtaskDraft subtask }) return;
        if (subtask.IsCompleted)
        {
            await UiAlertService.ShowAsync(this, "Subtask can't be removed", "Completed or approved subtasks must stay with the task.", "OK");
            return;
        }
        EditableSubtasks.Remove(subtask);
        UpdateSubtaskVisibility();
    }

    private void UpdateSubtaskVisibility() => NoEditableSubtasksLabel.IsVisible = EditableSubtasks.Count == 0;
    private void OnPrioritySelected(object sender, EventArgs e)
    {
        if (_isReadOnly)
            return;
        _selectedPriority = PriorityPicker.SelectedItem?.ToString() ?? string.Empty;
    }

    private void SelectPriority(string? priority)
    {
        _selectedPriority = priority ?? string.Empty;
        PriorityPicker.SelectedItem = _selectedPriority;
    }
    private void OnDueDateSelected(object sender, DateChangedEventArgs e) =>
        DueDateDisplayLabel.Text = e.NewDate.ToString("MMM dd, yyyy");

    private void OnSelectTeacherClicked(object sender, EventArgs e) => TeacherPicker.Focus();
    private async void OnSaveClicked(object sender, EventArgs e)
    {
        if (_isReadOnly || _task is null || !_originalDeadline.HasValue)
            return;

        SetBusy(true);
        try
        {
            DateTime deadline = DueDatePicker.Date;
            bool updated = await _formViewModel.UpdateAsync(
                _task,
                TitleEntry.Text ?? string.Empty,
                DescriptionEditor.Text,
                DailyRemindSwitch.IsToggled,
                TeacherPicker.SelectedItem as TeacherOption,
                deadline,
                _originalDeadline.Value,
                _selectedPriority,
                EditableSubtasks);

            if (!updated)
                return;

            await UiAlertService.ShowAsync(this, "Task updated", "Your changes have been saved.", "OK");
            await ClosePageAsync();
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        if (_isReadOnly || _task is null)
            return;

        SetBusy(true);
        try
        {
            bool deleted = await _formViewModel.DeleteAsync(_taskID, _task.Title);
            if (!deleted)
                return;

            await UiAlertService.ShowAsync(this, "Task deleted", "The task has been removed.", "OK");
            await ClosePageAsync();
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnBackClicked(object sender, EventArgs e)
    {
        await ClosePageAsync();
    }

    private async Task ClosePageAsync()
    {
        if (Navigation.ModalStack.Contains(this))
            await Navigation.PopModalAsync(false);
        else
            await Navigation.PopAsync(false);
    }
    private void ApplyReadOnlyMode()
    {
        ReadOnlySummary.IsVisible = _isReadOnly;
        EditableDetailsForm.IsVisible = !_isReadOnly;
        EditableSchedule.IsVisible = !_isReadOnly;
        ReadOnlySchedule.IsVisible = _isReadOnly;
        BackButton.IsVisible = _isReadOnly;
        SaveButton.IsVisible = !_isReadOnly;
        DeleteButton.IsVisible = !_isReadOnly && UserSessionService.CanDeleteTasks;
        TitleEntry.IsReadOnly = _isReadOnly;
        DescriptionEditor.IsReadOnly = _isReadOnly;
        DueDatePicker.IsEnabled = !_isReadOnly;
        TeacherPicker.IsEnabled = !_isReadOnly;
        DailyRemindSwitch.IsEnabled = !_isReadOnly;
        PriorityPicker.IsEnabled = !_isReadOnly;
    }

    private void SetBusy(bool busy)
    {
        LoadingIndicator.IsVisible = busy;
        LoadingIndicator.IsRunning = busy;
        SaveButton.IsEnabled = !busy;
        DeleteButton.IsEnabled = !busy;
    }

}




