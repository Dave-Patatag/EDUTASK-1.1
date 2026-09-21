using EDUTASK_1._1.Services;
using EDUTASK_1._1.Helpers;
using EDUTASK_1._1.Views.Base;
using EDUTASK_1._1.ViewModels;
using System.Collections.ObjectModel;
using SubtaskDraft = EDUTASK_1._1.Models.SubtaskDraft;
using TeacherOption = EDUTASK_1._1.Models.TeacherOption;

namespace EDUTASK_1._1.Views;

public partial class CreateTaskPage : EduTaskPage
{
    private readonly TaskFormViewModel _viewModel;
    private bool _teachersLoaded;
    private List<TeacherOption> _teachers = [];
    private string _selectedPriority = string.Empty;
    private DateTime? _selectedDueDate;
    private List<TeacherOption> _selectedTeachers = [];
    public ObservableCollection<SubtaskDraft> Subtasks { get; } = [];

    public CreateTaskPage()
    {
        InitializeComponent();
        _viewModel = new TaskFormViewModel(this);
        BindingContext = this;
        PriorityPicker.HandlerChanged += (_, _) =>
            PriorityPickerStyling.Apply(PriorityPicker, _selectedPriority);
        PriorityPicker.SelectedIndex = 0;
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
    private async void OnDueDateTapped(object sender, TappedEventArgs e)
    {
        var picker = new DueDateSelectionPage(_selectedDueDate);
        DateTime? selected = await picker.ShowAsync(Navigation);
        if (selected is null)
            return;

        _selectedDueDate = selected;
        DueDateDisplayLabel.Text = selected.Value.ToString("MMM dd, yyyy");
        DueDateDisplayLabel.TextColor = AppColors.TextSecondary;
        FormFieldValidation.ClearFieldError(DueDateBorder, DueDateErrorLabel);
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

    private void OnSubtaskTextChanged(object sender, TextChangedEventArgs e)
    {
        if (Subtasks.Any(subtask => !string.IsNullOrWhiteSpace(subtask.Title)))
            FormFieldValidation.ClearFieldError(SubtasksBorder, SubtasksErrorLabel);
    }

    private void UpdateSubtaskVisibility() => NoSubtasksLabel.IsVisible = Subtasks.Count == 0;

    private void OnTitleChanged(object sender, TextChangedEventArgs e) =>
        FormFieldValidation.ClearFieldError(TitleBorder, TitleErrorLabel);

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
        if (_selectedTeachers.Count > 0)
            FormFieldValidation.ClearFieldError(TeachersBorder, TeachersErrorLabel);
    }

    private bool ValidateFields()
    {
        FormFieldValidation.ClearFieldError(TitleBorder, TitleErrorLabel);
        FormFieldValidation.ClearFieldError(TeachersBorder, TeachersErrorLabel);
        FormFieldValidation.ClearFieldError(DueDateBorder, DueDateErrorLabel);
        FormFieldValidation.ClearFieldError(SubtasksBorder, SubtasksErrorLabel);

        bool valid = true;
        if (string.IsNullOrWhiteSpace(TitleEntry.Text))
        {
            FormFieldValidation.SetFieldError(TitleBorder, TitleErrorLabel, "Enter a task title.");
            valid = false;
        }
        if (!Subtasks.Any(subtask => !string.IsNullOrWhiteSpace(subtask.Title)))
        {
            FormFieldValidation.SetFieldError(SubtasksBorder, SubtasksErrorLabel, "Add at least one subtask.");
            valid = false;
        }
        if (_selectedDueDate is null)
        {
            FormFieldValidation.SetFieldError(DueDateBorder, DueDateErrorLabel, "Select a due date.");
            valid = false;
        }
        if (_selectedTeachers.Count == 0)
        {
            FormFieldValidation.SetFieldError(TeachersBorder, TeachersErrorLabel, "Select at least one teacher.");
            valid = false;
        }
        return valid;
    }

    private void OnPrioritySelected(object sender, EventArgs e)
    {
        _selectedPriority = PriorityPicker.SelectedItem?.ToString() ?? string.Empty;
        PriorityPickerStyling.Apply(PriorityPicker, _selectedPriority);
    }

    private async void OnCreateTaskClicked(object sender, EventArgs e)
    {
        if (!ValidateFields())
            return;

        CreateButton.IsEnabled = false;
        try
        {
            DateTime deadline = _selectedDueDate!.Value;
            bool created = await _viewModel.CreateAsync(
                TitleEntry.Text ?? string.Empty,
                DescriptionEditor.Text,
                DailyRemindSwitch.IsToggled,
                _selectedTeachers,
                Subtasks,
                deadline,
                _selectedPriority,
                createIndividualTasks: false);

            if (!created)
                return;

            DashboardFlyoutPage.Current?.InvalidateTaskData();
            await UiAlertService.ShowAsync(this, "Task created", "The task is ready and has been assigned.", "OK");
            await ClosePageAsync();
        }
        finally
        {
            CreateButton.IsEnabled = true;
        }
    }
} 

