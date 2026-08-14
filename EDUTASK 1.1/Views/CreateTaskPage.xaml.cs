using EDUTASK_1._1.Services;
using EDUTASK_1._1.ViewModels;
using System.Collections.ObjectModel;
using SubtaskDraft = EDUTASK_1._1.Models.SubtaskDraft;
using TeacherOption = EDUTASK_1._1.Models.TeacherOption;

namespace EDUTASK_1._1.Views;

public partial class CreateTaskPage : ContentPage
{
    private readonly TaskFormViewModel _viewModel;
    private bool _teachersLoaded;
    private List<TeacherOption> _teachers = [];
    private string _selectedPriority = string.Empty;
    private List<TeacherOption> _selectedTeachers = [];
    private bool _createIndividualTasks = true;
    public ObservableCollection<SubtaskDraft> Subtasks { get; } = [];

    public CreateTaskPage()
    {
        InitializeComponent();
        _viewModel = new TaskFormViewModel(this);
        BindingContext = this;

        DateTime defaultDeadline = DateTime.Today.AddDays(1);
        DueDatePicker.Date = defaultDeadline.Date;
        DueDateDisplayLabel.Text = defaultDeadline.ToString("MMM dd, yyyy");
        PriorityPicker.SelectedIndex = 1;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_teachersLoaded)
            return;

        _teachers = await _viewModel.LoadActiveTeachersAsync();
        _teachersLoaded = true;
    }


    private async Task ClosePageAsync()
    {
        if (Navigation.ModalStack.Contains(this))
            await Navigation.PopModalAsync(false);
        else
            await Navigation.PopAsync(false);
    }
    private void OnDueDateSelected(object sender, DateChangedEventArgs e)
    {
        DueDateDisplayLabel.Text = e.NewDate.ToString("MMM dd, yyyy");
    }
    private async void OnBackTapped(object sender, EventArgs e)
    {
        await ClosePageAsync();
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        await ClosePageAsync();
    }


    private void OnAddSubtaskClicked(object sender, EventArgs e)
    {
        Subtasks.Add(new SubtaskDraft());
        UpdateSubtaskVisibility();
    }

    private void OnRemoveSubtaskClicked(object sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: SubtaskDraft subtask })
            Subtasks.Remove(subtask);
        UpdateSubtaskVisibility();
    }

    private void UpdateSubtaskVisibility() => NoSubtasksLabel.IsVisible = Subtasks.Count == 0;

    private async void OnSelectTeachersClicked(object sender, EventArgs e)
    {
        var selector = new TeacherSelectionPage(_teachers, _selectedTeachers, 1);
        IReadOnlyList<TeacherOption>? selection = await selector.ShowAsync(Navigation);
        if (selection is null)
            return;

        _selectedTeachers = selection.ToList();
        SelectedTeachersLabel.Text = _selectedTeachers.Count == 0
            ? "No teachers selected"
            : string.Join(", ", _selectedTeachers.Select(teacher => teacher.DisplayName));
    }

    private void OnPrioritySelected(object sender, EventArgs e)
    {
        _selectedPriority = PriorityPicker.SelectedItem?.ToString() ?? string.Empty;
    }

    private async void OnCreateTaskClicked(object sender, EventArgs e)
    {
        CreateButton.IsEnabled = false; 
        try
        {
            DateTime deadline = DueDatePicker.Date;
            bool created = await _viewModel.CreateAsync(
                TitleEntry.Text ?? string.Empty,
                DescriptionEditor.Text,
                DailyRemindSwitch.IsToggled,
                _selectedTeachers,
                Subtasks,
                deadline,
                _selectedPriority,
                _createIndividualTasks);

            if (!created)
                return;
                
            await UiAlertService.ShowAsync(this, "Task created", "The task is ready and has been assigned.", "OK");
            await ClosePageAsync();
        }
        finally
        {
            CreateButton.IsEnabled = true;
        }
    }
} 

