using System.Globalization;
using System.IO;
using System.Linq;

namespace EasyTABS.Converters
{
    // Рядок не порожній -> true (для показу повідомлень про помилки).
    public class StringNotEmptyConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => !string.IsNullOrWhiteSpace(value as string);

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    // Інвертування bool (наприклад, показ іконки завантаження доки файл не обрано).
    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is bool b && !b;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is bool b && !b;
    }

    // Колір ноти: активна — біла, неактивна — сіра.
    public class BoolToNoteColorConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => (value is bool b && b) ? Colors.White : Color.FromArgb("#808080");

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
    public class BytesToImageConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => value is byte[] bytes && bytes.Length > 0
                ? ImageSource.FromStream(() => new MemoryStream(bytes))
                : "album_placeholder.png";

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    // Активний перемикач (метроном/перевірка ноти) — фіолетовий, неактивний — сірий.
    public class BoolToToggleColorConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => (value is bool b && b) ? Color.FromArgb("#8A2BE2") : Color.FromArgb("#333333");

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    // Синтезатор: вимкнено (muted=true) — червоний, увімкнено — сірий.
    public class BoolToMuteColorConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => (value is bool b && b) ? Color.FromArgb("#DC143C") : Color.FromArgb("#333333");

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    // Гліф play/pause залежно від стану відтворення.
    public class PlayPauseGlyphConverter : IValueConverter
    {
        // Material Symbols: pause = \ue034, play_arrow = \ue037
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
            => (value is bool playing && playing) ? "\ue034" : "\ue037";

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
