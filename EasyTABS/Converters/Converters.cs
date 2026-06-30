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
}
