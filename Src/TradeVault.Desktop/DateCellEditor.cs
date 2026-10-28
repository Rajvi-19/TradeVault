using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
namespace TradeVault.Desktop;

/// <summary>Day-first typing mask; complete dates persist as ISO, partial/invalid input is retained.</summary>
public sealed class DateCellEditor : TextBox
{
    private bool syncing, deleting;
    public static readonly DependencyProperty ValueProperty=DependencyProperty.Register(nameof(Value),typeof(string),typeof(DateCellEditor),
        new FrameworkPropertyMetadata("",FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,(d,_)=>((DateCellEditor)d).Refresh()));
    public string Value{get=>(string)GetValue(ValueProperty);set=>SetValue(ValueProperty,value);}
    public DateCellEditor()
    {
        SetResourceReference(StyleProperty,typeof(TextBox));MinHeight=0;Padding=new Thickness(6,2,6,2);BorderThickness=new Thickness(0);Background=System.Windows.Media.Brushes.Transparent;
        PreviewKeyDown+=(_,e)=>deleting=e.Key is Key.Back or Key.Delete;
        TextChanged+=(_,e)=>
        {
            if(syncing)return;syncing=true;
            try
            {
                var caret=CaretIndex;var atEnd=caret==Text.Length||e.Changes.Any(change=>change.AddedLength>0&&change.Offset+change.AddedLength==Text.Length);
                var formatted=!deleting&&atEnd?FormatTyping(Text):Text;
                if(formatted!=Text){Text=formatted;CaretIndex=Text.Length;}
                var parts=Text.Split('-','/');
                var incompleteYear=parts.Length==3&&parts[0].Length!=4&&parts[2].Length<4;
                SetCurrentValue(ValueProperty,incompleteYear?Text:DateCellConverter.Normalize(Text));
            }
            finally{syncing=false;deleting=false;}
        };
        System.Windows.Automation.AutomationProperties.SetName(this,"Date, day-month-year");
    }
    public static string FormatTyping(string input)
    {
        if(string.IsNullOrWhiteSpace(input))return input;
        var normalized=DateCellConverter.Normalize(input);
        var segments=input.Split('-','/');
        var fullYear=segments.Length==3&&(segments[0].Length==4||segments[2].Length==4);
        if(fullYear&&DateOnly.TryParseExact(normalized,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date))return date.ToString("dd-MM-yyyy",CultureInfo.InvariantCulture);
        if(input.Any(c=>!char.IsAsciiDigit(c)&&c!='-'&&c!='/'))return input;
        var digits=new string(input.Where(char.IsAsciiDigit).ToArray());
        if(digits.Length<2)return digits;
        var day=digits[..2];if(digits.Length==2)return day+"-";
        var remainder=digits[2..];string month;string year;
        if(remainder[0]>'1'){month="0"+remainder[0];year=remainder[1..];}
        else if(remainder.Length>=2){month=remainder[..2];year=remainder[2..];}
        else return day+"-"+remainder;
        if(year.Length>4)return input;
        return day+"-"+month+"-"+year;
    }
    private void Refresh()
    {
        if(syncing)return;syncing=true;
        try{Text=DateOnly.TryParseExact(Value,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date)?date.ToString("dd-MM-yyyy",CultureInfo.InvariantCulture):Value;}
        finally{syncing=false;}
    }
}
