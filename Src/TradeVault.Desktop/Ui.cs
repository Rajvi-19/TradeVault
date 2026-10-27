using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shell;
namespace TradeVault.Desktop;
internal static class Ui
{
    public static void WindowFrame(Window window,UIElement body,string title)
    {
        window.Content=null;
        window.WindowStyle=WindowStyle.None;window.ResizeMode=ResizeMode.CanResize;
        WindowChrome.SetWindowChrome(window,new WindowChrome{CaptionHeight=38,ResizeBorderThickness=new(6),GlassFrameThickness=new(0),CornerRadius=new(0)});
        var frame=new DockPanel{Background=Brushes.White};var bar=new DockPanel{Height=38,Background=Color("#111E35")};
        var controls=new StackPanel{Orientation=Orientation.Horizontal};
        foreach(var (name,glyph,action) in new (string,string,Action)[]{("Minimize","\uE921",()=>SystemCommands.MinimizeWindow(window)),("Maximize or restore","\uE922",()=>{if(window.WindowState==WindowState.Maximized)SystemCommands.RestoreWindow(window);else SystemCommands.MaximizeWindow(window);}), ("Close","\uE8BB",()=>window.Close())})
        {
            var button=new Button{Content=Icon(glyph,"#FFFFFF",12),Width=46,Height=38,MinHeight=0,Padding=new(0),Background=Brushes.Transparent,BorderBrush=Brushes.Transparent,ToolTip=name};
            button.Click+=(_,_)=>action();System.Windows.Automation.AutomationProperties.SetName(button,name);WindowChrome.SetIsHitTestVisibleInChrome(button,true);controls.Children.Add(button);
        }
        DockPanel.SetDock(controls,Dock.Right);bar.Children.Add(controls);var caption=Text(title,12,"#D5DFEE",true);caption.Margin=new(16,0,0,0);bar.Children.Add(caption);
        DockPanel.SetDock(bar,Dock.Top);frame.Children.Add(bar);frame.Children.Add(body);window.Content=frame;
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
