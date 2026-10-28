using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shell;
namespace TradeVault.Desktop;
internal static class Ui
{
    public const string BuildLabel="UI 2026.10.27.9";
    internal static Size WorkspaceSize(Rect workArea)=>new(Math.Min(1440,workArea.Width),Math.Min(880,workArea.Height));
    public static void WindowFrame(Window window,UIElement body,string title,bool embeddedControls=false)
    {
        window.Content=null;
        window.WindowStyle=WindowStyle.None;
        WindowChrome.SetWindowChrome(window,new WindowChrome{CaptionHeight=embeddedControls?42:36,ResizeBorderThickness=new Thickness(6),GlassFrameThickness=new Thickness(0),CornerRadius=new CornerRadius(0),UseAeroCaptionButtons=false});
        window.ResizeMode=ResizeMode.CanResize;
        window.Title=title+" | "+BuildLabel;
        window.ShowInTaskbar=true;
        if(embeddedControls){window.Content=body;return;}
        var frame=new DockPanel{Background=Color("#111E35")};
        var caption=new DockPanel{Height=36,LastChildFill=true};
        var controls=new StackPanel{Orientation=Orientation.Horizontal};
        foreach(var (label,symbol,action) in new (string,string,Action)[]{("Minimize","−",()=>SystemCommands.MinimizeWindow(window)),("Maximize or restore","□",()=>{if(window.WindowState==WindowState.Maximized)SystemCommands.RestoreWindow(window);else SystemCommands.MaximizeWindow(window);}), ("Close","×",()=>window.Close())})
        {
            var button=Button(symbol,action,"QuietButton");button.Width=46;button.Height=36;button.MinHeight=0;button.Padding=new Thickness(0);button.Foreground=Brushes.White;button.ToolTip=label;
            System.Windows.Automation.AutomationProperties.SetName(button,label);WindowChrome.SetIsHitTestVisibleInChrome(button,true);controls.Children.Add(button);
        }
        DockPanel.SetDock(controls,Dock.Right);caption.Children.Add(controls);
        caption.Children.Add(new TextBlock{Text=title+"   |   "+BuildLabel,Foreground=Color("#D9E4F4"),FontSize=11,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(14,0,0,0)});
        DockPanel.SetDock(caption,Dock.Top);frame.Children.Add(caption);frame.Children.Add(body);window.Content=frame;
    }
    public static Brush Color(string hex)=>new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
    public static StackPanel WindowControls(Window window)
    {
        var panel=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};
        foreach(var (name,label,action) in new (string,string,Action)[]{("Minimize","—",()=>SystemCommands.MinimizeWindow(window)),("Maximize or restore","□",()=>{if(window.WindowState==WindowState.Maximized)SystemCommands.RestoreWindow(window);else SystemCommands.MaximizeWindow(window);}), ("Close","×",()=>window.Close())})
        {
            var button=Button(label,action,"QuietButton");button.Foreground=Brushes.White;button.FontSize=18;button.Width=44;button.Height=36;button.MinHeight=0;button.Padding=new Thickness(0);button.ToolTip=name;
            System.Windows.Automation.AutomationProperties.SetName(button,name);WindowChrome.SetIsHitTestVisibleInChrome(button,true);panel.Children.Add(button);
        }
        return panel;
    }
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
