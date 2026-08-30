using System.Threading.Tasks;

namespace EDUTASK_1._1.Helpers
{
    /// <summary>
    /// Drives an overflow menu — the "..." button that drops a short list of
    /// actions, as on the Profile page.
    ///
    /// This is the action-shaped sibling of <see cref="DropdownController"/>.
    /// That one answers "which value?" and so keeps and highlights a selection.
    /// A menu answers "do what?": each row fires once and nothing stays chosen.
    ///
    /// Everything below the semantics is shared on purpose — the DropdownPanel
    /// and DropdownOption styles, the open/close motion, and the outside-tap
    /// dismissal — so a menu opens exactly like every other panel in the app.
    /// </summary>
    public sealed class ActionMenuController
    {
        readonly Border _panel;
        readonly Layout _optionHost;
        readonly VisualElement? _anchor;
        readonly OutsideTapCatcher _outsideTap;

        bool _isOpen;

        /// <param name="panel">The bordered panel, styled with DropdownPanel.</param>
        /// <param name="optionHost">The stack inside the panel that holds the rows.</param>
        /// <param name="anchor">
        /// Optional. The trigger the panel should hang beneath. When supplied the
        /// panel is positioned on every open, so it stays put after a reflow;
        /// when null the panel keeps whatever position its markup gives it.
        /// </param>
        public ActionMenuController(Border panel, Layout optionHost, VisualElement? anchor = null)
        {
            _panel = panel;
            _optionHost = optionHost;
            _anchor = anchor;
            _outsideTap = new OutsideTapCatcher(panel, CloseAsync);

            _panel.IsVisible = false;
            _panel.Opacity = 0;
        }

        public bool IsOpen => _isOpen;

        /// <summary>
        /// Replaces the menu with the given actions, in order. Each runs after
        /// the panel has closed, so a handler that opens a page or a dialog is
        /// never racing the closing animation.
        /// </summary>
        public void SetActions(IEnumerable<(string Label, Func<Task> Invoke)> actions)
        {
            SetActions(actions.Select(action =>
                (action.Label, action.Invoke, TextColor: (Color?)null)));
        }

        /// <summary>
        /// Replaces the menu with actions that may override the standard label
        /// colour. Destructive actions use this to remain visually distinct
        /// without changing the appearance of ordinary menu options.
        /// </summary>
        public void SetActions(
            IEnumerable<(string Label, Func<Task> Invoke, Color? TextColor)> actions)
        {
            _optionHost.Clear();

            foreach (var (label, invoke, textColor) in actions)
            {
                var text = new Label { Text = label };
                AppStyles.Apply(text, "DropdownOptionLabel");
                if (textColor is not null)
                    text.TextColor = textColor;

                // The row's tap target is a transparent Button laid over it, so
                // it carries no text of its own and needs naming for a screen
                // reader.
                var hitTarget = new Button
                {
                    BackgroundColor = Colors.Transparent,
                    BorderWidth = 0,
                    CornerRadius = 0,
                };
                SemanticProperties.SetDescription(hitTarget, label);
                ToolTipProperties.SetText(hitTarget, label);

                var captured = invoke;
                hitTarget.Clicked += async (_, _) =>
                {
                    await CloseAsync();
                    await captured();
                };

                var grid = new Grid();
                grid.Add(text);
                grid.Add(hitTarget);

                var row = new Border { Content = grid };
                AppStyles.Apply(row, "DropdownOption");

                _optionHost.Add(row);
            }
        }

        public Task ToggleAsync() => _isOpen ? CloseAsync() : OpenAsync();

        public async Task OpenAsync()
        {
            if (_isOpen)
                return;

            _isOpen = true;

            if (_anchor is not null)
                _panel.TranslationY = OverlayPanel.TopBelow(_anchor, _panel.Parent);

            _outsideTap.Attach();
            await _panel.DropdownOpenAsync();
        }

        public async Task CloseAsync()
        {
            if (!_isOpen)
                return;

            _isOpen = false;
            _outsideTap.Detach();
            await _panel.DropdownCloseAsync();
        }

        /// <summary>Closes without animating. For navigation and submit paths.</summary>
        public void CloseImmediate()
        {
            _isOpen = false;
            _outsideTap.Detach();
            _panel.IsVisible = false;
            _panel.Opacity = 0;
        }
    }
}
