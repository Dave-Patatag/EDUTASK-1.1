using EDUTASK_1._1.Services;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Collections.ObjectModel;
using DashboardTaskItem = EDUTASK_1._1.Models.DashboardTaskItem;
using DeadlineTaskGroup = EDUTASK_1._1.Models.DeadlineTaskGroup;
using SubtaskDisplayItem = EDUTASK_1._1.Models.SubtaskDisplayItem;
using PreparedProofImage = EDUTASK_1._1.Models.PreparedProofImage;
using TeacherOption = EDUTASK_1._1.Models.TeacherOption;

namespace EDUTASK_1._1.Views
{
    public partial class TeacherDashboardPage : ContentPage
    {
        private DatabaseService _db = new DatabaseService();
        private readonly List<DashboardTaskItem> _loadedTasks = [];
        private string _currentFilter = "All";
        private DeadlineFilterSelection _deadlineFilter = DeadlineFilterSelection.AnyDate;
        private bool _teachersLoaded;
        private bool _isTodayExpanded = true;
        private bool _isCompletedTodayExpanded = true;
        private int _todayTaskCount;
        private int _completedTodayTaskCount;
        private readonly HashSet<int> _expandedTaskGroups = [];
        private bool _isReminderOpen;
        private int? _selectedReminderTeacherID;
        private int? _reminderShownForTeacherID;

        public TeacherDashboardPage()
        {
            InitializeComponent();
            WireProfileIcon();
        }

        private void WireProfileIcon()
        {
            if (Content is not Grid pageLayout)
                return;

            var bottomNavigation = pageLayout.Children
                .OfType<Grid>()
                .FirstOrDefault(grid => Grid.GetRow(grid) == 4);

            var profileIcon = bottomNavigation?.Children
                .OfType<Image>()
                .FirstOrDefault(image => image.Source is FileImageSource source &&
                                         string.Equals(source.File, "personicon.png", StringComparison.OrdinalIgnoreCase));

            if (profileIcon is null)
                return;

            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += OnProfileIconTapped;
            profileIcon.GestureRecognizers.Add(tapGesture);
        }

        private async void OnProfileIconTapped(object? sender, TappedEventArgs e)
        {
            if (TeacherPicker.SelectedItem is not TeacherOption selectedTeacher)
            {
                await UiAlertService.ShowAsync(this, "Choose a teacher", "Select a teacher before opening a profile.");
                return;
            }

            try
            {
                var teacher = await _db.GetTeacherByIdAsync(selectedTeacher.TeacherID);
                if (teacher is null)
                {
                    await UiAlertService.ShowAsync(this, "Profile not found", "We couldn't find this teacher's profile.");
                    return;
                }

                DashboardFlyoutPage.Current?.ShowDetail(new ProfilePage(teacher));
            }
            catch (SqlException ex)
            {
                await UiAlertService.ShowAsync(this, "Profile couldn't load", "This teacher's profile is not fully set up yet.");
            }
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            if (!_teachersLoaded)
                await LoadTeachersAsync();
            else
                await LoadTasks("All", showReminder: false);
        }

        private async Task LoadTeachersAsync()
        {
            try
            {
                var table = await _db.GetAllTeachersAsync();
                var teachers = table.AsEnumerable().Select(row => new TeacherOption
                {
                    TeacherID = row.Field<int>("TeacherID"),
                    FirstName = row.Field<string>("FirstName") ?? string.Empty,
                    LastName = row.Field<string>("LastName") ?? string.Empty
                }).ToList();

                TeacherPicker.ItemsSource = teachers;
                _teachersLoaded = true;
                TeacherPicker.SelectedIndex = teachers.Count > 0 ? 0 : -1;

                if (teachers.Count == 0)
                {
                    _loadedTasks.Clear();
                    ApplyTaskFilters();
                }
            }
            catch (Exception ex)
            {
                await UiAlertService.ShowAsync(this, "Teachers couldn't load", "We couldn't load the teacher list. Please try again.");
            }
        }

        private async void OnTeacherSelected(object sender, EventArgs e)
        {
            if (TeacherPicker.SelectedItem is not TeacherOption teacher)
                return;

            bool teacherChanged = _selectedReminderTeacherID != teacher.TeacherID;
            if (teacherChanged)
            {
                _selectedReminderTeacherID = teacher.TeacherID;
                _reminderShownForTeacherID = null;
            }

            await LoadTasks("All", showReminder: teacherChanged);
        }

        private async Task LoadTasks(string filter, bool showReminder = false)
        {
            HashSet<int> expandedTaskIDs = _loadedTasks
                .Where(task => task.IsExpanded)
                .Select(task => task.TaskID)
                .ToHashSet();
            try
            {
                _currentFilter = filter;
                if (TeacherPicker.SelectedItem is not TeacherOption teacher)
                    return;

                var dt = await _db.GetTeacherTasksAsync(teacher.TeacherID);
                _loadedTasks.Clear();

                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    string completion = dt.Rows[i]["CompletionStatus"]?.ToString() ?? "Pending";
                    bool isAcknowledged = dt.Rows[i].IsNull("IsAcknowledged")
                        ? false
                        : Convert.ToBoolean(dt.Rows[i]["IsAcknowledged"]);
                    DateTime? deadline = dt.Rows[i].IsNull("Deadline")
                        ? null
                        : Convert.ToDateTime(dt.Rows[i]["Deadline"]);

                    int taskID = Convert.ToInt32(dt.Rows[i]["TaskID"]);
                    var subtasks = await _db.GetTaskSubtasksAsync(taskID);
                    bool needsRevision = completion == "Returned" ||
                                         subtasks.Any(subtask => subtask.IsProofReturned) ||
                                         (completion == "For Validation" && subtasks.Any(subtask => !subtask.HasProof));
                    bool proofEditingIsAvailable = isAcknowledged &&
                                                   (completion is "Pending" or "Returned" || needsRevision);
                    foreach (var subtask in subtasks)
                    {
                        subtask.ProofEditingIsAvailable = proofEditingIsAvailable;
                        subtask.UnreadDiscussionCount = await _db.GetUnreadTaskCommentCountAsync(
                            subtask.SubtaskID, "Teacher", teacher.TeacherID);
                    }
                    string status = completion == "Completed"
                        ? "Completed"
                        : needsRevision
                            ? "Needs Revision"
                            : completion == "For Validation"
                                ? "For Validation"
                                : isAcknowledged ? "Acknowledged" : "Pending";
                    int submittedProgressItems = subtasks.Count(subtask => subtask.IsProofPending || subtask.IsProofApproved);
                    int verifiedProgressItems = subtasks.Count(subtask => subtask.IsProofApproved);

                    _loadedTasks.Add(new DashboardTaskItem
                    {
                        TaskID = taskID,
                        AssignmentID = Convert.ToInt32(dt.Rows[i]["AssignmentID"]),
                        Title = dt.Rows[i]["Title"].ToString(),
                        Description = string.IsNullOrWhiteSpace(dt.Rows[i]["Description"]?.ToString())
                            ? "No description provided."
                            : dt.Rows[i]["Description"].ToString()!,
                        TeacherName = teacher.DisplayName,
                        DeadlineDisplay = deadline.HasValue ? $"Due Date: {deadline:MMM dd, yyyy}" : "No deadline",
                        Priority = dt.Rows[i]["Priority"]?.ToString() ?? "Unassigned",
                        PriorityColor = GetPriorityColor(dt.Rows[i]["Priority"]?.ToString() ?? string.Empty),
                        Status = status,
                        StatusColor = status switch
                        {
                            "Completed" => Color.FromArgb("#16803A"),
                            "Needs Revision" => Color.FromArgb("#DC2626"),
                            "For Validation" => Color.FromArgb("#6554C0"),
                            "Acknowledged" => Color.FromArgb("#2563EB"),
                            _ => Color.FromArgb("#D97706")
                        },
                        Deadline = deadline,
                        CompletedAt = dt.Rows[i].IsNull("CompletedAt") ? null : dt.Rows[i].Field<DateTime>("CompletedAt"),
                        IsCompleted = completion == "Completed",
                        ShowAcknowledge = !isAcknowledged && completion == "Pending",
                        Subtasks = subtasks,
                        SubmittedProgressItems = submittedProgressItems,
                        VerifiedProgressItems = verifiedProgressItems,
                        TotalProgressItems = subtasks.Count,
                        IsExpanded = expandedTaskIDs.Contains(taskID)
                    });
                }

                ApplyTaskFilters();
                if (showReminder)
                    await ShowDueTaskReminderAsync();
            }
            catch (Exception)
            {
                await UiAlertService.ShowAsync(this, "Tasks couldn't load", "We couldn't load this teacher's tasks. Please try again.");
            }
        }

        private async Task ShowDueTaskReminderAsync()
        {
            if (_isReminderOpen || TeacherPicker.SelectedItem is not TeacherOption teacher ||
                _reminderShownForTeacherID == teacher.TeacherID)
                return;

            DateTime today = DateTime.Today;
            List<(string Title, string Priority)> dueToday = _loadedTasks
                .Where(task => !task.IsCompleted && task.Deadline?.Date == today)
                .Select(task => (task.Title, task.Priority))
                .ToList();
            List<(string Title, string Priority)> dueTomorrow = _loadedTasks
                .Where(task => !task.IsCompleted && task.Deadline?.Date == today.AddDays(1))
                .Select(task => (task.Title, task.Priority))
                .ToList();

            if (dueToday.Count == 0 && dueTomorrow.Count == 0)
                return;

            try
            {
                _isReminderOpen = true;
                _reminderShownForTeacherID = teacher.TeacherID;
                bool viewTasks = await UiAlertService.ShowTaskReminderAsync(this, dueToday, dueTomorrow, today);
                if (viewTasks)
                {
                    _currentFilter = dueToday.Count > 0 ? "Today" : "All";
                    _deadlineFilter = DeadlineFilterSelection.AnyDate;
                    ApplyTaskFilters();
                }
            }
            finally
            {
                _isReminderOpen = false;
            }
        }
        private void ApplyTaskFilters()
        {
            UpdateFilterChipStyles();
            string searchText = TaskSearchBar?.Text?.Trim() ?? string.Empty;
            IEnumerable<DashboardTaskItem> tasks = _loadedTasks.Where(task => _currentFilter switch
            {
                "Today" => task.Deadline is not null && task.Deadline.Value.Date == DateTime.Today,
                "Completed" => task.IsCompleted,
                "Overdue" => !task.IsCompleted && task.Deadline is not null && task.Deadline.Value.Date < DateTime.Today,
                _ => true
            });

            tasks = tasks.Where(task => _deadlineFilter.Matches(task.Deadline));

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                tasks = tasks.Where(task =>
                    task.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    task.TeacherName.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    task.Priority.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    task.Status.Contains(searchText, StringComparison.OrdinalIgnoreCase));
            }

            PopulateTodayAndCompletedSections(tasks.ToList());
        }

        private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            ClearSearchButton.IsVisible = !string.IsNullOrEmpty(TaskSearchBar.Text);
            ApplyTaskFilters();
        }

        private void OnClearSearchTapped(object sender, EventArgs e)
        {
            TaskSearchBar.Text = string.Empty;
            ApplyTaskFilters();
        }

        private void OnSearchIconTapped(object sender, EventArgs e)
        {
            TaskSearchBar.Focus();
            ApplyTaskFilters();
        }

        private static Color GetPriorityColor(string priority) => priority switch
        {
            "High" => Color.FromArgb("#DC2626"),
            "Medium" => Color.FromArgb("#EAB308"),
            "Low" => Color.FromArgb("#22A447"),
            _ => Colors.Gray
        };

        private static int GetPriorityRank(string? priority) => priority?.Trim().ToUpperInvariant() switch
        {
            "HIGH" => 0,
            "MEDIUM" => 1,
            "LOW" => 2,
            _ => 3
        };

        private void UpdateFilterChipStyles()
        {
            Color normal = Color.FromArgb("#F3F4F6");
            Color selected = Color.FromArgb("#DBEAFE");
            bool dateIsActive = _deadlineFilter.Kind != DeadlineFilterKind.AnyDate;

            AllFilterBorder.BackgroundColor = !dateIsActive && _currentFilter == "All" ? selected : normal;
            TodayFilterBorder.BackgroundColor = !dateIsActive && _currentFilter == "Today" ? selected : normal;
            OverdueFilterBorder.BackgroundColor = !dateIsActive && _currentFilter == "Overdue" ? selected : normal;
            DateFilterBorder.BackgroundColor = dateIsActive ? selected : normal;
        }

        private void ShowDateFilterPressedState()
        {
            Color normal = Color.FromArgb("#F3F4F6");
            AllFilterBorder.BackgroundColor = normal;
            TodayFilterBorder.BackgroundColor = normal;
            OverdueFilterBorder.BackgroundColor = normal;
            DateFilterBorder.BackgroundColor = Color.FromArgb("#DBEAFE");
        }
        private async void OnDateFilterClicked(object sender, EventArgs e)
        {
            ShowDateFilterPressedState();
            DeadlineFilterSelection? selected = await DeadlineFilterDialog.ShowAsync(this, _deadlineFilter, _loadedTasks);
            if (selected is null)
            {
                UpdateFilterChipStyles();
                return;
            }

            _deadlineFilter = selected;
            UpdateFilterChipStyles();
            ApplyTaskFilters();
        }
        private async Task SelectStatusFilterAsync(string filter)
        {
            _deadlineFilter = DeadlineFilterSelection.AnyDate;
            await LoadTasks(filter);
        }

        private async void OnAllTasksClicked(object sender, EventArgs e) => await SelectStatusFilterAsync("All");
        private async void OnTodayTasksClicked(object sender, EventArgs e) => await SelectStatusFilterAsync("Today");
        private async void OnOverdueTasksClicked(object sender, EventArgs e) => await SelectStatusFilterAsync("Overdue");

        private void OnMenuClicked(object sender, EventArgs e)
        {
            if (DashboardFlyoutPage.Current is { } flyout)
                flyout.IsPresented = true;
        }

        private async void OnAcknowledgeTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is not int assignmentId)
                return;
            try
            {
                await _db.AcknowledgeTaskAsync(assignmentId);
                await UiAlertService.ShowAsync(this, "Task acknowledged", "You can now start working on this task.");
                await LoadTasks(_currentFilter);
                var acknowledgedTask = _loadedTasks.FirstOrDefault(task => task.AssignmentID == assignmentId);
                if (acknowledgedTask is not null)
                {
                    acknowledgedTask.IsExpanded = true;
                    ApplyTaskFilters();
                }
            }
            catch (Exception)
            {
                await UiAlertService.ShowAsync(this, "Task couldn't be updated", "We couldn't update this task. Please try again.");
            }
        }

        private async void OnUploadProofClicked(object sender, EventArgs e)
        {
            if (sender is not ImageButton { CommandParameter: SubtaskDisplayItem subtask })
                return;
            try
            {
                FileResult? file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Select a file" });
                if (file is null)
                    return;
                PreparedProofImage image = await ProofImageService.PrepareAsync(file);
                if (!await _db.UploadSubtaskProofAsync(subtask.SubtaskID, image))
                {
                    await UiAlertService.ShowAsync(this, "File couldn't upload", "We couldn't save this file. Please try again.");
                    return;
                }
                await LoadTasks(_currentFilter);
                SubtaskDisplayItem? draft = _loadedTasks
                    .SelectMany(task => task.Subtasks)
                    .FirstOrDefault(item => item.SubtaskID == subtask.SubtaskID);
                if (draft is not null && draft.IsProofDraft)
                    await Navigation.PushModalAsync(new SubtaskProofDraftPage(
                        draft,
                        () => LoadTasks(_currentFilter)));
            }
            catch (Exception ex)
            {
                await UiAlertService.ShowAsync(this, "File couldn't upload", ex is InvalidOperationException or ArgumentException ? ex.Message : "We couldn't save this file. Please try again.");
            }
        }

        private async void OnTeacherProofButtonClicked(object sender, EventArgs e)
        {
            if (sender is not ImageButton { CommandParameter: SubtaskDisplayItem subtask })
                return;

            if (subtask.IsProofDraft)
            {
                await Navigation.PushModalAsync(new SubtaskProofDraftPage(
                    subtask,
                    () => LoadTasks(_currentFilter)));
                return;
            }

            if (subtask.HasProofHistory)
                await Navigation.PushModalAsync(new SubtaskProofHistoryPage(subtask));
        }

        private async void OnConfirmProofClicked(object sender, EventArgs e)
        {
            if (sender is not ImageButton { CommandParameter: SubtaskDisplayItem subtask })
                return;
            if (!await UiAlertService.ConfirmAsync(this, "Submit file", "Send this file to the admin for review?", "Submit", "Cancel"))
                return;
            try
            {
                if (!await _db.ConfirmSubtaskProofAsync(subtask.SubtaskID))
                {
                    await UiAlertService.ShowAsync(this, "File already updated", "This file was already submitted or replaced. Refresh the task to see the latest version.");
                    return;
                }
                await LoadTasks(_currentFilter);
            }
            catch (Exception)
            {
                await UiAlertService.ShowAsync(this, "File couldn't be submitted", "We couldn't send this file for review. Please try again.");
            }
        }

        private async void OnViewDraftProofClicked(object sender, EventArgs e)
        {
            if (sender is not ImageButton { CommandParameter: SubtaskDisplayItem subtask })
                return;
            try
            {
                var file = await _db.GetSubtaskProofImageAsync(subtask.SubtaskID);
                if (file is null)
                {
                    await UiAlertService.ShowAsync(this, "File unavailable", "We couldn't find the selected draft file.");
                    return;
                }
                await ProofFileViewerService.OpenAsync(this, file.Value, $"proof-draft-{subtask.SubtaskID}");
            }
            catch
            {
                await UiAlertService.ShowAsync(this, "File couldn't open", "We couldn't open this draft. Please try again.");
            }
        }

        private async void OnRemoveProofClicked(object sender, EventArgs e)
        {
            if (sender is not ImageButton { CommandParameter: SubtaskDisplayItem subtask })
                return;
            if (subtask.IsProofReturned &&
                !await UiAlertService.ConfirmAsync(
                    this,
                    "Remove returned file",
                    "Remove this file before uploading a new one?",
                    "Remove",
                    "Cancel"))
                return;
            try
            {
                if (!await _db.RemoveSubtaskProofAsync(subtask.SubtaskID))
                {
                    await UiAlertService.ShowAsync(this, "File can't be removed", "Only files that are waiting to be submitted or need changes can be removed.");
                    return;
                }
                await LoadTasks(_currentFilter);
            }
            catch (Exception)
            {
                await UiAlertService.ShowAsync(this, "File couldn't be removed", "We couldn't remove this file. Please try again.");
            }
        }

        private async void OnProofHistoryClicked(object sender, TappedEventArgs e)
        {
            if (e.Parameter is not SubtaskDisplayItem subtask || !subtask.HasProofHistory)
                return;
            await Navigation.PushModalAsync(new SubtaskProofHistoryPage(subtask));
        }

        private async void OnSubtaskDiscussionClicked(object sender, EventArgs e)
        {
            if (sender is not ImageButton { CommandParameter: SubtaskDisplayItem subtask })
                return;
            if (TeacherPicker.SelectedItem is not TeacherOption teacher)
                return;
            DashboardTaskItem? task = _loadedTasks.FirstOrDefault(item => item.TaskID == subtask.TaskID);
            var discussionPage = new TaskDiscussionPage(
                subtask.TaskID,
                subtask.SubtaskID,
                "Teacher",
                teacher.TeacherID,
                teacher.DisplayName,
                task?.IsCompleted ?? false);
            discussionPage.Disappearing += (_, _) => subtask.UnreadDiscussionCount = 0;
            await Navigation.PushAsync(discussionPage);
        }

        private ObservableCollection<DeadlineTaskGroup> BuildDeadlineGroups(IEnumerable<DashboardTaskItem> tasks)
        {
            return new ObservableCollection<DeadlineTaskGroup>(tasks
                .OrderBy(task => GetPriorityRank(task.Priority))
                .ThenBy(task => task.Deadline?.Date ?? DateTime.MaxValue)
                .ThenBy(task => task.Title)
                .Select(task => new DeadlineTaskGroup
                {
                    Deadline = task.Deadline?.Date,
                    TaskTitle = task.Title,
                    DeadlineDisplay = task.Deadline.HasValue ? FormatDeadlineGroupHeader(task.Deadline.Value) : "No deadline",
                    TeacherSummary = task.TeacherName,
                    Tasks = [task],
                    IsExpanded = _expandedTaskGroups.Contains(task.TaskID),
                    PriorityColor = task.PriorityColor
                }));
        }

        private static string FormatDeadlineGroupHeader(DateTime deadline)
        {
            DateTime today = DateTime.Today;
            int daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
            DateTime currentWeekStart = today.AddDays(-daysSinceMonday);
            DateTime nextWeekStart = currentWeekStart.AddDays(7);
            return deadline.Date >= currentWeekStart && deadline.Date < nextWeekStart
                ? deadline.ToString("dddd")
                : deadline.ToString("dddd, MMMM d");
        }

        private void OnDeadlineGroupTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is not DeadlineTaskGroup group || group.Tasks.FirstOrDefault() is not { } task)
                return;
            group.IsExpanded = !group.IsExpanded;
            if (group.IsExpanded)
                _expandedTaskGroups.Add(task.TaskID);
            else
                _expandedTaskGroups.Remove(task.TaskID);
        }

        private void OnTodayToggleClicked(object sender, EventArgs e)
        {
            _isTodayExpanded = !_isTodayExpanded;
            TodayToggleArrow.Text = _isTodayExpanded ? "\u25BC" : "\u25B2";
            UpdateTaskSectionVisibility();
        }

        private void OnCompletedTodayToggleClicked(object sender, EventArgs e)
        {
            _isCompletedTodayExpanded = !_isCompletedTodayExpanded;
            CompletedTodayToggleArrow.Text = _isCompletedTodayExpanded ? "\u25BC" : "\u25B2";
            UpdateTaskSectionVisibility();
        }

        private void UpdateTaskSectionVisibility()
        {
            bool hasActiveTasks = _todayTaskCount > 0;
            bool hasCompletedTasks = _completedTodayTaskCount > 0;
            TodaySectionHeader.IsVisible = hasActiveTasks;
            TaskListView.IsVisible = hasActiveTasks && _isTodayExpanded;
            bool showsCompletionArea = _currentFilter is "All" or "Today";
            CompletedTodaySectionHeader.IsVisible = _currentFilter == "All" || (_currentFilter == "Today" && hasCompletedTasks);
            CompletionHistoryLink.IsVisible = _currentFilter == "All" || (_currentFilter == "Today" && hasCompletedTasks);
            CompletedTodayTaskListView.IsVisible = hasCompletedTasks && _isCompletedTodayExpanded;
            NoTodayTasksLabel.IsVisible = !hasActiveTasks && !hasCompletedTasks;
        }

        private void PopulateTodayAndCompletedSections(IEnumerable<DashboardTaskItem> visibleTasks)
        {
            var activeTasks = visibleTasks.Where(task => !task.IsCompleted).ToList();
            var completedTasks = visibleTasks.Where(task => task.IsCompleted && task.CompletedAt?.Date == DateTime.Today).ToList();
            TodayHeaderLabel.Text = $"Active Tasks ({activeTasks.Count})";
            CompletedTodayHeaderLabel.Text = $"Completed Today ({completedTasks.Count})";
            BindableLayout.SetItemsSource(TaskListView, BuildDeadlineGroups(activeTasks));
            BindableLayout.SetItemsSource(CompletedTodayTaskListView, BuildDeadlineGroups(completedTasks));
            _todayTaskCount = activeTasks.Count;
            _completedTodayTaskCount = completedTasks.Count;
            UpdateTaskSectionVisibility();
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync();
        }
        private async void OnCompletionHistoryClicked(object sender, EventArgs e)
        {
            var history = _loadedTasks
                .Where(task => task.IsCompleted && task.CompletedAt?.Date < DateTime.Today)
                .OrderByDescending(task => task.CompletedAt)
                .ToList();
            await Navigation.PushAsync(new CompletionHistoryPage(history));
        }
    }
}
