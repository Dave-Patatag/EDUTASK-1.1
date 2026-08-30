namespace EDUTASK_1._1.Helpers
{
    /// <summary>
    /// Works out where a panel that hangs below a field should be drawn — the
    /// role and security-question menus on the authentication pages, and the
    /// period filter on the Summary Report.
    ///
    /// Two rules keep an open panel from disturbing the page:
    ///
    ///   * it is drawn with TranslationY, a render offset, so opening it never
    ///     re-runs layout on the form around it;
    ///   * it is parked in a host whose own height cannot be driven by the
    ///     panel — a page-filling layer or a star row, never an Auto row that
    ///     sizes to its tallest child.
    ///
    /// The second rule is the one Select Role broke: its two-option panel stood
    /// taller than the label-field-error column it shared a row with, so the
    /// Auto row grew while the menu was open and the Confirm button below it
    /// stepped down and back with every open and close.
    /// </summary>
    public static class OverlayPanel
    {
        /// <summary>Space between the bottom of the field and the top of the panel.</summary>
        public const double Gap = 4;

        /// <summary>
        /// The panel's TranslationY for sitting directly beneath
        /// <paramref name="field"/>, expressed in the coordinates of the host
        /// the panel is parked in.
        /// </summary>
        public static double TopBelow(VisualElement field, Element? host) =>
            OffsetWithin(field, host) + field.Height + Gap;

        /// <summary>
        /// Distance from the top of <paramref name="host"/> down to the top of
        /// <paramref name="element"/>, however deeply nested it is. A host that
        /// is not one of the element's ancestors falls back to the element's own
        /// offset within its parent.
        /// </summary>
        static double OffsetWithin(VisualElement element, Element? host)
        {
            double offset = 0;

            for (Element? current = element; current is not null; current = current.Parent)
            {
                if (current == host)
                    return offset;

                if (current is VisualElement visual)
                    offset += visual.Y;
            }

            return element.Y;
        }
    }
}
