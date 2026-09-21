using EDUTASK_1._1.Models;
using EDUTASK_1._1.Helpers;

namespace EDUTASK_1._1.Views;

public sealed class CompletionHistoryPage : ContentPage
{
    private static readonly Color Accent = AppColors.Slate400;
    private static readonly Color Text = AppColors.TextPrimary;
    private static readonly Color Muted = AppColors.Slate300;

    public CompletionHistoryPage(IReadOnlyList<DashboardTaskItem> tasks, bool showTeacherFilter = false)
    {
        Title = "Completion History";
        BackgroundColor = AppColors.SurfaceMuted;
        NavigationPage.SetHasNavigationBar(this, false);

        string selectedTeacher = "All teachers";
        var menuButton = new FlyoutMenuButton();

        var headerText = new VerticalStackLayout
        {
            Spacing = 2,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label
                {
                    Text = "Completion History",
                    FontSize = AppTypography.Heading,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Text,
                    HorizontalOptions = LayoutOptions.Fill,
                    HorizontalTextAlignment = TextAlignment.Center,
                    VerticalTextAlignment = TextAlignment.Center
                }
            }
        };
        var header = new Grid
        {
            Padding = new Thickness(15, 13),
            BackgroundColor = AppColors.SurfaceMuted,
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(42)),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(42))
            }
        };
        header.Add(menuButton);
        header.Add(headerText, 1);

        var completedTasks = tasks.Where(task => task.Completed_at.HasValue).ToList();
        var teachers = completedTasks
            .SelectMany(task => TeacherNames(task.TeacherName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name)
            .ToList();
        var timeline = new VerticalStackLayout { Padding = new Thickness(18, 6, 18, 28), Spacing = 0 };

        void RenderTimeline()
        {
            timeline.Children.Clear();
            bool allTeachers = selectedTeacher == "All teachers";
            List<DashboardTaskItem> filteredTasks = completedTasks
                .Where(task => allTeachers || TeacherNames(task.TeacherName).Contains(selectedTeacher, StringComparer.OrdinalIgnoreCase))
                .ToList();

            if (filteredTasks.Count == 0)
            {
                timeline.Children.Add(new Label
                {
                    Text = allTeachers ? "No completed tasks yet." : "No completed tasks found for this teacher.",
                    FontSize = AppTypography.BodySmall,
                    TextColor = Muted,
                    HorizontalTextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 50)
                });
                return;
            }

            foreach (var dateGroup in filteredTasks.GroupBy(task => task.Completed_at!.Value.Date).OrderByDescending(group => group.Key))
            {
                var groupGrid = new Grid
                {
                    ColumnDefinitions = { new ColumnDefinition(new GridLength(22)), new ColumnDefinition(GridLength.Star) },
                    ColumnSpacing = 10
                };
                var rail = new Grid { RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star) } };
                rail.Add(new Border
                {
                    WidthRequest = 11,
                    HeightRequest = 11,
                    BackgroundColor = AppColors.SurfaceSunken,
                    Stroke = Accent,
                    StrokeThickness = 2,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Start,
                    Margin = new Thickness(0, 4, 0, 0),
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 6 }
                });
                rail.Add(new BoxView
                {
                    Color = Accent,
                    WidthRequest = 2,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Fill
                }, 0, 1);
                groupGrid.Add(rail);

                var groupContent = new VerticalStackLayout { Spacing = 7, Margin = new Thickness(0, 0, 0, 14) };
                groupContent.Children.Add(new Label
                {
                    Text = dateGroup.Key.ToString("yyyy/MM/dd"),
                    FontSize = AppTypography.Body,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Accent
                });

                foreach (DashboardTaskItem task in dateGroup.OrderByDescending(item => item.Completed_at))
                {
                    var check = new Border
                    {
                        WidthRequest = 22,
                        HeightRequest = 22,
                        BackgroundColor = AppColors.StatusSuccess,
                        StrokeThickness = 0,
                        VerticalOptions = LayoutOptions.Center,
                        StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 11 },
                        Content = new Label
                        {
                            Text = "\u2713",
                            FontSize = AppTypography.Caption,
                            TextColor = AppColors.SurfaceBase,
                            HorizontalTextAlignment = TextAlignment.Center,
                            VerticalTextAlignment = TextAlignment.Center
                        }
                    };
                    var labels = new VerticalStackLayout { Spacing = 1 };
                    labels.Children.Add(new Label { Text = task.Title, FontSize = AppTypography.BodySmall, TextColor = AppColors.TextSecondary, TextDecorations = TextDecorations.Strikethrough });
                    if (showTeacherFilter && allTeachers)
                        labels.Children.Add(new Label { Text = task.TeacherName, FontSize = AppTypography.Micro, TextColor = Accent });
                    labels.Children.Add(new Label { Text = task.Completed_at!.Value.ToString("h:mm tt"), FontSize = AppTypography.Micro, TextColor = AppColors.TextDisabled });

                    var row = new Grid
                    {
                        ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                        ColumnSpacing = 11
                    };
                    row.Add(check);
                    row.Add(labels, 1);
                    row.Add(new Label { Text = "\u203A", FontSize = 22, TextColor = AppColors.Slate200, VerticalTextAlignment = TextAlignment.Center }, 2);

                    var card = new Border
                    {
                        BackgroundColor = AppColors.SurfaceBase,
                        Stroke = AppColors.BorderSubtle,
                        StrokeThickness = 1,
                        Padding = new Thickness(12, 10),
                        StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                        Shadow = new Shadow
                        {
                            Brush = Colors.Black,
                            Offset = new Point(0, 2),
                            Radius = 5,
                            Opacity = 0.09f
                        },
                        Content = row
                    };
                    var tap = new TapGestureRecognizer();
                    tap.Tapped += async (_, _) => await Navigation.PushModalAsync(new EditTaskPage(task.Task_id, true), false);
                    card.GestureRecognizers.Add(tap);
                    groupContent.Children.Add(card);
                }
                groupGrid.Add(groupContent, 1);
                timeline.Children.Add(groupGrid);
            }
        }

        var main = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            }
        };
        main.Add(header);
        main.Add(new ScrollView { Content = timeline }, 0, 2);

        var pageRoot = new Grid();
        pageRoot.Add(main);

        var selectorContent = new Label
        {
            Text = selectedTeacher,
            FontSize = AppTypography.Caption,
            TextColor = AppColors.TextInverse,
            VerticalTextAlignment = TextAlignment.Center,
            HorizontalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.TailTruncation
        };
        var selectorButtonContent = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(16)),
                new ColumnDefinition(GridLength.Star)
            },
            ColumnSpacing = 7,
            Children =
            {
                new Image
                {
                    Source = "whitefiltericon.png",
                    WidthRequest = 14,
                    HeightRequest = 14,
                    VerticalOptions = LayoutOptions.Center,
                    HorizontalOptions = LayoutOptions.Center
                },
                selectorContent
            }
        };
        Grid.SetColumn(selectorContent, 1);
        var selector = new Border
        {
            IsVisible = showTeacherFilter,
            Margin = new Thickness(18, 0, 18, 10),
            Padding = new Thickness(12, 0),
            HeightRequest = 36,
            MinimumHeightRequest = 36,
            MinimumWidthRequest = 124,
            MaximumWidthRequest = 210,
            BackgroundColor = AppColors.Brand800,
            Stroke = Colors.Transparent,
            StrokeThickness = 0,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Center,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 9 },
            Content = selectorButtonContent
        };

        void ApplySelectorState(bool active)
        {
            selector.BackgroundColor = AppColors.Brand800;
            selector.Stroke = Colors.Transparent;
            selectorContent.TextColor = AppColors.TextInverse;
            selectorContent.Text = selectedTeacher;
            selectorContent.FontAttributes = active ? FontAttributes.Bold : FontAttributes.None;
        }

        ApplySelectorState(active: false);
        main.Add(selector, 0, 1);
        var results = new VerticalStackLayout { Spacing = 2 };
        var search = new Entry
        {
            Placeholder = "Search teacher name",
            FontSize = AppTypography.BodySmall,
            TextColor = Text,
            PlaceholderColor = Muted,
            BackgroundColor = Colors.Transparent,
            VerticalTextAlignment = TextAlignment.Center,
            VerticalOptions = LayoutOptions.Fill,
            HorizontalOptions = LayoutOptions.Fill,
            Margin = 0,
            ClearButtonVisibility = ClearButtonVisibility.Never
        };
        search.HandlerChanged += (_, _) =>
        {
#if WINDOWS
            if (search.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.TextBox windowsEntry)
            {
                windowsEntry.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
                var transparent = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
                windowsEntry.Resources["TextControlBorderBrush"] = transparent;
                windowsEntry.Resources["TextControlBorderBrushPointerOver"] = transparent;
                windowsEntry.Resources["TextControlBorderBrushFocused"] = transparent;
            }
#elif ANDROID
            if (search.Handler?.PlatformView is Android.Widget.EditText androidEntry)
                androidEntry.BackgroundTintList =
                    Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
#elif IOS || MACCATALYST
            if (search.Handler?.PlatformView is UIKit.UITextField appleEntry)
                appleEntry.BorderStyle = UIKit.UITextBorderStyle.None;
#endif
        };

        var searchIcon = new Image
        {
            Source = "search.png",
            WidthRequest = 18,
            HeightRequest = 18,
            Aspect = Aspect.AspectFit,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.End
        };
        var focusSearch = new TapGestureRecognizer();
        focusSearch.Tapped += (_, _) => search.Focus();
        searchIcon.GestureRecognizers.Add(focusSearch);

        var searchContent = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(18))
            },
            ColumnSpacing = 8
        };
        searchContent.Add(search);
        searchContent.Add(searchIcon, 1);

        var searchField = new Border
        {
            HeightRequest = 44,
            MinimumHeightRequest = 44,
            Padding = new Thickness(12, 0),
            BackgroundColor = AppColors.SurfaceMuted,
            Stroke = AppColors.BorderDefault,
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
            Content = searchContent
        };
        var overlay = new Grid
        {
            IsVisible = false,
            BackgroundColor = AppColors.ScrimLight,
            Padding = new Thickness(24)
        };
        var outsideButton = new Button
        {
            BackgroundColor = Colors.Transparent,
            BorderWidth = 0
        };
        overlay.Add(outsideButton);

        void HideSelector()
        {
            overlay.IsVisible = false;
            search.Unfocus();
            search.Text = string.Empty;
            ApplySelectorState(!string.Equals(selectedTeacher, "All teachers", StringComparison.Ordinal));
        }

        void RenderTeacherResults(string query = "")
        {
            results.Children.Clear();
            IEnumerable<string> options = new[] { "All teachers" }.Concat(teachers);
            if (!string.IsNullOrWhiteSpace(query))
                options = options.Where(name => name.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase));

            foreach (string teacher in options)
            {
                var name = new Label
                {
                    Text = teacher,
                    FontSize = AppTypography.BodySmall,
                    FontAttributes = teacher == selectedTeacher ? FontAttributes.Bold : FontAttributes.None,
                    TextColor = Text,
                    VerticalTextAlignment = TextAlignment.Center
                };
                var row = new Border
                {
                    Padding = new Thickness(10, 7),
                    BackgroundColor = teacher == selectedTeacher ? AppColors.SurfaceSunken : Colors.Transparent,
                    StrokeThickness = 0,
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                    Content = name
                };
                var choose = new TapGestureRecognizer();
                choose.Tapped += (_, _) =>
                {
                    selectedTeacher = teacher;
                    RenderTimeline();
                    HideSelector();
                };
                row.GestureRecognizers.Add(choose);
                results.Children.Add(row);
            }

            if (results.Children.Count == 0)
                results.Children.Add(new Label { Text = "No teachers found.", FontSize = AppTypography.BodySmall, TextColor = Muted, HorizontalTextAlignment = TextAlignment.Center, Margin = new Thickness(0, 24) });
        }

        var dialogTitle = new Label
        {
            Text = "Filter by teacher",
            FontSize = AppTypography.Body,
            FontAttributes = FontAttributes.None,
            TextColor = Text,
            HorizontalOptions = LayoutOptions.Fill,
            HorizontalTextAlignment = TextAlignment.Center
        };
        var resultScroller = new ScrollView
        {
            Content = results,
            MaximumHeightRequest = 360
        };
        var dialogContent = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            },
            RowSpacing = 8
        };
        dialogContent.Add(dialogTitle);
        dialogContent.Add(searchField, 0, 1);
        dialogContent.Add(resultScroller, 0, 2);

        var dialog = new Border
        {
            MaximumWidthRequest = 400,
            MaximumHeightRequest = 520,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Center,
            Padding = new Thickness(14),
            BackgroundColor = AppColors.SurfaceBase,
            Stroke = AppColors.BorderSubtle,
            StrokeThickness = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 16 },
            Content = dialogContent
        };
        overlay.Add(dialog);
        pageRoot.Add(overlay);

        outsideButton.Clicked += (_, _) => HideSelector();
        search.TextChanged += (_, eventArgs) => RenderTeacherResults(eventArgs.NewTextValue ?? string.Empty);
        var openSelector = new TapGestureRecognizer();
        openSelector.Tapped += (_, _) =>
        {
            RenderTeacherResults();
            ApplySelectorState(active: true);
            overlay.IsVisible = true;
            search.Focus();
        };
        selector.GestureRecognizers.Add(openSelector);

        RenderTimeline();

        var shell = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            }
        };
        shell.Add(pageRoot);
        // History is a detail destination, not the active task dashboard.
        // Leaving the task tab unselected also keeps the destination state
        // consistent with the back button and the shared navigation bar.
        shell.Add(new BottomNavigationBar { ActiveTab = BottomNavigationTab.None }, 0, 1);
        Content = shell;
    }

    private static IEnumerable<string> TeacherNames(string teacherNames) =>
        (teacherNames ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
