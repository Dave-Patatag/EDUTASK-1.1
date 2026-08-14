using System.Collections.ObjectModel;
using System.ComponentModel;
using EDUTASK_1._1.Models;
using EDUTASK_1._1.Services;

namespace EDUTASK_1._1.Views;

public partial class SubtaskProofHistoryPage : ContentPage
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
        int? reviewableHistoryID = canReview
            ? subtask.ProofHistory
                .Where(item => item.ValidationStatus == "Pending")
                .OrderByDescending(item => item.AttemptNumber)
                .Select(item => (int?)item.HistoryID)
                .FirstOrDefault()
            : null;
        Attempts = new ObservableCollection<ProofHistoryRowViewModel>(
            subtask.ProofHistory
                .OrderBy(item => item.AttemptNumber)
                .Select(item => new ProofHistoryRowViewModel(
                    item,
                    reviewableHistoryID == item.HistoryID)));
        _reviewableRow = Attempts.FirstOrDefault(item => item.CanReview);
        ReviewActionsPanel.IsVisible = _reviewableRow is not null;
        BindingContext = this;
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width <= 0 || height <= 0)
            return;

        PopupPanel.WidthRequest = Math.Min(680, Math.Max(300, width - 32));
        PopupPanel.HeightRequest = Math.Min(520, Math.Max(260, height - 48));
    }

    private async void OnCloseClicked(object sender, EventArgs e) =>
        await Navigation.PopModalAsync();

    private async void OnViewProofTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is not ProofHistoryRowViewModel row)
            return;
        try
        {
            var file = await _db.GetSubtaskProofHistoryFileAsync(row.HistoryID);
            if (file is null)
            {
                await UiAlertService.ShowAsync(this, "File unavailable", "We couldn't find this proof-history file.");
                return;
            }
            await ProofFileViewerService.OpenAsync(this, file.Value, $"proof-attempt-{row.AttemptNumber}");
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
                this, "Approve file", "Approve this submission and complete the subtask?", "Approve", "Cancel"))
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
                _subtask.SubtaskID,
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
                var currentUser = await UserSessionService.GetCurrentUserAsync();
                string authorName = currentUser is null
                    ? "Admin"
                    : $"{currentUser.FirstName} {currentUser.LastName}".Trim();
                await _db.AddTaskCommentAsync(
                    _subtask.TaskID,
                    _subtask.SubtaskID,
                    "User",
                    UserSessionService.CurrentUserId,
                    authorName,
                    remarks,
                    "ProofReturn");
            }

            row.CompleteReview(approve ? "Approved" : "Returned");
            ReviewActionsPanel.IsVisible = false;
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

    public ProofHistoryRowViewModel(SubtaskProofHistoryItem item, bool canReview)
    {
        HistoryID = item.HistoryID;
        AttemptNumber = item.AttemptNumber;
        FileName = item.FileName;
        SubmittedAtDisplay = item.SubmittedAtDisplay;
        _validationStatus = item.ValidationStatus;
        _canReview = canReview;
    }

    public int HistoryID { get; }
    public int AttemptNumber { get; }
    public string AttemptDisplay => AttemptNumber.ToString();
    public string FileName { get; }
    public string SubmittedAtDisplay { get; }
    public string ValidationStatus => _validationStatus;
    public bool CanReview => _canReview;
    public Color StatusColor => _validationStatus switch
    {
        "Approved" => Color.FromArgb("#16803A"),
        "Returned" => Color.FromArgb("#DC2626"),
        "Pending" => Color.FromArgb("#D97706"),
        "Ready" => Color.FromArgb("#2563EB"),
        _ => Color.FromArgb("#6B7280")
    };

    public void CompleteReview(string status)
    {
        _validationStatus = status;
        _canReview = false;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ValidationStatus)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusColor)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanReview)));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
