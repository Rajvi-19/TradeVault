using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace TradeVault.Desktop;

/// <summary>Live shared brushes; preference is stored only beside this application.</summary>
internal static class AppTheme
{
    private static readonly Dictionary<string,string> dark=new(StringComparer.OrdinalIgnoreCase)
    {
        ["#111E35"]="#10141C",["#18243B"]="#E6EDF3",["#2458D3"]="#9DBEFF",
        ["#247653"]="#85D4AE",["#407057"]="#9BD7B7",["#52627A"]="#BAC4D2",
        ["#64748B"]="#9DAABB",["#718096"]="#A1ADBD",["#8FAEEA"]="#6089CF",
        ["#9A6700"]="#E6BD73",["#A63E3E"]="#F8A49C",["#B42318"]="#FFAAA0",
        ["#C2413C"]="#FFAAA0",["#CBD5E1"]="#394351",["#CDD9EC"]="#374150",
        ["#D4E2D9"]="#29483D",["#D9E4F4"]="#DCE5F1",["#DCE3ED"]="#252D39",
        ["#E2E8F0"]="#252D39",["#E7EFFF"]="#263B5E",["#E8EFFD"]="#223553",
        ["#EDF1F6"]="#1C2430",["#EDF3FF"]="#202D43",["#EDF4FF"]="#202D43",
        ["#F0F5FF"]="#1B283D",["#F3C8C3"]="#65403D",["#F3F8F5"]="#192B25",
        ["#F4F6FA"]="#0E131B",["#F7F9FC"]="#1C232F",["#FAFBFD"]="#191F29",
        ["#FEECE9"]="#352024",["#FFF2F0"]="#352024",["#FFFFFF"]="#161C26"
    };
    private sealed class Entry(string original):INotifyPropertyChanged
    {
        public Color Value=>Parse(IsDark&&dark.TryGetValue(original,out var value)?value:original);
        public event PropertyChangedEventHandler? PropertyChanged;
        internal void Refresh()=>PropertyChanged?.Invoke(this,new(nameof(Value)));
    }
    private static readonly Dictionary<string,(Entry Entry,SolidColorBrush Brush)> brushes=new(StringComparer.OrdinalIgnoreCase);
    private static string? settingsPath;
    private sealed record Preference(bool Dark);
    internal static bool IsDark{get;private set;}
    internal static event Action? Changed;
    internal static Brush AccentFill{get;}=new SolidColorBrush(Parse("#2458D3"));
    internal static Brush DangerFill{get;}=new SolidColorBrush(Parse("#B42318"));
    private static Color Parse(string hex)=>(Color)ColorConverter.ConvertFromString(hex);
    internal static Brush Brush(string hex)
    {
        if(brushes.TryGetValue(hex,out var value))return value.Brush;
        var entry=new Entry(hex);var brush=new SolidColorBrush();
        // Binding keeps brushes live even when a WPF style/template is sealed.
        BindingOperations.SetBinding(brush,SolidColorBrush.ColorProperty,new Binding(nameof(Entry.Value)){Source=entry,Mode=BindingMode.OneWay});
        brushes.Add(hex,(entry,brush));
        if(System.Windows.Application.Current!=null)System.Windows.Application.Current.Resources["Theme"+hex.TrimStart('#').ToUpperInvariant()]=brush;
        return brush;
    }
    internal static void Initialize(string? path)
    {
        settingsPath=path;var selected=false;
        try{if(path!=null&&File.Exists(path)&&new FileInfo(path).Length<=2048)selected=JsonSerializer.Deserialize<Preference>(File.ReadAllText(path))?.Dark??false;}
        catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or JsonException){/* An invalid preference never blocks company records. */}
        foreach(var color in dark.Keys)Brush(color);
        var resources=System.Windows.Application.Current.Resources;
        resources["ThemeAccentFill"]=AccentFill;
        foreach(var key in new[]{SystemColors.WindowBrushKey,SystemColors.ControlLightLightBrushKey})resources[key]=Brush("#FFFFFF");
        foreach(var key in new[]{SystemColors.ControlBrushKey,SystemColors.ControlLightBrushKey})resources[key]=Brush("#F4F6FA");
        foreach(var key in new[]{SystemColors.ControlTextBrushKey,SystemColors.WindowTextBrushKey,SystemColors.MenuTextBrushKey})resources[key]=Brush("#18243B");
        resources[SystemColors.GrayTextBrushKey]=Brush("#64748B");resources[SystemColors.ControlDarkBrushKey]=Brush("#CBD5E1");
        resources[SystemColors.MenuBrushKey]=Brush("#FFFFFF");resources[SystemColors.HighlightBrushKey]=AccentFill;resources[SystemColors.HighlightTextBrushKey]=Brushes.White;
        SetDark(selected);
    }
    internal static void Toggle()=>SetDark(!IsDark,true);
    internal static void SetDark(bool value,bool persist=false)
    {
        if(persist&&settingsPath!=null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            var pending=settingsPath+".pending";File.WriteAllText(pending,JsonSerializer.Serialize(new Preference(value)));File.Move(pending,settingsPath,true);
        }
        IsDark=value;foreach(var entry in brushes.Values)entry.Entry.Refresh();Changed?.Invoke();
    }
}
