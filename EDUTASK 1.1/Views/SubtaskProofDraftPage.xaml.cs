using System.Collections.ObjectModel;
using EDUTASK_1._1.Models;
using EDUTASK_1._1.Services;

namespace EDUTASK_1._1.Views;

public partial class SubtaskProofDraftPage : ContentPage
{
    private readonly DatabaseService _db = new();
    private readonly SubtaskDisplayItem _subtask;
    private readonly Func<System.Threading.Tasks.Task>? _onSubmitted;
    private bool _isSubmitting;

    public string SubtaskTitle => _subtask.Title;
    public string FileName => _subtask.ProofFileName ?? "proof";
    public string PreparedAtDisplay =>
        (_subtask.ProofUploadedAt ?? DateTime.Now).ToString("MMM d, yyyy h:mm tt");
    public ObservableCollection<ProofHistoryRowViewModel> Attempts { get; }

    public SubtaskProofDraftPage(
        SubtaskDisplayItem subtask,
        Func<System.Threading.Tasks.Task>? onSubmitted = null)
    {
        InitializeComponent();
        _subtask = subtask;
        _onSubmitted = onSubmitted;
        var rows = subtask.ProofHistory
            .OrderBy(item => item.AttemptNumber)
            .Select(item => new ProofHistoryRowViewModel(item, false))
            .ToList();
        int draftAttemptNumber = rows.Count == 0 ? 1 : rows.Max(item => item.AttemptNumber) + 1;
        rows.Add(new ProofHistoryRowViewModel(new SubtaskProofHistoryItem
        {
            HistoryID = 0,
            AttemptNumber = draftAttemptNumber,
            FileName = FileName,
            ContentType = string.Empty,
            ValidationStatus = "Ready",
            SubmittedAt = subtask.ProofUploadedAt ?? DateTime.Now
        }, false));
        Attempts = new ObservableCollection<ProofHistoryRowViewModel>(rows);
        BindingContext = this;
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (width <= 0 || height <= 0)
            return;
        PopupPanel.WidthRequest = Math.Min(680, Math.Max(300, width - 32));
        PopupPanel.HeightRequest = Math.Min(360, Math.Max(280, height - 48));
    }

    private async void OnViewFileTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is not ProofHistoryRowViewModel row)
            return;
        try
        {
            var file = row.HistoryID == 0
                ? await _db.GetSubtaskProofImageAsync(_subtask.SubtaskID)
                : await _db.GetSubtaskProofHistoryFileAsync(row.HistoryID);
            if (file is null)
            {
                await UiAlertService.ShowAsync(this, "File unavailable", "We couldn't find the selected draft file.");
                return;
            }
            await ProofFileViewerService.OpenAsync(this, file.Value, $"proof-attempt-{row.AttemptNumber}");
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
            if (!await _db.ConfirmSubtaskProofAsync(_subtask.SubtaskID))
            {
                await UiAlertService.ShowAsync(this, "File already updated", "This draft was already submitted or replaced.");
                return;
            }
            if (_onSubmitted is not null)
                await _onSubmitted();
            await Navigation.PopModalAsync();
        }
        catch
        {
            await UiAlertService.ShowAsync(this, "File couldn't be submitted", "Please try again.");
        }
        finally
        {
            _isSubmitting = false;
        }
    }

    private async void OnCloseClicked(object sender, EventArgs e) =>
        await Navigation.PopModalAsync();

    private async void OnRemoveClicked(object sender, EventArgs e)
    {
        if (!await UiAlertService.ConfirmAsync(this, "Remove draft", "Remove this selected file?", "Remove", "Cancel"))
            return;

        try
        {
            if (!await _db.RemoveSubtaskProofAsync(_subtask.SubtaskID))
            {
                await UiAlertService.ShowAsync(this, "Draft already updated", "This draft can no longer be removed.");
                return;
            }

            if (_onSubmitted is not null)
                await _onSubmitted();
            await Navigation.PopModalAsync();
        }
        catch
        {
            await UiAlertService.ShowAsync(this, "Draft couldn't be removed", "Please try again.");
        }
    }
}
