namespace EDUTASK_1._1.Helpers;

public static class PriorityPickerStyling
{
    public static void Apply(Picker picker, string? priority)
    {
        Color priorityColor = TaskPalette.PriorityColor(priority);
        picker.TextColor = priorityColor;

#if WINDOWS
        if (picker.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.ComboBox comboBox)
        {
            var nativeColor = Windows.UI.Color.FromArgb(
                ToByte(priorityColor.Alpha),
                ToByte(priorityColor.Red),
                ToByte(priorityColor.Green),
                ToByte(priorityColor.Blue));

            comboBox.Resources["ComboBoxItemPillFillBrush"] =
                new Microsoft.UI.Xaml.Media.SolidColorBrush(nativeColor);
        }
#endif
    }

#if WINDOWS
    private static byte ToByte(float component) =>
        (byte)Math.Clamp((int)Math.Round(component * 255), 0, 255);
#endif
}
