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
        ["#111E35"]="#0D1420",["#18243B"]="#E5ECF6",["#2458D3"]="#94B8FF",
        ["#247653"]="#8CD5AE",["#407057"]="#9AD5B7",["#52627A"]="#BECBDD",
        ["#64748B"]="#A8B8CE",["#718096"]="#AEBFD5",["#8FAEEA"]="#759CE1",
        ["#9A6700"]="#EDC775",["#A63E3E"]="#FFA8A3",["#B42318"]="#FFAAA3",
        ["#C2413C"]="#FFAAA3",["#CBD5E1"]="#40516A",["#CDD9EC"]="#40516A",
        ["#D4E2D9"]="#395B4D",["#D9E4F4"]="#D9E4F4",["#DCE3ED"]="#35465D",
        ["#E2E8F0"]="#35465D",["#E7EFFF"]="#2D456C",["#E8EFFD"]="#2D456C",
        ["#EDF1F6"]="#2A394E",["#EDF3FF"]="#21334F",["#EDF4FF"]="#21334F",
        ["#F0F5FF"]="#1D2D47",["#F3C8C3"]="#754A4A",["#F3F8F5"]="#1C3029",
        ["#F4F6FA"]="#111925",["#F7F9FC"]="#1D293B",["#FAFBFD"]="#1B2738",
        ["#FEECE9"]="#3B252D",["#FFF2F0"]="#3B252D",["#FFFFFF"]="#172234"
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
