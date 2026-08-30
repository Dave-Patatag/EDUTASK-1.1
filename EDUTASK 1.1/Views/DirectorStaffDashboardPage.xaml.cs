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
        private readonly HashSet<int> _expandedTaskGroups = [];

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
            await LoadDashboardDataAsync();
            StartProgressMonitoring();
        }

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

                    var subtasks = await _db.GetTaskSubtasksAsync(task.TaskID);
                    object? completionValue = await _db.ExecuteScalarAsync(
                        "SELECT CompletionStatus FROM TaskAssignment WHERE AssignmentID = @AssignmentID",
                        [new SqlParameter("@AssignmentID", SqlDbType.Int) { Value = task.AssignmentID }]);
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

        private async Task LoadDashboardDataAsync()
        {
            try
            {
                if (await UserSessionService.GetCurrentUserAsync() is { } currentUser)
                {
                    string role = string.IsNullOrWhiteSpace(currentUser.RoleName)
                        ? "Staff"
                        : currentUser.RoleName.Trim();
                    DashboardUserNameLabel.Text = $"{role} {currentUser.FirstName}".Trim();
                }

                _viewModel.TotalTasks = Convert.ToInt32(
                    await _db.ExecuteScalarAsync("SELECT COUNT(DISTINCT TaskID) FROM TaskAssignment"));

                const string completedQuery = "SELECT COUNT(DISTINCT TaskID) FROM TaskAssignment WHERE CompletionStatus = @CompletionStatus";
                _viewModel.CompletedTasks = Convert.ToInt32(await _db.ExecuteScalarAsync(
                    completedQuery,
                    [new SqlParameter("@CompletionStatus", SqlDbType.NVarChar, 20) { Value = "Completed" }]));

                const string pendingQuery = "SELECT COUNT(DISTINCT TaskID) FROM TaskAssignment WHERE CompletionStatus <> @CompletionStatus OR CompletionStatus IS NULL";
                _viewModel.PendingTasks = Convert.ToInt32(await _db.ExecuteScalarAsync(
                    pendingQuery,
                    [new SqlParameter("@CompletionStatus", SqlDbType.NVarChar, 20) { Value = "Completed" }]));

                await LoadTasks("All");
            }
            catch (Exception ex)
            {
                await UiAlertService.ShowAsync(this, "Dashboard couldn't load", "We couldn't load the dashboard. Please try again.", "OK");
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
                _loadedTasks.Clear();

                int[] taskIDs = dt.AsEnumerable().Select(row => row.Field<int>("TaskID")).Distinct().ToArray();
                Dictionary<int, System.Threading.Tasks.Task<List<EDUTASK_1._1.Models.SubtaskDisplayItem>>> subtaskLoads =
                    taskIDs.ToDictionary(taskID => taskID, taskID => _db.GetTaskSubtasksAsync(taskID));
                foreach (IGrouping<int, DataRow> taskGroup in dt.AsEnumerable().GroupBy(row => row.Field<int>("TaskID")))
                {
                    DataRow[] rows = taskGroup.ToArray();
                    bool allAssignmentsAwaitingValidation = rows.Length > 0 && rows.All(row =>
                        string.Equals(row["CompletionStatus"]?.ToString(), "For Validation", StringComparison.Ordinal));
                    bool allAssignmentsCompleted = rows.Length > 0 && rows.All(row =>
                        string.Equals(row["CompletionStatus"]?.ToString(), "Completed", StringComparison.Ordinal));
                    string completion = rows
                        .Select(row => row["CompletionStatus"]?.ToString() ?? "Pending")
                        .OrderByDescending(status => status switch
                        {
                            "Completed" => 4,
                            "Needs Revision" => 3,
                            "For Validation" => 2,
                            _ => 1
                        })
                        .First();
                    DataRow displayRow = rows.FirstOrDefault(row =>
                        string.Equals(row["CompletionStatus"]?.ToString() ?? "Pending", completion, StringComparison.Ordinal)) ?? rows[0];
                    bool isAcknowledged = rows.Any(row => !row.IsNull("IsAcknowledged") && Convert.ToBoolean(row["IsAcknowledged"]));
                    DateTime? deadline = displayRow.IsNull("Deadline")
                        ? null
                        : Convert.ToDateTime(displayRow["Deadline"]);
                    string teacherSummary = string.Join(", ", rows
                        .Select(row => row["TeacherName"]?.ToString())
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .Distinct(StringComparer.OrdinalIgnoreCase));
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
                            unreadCounts = await _db.GetUnreadTaskCommentCountsAsync(
                                subtasks.Select(subtask => subtask.SubtaskID), "User", UserSessionService.CurrentUserId);
                        }
                        catch (Exception discussionException)
                        {
                            System.Diagnostics.Debug.WriteLine($"Unread discussion counts failed for task {taskID}: {discussionException}");
                            unreadCounts = [];
                        }
                        foreach (var subtask in subtasks)
                        {
                            subtask.ReviewIsAvailable = reviewIsAvailable;
                            subtask.UnreadDiscussionCount = unreadCounts.GetValueOrDefault(subtask.SubtaskID);
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
                        TaskID = taskID,
                        AssignmentID = displayRow.IsNull("AssignmentID") ? 0 : Convert.ToInt32(displayRow["AssignmentID"]),
                        CreatedByUserID = displayRow.IsNull("CreatedByUserID")
                            ? Convert.ToInt32(displayRow["UserID"])
                            : Convert.ToInt32(displayRow["CreatedByUserID"]),
                        Title = displayRow["Title"].ToString(),
                        Description = string.IsNullOrWhiteSpace(displayRow["Description"]?.ToString())
                            ? "No description provided."
                            : displayRow["Description"].ToString()!,
                        TeacherName = teacherSummary,
                        DeadlineDisplay = deadline?.ToString("MMM dd, yyyy") ?? "No deadline",
                        Priority = displayRow["Priority"]?.ToString() ?? "Unassigned",
                        PriorityColor = TaskPalette.PriorityColor(displayRow["Priority"]?.ToString() ?? string.Empty),
                        Status = status,
                        StatusColor = TaskPalette.StatusColor(status),
                        Deadline = deadline,
                        CompletedAt = rows.Where(row => !row.IsNull("CompletedAt"))
                            .Select(row => row.Field<DateTime>("CompletedAt"))
                            .DefaultIfEmpty()
                            .Max(),
                        IsCompleted = allAssignmentsCompleted,
                        IsAwaitingValidation = allAssignmentsAwaitingValidation,
                        Subtasks = subtasks,
                        SubmittedProgressItems = submittedProgressItems,
                        VerifiedProgressItems = verifiedProgressItems,
                        TotalProgressItems = subtasks.Count
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

            var visibleTasks = new ObservableCollection<DashboardTaskItem>(tasks);
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
                var proof = await _db.GetSubtaskProofImageAsync(subtask.SubtaskID);
                if (proof is null)
                {
                    await UiAlertService.ShowAsync(this, "File unavailable", "We couldn't find the submitted file. It may have been replaced or removed.");
                    return;
                }

                byte[] bytes = proof.Value.Data;
                if (string.Equals(proof.Value.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
                {
                    string safeFileName = Path.GetFileName(proof.Value.FileName);
                    if (string.IsNullOrWhiteSpace(safeFileName) || !safeFileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                        safeFileName = $"proof-{subtask.SubtaskID}.pdf";
                    string localPath = Path.Combine(FileSystem.CacheDirectory, safeFileName);
                    await File.WriteAllBytesAsync(localPath, bytes);
                    await Launcher.Default.OpenAsync(new OpenFileRequest
                    {
                        Title = safeFileName,
                        File = new ReadOnlyFile(localPath, "application/pdf")
                    });
                    return;
                }

                var image = new Image
                {
                    Source = ImageSource.FromStream(() => new MemoryStream(bytes)),
                    Aspect = Aspect.AspectFit,
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill
                };
                var closeButton = new Button { Text = "Close", HorizontalOptions = LayoutOptions.Center };
                var previewPage = new ContentPage
                {
                    Title = proof.Value.FileName,
                    BackgroundColor = AppColors.TextPrimary,
                    Content = new Grid
                    {
                        RowDefinitions = { new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) },
                        Padding = new Thickness(12),
                        Children = { image, closeButton }
                    }
                };
                Grid.SetRow(closeButton, 1);
                closeButton.Clicked += async (_, _) => await Navigation.PopModalAsync(false);
                await Navigation.PushModalAsync(previewPage, false);
            }
            catch
            {
                await UiAlertService.ShowAsync(this, "File couldn't open", "We couldn't open this file. Please try again.");
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
                bool reviewed = await _db.ReviewSubtaskProofAsync(subtask.SubtaskID, approve, UserSessionService.CurrentUserId, remarks);
                if (!reviewed)
                {
                    await UiAlertService.ShowAsync(this, "File already updated", "This file was already reviewed or replaced. Refresh the task to see the latest version.");
                    return;
                }
                if (!approve && !string.IsNullOrWhiteSpace(remarks))
                {
                    var currentUser = await UserSessionService.GetCurrentUserAsync();
                    string authorName = currentUser is null
                        ? "Admin"
                        : $"{currentUser.FirstName} {currentUser.LastName}".Trim();
                    await _db.AddTaskCommentAsync(
                        subtask.TaskID,
                        subtask.SubtaskID,
                        "User",
                        UserSessionService.CurrentUserId,
                        authorName,
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
            if (sender is not Button { CommandParameter: int id }) return;
            if (await UiAlertService.ConfirmAsync(this, "Mark incomplete", "Return this task to the teacher?", "Return", "Cancel") && await _db.RejectTaskCompletionAsync(id, UserSessionService.CurrentUserId, "Please revise the submitted work.")) await LoadDashboardDataAsync();
        }
        private void OnMenuBarClicked(object sender, EventArgs e)
        {
            if (DashboardFlyoutPage.Current is { } flyout)
                flyout.IsPresented = true;
        }

        private async void OnTaskSelected(object sender, SelectedItemChangedEventArgs e)
        {
            if (e.SelectedItem is not DashboardTaskItem task)
                return;

            await Navigation.PushModalAsync(new EditTaskPage(task.TaskID, !task.CanEdit), false);
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
                    DeadlineDisplay = task.Deadline.HasValue
                        ? DeadlineTaskGroup.FormatHeader(task.Deadline.Value)
                        : "No deadline",
                    TeacherSummary = task.TeacherName,
                    Tasks = [task],
                        IsExpanded = _expandedTaskGroups.Contains(task.TaskID),
                    PriorityColor = task.PriorityColor
                }));
        }

        private static string FormatTeacherSummary(IEnumerable<DashboardTaskItem> tasks)
        {
            string[] teachers = tasks
                .Select(task => task.TeacherName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return teachers.Length switch
            {
                0 => "Unassigned",
                1 => teachers[0],
                _ => $"{teachers.Length} teachers"
            };
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
                Element? target = TodayTasksView.Children.OfType<Element>().FirstOrDefault(child =>
                    child.BindingContext is DeadlineTaskGroup group &&
                    group.Tasks.Any(task => task.TaskID == taskID));
                if (target is null)
                    target = CompletedTodayTasksView.Children.OfType<Element>().FirstOrDefault(child =>
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

            var user = await UserSessionService.GetCurrentUserAsync();
            string authorName = user is null
                ? "Admin"
                : $"{user.FirstName} {user.LastName}".Trim();
            DashboardTaskItem? task = _loadedTasks.FirstOrDefault(item => item.TaskID == subtask.TaskID);

            var discussionPage = new TaskDiscussionPage(
                subtask.TaskID,
                subtask.SubtaskID,
                "User",
                UserSessionService.CurrentUserId,
                authorName,
                task?.IsCompleted ?? false);
            discussionPage.Disappearing += (_, _) =>
            {
                subtask.UnreadDiscussionCount = 0;
                ApplyTaskFilters();
            };
            await Navigation.PushModalAsync(discussionPage, false);
        }

        private async void OnAdminEditTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is not DashboardTaskItem { CanEdit: true } task)
                return;

            await Navigation.PushModalAsync(new EditTaskPage(task.TaskID), false);
        }

        private async void OnTaskItemSelected(DashboardTaskItem task)
        {
            await Navigation.PushModalAsync(new EditTaskPage(task.TaskID, !task.CanEdit), false);
        }

        private void PopulateTodayAndCompletedSections(IEnumerable<DashboardTaskItem> visibleTasks)
        {
            bool dateFilterActive = _deadlineFilter.Kind != DeadlineFilterKind.AnyDate;
            var todayTasks = visibleTasks.Where(task => !task.IsCompleted).ToList();
            var completedTodayTasks = visibleTasks
                .Where(task => task.IsCompleted && (dateFilterActive || task.CompletedAt?.Date == DateTime.Today))
                .ToList();

            TodayHeaderLabel.Text = $"Active Tasks ({todayTasks.Count})";
            CompletedTodayHeaderLabel.Text = dateFilterActive
                ? $"Completed Tasks ({completedTodayTasks.Count})"
                : $"Completed Today ({completedTodayTasks.Count})";

            BindableLayout.SetItemsSource(TodayTasksView, BuildDeadlineGroups(todayTasks));
            BindableLayout.SetItemsSource(CompletedTodayTasksView, BuildDeadlineGroups(completedTodayTasks));

            bool hasTodayTasks = todayTasks.Count > 0;
            bool hasCompletedTasks = completedTodayTasks.Count > 0;
            bool hasAnyTasks = hasTodayTasks || hasCompletedTasks;
            TodaySection.IsVisible = hasTodayTasks;
            CompletedTodaySection.IsVisible = true;
            CompletedTodaySectionHeader.IsVisible = true;
            CompletedTodayTasksView.IsVisible = hasCompletedTasks && _isCompletedTodayExpanded;
            CompletionHistoryLink.IsVisible = true;
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
                .Where(task => task.IsCompleted && task.CompletedAt?.Date < DateTime.Today)
                .OrderByDescending(task => task.CompletedAt)
                .ToList();
            return new CompletionHistoryPage(history, showTeacherFilter: true);
        }

    }
}
