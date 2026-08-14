using EDUTASK_1._1.ViewModels;
using AdministratorTaskItem = EDUTASK_1._1.Models.AdministratorTaskItem;
using System.Windows.Input;

namespace EDUTASK_1._1.Views;

public partial class ViewTasksPage : ContentPage
{
    private readonly bool? _filterCompleted;
    private readonly TaskListViewModel _viewModel;

    public ViewTasksPage() : this(null)
    {
    }

    public ViewTasksPage(bool? filterCompleted)
    {
        InitializeComponent();
        _filterCompleted = filterCompleted;
        _viewModel = new TaskListViewModel(this);
        _viewModel.RefreshCommand = new Command(async () => await RefreshAsync());
        BindingContext = _viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        try
        {
            await _viewModel.LoadAsync(_filterCompleted);
        }
        finally
        {
            TaskRefreshView.IsRefreshing = false;
        }
    }

    private async void OnCreateClicked(object sender, EventArgs e)
    {
        await Navigation.PushModalAsync(new CreateTaskPage(), false);
    }

    private async void OnTaskSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not AdministratorTaskItem task)
            return;

        TaskCollectionView.SelectedItem = null;
        await Navigation.PushModalAsync(new EditTaskPage(task.TaskID), false);
    }

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: AdministratorTaskItem task })
            await _viewModel.DeleteAsync(task);
    }
}
