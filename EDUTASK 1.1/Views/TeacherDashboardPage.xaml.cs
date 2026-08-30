using EDUTASK_1._1.Services;
using Microsoft.Data.SqlClient;
using EDUTASK_1._1.Helpers;
using EDUTASK_1._1.Views.Base;
using System.Data;
using System.Collections.ObjectModel;
using DashboardTaskItem = EDUTASK_1._1.Models.DashboardTaskItem;
using DeadlineTaskGroup = EDUTASK_1._1.Models.DeadlineTaskGroup;
using SubtaskDisplayItem = EDUTASK_1._1.Models.SubtaskDisplayItem;
using PreparedProofImage = EDUTASK_1._1.Models.PreparedProofImage;
using TeacherOption = EDUTASK_1._1.Models.TeacherOption;
using Teachers = EDUTASK_1._1.Models.Teachers;

namespace EDUTASK_1._1.Views
{
    // Shares ~65% of its code-behind with DirectorStaffDashboardPage. See
    // DASHBOARD-DUPLICATION.md in this folder for what overlaps and what does not.
    public partial class TeacherDashboardPage : EduTaskPage
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
        private TeacherOption? _currentTeacher;

        public TeacherDashboardPage()
        {
            InitializeComponent();  
            UpdateGreeting();
            WireProfileIcon();

        }

        private void UpdateGreeting()
        {
            int hour = DateTime.Now.Hour;
            GreetingLabel.Text = hour < 12
                ? "Good morning"
                : hour < 18
                    ? "Good afternoon"
                    : "Good evening";
        }

        private void WireProfileIcon()
        {
            if (Content is not Grid pageLayout)
                return;
                
            var bottomNavigation = pageLayout.Children
                .OfType<Grid>()
                .FirstOrDefault(grid => Grid.GetRow(grid) == 3);

            var profileIcon = bottomNavigation?.Children
                .OfType<Image>()
                .FirstOrDefault(image => image.Source is FileImageSource source &&
                                         string.Equals(source.File, "defaultprofile.png", StringComparison.OrdinalIgnoreCase));

            if (profileIcon is null)
                return;

            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += OnProfileIconTapped;
            profileIcon.GestureRecognizers.Add(tapGesture);
        }

        private async void OnProfileIconTapped(object? sender, TappedEventArgs e)
        {
            if (_currentTeacher is not { } selectedTeacher)
            {
                await UiAlertService.ShowAsync(this, "Profile unavailable", "We couldn't load your profile. Please try again.");
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

                DashboardFlyoutPage.Current?.ShowProfile(teacher);
            }
            catch (SqlException ex)
            {
                await UiAlertService.ShowAsync(this, "Profile couldn't load", "This teacher's profile is not fully set up yet.");
            }
        }

        private void OnNotificationIconTapped(object? sender, TappedEventArgs e)
        {
            Teachers? teacher = _currentTeacher is { } selected
                ? new Teachers { TeacherID = selected.TeacherID }
                : null;
            DashboardFlyoutPage.Current?.ShowNotification(teacher);
        }
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            UpdateGreeting();
            if (!_teachersLoaded)
                await LoadTeachersAsync();
            else
                await LoadTasks(_currentFilter, showReminder: false);
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

                if (TeacherSessionService.CurrentTeacher is { } signedInTeacher)
                    teachers = teachers.Where(item => item.TeacherID == signedInTeacher.TeacherID).ToList();

                _teachersLoaded = true;
                _currentTeacher = teachers.FirstOrDefault();

                if (_currentTeacher is not { } teacher)
                {
                    _loadedTasks.Clear();
                    ApplyTaskFilters();
                    return;
                }

                DashboardUserNameLabel.Text = $"Teacher {teacher.FirstName}".Trim();

                bool teacherChanged = _selectedReminderTeacherID != teacher.TeacherID;
                if (teacherChanged)
                {
                    _selectedReminderTeacherID = teacher.TeacherID;
                    _reminderShownForTeacherID = null;
                }

                await LoadTasks("All", showReminder: teacherChanged);
            }
            catch (Exception ex)
            {
                await UiAlertService.ShowAsync(this, "Teachers couldn't load", "We couldn't load the teacher list. Please try again.");
            }
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
                if (_currentTeacher is not { } teacher)
                    return;

                var dt = await _db.GetTeacherTasksAsync(teacher.TeacherID);
                _loadedTasks.Clear();

                int[] taskIDs = dt.AsEnumerable().Select(row => row.Field<int>("TaskID")).Distinct().ToArray();
                Dictionary<int, System.Threading.Tasks.Task<List<SubtaskDisplayItem>>> subtaskLoads =
                    taskIDs.ToDictionary(taskID => taskID, taskID => _db.GetTaskSubtasksAsync(taskID));
                await System.Threading.Tasks.Task.WhenAll(subtaskLoads.Values);

                // A teacher should see one card per task. Older data can contain
                // duplicate assignment rows for the same teacher/task, which
                // otherwise makes acknowledgement appear to duplicate the card.
                HashSet<int> seenTaskIDs = [];
                for (int i = 0; i < dt.Rows.Count; i++)
                {
                    int taskID = Convert.ToInt32(dt.Rows[i]["TaskID"]);
                    if (!seenTaskIDs.Add(taskID))
                        continue;

                    string completion = dt.Rows[i]["CompletionStatus"]?.ToString() ?? "Pending";
                    bool isAcknowledged = dt.Rows[i].IsNull("IsAcknowledged")
                        ? false
                        : Convert.ToBoolean(dt.Rows[i]["IsAcknowledged"]);
                    DateTime? deadline = dt.Rows[i].IsNull("Deadline")
                        ? null
                        : Convert.ToDateTime(dt.Rows[i]["Deadline"]);

                    var subtasks = await subtaskLoads[taskID];
                    bool needsRevision = completion == "Needs Revision" ||
                                         subtasks.Any(subtask => subtask.IsProofReturned) ||
                                         (completion == "For Validation" && subtasks.Any(subtask => !subtask.HasProof));
                    // AcknowledgeTaskAsync advances Pending to Acknowledged.
                    // Keep both values for older rows where IsAcknowledged was
                    // set without advancing CompletionStatus.
                    bool proofEditingIsAvailable = isAcknowledged &&
                                                   (completion is "Pending" or "Acknowledged" or "Needs Revision" ||
                                                    needsRevision);
                    Dictionary<int, int> unreadCounts = await _db.GetUnreadTaskCommentCountsAsync(
                        subtasks.Select(subtask => subtask.SubtaskID), "Teacher", teacher.TeacherID);
                    foreach (var subtask in subtasks)
                    {
                        subtask.ProofEditingIsAvailable = proofEditingIsAvailable;
                        subtask.UnreadDiscussionCount = unreadCounts.GetValueOrDefault(subtask.SubtaskID);
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
                        PriorityColor = TaskPalette.PriorityColor(dt.Rows[i]["Priority"]?.ToString() ?? string.Empty),
                        Status = status,
                        StatusColor = TaskPalette.StatusColor(status),
                        Deadline = deadline,
                        CompletedAt = dt.Rows[i].IsNull("CompletedAt") ? null : dt.Rows[i].Field<DateTime>("CompletedAt"),
                        IsCompleted = completion == "Completed",
                        ShowAcknowledge = !isAcknowledged && completion == "Pending",
                        Subtasks = subtasks,
                        SubmittedProgressItems = submittedProgressItems,
                        VerifiedProgressItems = verifiedProgressItems,
                        TotalProgressItems = subtasks.Count,
                        IsExpanded = expandedTaskIDs.Contains(taskID) || _expandedTaskGroups.Contains(taskID)
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
            if (_isReminderOpen || _currentTeacher is not { } teacher ||
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



        private void UpdateFilterChipStyles()
        {
            bool dateIsActive = _deadlineFilter.Kind != DeadlineFilterKind.AnyDate;

            FilterButtonState.Apply(AllFilterBorder, !dateIsActive && _currentFilter == "All", AllFilterLabel);
            FilterButtonState.Apply(TodayFilterBorder, !dateIsActive && _currentFilter == "Today", TodayFilterLabel);
            FilterButtonState.Apply(OverdueFilterBorder, !dateIsActive && _currentFilter == "Overdue", OverdueFilterLabel);
            StyleTaskToolsButton(dateIsActive);
        }

        private void ShowDateFilterPressedState()
        {
            FilterButtonState.Apply(AllFilterBorder, false, AllFilterLabel);
            FilterButtonState.Apply(TodayFilterBorder, false, TodayFilterLabel);
            FilterButtonState.Apply(OverdueFilterBorder, false, OverdueFilterLabel);
            StyleTaskToolsButton(active: true);
        }

        private void StyleTaskToolsButton(bool active)
        {
            DateFilterBorder.WidthRequest = 42;
            DateFilterBorder.HeightRequest = 42;
            DateFilterBorder.MinimumHeightRequest = 42;
            DateFilterBorder.BackgroundColor = Colors.Transparent;
            DateFilterBorder.Stroke = Colors.Transparent;
            DateFilterBorder.StrokeThickness = 0;
            DateFilterIcon.Source = "blackfiltericon.png";
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
            if (_deadlineFilter.Kind != DeadlineFilterKind.AnyDate)
                _currentFilter = "All";
            UpdateFilterChipStyles();
            ApplyTaskFilters();
        }

        private void OnTaskToolsClicked(object sender, EventArgs e) =>
            OnDateFilterClicked(sender, e);

        private void OnTaskToolsDismissClicked(object sender, EventArgs e) =>
            TaskToolsOverlay.IsVisible = false;

        private void OnTaskToolsDateFilterClicked(object sender, EventArgs e)
        {
            TaskToolsOverlay.IsVisible = false;
            OnDateFilterClicked(sender, e);
        }

        private void OnTaskToolsHistoryClicked(object sender, EventArgs e)
        {
            TaskToolsOverlay.IsVisible = false;
            OnCompletionHistoryClicked(sender, e);
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
                // The update is guarded on IsAcknowledged = 0, so a second tap
                // changes nothing — it must not claim it did, and it must not
                // move the acknowledgement time the director was shown.
                bool acknowledged = await _db.AcknowledgeTaskAsync(assignmentId);
                await UiAlertService.ShowAsync(this,
                    acknowledged ? "Task acknowledged" : "Already acknowledged",
                    acknowledged
                        ? "You can now start working on this task."
                        : "This task was acknowledged earlier, so nothing changed.");
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
                await Navigation.PushModalAsync(new SubtaskProofHistoryPage(subtask), false);
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
            await Navigation.PushModalAsync(new SubtaskProofHistoryPage(subtask), false);
        }

        private async void OnSubtaskDiscussionClicked(object sender, EventArgs e)
        {
            if (sender is not ImageButton { CommandParameter: SubtaskDisplayItem subtask })
                return;
            if (_currentTeacher is not { } teacher)
                return;
            DashboardTaskItem? task = _loadedTasks.FirstOrDefault(item => item.TaskID == subtask.TaskID);
            var discussionPage = new TaskDiscussionPage(
                subtask.TaskID,
                subtask.SubtaskID,
                "Teacher",
                teacher.TeacherID,
                teacher.DisplayName,
                task?.IsCompleted ?? false);
            discussionPage.Disappearing += (_, _) =>
            {
                subtask.UnreadDiscussionCount = 0;
                ApplyTaskFilters();
            };
            await Navigation.PushModalAsync(discussionPage, false);
        }

        private ObservableCollection<DeadlineTaskGroup> BuildDeadlineGroups(IEnumerable<DashboardTaskItem> tasks)
        {
            return new ObservableCollection<DeadlineTaskGroup>(tasks
                .OrderBy(task => TaskPalette.PriorityRank(task.Priority))
                .ThenBy(task => task.Deadline?.Date ?? DateTime.MaxValue)
                .ThenBy(task => task.Title)
                .Select(task => new DeadlineTaskGroup
                {
                    Deadline = task.Deadline?.Date,
                    TaskTitle = task.Title,
                    DeadlineDisplay = task.Deadline.HasValue ? DeadlineTaskGroup.FormatHeader(task.Deadline.Value) : "No deadline",
                    TeacherSummary = task.TeacherName,
                    Tasks = [task],
                    IsExpanded = _expandedTaskGroups.Contains(task.TaskID),
                    PriorityColor = task.PriorityColor
                }));
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

        public void ExpandTask(int taskID)
        {
            _expandedTaskGroups.Add(taskID);
            DashboardTaskItem? task = _loadedTasks.FirstOrDefault(item => item.TaskID == taskID);
            if (task is null)
                return;

            task.IsExpanded = true;
            ApplyTaskFilters();
        }

        public async Task FocusTaskAsync(int taskID)
        {
            ExpandTask(taskID);
            for (int attempt = 0; attempt < 8; attempt++)
            {
                await Task.Delay(50);
                Element? target = TaskListView.Children.OfType<Element>().FirstOrDefault(child =>
                    child.BindingContext is DeadlineTaskGroup group &&
                    group.Tasks.Any(task => task.TaskID == taskID));
                if (target is null)
                    target = CompletedTodayTaskListView.Children.OfType<Element>().FirstOrDefault(child =>
                        child.BindingContext is DeadlineTaskGroup group &&
                        group.Tasks.Any(task => task.TaskID == taskID));
                if (target is null)
                    continue;

                await TasksScrollView.ScrollToAsync(target, ScrollToPosition.Center, true);
                return;
            }
        }

        private void OnTodayToggleClicked(object sender, EventArgs e)
        {
            _isTodayExpanded = !_isTodayExpanded;
            TodayToggleArrow.Source = _isTodayExpanded ? "collapse.png" : "uncollapse.png";
            UpdateTaskSectionVisibility();
        }

        private void OnCompletedTodayToggleClicked(object sender, EventArgs e)
        {
            _isCompletedTodayExpanded = !_isCompletedTodayExpanded;
            CompletedTodayToggleArrow.Source = _isCompletedTodayExpanded ? "collapse.png" : "uncollapse.png";
            UpdateTaskSectionVisibility();
        }

        private void UpdateTaskSectionVisibility()
        {
            bool hasActiveTasks = _todayTaskCount > 0;
            bool hasCompletedTasks = _completedTodayTaskCount > 0;
            bool hasAnyTasks = hasActiveTasks || hasCompletedTasks;
            TodaySectionHeader.IsVisible = hasActiveTasks;
            TaskListView.IsVisible = hasActiveTasks && _isTodayExpanded;
            CompletedTodaySectionHeader.IsVisible = true;
            CompletionHistoryLink.IsVisible = true;
            CompletedTodayTaskListView.IsVisible = hasCompletedTasks && _isCompletedTodayExpanded;
            NoTodayTasksLabel.IsVisible = !hasAnyTasks;
            int overdueCount = _loadedTasks.Count(task => !task.IsCompleted && task.Deadline?.Date < DateTime.Today);
            bool hasSearch = !string.IsNullOrWhiteSpace(TaskSearchBar?.Text);
            bool cleanEmptyState = !hasAnyTasks && !hasSearch
                && _deadlineFilter.Kind == DeadlineFilterKind.AnyDate
                && (_currentFilter is "All" or "Today");
            NoTasksTitleLabel.IsVisible = true;
            NoTasksMessageLabel.IsVisible = true;
            if (hasSearch)
            {
                NoTasksTitleLabel.Text = "No matching tasks";
                NoTasksMessageLabel.Text = "Try a different search term or filter.";
            }
            else if (_deadlineFilter.Kind != DeadlineFilterKind.AnyDate)
            {
                NoTasksTitleLabel.Text = "No tasks for these dates";
                NoTasksMessageLabel.Text = "Try another date range.";
            }
            else if (_currentFilter == "Today")
            {
                // The illustration already communicates the empty state; keep
                // this view quieter by omitting the redundant heading.
                NoTasksTitleLabel.IsVisible = false;
                NoTasksMessageLabel.Text = overdueCount > 0
                    ? $"You're clear for today. You still have {overdueCount} overdue {(overdueCount == 1 ? "task" : "tasks")}."
                    : "You're clear for today.";
            }
            else if (_currentFilter == "Overdue")
            {
                NoTasksTitleLabel.Text = "No overdue tasks";
                NoTasksMessageLabel.Text = "Everything is up to date.";
            }
            else
            {
                NoTasksTitleLabel.Text = "No tasks yet";
                NoTasksMessageLabel.Text = "Assigned tasks will appear here.";
            }
            ViewOverdueEmptyLink.IsVisible = _currentFilter == "Today" && overdueCount > 0 && !hasSearch
                && _deadlineFilter.Kind == DeadlineFilterKind.AnyDate;
        }
        private async void OnEmptyViewOverdueClicked(object sender, EventArgs e) => await SelectStatusFilterAsync("Overdue");

        private void PopulateTodayAndCompletedSections(IEnumerable<DashboardTaskItem> visibleTasks)
        {
            bool dateFilterActive = _deadlineFilter.Kind != DeadlineFilterKind.AnyDate;
            var activeTasks = visibleTasks.Where(task => !task.IsCompleted).ToList();
            var completedTasks = visibleTasks
                .Where(task => task.IsCompleted && (dateFilterActive || task.CompletedAt?.Date == DateTime.Today))
                .ToList();
            TodayHeaderLabel.Text = $"Active Tasks ({activeTasks.Count})";
            CompletedTodayHeaderLabel.Text = dateFilterActive
                ? $"Completed Tasks ({completedTasks.Count})"
                : $"Completed Today ({completedTasks.Count})";
            BindableLayout.SetItemsSource(TaskListView, BuildDeadlineGroups(activeTasks));
            BindableLayout.SetItemsSource(CompletedTodayTaskListView, BuildDeadlineGroups(completedTasks));
            _todayTaskCount = activeTasks.Count;
            _completedTodayTaskCount = completedTasks.Count;
            UpdateTaskSectionVisibility();
        }

        private async void OnBackClicked(object sender, EventArgs e)
        {
            await Navigation.PopAsync(false);
        }
        internal CompletionHistoryPage CreateCompletionHistoryPage()
        {
            var history = _loadedTasks
                .Where(task => task.IsCompleted && task.CompletedAt?.Date < DateTime.Today)
                .OrderByDescending(task => task.CompletedAt)
                .ToList();
            return new CompletionHistoryPage(history);
        }

        private async void OnCompletionHistoryClicked(object sender, EventArgs e)
        {
            if (DashboardFlyoutPage.Current is { } flyout)
            {
                // Keep history inside the shared detail navigation so its
                // bottom bar can switch to tasks, notifications, or profile.
                flyout.ShowDetail(CreateCompletionHistoryPage());
                return;
            }

            await Navigation.PushModalAsync(CreateCompletionHistoryPage(), false);
        }

    }
}
