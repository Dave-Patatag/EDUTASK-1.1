using System.Threading.Tasks;

namespace EDUTASK_1._1.Helpers
{
    /// <summary>
    /// Lays a transparent catcher behind an open overlay so that tapping
    /// anywhere else dismisses it.
    ///
    /// Shared by every dropdown in the app: the single-select ones driven by
    /// <see cref="DropdownController"/> and the multi-select security-question
    /// panel on the registration page, which cannot use that controller but
    /// should still close the same way.
    /// </summary>
    public sealed class OutsideTapCatcher
    {
        // One percent alpha rather than fully transparent: a fully transparent
        // view is skipped by hit-testing on some platforms, which would leave
        // the catcher dead. At 1% it is imperceptible but still solid to touch.
        static readonly Color Invisible = Color.FromRgba(0f, 0f, 0f, 0.01f);

        readonly VisualElement _panel;
        readonly Func<Task> _onTapOutside;

        View? _scrim;

        public OutsideTapCatcher(VisualElement panel, Func<Task> onTapOutside)
        {
            _panel = panel;
            _onTapOutside = onTapOutside;
        }

        /// <summary>Inserts the catcher just beneath the panel. Safe to call twice.</summary>
        public void Attach()
        {
            if (_scrim is not null || _panel.Parent is not Layout host)
                return;

            var scrim = new BoxView
            {
                // Color, not just BackgroundColor: a BoxView paints itself with
                // Color. Leaving it unset means it picks up whatever the theme
                // defines, which is how this catcher once rendered as a solid
                // grey sheet across the login form.
                Color = Invisible,
                BackgroundColor = Invisible,
                ZIndex = _panel.ZIndex - 1,
            };

            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) => await _onTapOutside();
            scrim.GestureRecognizers.Add(tap);

            // Cover the whole host, however it is laid out.
            if (host is Grid grid)
            {
                Grid.SetRow(scrim, 0);
                Grid.SetColumn(scrim, 0);
                Grid.SetRowSpan(scrim, Math.Max(grid.RowDefinitions.Count, 1));
                Grid.SetColumnSpan(scrim, Math.Max(grid.ColumnDefinitions.Count, 1));
            }

            host.Add(scrim);
            _scrim = scrim;
        }

        /// <summary>Removes the catcher. Safe to call when nothing is attached.</summary>
        public void Detach()
        {
            if (_scrim is null)
                return;

            if (_scrim.Parent is Layout host)
                host.Remove(_scrim);

            _scrim = null;
        }
    }
}
