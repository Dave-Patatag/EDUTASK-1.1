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
        string cancelText)
    {
        return await ShowCoreAsync(owner, title, message, acceptText, cancelText);
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
        var accent = Color.FromArgb("#DC2626");
        var themePrimary = Color.FromArgb("#5D6D7E");

        var modal = new ContentPage
        {
            BackgroundColor = Colors.Transparent,
            Padding = 0
        };

        var iconCircle = new Border
        {
            WidthRequest = 42,
            HeightRequest = 42,
            BackgroundColor = Color.FromArgb("#FEF2F2"),
            StrokeThickness = 0,
            HorizontalOptions = LayoutOptions.Center,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 21 },
            Content = new Label
            {
                Text = "!",
                TextColor = accent,
                FontSize = 20,
                FontAttributes = FontAttributes.Bold,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            }
        };

        var editor = new Editor
        {
            Placeholder = "What should the teacher update?",
            MaxLength = maxLength,
            HeightRequest = 96,
            AutoSize = EditorAutoSizeOption.Disabled,
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#111827"),
            PlaceholderColor = Color.FromArgb("#9CA3AF"),
            FontSize = 14,
            Margin = new Thickness(4, 1)
        };

        var inputBorder = new Border
        {
            BackgroundColor = Color.FromArgb("#F9FAFB"),
            Stroke = Color.FromArgb("#D1D5DB"),
            StrokeThickness = 1,
            Padding = new Thickness(8, 4),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 9 },
            Content = editor
        };

        var errorLabel = new Label
        {
            Text = "A reason is required.",
            FontSize = 11,
            TextColor = accent,
            IsVisible = false
        };

        var primaryButton = CreateButton(primaryText, themePrimary, Colors.White);
        primaryButton.FontSize = 12;
        primaryButton.Padding = new Thickness(8, 0);
        var cancelButton = CreateButton(cancelText, Color.FromArgb("#E5E7EB"), Color.FromArgb("#111827"));
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
            inputBorder.Stroke = hasReason ? Color.FromArgb("#D1D5DB") : inputBorder.Stroke;
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
        var promptBackdrop = new BoxView { Color = Color.FromArgb("#80000000") };
        var promptBackdropTap = new TapGestureRecognizer();
        promptBackdropTap.Tapped += async (_, _) => await CloseAsync(null);
        promptBackdrop.GestureRecognizers.Add(promptBackdropTap);

        var buttons = new Grid
        {
            ColumnSpacing = 10,
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(0.9, GridUnitType.Star)),
                new ColumnDefinition(new GridLength(1.1, GridUnitType.Star))
            }
        };
        buttons.Add(cancelButton);
        buttons.Add(primaryButton, 1);

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
                    BackgroundColor = Colors.White,
                    Stroke = Color.FromArgb("#FEE2E2"),
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
                                FontSize = 18,
                                FontAttributes = FontAttributes.Bold,
                                TextColor = Color.FromArgb("#111827"),
                                HorizontalTextAlignment = TextAlignment.Center
                            },
                            new Label
                            {
                                Text = message,
                                FontSize = 13,
                                TextColor = Color.FromArgb("#4B5563"),
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
        await owner.Navigation.PushModalAsync(modal);
        editor.Focus();
        return await completion.Task;
    }
    public static async Task<bool> ShowTaskReminderAsync(Page owner, IReadOnlyList<(string Title, string Priority)> dueToday, IReadOnlyList<(string Title, string Priority)> dueTomorrow, DateTime today)
    {
var completion = new TaskCompletionSource<bool>();
        var overlay = new Grid { BackgroundColor = Color.FromArgb("#99000000"), Padding = new Thickness(20), ZIndex = 1000 };

        var icon = new Border
        {
            WidthRequest = 42, HeightRequest = 42, BackgroundColor = Color.FromArgb("#EEF4FF"), StrokeThickness = 0,
            HorizontalOptions = LayoutOptions.Center,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 21 },
            Content = new Label { Text = "!", FontSize = 20, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#2563EB"), HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center }
        };
        var header = new VerticalStackLayout
        {
            Spacing = 5, HorizontalOptions = LayoutOptions.Center,
            Children =
            {
                icon,
                new Label { Text = "Task reminder", FontSize = 19, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#243447"), HorizontalTextAlignment = TextAlignment.Center }
            }
        };
        var taskContent = new VerticalStackLayout { Spacing = 10 };

        void AddSection(IReadOnlyList<(string Title, string Priority)> tasks, bool isToday)
        {
            if (tasks.Count == 0) return;
            DateTime date = isToday ? today : today.AddDays(1);
            taskContent.Children.Add(new Label
            {
                Text = date.ToString("MMMM d, yyyy"), FontSize = 15, FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#526273"), Margin = new Thickness(0, 2, 0, 2)
            });
            foreach (var task in tasks)
            {
                string priority = string.IsNullOrWhiteSpace(task.Priority) ? "Unassigned" : task.Priority;
                Color priorityColor = priority.ToLowerInvariant() switch
                {
                    "high" => Color.FromArgb("#DC2626"),
                    "medium" => Color.FromArgb("#D97706"),
                    "low" => Color.FromArgb("#16803A"),
                    _ => Color.FromArgb("#687786")
                };
                var row = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) }, ColumnSpacing = 8 };
                row.Add(new VerticalStackLayout
                {
                    Spacing = 2,
                    Children =
                    {
                        new Label { Text = task.Title, FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#243447") },
                        new Label { Text = isToday ? "Due today" : "Due tomorrow", FontSize = 11, TextColor = isToday ? Color.FromArgb("#B42318") : Color.FromArgb("#687786") }
                    }
                });
                row.Add(new Border
                {
                    Padding = new Thickness(8, 3), BackgroundColor = Colors.Transparent, Stroke = priorityColor, StrokeThickness = 1,
                    VerticalOptions = LayoutOptions.Center,
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
                    Content = new Label { Text = priority, FontSize = 10, FontAttributes = FontAttributes.Bold, TextColor = priorityColor }
                }, 1);
                taskContent.Children.Add(new Border
                {
                    BackgroundColor = Color.FromArgb("#F7F9FC"), Stroke = Color.FromArgb("#DCE4ED"), StrokeThickness = 1,
                    Padding = new Thickness(12, 11), StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 }, Content = row
                });
            }
        }

        AddSection(dueToday, true);
        AddSection(dueTomorrow, false);
        bool isClosing = false;
        var viewTasksButton = CreateButton("View tasks", Color.FromArgb("#5D6D7E"), Colors.White);
        viewTasksButton.WidthRequest = 170;
        viewTasksButton.Margin = new Thickness(0, 14, 0, 0);
        viewTasksButton.HorizontalOptions = LayoutOptions.Center;
        viewTasksButton.Clicked += (_, _) =>
        {
            if (isClosing) return;
            isClosing = true;

            completion.TrySetResult(true);
        };

        var dialogLayout = new Grid
        {
            RowSpacing = 12,
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) }
        };
        dialogLayout.Add(header);
        dialogLayout.Add(new ScrollView { Content = taskContent }, 0, 1);
        dialogLayout.Add(viewTasksButton, 0, 2);

        int taskCount = dueToday.Count + dueTomorrow.Count;
        int sectionCount = (dueToday.Count > 0 ? 1 : 0) + (dueTomorrow.Count > 0 ? 1 : 0);
        double desiredHeight = Math.Min(378, 198 + Math.Min(taskCount, 2) * 72 + sectionCount * 28);
        var reminderBackdrop = new BoxView { Color = Colors.Transparent };
        var reminderBackdropTap = new TapGestureRecognizer();
        reminderBackdropTap.Tapped += (_, _) =>
        {
            if (isClosing) return;
            isClosing = true;

            completion.TrySetResult(false);
        };
        reminderBackdrop.GestureRecognizers.Add(reminderBackdropTap);
        overlay.BackgroundColor = Colors.Transparent;
        overlay.Children.Add(reminderBackdrop);
        overlay.Children.Add(new Border
        {
            WidthRequest = Math.Min(360, Math.Max(300, owner.Width - 40)), MaximumWidthRequest = 360, HeightRequest = Math.Min(desiredHeight, Math.Max(260, owner.Height - 48)), MaximumHeightRequest = 378,
            Padding = new Thickness(20), BackgroundColor = Colors.White, StrokeThickness = 0,
            HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 }, Content = dialogLayout
        });
        var modal = new ContentPage
        {
            BackgroundColor = Color.FromArgb("#99000000"),
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
        string? secondaryText)
    {
        var completion = new TaskCompletionSource<bool>();
        bool isProblem = IsProblem(title);
        bool isSuccess = !isProblem && IsSuccess(title);
        bool isDestructive = IsDestructive(title);
        Color themePrimary = Color.FromArgb("#5D6D7E");
        Color accent = Color.FromArgb(isSuccess ? "#27AE60" : isDestructive || isProblem ? "#EF4444" : "#5D6D7E");
        Color softAccent = Color.FromArgb(isSuccess ? "#ECFDF5" : isDestructive || isProblem ? "#FEF2F2" : "#EEF1F4");

        var modal = new ContentPage
        {
            BackgroundColor = Colors.Transparent,
            Padding = 0
        };

        var icon = new Label
        {
            Text = isSuccess ? "\u2713" : isDestructive || isProblem ? "!" : "i",
            TextColor = Colors.White,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center
        };

        var iconCircle = new Border
        {
            WidthRequest = 42,
            HeightRequest = 42,
            BackgroundColor = accent,
            StrokeThickness = 0,
            HorizontalOptions = LayoutOptions.Center,
            Content = icon,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 21 }
        };

        var titleLabel = new Label
        {
            Text = title,
            FontSize = 18,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#111827"),
            HorizontalTextAlignment = TextAlignment.Center
        };

        var messageLabel = new Label
        {
            Text = message,
            FontSize = 14,
            TextColor = Color.FromArgb("#4B5563"),
            HorizontalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.WordWrap
        };

        var primaryButton = CreateButton(primaryText, isDestructive ? accent : themePrimary, Colors.White);
        var secondaryButton = secondaryText is null
            ? null
            : CreateButton(secondaryText, Color.FromArgb("#E5E7EB"), Color.FromArgb("#111827"));

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
                await modal.Navigation.PopModalAsync(false);
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
            Color = Color.FromArgb("#80000000"),
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill
        };
        var alertBackdropTap = new TapGestureRecognizer();
        alertBackdropTap.Tapped += async (_, _) => await CloseAsync(false);
        alertBackdrop.GestureRecognizers.Add(alertBackdropTap);
        if (secondaryButton is not null)
            secondaryButton.Clicked += async (_, _) => await CloseAsync(false);

        var buttons = new Grid
        {
            ColumnSpacing = 10,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            }
        };

        if (secondaryButton is null)
        {
            buttons.ColumnDefinitions.RemoveAt(1);
            buttons.Add(primaryButton);
        }
        else
        {
            buttons.Add(secondaryButton);
            buttons.Add(primaryButton, 1);
        }

        var content = new VerticalStackLayout
        {
            Spacing = 14,
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
                    Padding = new Thickness(22),
                    BackgroundColor = Colors.White,
                    Stroke = softAccent,
                    StrokeThickness = 1,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 },
                    Content = content
                }
            }
        };

        modal.Disappearing += (_, _) => completion.TrySetResult(closingResult ?? false);
        await owner.Navigation.PushModalAsync(modal);
        return await completion.Task;
    }

    private static Button CreateButton(string text, Color background, Color foreground)
    {
        return new Button
        {
            Text = text,
            HeightRequest = 46,
            CornerRadius = 9,
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            BackgroundColor = background,
            TextColor = foreground
        };
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
