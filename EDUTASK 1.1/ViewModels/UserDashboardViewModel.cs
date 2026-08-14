using EDUTASK_1._1.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace EDUTASK_1._1.ViewModels;

public sealed class UserDashboardViewModel : INotifyPropertyChanged
{
    private int _totalTasks;
    private int _completedTasks;
    private int _pendingTasks;
    private ObservableCollection<DashboardTaskItem> _tasks = [];

    public int TotalTasks { get => _totalTasks; set { _totalTasks = value; OnPropertyChanged(); } }
    public int CompletedTasks { get => _completedTasks; set { _completedTasks = value; OnPropertyChanged(); } }
    public int PendingTasks { get => _pendingTasks; set { _pendingTasks = value; OnPropertyChanged(); } }
    public ObservableCollection<DashboardTaskItem> Tasks { get => _tasks; set { _tasks = value; OnPropertyChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
