using System.Collections.ObjectModel;
using EDUTASK_1._1.Helpers;
using EDUTASK_1._1.Views.Base;
using System.ComponentModel;
using EDUTASK_1._1.Models;
using EDUTASK_1._1.Services;

namespace EDUTASK_1._1.Views;

public partial class SubtaskProofHistoryPage : EduTaskPage
{
    private readonly DatabaseService _db = new();
    private readonly SubtaskDisplayItem _subtask;
    private readonly Func<System.Threading.Tasks.Task>? _onReviewCompleted;
    private readonly ProofHistoryRowViewModel? _reviewableRow;

    public string SubtaskTitle => _subtask.Title;
    public ObservableCollection<ProofHistoryRowViewModel> Attempts { get; }

    public SubtaskProofHistoryPage(
        SubtaskDisplayItem subtask,
        bool canReview = false,
        Func<System.Threading.Tasks.Task>? onReviewCompleted = null)
    {
        InitializeComponent();
        _subtask = subtask;
        _onReviewCompleted = onReviewCompleted;
        int? reviewableSubmissionID = canReview
            ? subtask.ProofHistory
                .Where(item => item.ValidationStatus == "Pending")
                .OrderByDescending(item => item.AttemptNumber)
                .Select(item => (int?)item.SubmissionID)
                .FirstOrDefault()
            : null;
        Attempts = new ObservableCollection<ProofHistoryRowViewModel>(
            subtask.ProofHistory
                .OrderBy(item => item.AttemptNumber)
                .Select(item => new ProofHistoryRowViewModel(
                    item,
                    reviewableSubmissionID == item.SubmissionID)));
        _reviewableRow = Attempts.FirstOrDefault(item => item.CanReview);
        BindingContext = this;
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width <= 0 || height <= 0)
            return;

        double horizontalMargin = width >= 400 ? 40 : 20;
        PopupPanel.WidthRequest = Math.Min(620, Math.Max(0, width - horizontalMargin));

        int attemptCount = Math.Max(1, Attempts?.Count ?? 0);
        bool hasReviewControls = Attempts?.Any(attempt => attempt.CanReview) == true;
        double desiredHeight = 300 + attemptCount * 82 + (hasReviewControls ? 48 : 0);
        double availableHeight = Math.Max(0, height - 40);
        PopupPanel.HeightRequest = Math.Min(desiredHeight, availableHeight);
    }

    private async void OnCloseClicked(object sender, EventArgs e) =>
        await Navigation.PopModalAsync(false);

    private async void OnCloseTapped(object sender, TappedEventArgs e) =>
        await Navigation.PopModalAsync(false);

    private async void OnViewProofTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is not ProofHistoryRowViewModel row)
            return;
        try
        {
            List<PreparedProofImage> files = await _db.GetProofSubmissionFilesAsync(row.SubmissionID);
            if (files.Count == 0)
            {
                await UiAlertService.ShowAsync(this, "Files unavailable", "We couldn't find files for this submission.");
                return;
            }
            await ProofFileViewerService.OpenManyAsync(this, files, $"proof-attempt-{row.AttemptNumber}");
        }
        catch
        {
            await UiAlertService.ShowAsync(this, "File couldn't open", "We couldn't open this file. Please try again.");
        }
    }

    private async void OnApproveClicked(object sender, EventArgs e)
    {
        if (_reviewableRow is not { CanReview: true } row)
            return;
        if (!await UiAlertService.ConfirmAsync(
                this, "Approve submission", "Approve these proof files and complete the subtask?", "Approve", "Cancel"))
            return;
        await ReviewAsync(row, true, null);
    }

    private async void OnReturnClicked(object sender, EventArgs e)
    {
        if (_reviewableRow is not { CanReview: true } row)
            return;
        string? remarks = await UiAlertService.PromptAsync(
            this,
            "Request changes",
            "Tell the teacher what needs to be updated.",
            "Request changes",
            "Cancel",
            maxLength: 500);
        if (remarks is null)
            return;
        if (string.IsNullOrWhiteSpace(remarks))
        {
            await UiAlertService.ShowAsync(this, "Add a note", "Please explain what needs to be changed.");
            return;
        }
        await ReviewAsync(row, false, remarks.Trim());
    }

    private async System.Threading.Tasks.Task ReviewAsync(
        ProofHistoryRowViewModel row,
        bool approve,
        string? remarks)
    {
        try
        {
            bool reviewed = await _db.ReviewSubtaskProofAsync(
                _subtask.Subtask_id,
                approve,
                UserSessionService.CurrentUserId,
                remarks);
            if (!reviewed)
            {
                await UiAlertService.ShowAsync(
                    this,
                    "Submission already updated",
                    "This submission was already reviewed or replaced.");
                return;
            }

            if (!approve && !string.IsNullOrWhiteSpace(remarks))
            {
                await _db.AddTaskDiscussionAsync(
                    _subtask.Task_id,
                    _subtask.Subtask_id,
                    "User",
                    UserSessionService.CurrentUserId,
                    remarks,
                    "ProofReturn");
            }

            row.CompleteReview(approve ? "Approved" : "Returned");
            if (_onReviewCompleted is not null)
                await _onReviewCompleted();
            await UiAlertService.ShowAsync(
                this,
                approve ? "Submission approved" : "Changes requested",
                approve
                    ? "The submission was approved."
                    : "The teacher can now upload a revised file.");
        }
        catch
        {
            await UiAlertService.ShowAsync(this, "Review couldn't be saved", "Please try again.");
        }
    }
}

public sealed class ProofHistoryRowViewModel : INotifyPropertyChanged
{
    private string _validationStatus;
    private bool _canReview;

    public ProofHistoryRowViewModel(ProofSubmissionItem item, bool canReview)
    {
        SubmissionID = item.SubmissionID;
        AttemptNumber = item.AttemptNumber;
        File_name = item.File_name;
        FileCount = Math.Max(1, item.FileCount);
        SubmittedAtDisplay = item.SubmittedAtDisplay;
        SubmittedDateDisplay = item.SubmittedDateDisplay;
        SubmittedTimeDisplay = item.SubmittedTimeDisplay;
        _validationStatus = item.ValidationStatus;
        _canReview = canReview;
    }

    public int SubmissionID { get; }
    public int AttemptNumber { get; }
    public string AttemptDisplay => AttemptNumber.ToString();
    public string File_name { get; }
    public int FileCount { get; }
    public string ViewFilesText => FileCount == 1 ? "View file" : $"View files ({FileCount})";
    public string SubmittedAtDisplay { get; }
    public string SubmittedDateDisplay { get; }
    public string SubmittedTimeDisplay { get; }
    public string ValidationStatus => _validationStatus;
    public bool CanReview => _canReview;
    public Color StatusColor => _validationStatus switch
    {
        "Approved" => AppColors.StatusSuccess,
        "Returned" => AppColors.StatusDanger,
        "Pending" => AppColors.StatusPending,
        "Ready" => AppColors.Accent500,
        _ => AppColors.TextTertiary
    };
    public Color StatusSurfaceColor => _validationStatus switch
    {
        "Approved" => AppColors.StatusSuccessSurface,
        "Returned" => AppColors.StatusDangerSurface,
        "Pending" => AppColors.StatusPendingSurface,
        "Ready" => AppColors.Accent100,
        _ => AppColors.SurfaceSunken
    };

    public void CompleteReview(string status)
    {
        _validationStatus = status;
        _canReview = false;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ValidationStatus)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusColor)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusSurfaceColor)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanReview)));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
