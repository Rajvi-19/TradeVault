using System.Globalization;
using System.Windows.Data;

namespace TradeVault.Desktop;

// Presentation parsing only; persisted dates continue to use the existing ISO contract.
internal sealed class DateCellConverter : IValueConverter
{
    public static string Normalize(string input)
    {
        var formats=new[]{"yyyy-MM-dd","d/M/yyyy","d-M-yyyy","d.M.yyyy","d/M/yy","d-M-yy","d MMM yyyy","d-MMM-yyyy","d MMMM yyyy"};
        return DateOnly.TryParseExact(input.Trim(),formats,CultureInfo.InvariantCulture,DateTimeStyles.AllowWhiteSpaces,out var date)
            ? date.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture):input;
    }
    public object Convert(object value,Type targetType,object parameter,CultureInfo culture)=>DateOnly.TryParseExact(value?.ToString(),"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date)?date.ToString("dd-MM-yyyy",CultureInfo.InvariantCulture):value??"";
    public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>Normalize(value?.ToString()??"");
}
