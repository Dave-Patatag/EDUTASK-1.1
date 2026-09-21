using EDUTASK_1._1.Services;
using System.Data;
using EDUTASK_1._1.Helpers;
using EDUTASK_1._1.Views.Base;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Data.SqlClient;
using DashboardTaskItem = EDUTASK_1._1.Models.DashboardTaskItem;
using DeadlineTaskGroup = EDUTASK_1._1.Models.DeadlineTaskGroup;
using PreparedProofImage = EDUTASK_1._1.Models.PreparedProofImage;
using EDUTASK_1._1.ViewModels;

namespace EDUTASK_1._1.Views
{
    // Shares ~65% of its code-behind with TeacherDashboardPage. See
    // DASHBOARD-DUPLICATION.md in this folder for what overlaps and what does not.
    public partial class DirectorStaffDashboardPage : EduTaskPage
    {
        private DatabaseService _db = new DatabaseService();
        private UserDashboardViewModel _viewModel;
        private readonly List<DashboardTaskItem> _loadedTasks = [];
        private string _currentFilter = "All";
        private DeadlineFilterSelection _deadlineFilter = DeadlineFilterSelection.AnyDate;
        private bool _isTodayExpanded = true;
        private bool _isCompletedTodayExpanded = true;
        private CancellationTokenSource? _monitoringCancellation;
        private bool _isLoadingTasks;
        private bool _isRefreshingProgress;
        private bool _hasLoadedDashboard;
        private int? _expandedTaskID;

        public DirectorStaffDashboardPage()
        {
            InitializeComponent();
            UpdateGreeting();
            _viewModel = new UserDashboardViewModel();
            BindingContext = _viewModel;
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
            try
            {
                var user = await UserSessionService.GetCurrentUserAsync(forceRefresh: true);
                if (user is null)
                {
                    await UiAlertService.ShowAsync(this, "Profile not found", "We couldn't find your profile. Please restart the app and try again.", "OK");
                    return;
                }

                DashboardFlyoutPage.Current?.ShowProfile(user);
            }
            catch (SqlException ex)
            {
                await UiAlertService.ShowAsync(this, 
                    "Unable to load profile",
                    $"Run Database/MigrateUserProfile.sql in SSMS, then try again.\n\n{ex.Message}",
                    "OK");
            }
        }

        private void OnNotificationIconTapped(object? sender, TappedEventArgs e)
        {
            DashboardFlyoutPage.Current?.ShowNotification(UserSessionService.CurrentUser);
        }
        protected override async void OnAppearing()
        {
            base.OnAppearing();
            UpdateGreeting();
            if (!_hasLoadedDashboard)
                _hasLoadedDashboard = await LoadDashboardDataAsync();
            StartProgressMonitoring();
        }

        internal void RequestDataRefresh() => _hasLoadedDashboard = false;

        protected override void OnDisappearing()
        {
            _monitoringCancellation?.Cancel();
            _monitoringCancellation?.Dispose();
            _monitoringCancellation = null;
            base.OnDisappearing();
        }

        private void StartProgressMonitoring()
        {
            _monitoringCancellation?.Cancel();
            _monitoringCancellation?.Dispose();
            _monitoringCancellation = new CancellationTokenSource();
            _ = MonitorProgressAsync(_monitoringCancellation.Token);
        }

        private async Task MonitorProgressAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
                while (await timer.WaitForNextTickAsync(cancellationToken))
                    await RefreshProgressAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when the admin leaves this page.
            }
        }

        private async Task RefreshProgressAsync(CancellationToken cancellationToken)
        {
            if (_isLoadingTasks || _isRefreshingProgress)
                return;

            _isRefreshingProgress = true;
            try
            {
                foreach (DashboardTaskItem task in _loadedTasks.ToList())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var subtasks = await _db.GetTaskSubtasksAsync(task.Task_id);
                    object? completionValue = await _db.ExecuteScalarAsync(
                        "SELECT Completion_status FROM TaskAssignment WHERE Assignment_id = @Assignment_id",
                        [new SqlParameter("@Assignment_id", SqlDbType.Int) { Value = task.Assignment_id }]);
                    string completion = completionValue?.ToString() ?? "Pending";
                    int submittedItems = subtasks.Count(subtask => subtask.IsProofPending || subtask.IsProofApproved);
                    int verifiedItems = subtasks.Count(subtask => subtask.IsProofApproved);
                    int totalItems = subtasks.Count;

                    if (task.SubmittedProgressItems != submittedItems)
                        task.SubmittedProgressItems = submittedItems;
                    if (task.VerifiedProgressItems != verifiedItems)
                        task.VerifiedProgressItems = verifiedItems;
                    if (task.TotalProgressItems != totalItems)
                        task.TotalProgressItems = totalItems;
                }
            }
            finally
            {
                _isRefreshingProgress = false;
            }
        }

        private async Task<bool> LoadDashboardDataAsync()
        {
            try
            {
                if (await UserSessionService.GetCurrentUserAsync() is { } currentUser)
                {
                    string role = string.IsNullOrWhiteSpace(currentUser.Role_name)
                        ? "Staff"
                        : currentUser.Role_name.Trim();
                    DashboardUserNameLabel.Text = $"{role} {currentUser.First_name}".Trim();
                }

                _viewModel.TotalTasks = Convert.ToInt32(
                    await _db.ExecuteScalarAsync("SELECT COUNT(DISTINCT Task_id) FROM TaskAssignment"));

                const string completedQuery = "SELECT COUNT(DISTINCT Task_id) FROM TaskAssignment WHERE Completion_status = @Completion_status";
                _viewModel.CompletedTasks = Convert.ToInt32(await _db.ExecuteScalarAsync(
                    completedQuery,
                    [new SqlParameter("@Completion_status", SqlDbType.NVarChar, 20) { Value = "Completed" }]));

                const string pendingQuery = "SELECT COUNT(DISTINCT Task_id) FROM TaskAssignment WHERE Completion_status <> @Completion_status OR Completion_status IS NULL";
                _viewModel.PendingTasks = Convert.ToInt32(await _db.ExecuteScalarAsync(
                    pendingQuery,
                    [new SqlParameter("@Completion_status", SqlDbType.NVarChar, 20) { Value = "Completed" }]));

                await LoadTasks("All");
                return true;
            }
            catch (Exception ex)
            {
                await UiAlertService.ShowAsync(this, "Dashboard couldn't load", "We couldn't load the dashboard. Please try again.", "OK");
                return false;
            }
        }

        private async Task LoadTasks(string filter, bool showErrors = true)
        {
            if (_isLoadingTasks)
                return;

            _isLoadingTasks = true;
            try
            {
                _currentFilter = filter;
                var dt = await _db.GetAllTasksWithTeachersAsync();
                HashSet<int> previousDiscussionTasks = await _db.GetTasksWithPreviousDiscussionsAsync();
                _loadedTasks.Clear();

                int[] taskIDs = dt.AsEnumerable().Select(row => row.Field<int>("Task_id")).Distinct().ToArray();
                Dictionary<int, System.Threading.Tasks.Task<List<EDUTASK_1._1.Models.SubtaskDisplayItem>>> subtaskLoads =
                    taskIDs.ToDictionary(taskID => taskID, taskID => _db.GetTaskSubtasksAsync(taskID));
                foreach (IGrouping<int, DataRow> taskGroup in dt.AsEnumerable().GroupBy(row => row.Field<int>("Task_id")))
                {
                    DataRow[] rows = taskGroup.ToArray();
                    bool allAssignmentsAwaitingValidation = rows.Length > 0 && rows.All(row =>
                        string.Equals(row["Completion_status"]?.ToString(), "For Validation", StringComparison.Ordinal));
                    bool allAssignmentsCompleted = rows.Length > 0 && rows.All(row =>
                        string.Equals(row["Completion_status"]?.ToString(), "Completed", StringComparison.Ordinal));
                    string completion = rows
                        .Select(row => row["Completion_status"]?.ToString() ?? "Pending")
                        .OrderByDescending(status => status switch
                        {
                            "Completed" => 4,
                            "Needs Revision" => 3,
                            "For Validation" => 2,
                            _ => 1
                        })
                        .First();
                    DataRow displayRow = rows.FirstOrDefault(row =>
                        string.Equals(row["Completion_status"]?.ToString() ?? "Pending", completion, StringComparison.Ordinal)) ?? rows[0];
                    bool isAcknowledged = rows.Any(row => !row.IsNull("Is_acknowledged") && Convert.ToBoolean(row["Is_acknowledged"]));
                    DateTime? deadline = displayRow.IsNull("Deadline")
                        ? null
                        : Convert.ToDateTime(displayRow["Deadline"]);
                    string[] teacherNames = rows
                        .Select(row => row["TeacherName"]?.ToString())
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .Select(name => name!)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    string teacherSummary = string.Join(", ", teacherNames);
                    if (string.IsNullOrWhiteSpace(teacherSummary))
                        teacherSummary = "Unassigned";

                    int taskID = taskGroup.Key;
                    List<EDUTASK_1._1.Models.SubtaskDisplayItem> subtasks;
                    try
                    {
                        subtasks = await subtaskLoads[taskID];
                        bool reviewIsAvailable = UserSessionService.CanReviewSubtaskProof &&
                                                 completion != "Completed";
                        Dictionary<int, int> unreadCounts;
                        try
                        {
                            unreadCounts = await _db.GetUnreadTaskDiscussionCountsAsync(
                                subtasks.Select(subtask => subtask.Subtask_id), "User", UserSessionService.CurrentUserId);
                        }
                        catch (Exception discussionException)
                        {
                            System.Diagnostics.Debug.WriteLine($"Unread discussion counts failed for task {taskID}: {discussionException}");
                            unreadCounts = [];
                        }
                        foreach (var subtask in subtasks)
                        {
                            subtask.ReviewIsAvailable = reviewIsAvailable;
                            subtask.UnreadDiscussionCount = unreadCounts.GetValueOrDefault(subtask.Subtask_id);
                        }
                    }
                    catch (Exception subtaskException)
                    {
                        System.Diagnostics.Debug.WriteLine($"Subtasks failed to load for task {taskID}: {subtaskException}");
                        subtasks = [];
                    }
                    int submittedProgressItems = subtasks.Count(subtask => subtask.IsProofPending || subtask.IsProofApproved);
                    int verifiedProgressItems = subtasks.Count(subtask => subtask.IsProofApproved);
                    bool needsRevision = completion == "Needs Revision" || subtasks.Any(subtask => subtask.IsProofReturned);
                    string status = completion == "Completed"
                        ? "Completed"
                        : needsRevision
                            ? "Needs Revision"
                            : completion == "For Validation"
                                ? "For Validation"
                                : isAcknowledged ? "Acknowledged" : "Pending";

                    _loadedTasks.Add(new DashboardTaskItem
                    {
                        Task_id = taskID,
                        Assignment_id = displayRow.IsNull("Assignment_id") ? 0 : Convert.ToInt32(displayRow["Assignment_id"]),
                        Createdby_user_id = Convert.ToInt32(displayRow["Createdby_user_id"]),
                        Created_at = displayRow.Field<DateTime>("Created_at"),
                        Title = displayRow["Title"].ToString(),
                        Description = string.IsNullOrWhiteSpace(displayRow["Description"]?.ToString())
                            ? "No description provided."
                            : displayRow["Description"].ToString()!,
                        TeacherName = teacherSummary,
                        TeacherNames = teacherNames,
                        DeadlineDisplay = deadline?.ToString("MMM dd, yyyy") ?? "No deadline",
                        Priority = displayRow["Priority"]?.ToString() ?? "Unassigned",
                        PriorityColor = TaskPalette.PriorityColor(displayRow["Priority"]?.ToString() ?? string.Empty),
                        Status = status,
                        StatusColor = TaskPalette.StatusColor(status),
                        Deadline = deadline,
                        Completed_at = rows.Where(row => !row.IsNull("Completed_at"))
                            .Select(row => row.Field<DateTime>("Completed_at"))
                            .DefaultIfEmpty()
                            .Max(),
                        Is_completed = allAssignmentsCompleted,
                        IsAwaitingValidation = allAssignmentsAwaitingValidation,
                        Subtasks = subtasks,
                        HasPreviousDiscussion = previousDiscussionTasks.Contains(taskID),
                        SubmittedProgressItems = submittedProgressItems,
                        VerifiedProgressItems = verifiedProgressItems,
                        TotalProgressItems = subtasks.Count,
                        IsExpanded = _expandedTaskID == taskID
                    });
                }
                ApplyTaskFilters();
            }
            catch (Exception ex)
            {
                if (showErrors)
                    await UiAlertService.ShowAsync(this, "Tasks couldn't load", "We couldn't load the tasks. Please try again.", "OK");
            }
            finally
            {
                _isLoadingTasks = false;
            }
        }

        private void ApplyTaskFilters()
        {
            UpdateFilterChipStyles();
            string searchText = TaskSearchBar?.Text?.Trim() ?? string.Empty;

            IEnumerable<DashboardTaskItem> tasks = _loadedTasks.Where(task => _currentFilter switch
            {
                "Today" => task.Deadline is not null && task.Deadline.Value.Date == DateTime.Today,
                "Completed" => task.Is_completed,
                "Overdue" => !task.Is_completed && task.Deadline is not null && task.Deadline.Value.Date < DateTime.Today,
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

            var visibleTasks = new ObservableCollection<DashboardTaskItem>(tasks);
            if (_expandedTaskID is int expandedTaskID &&
                visibleTasks.All(task => task.Task_id != expandedTaskID))
            {
                SetExpandedTask(null);
            }

            _viewModel.Tasks = visibleTasks;
            PopulateTodayAndCompletedSections(visibleTasks);
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

        private async void OnCreateTaskClicked(object sender, EventArgs e)
        {
            await Navigation.PushModalAsync(new CreateTaskPage(), false);
        }

        private async void OnViewProofClicked(object sender, EventArgs e)
        {
            if (sender is not ImageButton { CommandParameter: EDUTASK_1._1.Models.SubtaskDisplayItem subtask })
                return;
            try
            {
                List<PreparedProofImage> files = await _db.GetSubtaskProofFilesAsync(subtask.Subtask_id);
                if (files.Count == 0)
                {
                    await UiAlertService.ShowAsync(this, "Files unavailable", "We couldn't find the submitted files. They may have been replaced or removed.");
                    return;
                }
                await ProofFileViewerService.OpenManyAsync(this, files, $"proof-{subtask.Subtask_id}");
            }
            catch
            {
                await UiAlertService.ShowAsync(this, "Files couldn't open", "We couldn't open these files. Please try again.");
            }
        }

        private async void OnProofHistoryClicked(object sender, TappedEventArgs e)
        {
            if (e.Parameter is not EDUTASK_1._1.Models.SubtaskDisplayItem subtask || !subtask.HasProofHistory)
                return;
            await Navigation.PushModalAsync(new SubtaskProofHistoryPage(
                subtask,
                subtask.CanReviewProof,
                LoadDashboardDataAsync));
        }

        private async void OnProofHistoryButtonClicked(object sender, EventArgs e)
        {
            if (sender is not ImageButton
                {
                    CommandParameter: EDUTASK_1._1.Models.SubtaskDisplayItem subtask
                } || !subtask.HasProofHistory)
                return;
            await Navigation.PushModalAsync(new SubtaskProofHistoryPage(
                subtask,
                subtask.CanReviewProof,
                LoadDashboardDataAsync));
        }

        private async void OnApproveProofClicked(object sender, EventArgs e)
        {
            if (sender is not ImageButton { CommandParameter: EDUTASK_1._1.Models.SubtaskDisplayItem subtask })
                return;
            if (!await UiAlertService.ConfirmAsync(this, "Approve file", "Approve this file and mark the subtask as complete?", "Approve", "Cancel"))
                return;
            await ReviewProofAsync(subtask, true, null);
        }

        private async void OnReturnProofClicked(object sender, EventArgs e)
        {
            if (sender is not ImageButton { CommandParameter: EDUTASK_1._1.Models.SubtaskDisplayItem subtask })
                return;
            string? remarks = await UiAlertService.PromptAsync(this, "Request changes", "Tell the teacher what needs to be updated.", "Request changes", "Cancel", maxLength: 500);
            if (remarks is null)
                return;
            if (string.IsNullOrWhiteSpace(remarks))
            {
                await UiAlertService.ShowAsync(this, "Add a note", "Please tell the teacher what needs to be changed.");
                return;
            }
            await ReviewProofAsync(subtask, false, remarks);
        }

        private async Task ReviewProofAsync(EDUTASK_1._1.Models.SubtaskDisplayItem subtask, bool approve, string? remarks)
        {
            try
            {
                bool reviewed = await _db.ReviewSubtaskProofAsync(subtask.Subtask_id, approve, UserSessionService.CurrentUserId, remarks);
                if (!reviewed)
                {
                    await UiAlertService.ShowAsync(this, "File already updated", "This file was already reviewed or replaced. Refresh the task to see the latest version.");
                    return;
                }
                if (!approve && !string.IsNullOrWhiteSpace(remarks))
                {
                    await _db.AddTaskDiscussionAsync(
                        subtask.Task_id,
                        subtask.Subtask_id,
                        "User",
                        UserSessionService.CurrentUserId,
                        remarks.Trim(),
                        "ProofReturn");
                }
                await UiAlertService.ShowAsync(this, approve ? "File approved" : "Changes requested",
                    approve ? "This subtask is now complete." : "Your note was sent to the teacher. They can update and resubmit the file.");
                await LoadDashboardDataAsync();
            }
            catch (Exception ex)
            {
                await UiAlertService.ShowAsync(this, "Review couldn't be saved", ex is ArgumentException ? ex.Message : "We couldn't save this review. Please try again.");
            }
        }
        private async void OnValidateClicked(object sender, EventArgs e)
        {
            if (sender is not Button { CommandParameter: int id }) return;
            if (!await UiAlertService.ConfirmAsync(
                    this,
                    "Complete this task?",
                    "Everything looks good? This will mark the task as completed.",
                    "Mark Complete",
                    "Not Yet"))
                return;

            try
            {
                await _db.ValidateTaskAsync(id, UserSessionService.CurrentUserId);
                await LoadDashboardDataAsync();
            }
            catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number is 51111 or 51112)
            {
                await UiAlertService.ShowAsync(
                    this,
                    "Task isn't ready",
                    ex.Number == 51111
                        ? "Every assigned teacher must submit their work for validation before final approval."
                        : "Every subtask must have approved proof before final approval.");
                await LoadDashboardDataAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Final task approval failed: {ex}");
                await UiAlertService.ShowAsync(this, "Task couldn't be completed", "We couldn't complete this task. Please refresh and try again.");
            }
        }
        private async void OnMarkIncompleteClicked(object sender, EventArgs e)
        {
            if (sender is not Button { CommandParameter: int id } button)
                return;
            if (!await UiAlertService.ConfirmAsync(
                    this,
                    "Mark incomplete",
                    "Return this task to the teacher?",
                    "Return",
                    "Cancel"))
                return;

            button.IsEnabled = false;
            try
            {
                if (await _db.RejectTaskCompletionAsync(id, UserSessionService.CurrentUserId))
                    await LoadDashboardDataAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Returning task failed: {ex}");
                await UiAlertService.ShowAsync(
                    this,
                    "Task couldn't be returned",
                    "We couldn't return this task. Please refresh and try again.");
            }
            finally
            {
                button.IsEnabled = true;
            }
        }
        private async void OnTaskSelected(object sender, SelectedItemChangedEventArgs e)
        {
            if (e.SelectedItem is not DashboardTaskItem task)
                return;

            await Navigation.PushModalAsync(new EditTaskPage(task.Task_id, !task.CanEdit), false);
        }

        private ObservableCollection<DeadlineTaskGroup> BuildDeadlineGroups(
            IEnumerable<DashboardTaskItem> tasks,
            bool organizeByUrgency = false)
        {
            List<DashboardTaskItem> orderedTasks = organizeByUrgency
                ? tasks
                    .OrderBy(UrgencyRank)
                    .ThenBy(task => task.Deadline?.Date ?? DateTime.MaxValue)
                    .ThenBy(task => TaskPalette.PriorityRank(task.Priority))
                    .ThenBy(task => task.Title)
                    .ToList()
                : tasks
                    .OrderBy(task => TaskPalette.PriorityRank(task.Priority))
                    .ThenBy(task => task.Deadline?.Date ?? DateTime.MaxValue)
                    .ThenBy(task => task.Title)
                    .ToList();

            List<List<DashboardTaskItem>> taskCards = orderedTasks
                .GroupBy(TaskCardKey)
                .Select(group => group.ToList())
                .ToList();

            Dictionary<string, int> sectionCounts = taskCards
                .GroupBy(card => UrgencySection(card.OrderBy(UrgencyRank).First()))
                .ToDictionary(group => group.Key, group => group.Count());
            string? previousSection = null;
            var groups = new List<DeadlineTaskGroup>(taskCards.Count);

            foreach (List<DashboardTaskItem> cardTasks in taskCards)
            {
                DashboardTaskItem task = cardTasks.OrderBy(UrgencyRank).First();
                string section = UrgencySection(task);
                bool showSectionHeader = organizeByUrgency && section != previousSection;
                previousSection = section;
                int sectionCount = sectionCounts[section];

                groups.Add(new DeadlineTaskGroup
                {
                    Deadline = task.Deadline?.Date,
                    DeadlineDisplay = task.Deadline.HasValue
                        ? DeadlineTaskGroup.FormatHeader(task.Deadline.Value)
                        : "No deadline",
                    TaskTitle = task.Title,
                    TeacherSummary = FormatTeacherSummary(cardTasks),
                    ShowSectionHeader = showSectionHeader,
                    SectionTitle = section,
                    SectionCountText = $"{sectionCount} {(sectionCount == 1 ? "task" : "tasks")}",
                    SectionColor = UrgencyColor(section),
                    Tasks = cardTasks,
                    IsExpanded = _expandedTaskID.HasValue && cardTasks.Any(item => item.Task_id == _expandedTaskID.Value),
                    PriorityColor = task.PriorityColor
                });
            }

            return new ObservableCollection<DeadlineTaskGroup>(groups);
        }

        private static int UrgencyRank(DashboardTaskItem task) => UrgencySection(task) switch
        {
            "Overdue" => 0,
            "Due today" => 1,
            "Upcoming" => 2,
            _ => 3
        };

        private static string UrgencySection(DashboardTaskItem task)
        {
            if (task.Deadline is not DateTime deadline)
                return "No deadline";
            if (deadline.Date < DateTime.Today)
                return "Overdue";
            return deadline.Date == DateTime.Today ? "Due today" : "Upcoming";
        }

        private static Color UrgencyColor(string section) => section switch
        {
            "Overdue" => AppColors.StatusDanger,
            "Due today" => AppColors.Accent500,
            "Upcoming" => AppColors.StatusSuccess,
            _ => AppColors.TextSecondary
        };

        private static string FormatTeacherSummary(IEnumerable<DashboardTaskItem> tasks)
        {
            string[] teachers = tasks
                .SelectMany(task => task.TeacherNames.Count > 0
                    ? task.TeacherNames
                    : [task.TeacherName])
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return teachers.Length switch
            {
                0 => "Unassigned",
                1 => teachers[0],
                2 => string.Join(", ", teachers),
                _ => $"{string.Join(", ", teachers.Take(2))} +{teachers.Length - 2} more"
            };
        }

        private static string TaskCardKey(DashboardTaskItem task)
        {
            static string Normalize(string? value) => value?.Trim().ToUpperInvariant() ?? string.Empty;

            string subtaskTitles = string.Join("\u001e", task.Subtasks.Select(subtask => Normalize(subtask.Title)));
            return string.Join("\u001f",
                task.Createdby_user_id,
                task.Created_at.Date.ToString("yyyyMMdd"),
                Normalize(task.Title),
                Normalize(task.Description),
                task.Deadline?.Date.ToString("yyyyMMdd") ?? string.Empty,
                Normalize(task.Priority),
                subtaskTitles);
        }

        private void OnDeadlineGroupTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is not DeadlineTaskGroup group || group.Tasks.FirstOrDefault() is not { } task)
                return;

            SetExpandedTask(group.IsExpanded ? null : task.Task_id);
        }

        public void ExpandTask(int taskID)
        {
            SetExpandedTask(taskID);
            DashboardTaskItem? task = _loadedTasks.FirstOrDefault(item => item.Task_id == taskID);
            if (task is null)
                return;

            ApplyTaskFilters();
        }

        private void SetExpandedTask(int? taskID)
        {
            _expandedTaskID = taskID;
            foreach (DashboardTaskItem task in _loadedTasks)
                task.IsExpanded = taskID.HasValue && task.Task_id == taskID.Value;

            IEnumerable<DeadlineTaskGroup> visibleGroups = TodayTasksView.Children
                .Concat(CompletedTodayTasksView.Children)
                .OfType<Element>()
                .Select(child => child.BindingContext)
                .OfType<DeadlineTaskGroup>();
            foreach (DeadlineTaskGroup visibleGroup in visibleGroups)
            {
                visibleGroup.IsExpanded = taskID.HasValue &&
                    visibleGroup.Tasks.Any(task => task.Task_id == taskID.Value);
            }
        }

        public async Task FocusTaskAsync(int taskID)
        {
            _currentFilter = "All";
            _deadlineFilter = DeadlineFilterSelection.AnyDate;
            if (TaskSearchBar is not null)
                TaskSearchBar.Text = string.Empty;
            ExpandTask(taskID);
            for (int attempt = 0; attempt < 40; attempt++)
            {
                await Task.Delay(50);
                Element? target = TodayTasksView.Children.OfType<Element>().FirstOrDefault(child =>
                    child.BindingContext is DeadlineTaskGroup group &&
                    group.Tasks.Any(task => task.Task_id == taskID));
                if (target is null)
                    target = CompletedTodayTasksView.Children.OfType<Element>().FirstOrDefault(child =>
                        child.BindingContext is DeadlineTaskGroup group &&
                        group.Tasks.Any(task => task.Task_id == taskID));
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
            UpdateTodayTasksVisibility();
        }

        private void OnCompletedTodayToggleClicked(object sender, EventArgs e)
        {
            _isCompletedTodayExpanded = !_isCompletedTodayExpanded;
            CompletedTodayToggleArrow.Source = _isCompletedTodayExpanded ? "collapse.png" : "uncollapse.png";
            UpdateCompletedTodayTasksVisibility();
        }

        private void UpdateTodayTasksVisibility()
        {
            TodayTasksView.IsVisible = _isTodayExpanded;
        }

        private void UpdateCompletedTodayTasksVisibility()
        {
            CompletedTodayTasksView.IsVisible = _isCompletedTodayExpanded;
        }

        private void OnTodayTaskSelected(object sender, EventArgs e)
        {
            if (sender is Grid grid && grid.BindingContext is DashboardTaskItem task)
            {
                OnTaskItemSelected(task);
            }
        }

        private void OnCompletedTodayTaskSelected(object sender, EventArgs e)
        {
            if (sender is Grid grid && grid.BindingContext is DashboardTaskItem task)
            {
                OnTaskItemSelected(task);
            }
        }

        private async void OnSubtaskDiscussionClicked(object sender, EventArgs e)
        {
            if (sender is not ImageButton { CommandParameter: EDUTASK_1._1.Models.SubtaskDisplayItem subtask })
                return;

            DashboardTaskItem? task = _loadedTasks.FirstOrDefault(item => item.Task_id == subtask.Task_id);

            var discussionPage = new TaskDiscussionPage(
                subtask.Task_id,
                subtask.Subtask_id,
                UserSessionService.CurrentUserId,
                null,
                task?.Is_completed ?? false);
            discussionPage.Disappearing += (_, _) =>
            {
                subtask.UnreadDiscussionCount = 0;
                ApplyTaskFilters();
            };
            await Navigation.PushModalAsync(discussionPage, false);
        }

        private async void OnPreviousDiscussionClicked(object sender, EventArgs e)
        {
            if (sender is not Button { CommandParameter: DashboardTaskItem task })
                return;
            var user = await UserSessionService.GetCurrentUserAsync();
            if (user is null)
                return;
            await Navigation.PushModalAsync(new TaskDiscussionPage(
                task.Task_id, null, user.User_id, null, isReadOnly: true), false);
        }

        private async void OnAdminEditTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is not DashboardTaskItem { CanEdit: true } task)
                return;

            await Navigation.PushModalAsync(new EditTaskPage(task.Task_id), false);
        }

        private async void OnTaskItemSelected(DashboardTaskItem task)
        {
            await Navigation.PushModalAsync(new EditTaskPage(task.Task_id, !task.CanEdit), false);
        }

        private void PopulateTodayAndCompletedSections(IEnumerable<DashboardTaskItem> visibleTasks)
        {
            bool dateFilterActive = _deadlineFilter.Kind != DeadlineFilterKind.AnyDate;
            var todayTasks = visibleTasks.Where(task => !task.Is_completed).ToList();
            var completedTodayTasks = visibleTasks
                .Where(task => task.Is_completed && (dateFilterActive || task.Completed_at?.Date == DateTime.Today))
                .ToList();

            bool organizeByUrgency = !dateFilterActive && _currentFilter == "All";
            ObservableCollection<DeadlineTaskGroup> activeTaskCards = BuildDeadlineGroups(todayTasks, organizeByUrgency);
            ObservableCollection<DeadlineTaskGroup> completedTaskCards = BuildDeadlineGroups(completedTodayTasks);

            TodayHeaderLabel.Text = $"Active Tasks ({activeTaskCards.Count})";
            CompletedTodayHeaderLabel.Text = dateFilterActive
                ? $"Completed Tasks ({completedTaskCards.Count})"
                : $"Completed Today ({completedTaskCards.Count})";

            BindableLayout.SetItemsSource(TodayTasksView, activeTaskCards);
            BindableLayout.SetItemsSource(CompletedTodayTasksView, completedTaskCards);

            bool hasTodayTasks = todayTasks.Count > 0;
            bool hasCompletedTasks = completedTodayTasks.Count > 0;
            bool hasAnyTasks = hasTodayTasks || hasCompletedTasks;
            TodaySection.IsVisible = hasTodayTasks;
            CompletedTodaySection.IsVisible = hasCompletedTasks;
            CompletedTodaySectionHeader.IsVisible = hasCompletedTasks;
            CompletedTodayTasksView.IsVisible = hasCompletedTasks && _isCompletedTodayExpanded;
            CompletionHistoryLink.IsVisible = hasCompletedTasks;
            NoTodayTasksLabel.IsVisible = !hasAnyTasks;
            int overdueCount = _loadedTasks
                .Where(task => !task.Is_completed && task.Deadline?.Date < DateTime.Today)
                .DistinctBy(TaskCardKey)
                .Count();
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
                NoTasksMessageLabel.Text = "Create a task to get started.";
            }
            ViewOverdueEmptyLink.IsVisible = _currentFilter == "Today" && overdueCount > 0 && !hasSearch
                && _deadlineFilter.Kind == DeadlineFilterKind.AnyDate;
        }
        private async void OnEmptyViewOverdueClicked(object sender, EventArgs e) => await SelectStatusFilterAsync("Overdue");
        private async void OnCompletionHistoryClicked(object sender, EventArgs e)
        {
            var history = CreateCompletionHistoryPage();

            if (DashboardFlyoutPage.Current is { } flyout)
            {
                flyout.ShowDetail(history);
                return;
            }

            await Navigation.PushModalAsync(history, false);
        }

        internal CompletionHistoryPage CreateCompletionHistoryPage()
        {
            var history = _loadedTasks
                .Where(task => task.Is_completed && task.Completed_at?.Date < DateTime.Today)
                .OrderByDescending(task => task.Completed_at)
                .ToList();
            return new CompletionHistoryPage(history, showTeacherFilter: true);
        }

        internal async Task<CompletionHistoryPage> CreateCompletionHistoryPageAsync()
        {
            if (_loadedTasks.Count == 0)
                await LoadTasks("All", showErrors: false);

            return CreateCompletionHistoryPage();
        }

    }
}
