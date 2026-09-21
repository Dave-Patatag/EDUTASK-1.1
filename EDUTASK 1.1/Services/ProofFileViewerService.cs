using EDUTASK_1._1.Helpers;
using EDUTASK_1._1.Models;

namespace EDUTASK_1._1.Services;

public static class ProofFileViewerService
{
    public static Task OpenAsync(
        Page owner,
        (byte[] Data, string File_type, string File_name) file,
        string fallbackName) =>
        OpenManyAsync(owner,
        [
            new PreparedProofImage
            {
                Data = file.Data,
                File_type = file.File_type,
                File_name = file.File_name
            }
        ], fallbackName);

    public static async Task OpenManyAsync(
        Page owner,
        IReadOnlyList<PreparedProofImage> files,
        string fallbackName)
    {
        if (files.Count == 0)
        {
            await UiAlertService.ShowAsync(owner, "Files unavailable", "We couldn't find files for this submission.");
            return;
        }

        List<ProofViewerItem> items = files
            .Select((file, index) => new ProofViewerItem(file, index + 1))
            .ToList();

        var fileNameLabel = new Label
        {
            Text = items[0].File_name,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = AppColors.TextPrimary,
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 1,
            VerticalTextAlignment = TextAlignment.Center
        };
        var positionLabel = new Label
        {
            Text = $"1 of {items.Count}",
            FontSize = 12,
            TextColor = AppColors.TextSecondary,
            HorizontalTextAlignment = TextAlignment.End,
            VerticalTextAlignment = TextAlignment.Center
        };

        var carousel = new CarouselView
        {
            ItemsSource = items,
            Loop = false,
            IsBounceEnabled = true,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Never,
            ItemTemplate = new DataTemplate(() =>
            {
                var image = new Image
                {
                    Aspect = Aspect.AspectFit,
                    Margin = new Thickness(8),
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Fill
                };
                image.SetBinding(Image.SourceProperty, nameof(ProofViewerItem.PreviewSource));
                image.SetBinding(VisualElement.IsVisibleProperty, nameof(ProofViewerItem.IsImage));

                var documentPanel = new VerticalStackLayout
                {
                    Spacing = 10,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    Children =
                    {
                        new Label
                        {
                            Text = "PDF",
                            FontSize = 28,
                            FontAttributes = FontAttributes.Bold,
                            TextColor = AppColors.Accent500,
                            HorizontalTextAlignment = TextAlignment.Center
                        },
                        new Label
                        {
                            Text = "Tap Open externally to view this document.",
                            FontSize = 12,
                            TextColor = AppColors.TextSecondary,
                            HorizontalTextAlignment = TextAlignment.Center
                        }
                    }
                };
                documentPanel.SetBinding(VisualElement.IsVisibleProperty, nameof(ProofViewerItem.IsDocument));

                return new Grid
                {
                    BackgroundColor = AppColors.SurfaceBase,
                    Children = { image, documentPanel }
                };
            })
        };

        var closeButton = new Button
        {
            Text = "Close",
            HeightRequest = 42,
            Padding = new Thickness(18, 0),
            CornerRadius = 9,
            BackgroundColor = AppColors.ActionDismiss,
            TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.Start
        };
        var openButton = new Button
        {
            Text = "Open externally",
            HeightRequest = 42,
            Padding = new Thickness(18, 0),
            CornerRadius = 9,
            BackgroundColor = AppColors.ActionPrimary,
            TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.End
        };

        var header = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 12,
            Children = { fileNameLabel, positionLabel }
        };
        Grid.SetColumn(positionLabel, 1);

        var actions = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 10,
            Children = { closeButton, openButton }
        };
        Grid.SetColumn(openButton, 1);

        var content = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            },
            RowSpacing = 12,
            Padding = new Thickness(14),
            Children = { header, carousel, actions }
        };
        Grid.SetRow(carousel, 1);
        Grid.SetRow(actions, 2);

        var viewerPage = new ContentPage
        {
            BackgroundColor = AppColors.ViewerBackdrop,
            Content = content
        };

        carousel.PositionChanged += (_, eventArgs) =>
        {
            int position = Math.Clamp(eventArgs.CurrentPosition, 0, items.Count - 1);
            fileNameLabel.Text = items[position].File_name;
            positionLabel.Text = $"{position + 1} of {items.Count}";
        };
        closeButton.Clicked += async (_, _) => await viewerPage.Navigation.PopModalAsync(false);
        openButton.Clicked += async (_, _) =>
        {
            int position = Math.Clamp(carousel.Position, 0, files.Count - 1);
            try
            {
                await OpenExternallyAsync(files[position], $"{fallbackName}-{position + 1}");
            }
            catch
            {
                await UiAlertService.ShowAsync(viewerPage, "File couldn't open", "No compatible viewer could open this file.");
            }
        };

        await owner.Navigation.PushModalAsync(viewerPage, false);
    }

    private static async Task OpenExternallyAsync(PreparedProofImage file, string fallbackName)
    {
        string extension = file.File_type switch
        {
            "application/pdf" => ".pdf",
            "image/png" => ".png",
            _ => ".jpg"
        };
        string safeFileName = Path.GetFileName(file.File_name);
        if (string.IsNullOrWhiteSpace(safeFileName))
            safeFileName = $"{fallbackName}{extension}";
        if (string.IsNullOrWhiteSpace(Path.GetExtension(safeFileName)))
            safeFileName += extension;

        string localPath = Path.Combine(FileSystem.CacheDirectory, $"{Guid.NewGuid():N}-{safeFileName}");
        await File.WriteAllBytesAsync(localPath, file.Data);
        await Launcher.Default.OpenAsync(new OpenFileRequest
        {
            Title = safeFileName,
            File = new ReadOnlyFile(localPath, file.File_type)
        });
    }

    private sealed class ProofViewerItem
    {
        public ProofViewerItem(PreparedProofImage file, int number)
        {
            File_name = string.IsNullOrWhiteSpace(file.File_name) ? $"Proof file {number}" : file.File_name;
            IsImage = file.File_type.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
            IsDocument = !IsImage;
            if (IsImage)
                PreviewSource = ImageSource.FromStream(() => new MemoryStream(file.Data, writable: false));
        }

        public string File_name { get; }
        public bool IsImage { get; }
        public bool IsDocument { get; }
        public ImageSource? PreviewSource { get; }
    }
}
