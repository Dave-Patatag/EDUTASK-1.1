using Microsoft.Extensions.Logging;
using EDUTASK_1._1.Helpers;

namespace EDUTASK_1._1
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });


#if ANDROID
            // Android's EditText supplies its own underline, padding and text
            // baseline. The surrounding MAUI Border owns that presentation,
            // so remove the native decoration and centre single-line input.
            Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("EduTaskEntryInsets", (handler, view) =>
            {
                AndroidX.AppCompat.Widget.AppCompatEditText editText = handler.PlatformView;

                editText.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
                editText.SetPadding(0, 0, 0, 0);
                editText.SetIncludeFontPadding(false);
                editText.Gravity = Android.Views.GravityFlags.CenterVertical | Android.Views.GravityFlags.Start;
            });

            // Editors use the same Android underline. Keep multiline text at
            // the top-left, but let the app's field border be the only line.
            Microsoft.Maui.Handlers.EditorHandler.Mapper.AppendToMapping("EduTaskEditorInsets", (handler, view) =>
            {
                AndroidX.AppCompat.Widget.AppCompatEditText editText = handler.PlatformView;

                editText.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
                editText.SetPadding(0, 0, 0, 0);
                editText.SetIncludeFontPadding(false);
                editText.Gravity = Android.Views.GravityFlags.Top | Android.Views.GravityFlags.Start;
            });

            // A Picker is also backed by an Android text field and otherwise
            // draws a native underline inside the app-owned field border.
            Microsoft.Maui.Handlers.PickerHandler.Mapper.AppendToMapping("EduTaskPickerInsets", (handler, view) =>
            {
                var picker = handler.PlatformView;

                picker.BackgroundTintList = Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent);
                picker.SetPadding(0, 0, 0, 0);
                picker.SetIncludeFontPadding(false);
                picker.Gravity = Android.Views.GravityFlags.CenterVertical | Android.Views.GravityFlags.Start;
            });
#endif

#if WINDOWS
            Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("EduTaskBorderlessEntry", (handler, view) =>
            {
                Microsoft.UI.Xaml.Controls.TextBox textBox = handler.PlatformView;
                var transparent = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);

                textBox.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);

                // The TextBox brings its own inset. Left in place, an entry's text sits
                // a few pixels right of the label or icon beside it, which is what put
                // the auth fields out of alignment. The Border around the entry owns
                // the padding instead.
                textBox.Padding = new Microsoft.UI.Xaml.Thickness(0);
                textBox.VerticalContentAlignment = Microsoft.UI.Xaml.VerticalAlignment.Center;
                textBox.Resources["TextControlBorderBrush"] = transparent;
                textBox.Resources["TextControlBorderBrushPointerOver"] = transparent;
                textBox.Resources["TextControlBorderBrushFocused"] = transparent;
            });

            Microsoft.Maui.Handlers.PickerHandler.Mapper.AppendToMapping("EduTaskDropdown", (handler, view) =>
            {
                Microsoft.UI.Xaml.Controls.ComboBox comboBox = handler.PlatformView;

                // WinUI templates this popup itself, so its colours have to be
                // pushed across by hand. They used to be typed here as raw RGB
                // triples and had drifted off the palette — the selected row was
                // a lavender (#EAEBFF) that appears nowhere else in the app, and
                // the text was one of the stray darks Colors.xaml consolidated.
                // They now resolve from the same tokens as every other surface.
                var surface = ToBrush(AppColors.SurfaceBase);
                var text = ToBrush(AppColors.TextPrimary);
                var border = ToBrush(AppColors.BorderDefault);
                var hover = ToBrush(AppColors.SurfaceHover);
                var selected = ToBrush(AppColors.SelectionSurface);

                comboBox.RequestedTheme = Microsoft.UI.Xaml.ElementTheme.Light;
                comboBox.Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
                comboBox.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
                comboBox.CornerRadius = new Microsoft.UI.Xaml.CornerRadius(0);
                comboBox.Resources["ComboBoxDropDownBackground"] = surface;
                comboBox.Resources["ComboBoxDropDownForeground"] = text;
                comboBox.Resources["ComboBoxDropDownBorderBrush"] = border;
                comboBox.Resources["ComboBoxItemBackground"] = surface;
                comboBox.Resources["ComboBoxItemForeground"] = text;
                comboBox.Resources["ComboBoxItemBackgroundPointerOver"] = hover;
                comboBox.Resources["ComboBoxItemForegroundPointerOver"] = text;
                comboBox.Resources["ComboBoxItemBackgroundSelected"] = selected;
                comboBox.Resources["ComboBoxItemForegroundSelected"] = text;
                comboBox.Resources["ComboBoxItemBackgroundSelectedPointerOver"] = selected;
                comboBox.Resources["ComboBoxItemForegroundSelectedPointerOver"] = text;

                void PositionPopup(object sender, Microsoft.UI.Xaml.RoutedEventArgs args)
                {
                    comboBox.ApplyTemplate();
                    if (comboBox.FindName("Popup") is Microsoft.UI.Xaml.Controls.Primitives.Popup popup)
                    {
                        popup.PlacementTarget = comboBox;
                        popup.DesiredPlacement = Microsoft.UI.Xaml.Controls.Primitives.PopupPlacementMode.BottomEdgeAlignedLeft;
                        popup.RequestedTheme = Microsoft.UI.Xaml.ElementTheme.Light;
                    }
                }

                comboBox.Loaded += PositionPopup;
                if (comboBox.IsLoaded)
                    PositionPopup(comboBox, new Microsoft.UI.Xaml.RoutedEventArgs());
            });
#endif

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }

#if WINDOWS
        /// <summary>
        /// Bridges a design token across to WinUI. The Windows ComboBox popup is
        /// templated outside the MAUI style system, so the only way to keep it on
        /// palette is to hand it brushes built from the same tokens.
        /// </summary>
        private static Microsoft.UI.Xaml.Media.SolidColorBrush ToBrush(Color color) =>
            new(Microsoft.UI.ColorHelper.FromArgb(
                (byte)(color.Alpha * 255),
                (byte)(color.Red * 255),
                (byte)(color.Green * 255),
                (byte)(color.Blue * 255)));
#endif
    }
}
