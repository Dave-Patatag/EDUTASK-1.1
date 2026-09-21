using System.Collections.ObjectModel;
using EDUTASK_1._1.Views.Base;
using EDUTASK_1._1.Models;
using EDUTASK_1._1.Services;

namespace EDUTASK_1._1.Views;

public partial class SubtaskProofDraftPage : EduTaskPage
{
    private readonly DatabaseService _db = new();
    private readonly SubtaskDisplayItem _subtask;
    private readonly int _teacherID;
    private readonly Func<System.Threading.Tasks.Task>? _onSubmitted;
    private bool _isSubmitting;

    public string SubtaskTitle => _subtask.Title;
    public string File_name => _subtask.Proof_file_name ?? "proof";
    public string PreparedAtDisplay =>
        (_subtask.Proof_uploaded_at ?? DateTime.Now).ToString("MMM d, yyyy h:mm tt");
    public ObservableCollection<ProofHistoryRowViewModel> Attempts { get; }

    public SubtaskProofDraftPage(
        SubtaskDisplayItem subtask,
        int teacherID,
        Func<System.Threading.Tasks.Task>? onSubmitted = null)
    {
        InitializeComponent();
        _subtask = subtask;
        _teacherID = teacherID;
        _onSubmitted = onSubmitted;
        var rows = subtask.ProofHistory
            .OrderBy(item => item.AttemptNumber)
            .Select(item => new ProofHistoryRowViewModel(item, false))
            .ToList();
        int draftAttemptNumber = rows.Count == 0 ? 1 : rows.Max(item => item.AttemptNumber) + 1;
        rows.Add(new ProofHistoryRowViewModel(new ProofSubmissionItem
        {
            SubmissionID = 0,
            AttemptNumber = draftAttemptNumber,
            File_name = File_name,
            FileCount = Math.Max(1, subtask.ProofFileCount),
            File_type = string.Empty,
            ValidationStatus = "Ready",
            SubmittedAt = subtask.Proof_uploaded_at ?? DateTime.Now
        }, false));
        Attempts = new ObservableCollection<ProofHistoryRowViewModel>(rows);
        Attempts.CollectionChanged += (_, _) => UpdatePopupSize(Width, Height);
        BindingContext = this;
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        UpdatePopupSize(width, height);
    }

    private void UpdatePopupSize(double width, double height)
    {
        if (width <= 0 || height <= 0)
            return;

        // Reserve space for the title, table header and Back button, then grow
        // with the proof rows. The existing ScrollView handles longer histories.
        double desiredHeight = 180 + 56 * (Attempts?.Count ?? 1);
        PopupPanel.WidthRequest = Math.Min(680, Math.Max(0, width - 32));
        PopupPanel.HeightRequest = Math.Min(desiredHeight, Math.Min(560, Math.Max(0, height - 48)));
    }

    private async void OnViewFileTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is not ProofHistoryRowViewModel row)
            return;
        try
        {
            List<PreparedProofImage> files = row.SubmissionID == 0
                ? await _db.GetSubtaskProofFilesAsync(_subtask.Subtask_id)
                : await _db.GetProofSubmissionFilesAsync(row.SubmissionID);
            if (files.Count == 0)
            {
                await UiAlertService.ShowAsync(this, "Files unavailable", "We couldn't find the selected draft files.");
                return;
            }
            await ProofFileViewerService.OpenManyAsync(this, files, $"proof-attempt-{row.AttemptNumber}");
        }
        catch
        {
            await UiAlertService.ShowAsync(this, "File couldn't open", "We couldn't open this draft. Please try again.");
        }
    }

    private async void OnSubmitClicked(object sender, EventArgs e)
    {
        if (_isSubmitting)
            return;
        _isSubmitting = true;
        try
        {
            if (!await _db.ConfirmSubtaskProofAsync(_subtask.Subtask_id, _teacherID))
            {
                await UiAlertService.ShowAsync(this, "Files already updated", "This draft was already submitted or replaced.");
                return;
            }
            if (_onSubmitted is not null)
                await _onSubmitted();
            await Navigation.PopModalAsync(false);
        }
        catch
        {
            await UiAlertService.ShowAsync(this, "Files couldn't be submitted", "Please try again.");
        }
        finally
        {
            _isSubmitting = false;
        }
    }

    private async void OnCloseClicked(object sender, EventArgs e) =>
        await Navigation.PopModalAsync(false);

    private async void OnRemoveClicked(object sender, EventArgs e)
    {
        if (!await UiAlertService.ConfirmAsync(this, "Remove draft", "Remove the selected proof files?", "Remove", "Cancel"))
            return;

        try
        {
            if (!await _db.RemoveSubtaskProofAsync(_subtask.Subtask_id, _teacherID))
            {
                await UiAlertService.ShowAsync(this, "Draft already updated", "This draft can no longer be removed.");
                return;
            }

            if (_onSubmitted is not null)
                await _onSubmitted();
            await Navigation.PopModalAsync(false);
        }
        catch
        {
            await UiAlertService.ShowAsync(this, "Draft couldn't be removed", "Please try again.");
        }
    }
}
