using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shell;
namespace TradeVault.Desktop;
internal static class Ui
{
    public const string BuildLabel="UI 2026.10.26.3";
    public static void WindowFrame(Window window,UIElement body,string title)
    {
        window.Content=null;
        WindowChrome.SetWindowChrome(window,null);
        window.WindowStyle=WindowStyle.SingleBorderWindow;
        window.ResizeMode=ResizeMode.CanResize;
        window.Title=title+" | "+BuildLabel;
        window.ShowInTaskbar=true;
        window.Content=body;
    }
    public static Brush Color(string hex)=>new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
    public static TextBlock Text(string text,double size=13,string color="#18243B",bool bold=false)=>new(){Text=text,FontSize=size,Foreground=Color(color),FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal,TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center};
    public static TextBlock Icon(string glyph,string color="#64748B",double size=17)=>new(){Text=glyph,FontFamily=new("Segoe MDL2 Assets"),Foreground=Color(color),FontSize=size,VerticalAlignment=VerticalAlignment.Center};
    public static Button Button(string label,Action action,string? style=null,string? glyph=null)
    {
        var button=new Button();if(style!=null)button.SetResourceReference(FrameworkElement.StyleProperty,style);
        var content=new StackPanel{Orientation=Orientation.Horizontal};
        if(glyph!=null){var icon=Icon(glyph);icon.SetBinding(TextBlock.ForegroundProperty,new System.Windows.Data.Binding("Foreground"){Source=button});icon.Margin=new(0,0,8,0);content.Children.Add(icon);}
        var text=new TextBlock{Text=label,VerticalAlignment=VerticalAlignment.Center};text.SetBinding(TextBlock.ForegroundProperty,new System.Windows.Data.Binding("Foreground"){Source=button});content.Children.Add(text);button.Content=content;
        System.Windows.Automation.AutomationProperties.SetName(button,label);
        button.Click+=(_,_)=>action();return button;
    }
    public static Border Card(UIElement child,Thickness? padding=null)=>new(){Background=Brushes.White,BorderBrush=Color("#E2E8F0"),BorderThickness=new(1),CornerRadius=new(8),Padding=padding??new(0),Child=child};
    public static Border Badge(string text,string background="#EDF3FF",string foreground="#2458D3")=>new(){Background=Color(background),CornerRadius=new(4),Padding=new(8,4,8,4),Child=Text(text,11,foreground,true),VerticalAlignment=VerticalAlignment.Center};
}
