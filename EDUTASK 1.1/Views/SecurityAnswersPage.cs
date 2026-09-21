using EDUTASK_1._1.Services;
using EDUTASK_1._1.Helpers;

namespace EDUTASK_1._1.Views;

public sealed class SecurityAnswersPage : ContentPage
{
    private readonly Entry _firstAnswer;
    private readonly Entry _secondAnswer;
    private readonly Border _firstAnswerBorder;
    private readonly Border _secondAnswerBorder;
    private readonly Label _firstAnswerError;
    private readonly Label _secondAnswerError;

    private readonly TaskCompletionSource<(string First, string Second)?> _result = new();

    public SecurityAnswersPage(
        string firstQuestion,
        string secondQuestion,
        string? firstAnswer,
        string? secondAnswer)
    {
        NavigationPage.SetHasNavigationBar(this, false);

        BackgroundColor = AppColors.Scrim;

        _firstAnswer = CreateAnswerEntry(
            "Type your answer",
            firstAnswer);

        _secondAnswer = CreateAnswerEntry(
            "Type your answer",
            secondAnswer);

        _firstAnswerBorder = CreateAnswerField(_firstAnswer);
        _secondAnswerBorder = CreateAnswerField(_secondAnswer);

        _firstAnswerError = CreateErrorLabel();
        _secondAnswerError = CreateErrorLabel();

        _firstAnswer.TextChanged += (_, _) =>
            ClearFieldError(
                _firstAnswerBorder,
                _firstAnswerError);

        _secondAnswer.TextChanged += (_, _) =>
            ClearFieldError(
                _secondAnswerBorder,
                _secondAnswerError);

        var confirmButton = new Button
        {
            Text = "Confirm",

            FontSize = AppTypography.Body,

            FontAttributes = FontAttributes.Bold,

            BackgroundColor = AppColors.StatusSuccess,

            TextColor = AppColors.SurfaceBase,

            CornerRadius = 8,

            HeightRequest = 50,

            MinimumHeightRequest = 50,

            HorizontalOptions = LayoutOptions.Fill
        };

        confirmButton.Clicked += OnConfirm;

        var cancelButton = new Button
        {
            Text = "Cancel",

            FontSize = AppTypography.Body,

            FontAttributes = FontAttributes.Bold,

            BackgroundColor = AppColors.ActionDismiss,

            TextColor = AppColors.SurfaceBase,

            CornerRadius = 8,

            HeightRequest = 50,

            MinimumHeightRequest = 50,

            HorizontalOptions = LayoutOptions.Fill
        };

        cancelButton.Clicked += OnCancel;

        var actionButtons = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },

            ColumnSpacing = 12,

            Margin = new Thickness(0, 18, 0, 2),

            Children =
            {
                cancelButton,
                confirmButton
            }
        };

        Grid.SetColumn(confirmButton, 1);

        var content = new VerticalStackLayout
        {
            Spacing = 0
        };

        content.Add(
            CreateQuestionLabel(firstQuestion));

        content.Add(
            _firstAnswerBorder);

        content.Add(
            _firstAnswerError);

        content.Add(
            CreateQuestionLabel(secondQuestion));

        content.Add(
            _secondAnswerBorder);

        content.Add(
            _secondAnswerError);

        content.Add(
            actionButtons);

        Content = new Grid
        {
            Padding = new Thickness(24),

            Children =
            {
                new Border
                {
                    HeightRequest = 370,

                    BackgroundColor =
                        AppColors.SurfaceBase,

                    StrokeThickness = 0,

                    StrokeShape =
                        new Microsoft.Maui.Controls.Shapes.RoundRectangle
                        {
                            CornerRadius = 16
                        },

                    Padding =
                        new Thickness(
                             22,
                            28,
                             22,
                             20),

                    VerticalOptions =
                        LayoutOptions.Center,

                    Content = content
                }
            }
        };
    }

    public async System.Threading.Tasks.Task<
        (string First, string Second)?>
        ShowAsync(INavigation navigation)
    {
        await navigation.PushModalAsync(
            this,
            false);

        return await _result.Task;
    }

    private static Label CreateQuestionLabel(
        string question)
    {
        return new Label
        {
            Text = $"Q: {question}",

            FontSize = AppTypography.Body,

            FontAttributes =
                FontAttributes.Bold,

            TextColor =
                AppColors.TextPrimary,

            Margin =
                new Thickness(
                    0,
                    0,
                    0,
                    7)
        };
    }

    private static Entry CreateAnswerEntry(
        string placeholder,
        string? answer)
    {
        var entry = new Entry
        {
            Placeholder = placeholder,

            Text = answer,

            IsPassword = false
        };

        AppStyles.Apply(
            entry,
            "AuthFieldEntry");

        return entry;
    }

    /// <summary>
    /// Creates the same answer field style used
    /// throughout the authentication pages.
    /// </summary>
    private static Border CreateAnswerField(
        Entry entry)
    {
        var icon = new Image
        {
            Source =
                "loginsecurityquestion.png"
        };

        AppStyles.Apply(
            icon,
            "AuthFieldIcon");

        var content = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition
                {
                    Width =
                        new GridLength(24)
                },

                new ColumnDefinition
                {
                    Width =
                        GridLength.Star
                }
            },

            ColumnSpacing = 8,

            Children =
            {
                icon,
                entry
            }
        };

        Grid.SetColumn(
            entry,
            1);

        var field = new Border
        {
            Margin =
                new Thickness(
                    0,
                    0,
                    0,
                    2),

            Content = content
        };

        AppStyles.Apply(
            field,
            "AuthField");

        return field;
    }

    private static Label CreateErrorLabel()
    {
        var label = new Label();

        AppStyles.Apply(
            label,
            "AuthFieldError");

        label.Margin =
            new Thickness(
                13,
                2,
                13,
                12);

        label.TextColor =
            FormFieldValidation
                .ErrorStrokeColor;

        return label;
    }

    private static void SetFieldError(
        Border border,
        Label label)
    {
        FormFieldValidation.SetFieldError(
            border,
            label,
            "Enter your answer.");
    }

    private static void ClearFieldError(
        Border border,
        Label label)
    {
        FormFieldValidation.ClearFieldError(
            border,
            label);
    }

    private async void OnConfirm(
        object? sender,
        EventArgs e)
    {
        string first =
            _firstAnswer
                .Text?
                .Trim()
            ?? string.Empty;

        string second =
            _secondAnswer
                .Text?
                .Trim()
            ?? string.Empty;

        ClearFieldError(
            _firstAnswerBorder,
            _firstAnswerError);

        ClearFieldError(
            _secondAnswerBorder,
            _secondAnswerError);

        if (first.Length == 0)
        {
            SetFieldError(
                _firstAnswerBorder,
                _firstAnswerError);
        }

        if (second.Length == 0)
        {
            SetFieldError(
                _secondAnswerBorder,
                _secondAnswerError);
        }

        if (first.Length == 0 ||
            second.Length == 0)
        {
            return;
        }

        _result.TrySetResult(
            (first, second));

        await Navigation.PopModalAsync(
            false);
    }

    private async void OnCancel(
        object? sender,
        EventArgs e)
    {
        _result.TrySetResult(null);

        await Navigation.PopModalAsync(
            false);
    }
}
