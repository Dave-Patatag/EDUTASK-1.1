using EDUTASK_1._1.Models;

namespace EDUTASK_1._1.Views;

public sealed class CompletionHistoryPage : ContentPage
{
    private static readonly Color Accent = Color.FromArgb("#687786");
    private static readonly Color Text = Color.FromArgb("#243447");
    private static readonly Color Muted = Color.FromArgb("#8190A2");

    public CompletionHistoryPage(IReadOnlyList<DashboardTaskItem> tasks, bool showTeacherFilter = false)
    {
        Title = "Completion Time";
        BackgroundColor = Color.FromArgb("#FBFEFF");
        NavigationPage.SetHasNavigationBar(this, false);

        var backButton = new ImageButton
        {
            Source = "backicon.png",
            BackgroundColor = Colors.Transparent,
            Padding = 6,
            WidthRequest = 36,
            HeightRequest = 36,
            HorizontalOptions = LayoutOptions.Start
        };
        SemanticProperties.SetDescription(backButton, "Go back");
        backButton.Clicked += async (_, _) => await Navigation.PopAsync();

        var header = new VerticalStackLayout
        {
            Padding = new Thickness(16, 10, 18, 14),
            Spacing = 4,
            Children =
            {
                backButton,
                new Label
                {
                    Text = "Completion Time",
                    FontSize = 18,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Text,
                    Margin = new Thickness(10, 6, 0, 0)
                }
            }
        };

        var completedTasks = tasks.Where(task => task.CompletedAt.HasValue).ToList();
        var teachers = completedTasks
            .SelectMany(task => TeacherNames(task.TeacherName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name)
            .ToList();
        string selectedTeacher = "All teachers";
        var selectedTeacherLabel = new Label
        {
            Text = selectedTeacher,
            FontSize = 12,
            TextColor = Muted,
            Margin = new Thickness(10, 0, 0, 2)
        };
        var timeline = new VerticalStackLayout { Padding = new Thickness(18, 6, 18, showTeacherFilter ? 80 : 28), Spacing = 0 };

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
                    FontSize = 13,
                    TextColor = Muted,
                    HorizontalTextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 50)
                });
                return;
            }

            foreach (var dateGroup in filteredTasks.GroupBy(task => task.CompletedAt!.Value.Date).OrderByDescending(group => group.Key))
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
                    BackgroundColor = Color.FromArgb("#EEF1F4"),
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
                    FontSize = 14,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Accent
                });

                foreach (DashboardTaskItem task in dateGroup.OrderByDescending(item => item.CompletedAt))
                {
                    var check = new Border
                    {
                        WidthRequest = 22,
                        HeightRequest = 22,
                        BackgroundColor = Color.FromArgb("#DCE5EA"),
                        StrokeThickness = 0,
                        VerticalOptions = LayoutOptions.Center,
                        StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 11 },
                        Content = new Label
                        {
                            Text = "\u2713",
                            FontSize = 12,
                            TextColor = Colors.White,
                            HorizontalTextAlignment = TextAlignment.Center,
                            VerticalTextAlignment = TextAlignment.Center
                        }
                    };
                    var labels = new VerticalStackLayout { Spacing = 1 };
                    labels.Children.Add(new Label { Text = task.Title, FontSize = 13, TextColor = Color.FromArgb("#596574"), TextDecorations = TextDecorations.Strikethrough });
                    if (showTeacherFilter && allTeachers)
                        labels.Children.Add(new Label { Text = task.TeacherName, FontSize = 10, TextColor = Accent });
                    labels.Children.Add(new Label { Text = task.CompletedAt!.Value.ToString("h:mm tt"), FontSize = 10, TextColor = Color.FromArgb("#9AA6B2") });

                    var row = new Grid
                    {
                        ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
                        ColumnSpacing = 11
                    };
                    row.Add(check);
                    row.Add(labels, 1);
                    row.Add(new Label { Text = "\u203A", FontSize = 22, TextColor = Color.FromArgb("#B8C2CC"), VerticalTextAlignment = TextAlignment.Center }, 2);

                    var card = new Border
                    {
                        BackgroundColor = Color.FromArgb("#F4F8FA"),
                        StrokeThickness = 0,
                        Padding = new Thickness(12, 10),
                        StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                        Content = row
                    };
                    var tap = new TapGestureRecognizer();
                    tap.Tapped += async (_, _) => await Navigation.PushModalAsync(new EditTaskPage(task.TaskID, true), false);
                    card.GestureRecognizers.Add(tap);
                    groupContent.Children.Add(card);
                }
                groupGrid.Add(groupContent, 1);
                timeline.Children.Add(groupGrid);
            }
        }

        if (showTeacherFilter)
            header.Children.Add(selectedTeacherLabel);

        var main = new Grid
        {
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star) }
        };
        main.Add(header);
        main.Add(new ScrollView { Content = timeline }, 0, 1);

        var pageRoot = new Grid();
        pageRoot.Add(main);

        var selectorContent = new Label
        {
            Text = "Select teacher",
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            VerticalTextAlignment = TextAlignment.Center,
            HorizontalTextAlignment = TextAlignment.Center
        };
        var selector = new Border
        {
            IsVisible = showTeacherFilter,
            Margin = new Thickness(0, 0, 20, 20),
            Padding = new Thickness(13, 9),
            BackgroundColor = Accent,
            StrokeThickness = 0,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.End,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 18 },
            Shadow = new Shadow { Brush = Color.FromArgb("#40000000"), Offset = new Point(0, 3), Radius = 7, Opacity = 0.3f },
            Content = selectorContent
        };
        pageRoot.Add(selector);
        var results = new VerticalStackLayout { Spacing = 4 };
        var search = new SearchBar
        {
            Placeholder = "Search teacher name",
            FontSize = 14,
            TextColor = Text,
            PlaceholderColor = Muted,
            BackgroundColor = Color.FromArgb("#F4F6F8")
        };
        var overlay = new Grid
        {
            IsVisible = false,
            BackgroundColor = Color.FromArgb("#66000000"),
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
                    FontSize = 14,
                    FontAttributes = teacher == selectedTeacher ? FontAttributes.Bold : FontAttributes.None,
                    TextColor = Text,
                    VerticalTextAlignment = TextAlignment.Center
                };
                var row = new Border
                {
                    Padding = new Thickness(12, 11),
                    BackgroundColor = teacher == selectedTeacher ? Color.FromArgb("#E8EEF3") : Colors.Transparent,
                    StrokeThickness = 0,
                    StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                    Content = name
                };
                var choose = new TapGestureRecognizer();
                choose.Tapped += (_, _) =>
                {
                    selectedTeacher = teacher;
                    selectedTeacherLabel.Text = teacher;
                    RenderTimeline();
                    HideSelector();
                };
                row.GestureRecognizers.Add(choose);
                results.Children.Add(row);
            }

            if (results.Children.Count == 0)
                results.Children.Add(new Label { Text = "No teachers found.", FontSize = 13, TextColor = Muted, HorizontalTextAlignment = TextAlignment.Center, Margin = new Thickness(0, 24) });
        }

        var dialog = new Border
        {
            MaximumWidthRequest = 420,
            MaximumHeightRequest = 520,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Center,
            Padding = new Thickness(18),
            BackgroundColor = Colors.White,
            Stroke = Color.FromArgb("#E0E5EA"),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 18 },
            Content = new Grid
            {
                RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star) },
                RowSpacing = 12,
                Children =
                {
                    new Label { Text = "Select teacher", FontSize = 20, FontAttributes = FontAttributes.Bold, TextColor = Text },
                    search,
                    new ScrollView { Content = results, MaximumHeightRequest = 360 }
                }
            }
        };
        Grid.SetRow(search, 1);
        Grid.SetRow((BindableObject)((Grid)dialog.Content).Children[2], 2);
        overlay.Add(dialog);
        pageRoot.Add(overlay);

        outsideButton.Clicked += (_, _) => HideSelector();
        search.TextChanged += (_, eventArgs) => RenderTeacherResults(eventArgs.NewTextValue ?? string.Empty);
        var openSelector = new TapGestureRecognizer();
        openSelector.Tapped += (_, _) =>
        {
            RenderTeacherResults();
            overlay.IsVisible = true;
            search.Focus();
        };
        selector.GestureRecognizers.Add(openSelector);

        RenderTimeline();
        Content = pageRoot;
    }

    private static IEnumerable<string> TeacherNames(string teacherNames) =>
        (teacherNames ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}