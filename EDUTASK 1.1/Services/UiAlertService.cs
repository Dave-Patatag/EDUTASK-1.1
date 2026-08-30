using EDUTASK_1._1.Helpers;

namespace EDUTASK_1._1.Services;

public static class UiAlertService
{
    public static Task ShowAsync(Page owner, string title, string message, string buttonText = "OK")
    {
        return ShowCoreAsync(owner, title, message, buttonText, null);
    }

    public static async Task<bool> ConfirmAsync(
        Page owner,
        string title,
        string message,
        string acceptText,
        string cancelText,
        bool stackedButtons = false)
    {
        return await ShowCoreAsync(owner, title, message, acceptText, cancelText, stackedButtons);
    }

    public static async Task<string?> PromptAsync(
        Page owner,
        string title,
        string message,
        string primaryText,
        string cancelText,
        int maxLength = 500)
    {
        var completion = new TaskCompletionSource<string?>();
        var accent = AppColors.StatusDanger;
        var themePrimary = AppColors.Brand800;

        var modal = new ContentPage
        {
            BackgroundColor = Colors.Transparent,
            Padding = 0
        };

        var iconCircle = new Image
        {
            Source = "warningicon.png",
            WidthRequest = 58,
            HeightRequest = 58,
            HorizontalOptions = LayoutOptions.Center,
            Aspect = Aspect.AspectFit
        };

        var editor = new Editor
        {
            Placeholder = "What should the teacher update?",
            MaxLength = maxLength,
            HeightRequest = 96,
            AutoSize = EditorAutoSizeOption.Disabled,
            BackgroundColor = Colors.Transparent,
            TextColor = AppColors.TextPrimary,
            PlaceholderColor = AppColors.TextDisabled,
            FontSize = AppTypography.Body,
            Margin = new Thickness(4, 1)
        };

        var inputBorder = new Border
        {
            BackgroundColor = AppColors.SurfaceMuted,
            Stroke = AppColors.BorderStrong,
            StrokeThickness = 1,
            Padding = new Thickness(8, 4),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 9 },
            Content = editor
        };

        var errorLabel = new Label
        {
            Text = "A reason is required.",
            FontSize = AppTypography.Label,
            TextColor = accent,
            IsVisible = false
        };

        var primaryButton = CreateButton(primaryText, themePrimary, AppColors.TextInverse);
        primaryButton.FontSize = AppTypography.Caption;
        primaryButton.Padding = new Thickness(8, 0);
        var cancelButton = CreateSecondaryButton(cancelText);
        primaryButton.IsEnabled = false;

        bool isClosing = false;
        string? closingResult = null;

        async Task CloseAsync(string? result)
        {
            if (isClosing || completion.Task.IsCompleted)
                return;

            isClosing = true;
            closingResult = result;
            primaryButton.IsEnabled = false;
            cancelButton.IsEnabled = false;
            await modal.Navigation.PopModalAsync(false);
            completion.TrySetResult(result);
        }

        editor.TextChanged += (_, _) =>
        {
            bool hasReason = !string.IsNullOrWhiteSpace(editor.Text);
            primaryButton.IsEnabled = hasReason;
            errorLabel.IsVisible = false;
            inputBorder.Stroke = hasReason ? AppColors.BorderStrong : inputBorder.Stroke;
        };
        primaryButton.Clicked += async (_, _) =>
        {
            string reason = editor.Text?.Trim() ?? string.Empty;
            if (reason.Length == 0)
            {
                errorLabel.IsVisible = true;
                inputBorder.Stroke = accent;
                return;
            }

            await CloseAsync(reason);
        };
        cancelButton.Clicked += async (_, _) => await CloseAsync(null);
        var promptBackdrop = new BoxView { Color = AppColors.Scrim };
        var promptBackdropTap = new TapGestureRecognizer();
        promptBackdropTap.Tapped += async (_, _) => await CloseAsync(null);
        promptBackdrop.GestureRecognizers.Add(promptBackdropTap);

        var buttons = new VerticalStackLayout
        {
            Spacing = 10,
            Children = { primaryButton, cancelButton }
        };
        ApplyPrimaryButtonStyle(primaryButton);

        modal.Content = new Grid
        {
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                promptBackdrop,
                new Border
                {
                    MaximumWidthRequest = 420,
                    Padding = new Thickness(22),
                    BackgroundColor = AppColors.SurfaceBase,
                    Stroke = AppColors.StatusDangerSurface,
                    StrokeThickness = 1,
                    HorizontalOptions = LayoutOptions.Fill,
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 },
                    Content = new VerticalStackLayout
                    {
                        Spacing = 12,
                        Children =
                        {
                            iconCircle,
                            new Label
                            {
                                Text = title,
                                FontSize = AppTypography.Heading,
                                FontAttributes = FontAttributes.Bold,
                                TextColor = AppColors.TextPrimary,
                                HorizontalTextAlignment = TextAlignment.Center
                            },
                            new Label
                            {
                                Text = message,
                                FontSize = AppTypography.BodySmall,
                                TextColor = AppColors.TextSecondary,
                                HorizontalTextAlignment = TextAlignment.Center
                            },
                            inputBorder,
                            errorLabel,
                            buttons
                        }
                    }
                }
            }
        };

        modal.Disappearing += (_, _) => completion.TrySetResult(closingResult);
        await owner.Navigation.PushModalAsync(modal, false);
        editor.Focus();
        return await completion.Task;
    }
    public static async Task<bool> ShowTaskReminderAsync(Page owner, IReadOnlyList<(string Title, string Priority)> dueToday, IReadOnlyList<(string Title, string Priority)> dueTomorrow, DateTime today)
    {
        var completion = new TaskCompletionSource<bool>();
        var overlay = new Grid { BackgroundColor = Colors.Transparent, ZIndex = 1000 };
        bool isClosing = false;

        string title = dueToday.Count > 0 && dueTomorrow.Count > 0
            ? "Tasks due soon"
            : dueToday.Count > 0 ? "Tasks due today" : "Tasks due tomorrow";
        string message = dueToday.Count > 0 && dueTomorrow.Count > 0
            ? $"You have {dueToday.Count} task{(dueToday.Count == 1 ? string.Empty : "s")} due today and {dueTomorrow.Count} due tomorrow."
            : dueToday.Count > 0
                ? $"You have {dueToday.Count} task{(dueToday.Count == 1 ? string.Empty : "s")} due today."
                : $"You have {dueTomorrow.Count} task{(dueTomorrow.Count == 1 ? string.Empty : "s")} due tomorrow.";

        var sheetContent = new VerticalStackLayout
        {
            Spacing = 8,
            Padding = new Thickness(28, 0, 28, 26),
            Children =
            {
                new Image { Source = "tasknodue.png", WidthRequest = 110, HeightRequest = 90, Aspect = Aspect.AspectFit, HorizontalOptions = LayoutOptions.Center, Margin = new Thickness(0, 4, 0, 2) },
                new Label { Text = title, FontSize = AppTypography.Heading, FontAttributes = FontAttributes.Bold, TextColor = AppColors.TextPrimary, HorizontalTextAlignment = TextAlignment.Center },
                new Label { Text = message, FontSize = AppTypography.Body, TextColor = AppColors.TextSecondary, HorizontalTextAlignment = TextAlignment.Center, LineBreakMode = LineBreakMode.WordWrap },
            }
        };

        var gotItButton = CreateButton("Got it", AppColors.Accent500, AppColors.TextInverse);
        ApplySingleActionButtonStyle(gotItButton);
        gotItButton.Margin = new Thickness(0, 14, 0, 0);
        gotItButton.Clicked += (_, _) =>
        {
            if (isClosing) return;
            isClosing = true;
            completion.TrySetResult(false);
        };
        sheetContent.Children.Add(gotItButton);

        var reminderBackdrop = new BoxView { Color = Colors.Transparent };
        var reminderBackdropTap = new TapGestureRecognizer();
        reminderBackdropTap.Tapped += (_, _) =>
        {
            if (isClosing) return;
            isClosing = true;

            completion.TrySetResult(false);
        };
        reminderBackdrop.GestureRecognizers.Add(reminderBackdropTap);
        overlay.Children.Add(reminderBackdrop);
        overlay.Children.Add(new Border
        {
            WidthRequest = Math.Min(420, Math.Max(300, owner.Width)),
            MaximumWidthRequest = 480,
            BackgroundColor = AppColors.SurfaceBase, StrokeThickness = 0,
            HorizontalOptions = LayoutOptions.Fill, VerticalOptions = LayoutOptions.End,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(24, 24, 0, 0) },
            Content = sheetContent
        });
        var modal = new ContentPage
        {
            BackgroundColor = AppColors.Scrim,
            Padding = 0,
            Content = overlay
        };
        NavigationPage.SetHasNavigationBar(modal, false);

        INavigation modalNavigation = Application.Current?.Windows.FirstOrDefault()?.Page?.Navigation
            ?? owner.Navigation;
        bool result = false;
        try
        {
            await modalNavigation.PushModalAsync(modal, false);
            result = await completion.Task;
        }
        finally
        {
            if (modalNavigation.ModalStack.Contains(modal))
                await modalNavigation.PopModalAsync(false);
        }
        return result;
    }

    private static async Task<bool> ShowCoreAsync(
        Page owner,
        string title,
        string message,
        string primaryText,
        string? secondaryText,
        bool stackedButtons = false)
    {
        var completion = new TaskCompletionSource<bool>();
        bool isProblem = IsProblem(title);
        bool isSuccess = !isProblem && IsSuccess(title);
        bool isDestructive = IsDestructive(title);
        // A dialog's confirm button is the primary action on screen, so it wears
        // the brand rather than the muted grey it used to. The accent pair is
        // the status token and its own tinted surface.
        Color themePrimary = AppColors.Brand800;
        Color accent = isSuccess ? AppColors.StatusSuccess
            : isDestructive || isProblem ? AppColors.StatusDanger
            : AppColors.StatusNeutral;
        Color softAccent = isSuccess ? AppColors.StatusSuccessSurface
            : isDestructive || isProblem ? AppColors.StatusDangerSurface
            : AppColors.StatusNeutralSurface;

        var modal = new ContentPage
        {
            BackgroundColor = Colors.Transparent,
            Padding = 0
        };

        var icon = new Label
        {
            Text = isSuccess ? "\u2713" : isDestructive || isProblem ? "!" : "i",
            TextColor = AppColors.SurfaceBase,
            FontSize = AppTypography.Heading,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center
        };

        var iconCircle = new Border
        {
            WidthRequest = 62,
            HeightRequest = 62,
            BackgroundColor = Colors.Transparent,
            StrokeThickness = 0,
            HorizontalOptions = LayoutOptions.Center,
            Content = new Image
            {
                Source = isSuccess ? "approveicon.png" : isDestructive ? "areyousureicon.png" : isProblem ? "warningicon.png" : "remindericon.png",
                WidthRequest = 62,
                HeightRequest = 62,
                Aspect = Aspect.AspectFit
            },
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 31 }
        };

        var titleLabel = new Label
        {
            Text = title,
            FontSize = AppTypography.Heading,
            FontAttributes = FontAttributes.Bold,
            TextColor = AppColors.TextPrimary,
            HorizontalTextAlignment = TextAlignment.Center
        };

        var messageLabel = new Label
        {
            Text = message,
            FontSize = AppTypography.BodySmall,
            TextColor = AppColors.TextSecondary,
            HorizontalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.WordWrap,
            MaxLines = 3
        };

        var primaryButton = CreateButton(primaryText, isDestructive ? accent : themePrimary, AppColors.TextInverse);
        var secondaryButton = secondaryText is null
            ? null
            : CreateSecondaryButton(secondaryText);

        bool isClosing = false;
        bool? closingResult = null;

        async Task CloseAsync(bool result)
        {
            if (isClosing || completion.Task.IsCompleted)
                return;

            isClosing = true;
            closingResult = result;
            primaryButton.IsEnabled = false;
            if (secondaryButton is not null)
                secondaryButton.IsEnabled = false;

            try
            {
                if (owner.Navigation.ModalStack.Contains(modal))
                    await owner.Navigation.PopModalAsync(false);
                completion.TrySetResult(result);
            }
            catch
            {
                isClosing = false;
                closingResult = null;
                primaryButton.IsEnabled = true;
                if (secondaryButton is not null)
                    secondaryButton.IsEnabled = true;
                throw;
            }
        }

        primaryButton.Clicked += async (_, _) => await CloseAsync(true);
        var alertBackdrop = new BoxView
        {
            Color = AppColors.Scrim,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };
        var alertBackdropTap = new TapGestureRecognizer();
        alertBackdropTap.Tapped += async (_, _) => await CloseAsync(false);
        alertBackdrop.GestureRecognizers.Add(alertBackdropTap);
        if (secondaryButton is not null)
            secondaryButton.Clicked += async (_, _) => await CloseAsync(false);

        ApplyPrimaryButtonStyle(primaryButton);
        View buttons;
        if (secondaryButton is not null)
        {
            // Dialog actions are intentionally stacked so the primary action
            // reads first and the secondary action remains clearly separate.
            buttons = new VerticalStackLayout
            {
                Spacing = 10,
                HorizontalOptions = LayoutOptions.Fill,
                Children = { primaryButton, secondaryButton }
            };
        }
        else
        {
            ApplySingleActionButtonStyle(primaryButton);
            buttons = primaryButton;
        }

        if (stackedButtons)
        {
            titleLabel.Margin = new Thickness(0, 4, 0, 2);
            messageLabel.Margin = new Thickness(12, 0, 12, 8);
            messageLabel.LineHeight = 1.3;
        }
        var content = new VerticalStackLayout
        {
            Spacing = 16,
            Margin = new Thickness(0, 8, 0, 0),
            Children =
            {
                iconCircle,
                titleLabel,
                messageLabel,
                buttons
            }
        };

        modal.Content = new Grid
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            Children =
            {
                alertBackdrop,
                new Border
                {
                    MaximumWidthRequest = 420,
                    Margin = new Thickness(24),
                    Padding = new Thickness(28, 24, 28, 28),
                    BackgroundColor = AppColors.SurfaceBase,
                    Stroke = softAccent,
                    StrokeThickness = 1,
                    HorizontalOptions = LayoutOptions.Fill,
                    VerticalOptions = LayoutOptions.Center,
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 22 },
                    Shadow = new Shadow { Brush = AppColors.ShadowOverlay, Offset = new Point(0, 5), Radius = 12, Opacity = 0.35f },
                    Content = content
                }
            }
        };

        modal.Disappearing += (_, _) => completion.TrySetResult(closingResult ?? false);
        await owner.Navigation.PushModalAsync(modal, false);
        return await completion.Task;
    }

    private static Button CreateButton(string text, Color background, Color foreground)
    {
        return new Button
        {
            Text = text,
            HeightRequest = 50,
            MinimumHeightRequest = 50,
            CornerRadius = 8,
            FontSize = AppTypography.Body,
            FontAttributes = FontAttributes.Bold,
            BackgroundColor = background,
            TextColor = foreground
        };
    }

    private static Button CreateSecondaryButton(string text)
    {
        var button = CreateButton(text, AppColors.SurfaceBase, AppColors.Brand800);
        button.BorderColor = AppColors.Brand800;
        button.BorderWidth = 1;
        return button;
    }

    private static void ApplyPrimaryButtonStyle(Button button)
    {
        button.HeightRequest = 50;
        button.MinimumHeightRequest = 50;
        button.CornerRadius = 8;
        button.Padding = new Thickness(18, 0);
        button.BackgroundColor = AppColors.Brand800;
        button.TextColor = AppColors.TextInverse;
        button.BorderWidth = 0;
        button.HorizontalOptions = LayoutOptions.Fill;
    }

    /// <summary>
    /// Single-action dialogs use one strong, unmistakable dismissal action.
    /// The label is supplied by each caller; this method changes only the
    /// button's visual treatment.
    /// </summary>
    private static void ApplySingleActionButtonStyle(Button button)
    {
        button.HeightRequest = 50;
        button.MinimumHeightRequest = 50;
        button.CornerRadius = 8;
        button.Padding = new Thickness(18, 0);
        button.BackgroundColor = AppColors.Brand800;
        button.TextColor = AppColors.TextInverse;
        button.BorderWidth = 0;
        button.HorizontalOptions = LayoutOptions.Fill;
    }

    private static bool IsSuccess(string title)
    {
        return title.Contains("success", StringComparison.OrdinalIgnoreCase) ||
               title.Contains("saved", StringComparison.OrdinalIgnoreCase) ||
               title.Contains("completed", StringComparison.OrdinalIgnoreCase) ||
               title.Contains("acknowledged", StringComparison.OrdinalIgnoreCase) ||
               title.Contains("created", StringComparison.OrdinalIgnoreCase) ||
               title.Contains("updated", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDestructive(string title)
    {
        return title.Contains("delete", StringComparison.OrdinalIgnoreCase) ||
               title.Contains("logout", StringComparison.OrdinalIgnoreCase) ||
               title.Contains("remove", StringComparison.OrdinalIgnoreCase) ||
               title.Contains("mark incomplete", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProblem(string title)
    {
        return title.Contains("error", StringComparison.OrdinalIgnoreCase) ||
               title.Contains("unable", StringComparison.OrdinalIgnoreCase) ||
               title.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
               title.Contains("required", StringComparison.OrdinalIgnoreCase) ||
               title.Contains("couldn't", StringComparison.OrdinalIgnoreCase) ||
               title.Contains("can't", StringComparison.OrdinalIgnoreCase) ||
               title.Contains("unavailable", StringComparison.OrdinalIgnoreCase) ||
               title.Contains("invalid", StringComparison.OrdinalIgnoreCase) ||
               title.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
               title.Contains("permission", StringComparison.OrdinalIgnoreCase);
    }
}
