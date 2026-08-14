using EDUTASK_1._1.Services;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TeacherOption = EDUTASK_1._1.Models.TeacherOption;

namespace EDUTASK_1._1.Views;

public partial class TeacherSelectionPage : ContentPage
{
    private readonly List<SelectableTeacher> _teachers;
    private readonly TaskCompletionSource<IReadOnlyList<TeacherOption>?> _completion = new();
    private readonly int _minimumSelection;

    public TeacherSelectionPage(IEnumerable<TeacherOption> teachers, IEnumerable<TeacherOption> selectedTeachers, int minimumSelection = 1)
    {
        InitializeComponent();
        _minimumSelection = Math.Max(1, minimumSelection);
        var selectedIds = selectedTeachers.Select(teacher => teacher.TeacherID).ToHashSet();
        _teachers = teachers
            .Select(teacher => new SelectableTeacher(teacher, selectedIds.Contains(teacher.TeacherID), UpdateCount))
            .ToList();
        TeachersView.ItemsSource = _teachers;
        UpdateCount();
    }

    public async Task<IReadOnlyList<TeacherOption>?> ShowAsync(INavigation navigation)
    {
        await navigation.PushModalAsync(this, false);
        return await _completion.Task;
    }

    protected override bool OnBackButtonPressed()
    {
        _ = CloseAsync(null);
        return true;
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e) => ApplyFilter(e.NewTextValue);

    private void ApplyFilter(string? searchText)
    {
        string query = searchText?.Trim() ?? string.Empty;
        TeachersView.ItemsSource = string.IsNullOrEmpty(query)
            ? _teachers
            : _teachers.Where(item => item.Teacher.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private void OnSelectAllClicked(object sender, EventArgs e)
    {
        foreach (SelectableTeacher teacher in _teachers)
            teacher.IsSelected = true;
    }

    private void OnClearClicked(object sender, EventArgs e)
    {
        foreach (SelectableTeacher teacher in _teachers)
            teacher.IsSelected = false;
    }

    private async void OnCancelClicked(object sender, EventArgs e) => await CloseAsync(null);

    private async void OnDoneClicked(object sender, EventArgs e)
    {
        List<TeacherOption> selected = _teachers
            .Where(item => item.IsSelected)
            .Select(item => item.Teacher)
            .ToList();

        if (selected.Count < _minimumSelection)
        {
            await UiAlertService.ShowAsync(
                this,
                "Select a teacher",
                "Choose a teacher to assign the task.",
                "Got it");
            return;
        }

        await CloseAsync(selected);
    }

    private async Task CloseAsync(IReadOnlyList<TeacherOption>? result)
    {
        if (_completion.Task.IsCompleted)
            return;

        _completion.TrySetResult(result);
        await Navigation.PopModalAsync(false);
    }

    private void UpdateCount() =>
        SelectionCountLabel.Text = $"{_teachers.Count(item => item.IsSelected)} of {_teachers.Count} selected";

    private sealed class SelectableTeacher : INotifyPropertyChanged
    {
        private readonly Action _selectionChanged;
        private bool _isSelected;

        public SelectableTeacher(TeacherOption teacher, bool isSelected, Action selectionChanged)
        {
            Teacher = teacher;
            _isSelected = isSelected;
            _selectionChanged = selectionChanged;
        }

        public TeacherOption Teacher { get; }
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                    return;
                _isSelected = value;
                OnPropertyChanged();
                _selectionChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}


