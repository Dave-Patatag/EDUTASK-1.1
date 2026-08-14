namespace EDUTASK_1._1.Services;

public static class ProofFileViewerService
{
    public static async Task OpenAsync(
        Page owner,
        (byte[] Data, string ContentType, string FileName) file,
        string fallbackName)
    {
        if (string.Equals(file.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            string safeFileName = Path.GetFileName(file.FileName);
            if (string.IsNullOrWhiteSpace(safeFileName) ||
                !safeFileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                safeFileName = $"{fallbackName}.pdf";
            string localPath = Path.Combine(FileSystem.CacheDirectory, safeFileName);
            await File.WriteAllBytesAsync(localPath, file.Data);
            await Launcher.Default.OpenAsync(new OpenFileRequest
            {
                Title = safeFileName,
                File = new ReadOnlyFile(localPath, "application/pdf")
            });
            return;
        }

        var image = new Image
        {
            Source = ImageSource.FromStream(() => new MemoryStream(file.Data)),
            Aspect = Aspect.AspectFit,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };
        var closeButton = new Button { Text = "Close", HorizontalOptions = LayoutOptions.Center };
        var previewPage = new ContentPage
        {
            Title = file.FileName,
            BackgroundColor = Colors.Black,
            Content = new Grid
            {
                RowDefinitions = { new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) },
                Padding = new Thickness(12),
                Children = { image, closeButton }
            }
        };
        Grid.SetRow(closeButton, 1);
        closeButton.Clicked += async (_, _) => await owner.Navigation.PopModalAsync();
        await owner.Navigation.PushModalAsync(previewPage);
    }
}
