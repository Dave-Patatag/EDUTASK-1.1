using System.Threading.Tasks;

namespace EDUTASK_1._1.Helpers
{
    /// <summary>
    /// Drives every select-a-value dropdown in the app: the role pickers on the
    /// authentication pages and the period filter on the Summary Report.
    ///
    /// The auth screens each carried their own copy of this logic, which is why
    /// they had drifted apart — different row heights, different chevron resting
    /// angles, and none of them closed when you tapped somewhere else. This is
    /// the one implementation; pages supply the markup and get the behaviour.
    ///
    /// What it handles:
    ///   * animated open/close, with the chevron turning to match
    ///   * dismissal when the user taps outside the panel
    ///   * selection state shown by a tinted row and emphasized text
    ///   * screen-reader names on the option buttons, which were previously
    ///     empty because the tap target is a Button with no Text
    /// </summary>
    public sealed class DropdownController
    {
        readonly Grid _fieldContainer;
        readonly Border _field;
        readonly VisualElement? _chevron;
        readonly Border _panel;
        readonly Label? _displayLabel;
        readonly string _placeholder;
        readonly List<OptionRow> _options = new();

        readonly OutsideTapCatcher _outsideTap;
        bool _isOpen;

        /// <param name="chevron">
        /// Optional. A disclosure arrow to turn when the panel opens. Icon-only
        /// triggers (the report filter button) pass null.
        /// </param>
        /// <param name="displayLabel">
        /// Optional. The label on the closed field that shows the current choice.
        /// Menu-style triggers that show only an icon pass null.
        /// </param>
        public DropdownController(
            Grid fieldContainer,
            Border field,
            VisualElement? chevron,
            Border panel,
            Label? displayLabel,
            string placeholder)
        {
            _fieldContainer = fieldContainer;
            _field = field;
            _chevron = chevron;
            _panel = panel;
            _displayLabel = displayLabel;
            _placeholder = placeholder;
            _outsideTap = new OutsideTapCatcher(panel, CloseAsync);

            _panel.IsVisible = false;
            _panel.Opacity = 0;

            if (_chevron is not null)
                _chevron.Rotation = 0;

            SemanticProperties.SetDescription(_field, placeholder);
        }

        /// <summary>The currently chosen value, or empty if nothing is selected.</summary>
        public string SelectedValue { get; private set; } = string.Empty;

        /// <summary>
        /// Colour applied to the display label once something is chosen.
        /// Defaults to the normal body colour, which suits a field on a light
        /// surface; a trigger with a filled background sets this to the inverse
        /// so its label does not turn dark against the fill.
        /// </summary>
        public Color SelectedLabelColor { get; set; } = AppColors.TextPrimary;

        /// <summary>Colour applied to the display label while nothing is chosen.</summary>
        public Color PlaceholderLabelColor { get; set; } = AppColors.TextTertiary;

        public bool IsOpen => _isOpen;

        /// <summary>Raised after the user picks an option.</summary>
        public event EventHandler<string>? SelectionChanged;

        /// <summary>
        /// Raised when the panel opens and closes. Lets a trigger show that it
        /// is the one being interacted with — the task filters fill themselves
        /// while their panel is down.
        /// </summary>
        public event EventHandler? Opened;
        public event EventHandler? Closed;

        /// <summary>Registers one option row.</summary>
        public void AddOption(Border row, Label label, Button hitTarget, string value)
        {
            _options.Add(new OptionRow(row, label, value));

            // The tap target is a transparent Button sitting over the row, so it
            // has no text of its own and would otherwise be announced as an
            // unnamed button.
            hitTarget.CommandParameter = value;
            SemanticProperties.SetDescription(hitTarget, value);
            ToolTipProperties.SetText(hitTarget, value);

            ApplyOptionState(_options[^1], isSelected: false);
        }

        /// <summary>
        /// Replaces the option list with rows built in code, using the same
        /// styles as the hand-written ones. For dropdowns whose choices come
        /// from data — the teacher filter, whose names are not known until the
        /// tasks have loaded.
        /// </summary>
        /// <param name="host">The stack inside the panel that holds the rows.</param>
        public void SetOptions(Layout host, IEnumerable<string> values)
        {
            host.Clear();
            _options.Clear();

            foreach (var value in values)
            {
                var label = new Label { Text = value };
                ApplyStyle(label, "DropdownOptionLabel");

                var hitTarget = new Button
                {
                    BackgroundColor = Colors.Transparent,
                    BorderWidth = 0,
                    CornerRadius = 0,
                };
                var captured = value;
                hitTarget.Clicked += async (_, _) => await SelectAsync(captured);

                var grid = new Grid
                {
                    ColumnDefinitions = { new ColumnDefinition(GridLength.Star) },
                };
                grid.Add(label);
                grid.Add(hitTarget);

                var row = new Border { Content = grid };
                ApplyStyle(row, "DropdownOption");

                host.Add(row);
                AddOption(row, label, hitTarget, value);
            }

            // Keep the selection styling on whatever was already chosen, if it survived.
            if (!string.IsNullOrEmpty(SelectedValue))
            {
                foreach (var option in _options)
                    ApplyOptionState(option, option.Value == SelectedValue);
            }
        }

        static void ApplyStyle(VisualElement element, string key) => AppStyles.Apply(element, key);

        /// <summary>Opens the panel if closed, closes it if open.</summary>
        public Task ToggleAsync() => _isOpen ? CloseAsync() : OpenAsync();

        public async Task OpenAsync()
        {
            if (_isOpen)
                return;

            _isOpen = true;

            // Position the panel just below the field, recomputed on every open
            // so it stays correct after the form reflows.
            //
            // TranslationY, not Margin: translation is a render offset, so the
            // panel can hang outside its grid row without being re-laid-out or
            // clipped. The open animation only touches Opacity and ScaleY, so
            // nothing overwrites this.
            //
            // The offset is measured up the tree to whatever host the page
            // parked the panel in, which lets a page park it somewhere that
            // cannot grow around it — see OverlayPanel.
            _panel.TranslationY = OverlayPanel.TopBelow(_fieldContainer, _panel.Parent);

            _outsideTap.Attach();
            SemanticProperties.SetHint(_field, "Expanded");
            Opened?.Invoke(this, EventArgs.Empty);

            await Task.WhenAll(
                _panel.DropdownOpenAsync(),
                RotateChevronAsync(open: true));
        }

        /// <summary>
        /// Turns the chevron to point up when open, down when closed. Snaps
        /// rather than animates when the OS asks for reduced motion.
        /// </summary>
        Task RotateChevronAsync(bool open)
        {
            if (_chevron is null)
                return Task.CompletedTask;

            var target = open ? 180 : 0;

            if (Motion.ReduceMotion)
            {
                _chevron.Rotation = target;
                return Task.CompletedTask;
            }

            return _chevron.RotateTo(target, open ? Motion.Base : Motion.Fast, Motion.Standard);
        }

        public async Task CloseAsync()
        {
            if (!_isOpen)
                return;

            _isOpen = false;
            _outsideTap.Detach();
            SemanticProperties.SetHint(_field, "Collapsed");
            Closed?.Invoke(this, EventArgs.Empty);

            await Task.WhenAll(
                _panel.DropdownCloseAsync(),
                RotateChevronAsync(open: false));
        }

        /// <summary>Closes the panel immediately, without animating. For form submit paths.</summary>
        public void CloseImmediate()
        {
            _isOpen = false;
            _outsideTap.Detach();
            Closed?.Invoke(this, EventArgs.Empty);
            _panel.IsVisible = false;
            _panel.Opacity = 0;

            if (_chevron is not null)
                _chevron.Rotation = 0;
        }

        /// <summary>
        /// Applies a selection as though the user had picked it, then closes.
        /// </summary>
        public async Task SelectAsync(string value)
        {
            SelectedValue = value;

            if (_displayLabel is not null)
            {
                _displayLabel.Text = value;
                _displayLabel.TextColor = SelectedLabelColor;
            }

            foreach (var option in _options)
                ApplyOptionState(option, option.Value == value);

            SemanticProperties.SetDescription(_field, $"{_placeholder}. {value} selected.");
            SelectionChanged?.Invoke(this, value);

            await CloseAsync();
        }

        /// <summary>
        /// Marks a value as selected without animating, closing, or raising
        /// <see cref="SelectionChanged"/>. For seeding a default at construction
        /// time, before the panel has been laid out.
        /// </summary>
        public void SetInitialSelection(string value)
        {
            SelectedValue = value;

            if (_displayLabel is not null)
            {
                _displayLabel.Text = value;
                _displayLabel.TextColor = SelectedLabelColor;
            }

            foreach (var option in _options)
                ApplyOptionState(option, option.Value == value);

            SemanticProperties.SetDescription(_field, $"{_placeholder}. {value} selected.");
        }

        /// <summary>Returns the field to its unselected, placeholder state.</summary>
        public void Clear()
        {
            SelectedValue = string.Empty;

            if (_displayLabel is not null)
            {
                _displayLabel.Text = _placeholder;
                _displayLabel.TextColor = PlaceholderLabelColor;
            }

            foreach (var option in _options)
                ApplyOptionState(option, isSelected: false);
        }

        static void ApplyOptionState(OptionRow option, bool isSelected)
        {
            option.Row.BackgroundColor = isSelected ? AppColors.Brand50 : Colors.Transparent;
            option.Label.TextColor = isSelected ? AppColors.Brand800 : AppColors.TextPrimary;
            option.Label.FontAttributes = isSelected ? FontAttributes.Bold : FontAttributes.None;
        }

        sealed record OptionRow(Border Row, Label Label, string Value);
    }
}
