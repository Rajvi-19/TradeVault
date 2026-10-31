using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shell;
namespace TradeVault.Desktop;
internal static class Ui
{
    public const string BuildLabel="UI 2026.10.31.35";
    internal static Size WorkspaceSize(Rect workArea)=>new(Math.Min(1440,workArea.Width),Math.Min(880,workArea.Height));
    public static void WindowFrame(Window window,UIElement body,string title,bool embeddedControls=false)
    {
        window.Content=null;
        window.WindowStyle=WindowStyle.None;
        WindowChrome.SetWindowChrome(window,new WindowChrome{CaptionHeight=embeddedControls?42:36,ResizeBorderThickness=new Thickness(6),GlassFrameThickness=new Thickness(0),CornerRadius=new CornerRadius(0),UseAeroCaptionButtons=false});
        window.ResizeMode=ResizeMode.CanResize;
        WindowBounds.Attach(window);
        window.Title=title+" | "+BuildLabel;
        window.Icon=System.Windows.Media.Imaging.BitmapFrame.Create(new Uri("pack://application:,,,/Assets/TradeVault.ico"));
        window.ShowInTaskbar=true;
        if(embeddedControls){window.Content=body;return;}
        var frame=new DockPanel{Background=Color("#111E35")};
        var caption=new DockPanel{Height=36,LastChildFill=true};
        var controls=WindowControls(window);
        DockPanel.SetDock(controls,Dock.Right);caption.Children.Add(controls);
        caption.Children.Add(new TextBlock{Text=title,Foreground=Color("#D9E4F4"),FontSize=11,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(14,0,0,0)});
        DockPanel.SetDock(caption,Dock.Top);frame.Children.Add(caption);frame.Children.Add(new Border{Background=Color("#F4F6FA"),Child=body});window.Content=frame;
    }
    public static Brush Color(string hex)=>AppTheme.Brush(hex);
    public static Image BrandIcon(double size=32)=>new(){Source=new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Assets/TradeVault.png")),Width=size,Height=size,VerticalAlignment=VerticalAlignment.Center};
    public static StackPanel WindowControls(Window window)
    {
        var panel=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};
        foreach(var (name,label,action) in new (string,string,Action)[]{("Minimize","—",()=>SystemCommands.MinimizeWindow(window)),("Maximize or restore","□",()=>{if(window.WindowState==WindowState.Maximized)SystemCommands.RestoreWindow(window);else SystemCommands.MaximizeWindow(window);}), ("Close","×",()=>window.Close())})
        {
            var button=new Button{Width=44,Height=32,MinHeight=0,Padding=new Thickness(0),Margin=new Thickness(2,0,2,0),Foreground=Brushes.White,Background=Brushes.Transparent,BorderThickness=new Thickness(0),Focusable=false,ToolTip=null};
            var geometry=name=="Minimize"?"M2,8 H12":name=="Close"?"M2,2 L12,12 M12,2 L2,12": "M2,2 H12 V12 H2 Z";
            var icon=new System.Windows.Shapes.Path{Data=Geometry.Parse(geometry),Width=12,Height=12,Stretch=Stretch.Uniform,Stroke=Brushes.White,StrokeThickness=1.2};button.Content=icon;
            if(name=="Maximize or restore")
            {
                void UpdateCaption(){icon.Data=Geometry.Parse(window.WindowState==WindowState.Maximized?"M4,1 H13 V10 M1,4 H10 V13 H1 Z":"M2,2 H12 V12 H2 Z");System.Windows.Automation.AutomationProperties.SetName(button,window.WindowState==WindowState.Maximized?"Restore":"Maximize or restore");}
                EventHandler changed=(_,_)=>UpdateCaption();window.StateChanged+=changed;window.Closed+=(_,_)=>window.StateChanged-=changed;UpdateCaption();
            }
            var template=new ControlTemplate(typeof(Button));var border=new FrameworkElementFactory(typeof(Border));border.SetValue(Border.CornerRadiusProperty,new CornerRadius(5));border.SetBinding(Border.BackgroundProperty,new System.Windows.Data.Binding("Background"){RelativeSource=new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.TemplatedParent)});var presenter=new FrameworkElementFactory(typeof(ContentPresenter));presenter.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);presenter.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);border.AppendChild(presenter);template.VisualTree=border;button.Template=template;
            button.MouseEnter+=(_,_)=>button.Background=name=="Close"?new SolidColorBrush(System.Windows.Media.Color.FromRgb(196,43,55)):new SolidColorBrush(System.Windows.Media.Color.FromArgb(40,255,255,255));button.MouseLeave+=(_,_)=>button.Background=Brushes.Transparent;button.Click+=(_,_)=>action();System.Windows.Controls.ToolTipService.SetIsEnabled(button,false);
            System.Windows.Automation.AutomationProperties.SetName(button,name);WindowChrome.SetIsHitTestVisibleInChrome(button,true);panel.Children.Add(button);
        }
        return panel;
    }
    public static TextBlock Text(string text,double size=13,string color="#18243B",bool bold=false)=>new(){Text=text,FontSize=size,Foreground=color.Equals("#FFFFFF",StringComparison.OrdinalIgnoreCase)?Brushes.White:Color(color),FontWeight=bold?FontWeights.SemiBold:FontWeights.Normal,TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center};
    public static TextBlock Icon(string glyph,string color="#64748B",double size=17)=>new(){Text=glyph,FontFamily=new("Segoe MDL2 Assets"),Foreground=color.Equals("#FFFFFF",StringComparison.OrdinalIgnoreCase)?Brushes.White:Color(color),FontSize=size,VerticalAlignment=VerticalAlignment.Center};
    public static Button Button(string label,Action action,string? style=null,string? glyph=null)
    {
        var button=new Button();if(style!=null)button.SetResourceReference(FrameworkElement.StyleProperty,style);
        var content=new StackPanel{Orientation=Orientation.Horizontal};
        if(glyph!=null){var icon=Icon(glyph);icon.SetBinding(TextBlock.ForegroundProperty,new System.Windows.Data.Binding("Foreground"){Source=button});icon.Margin=new(0,0,8,0);content.Children.Add(icon);}
        var text=new TextBlock{Text=label,VerticalAlignment=VerticalAlignment.Center};text.SetBinding(TextBlock.ForegroundProperty,new System.Windows.Data.Binding("Foreground"){Source=button});content.Children.Add(text);button.Content=content;
        System.Windows.Automation.AutomationProperties.SetName(button,label);
        button.Click+=(_,_)=>action();return button;
    }
    public static Button LineIconButton(string label,Action action,string icon,string style="QuietButton")
    {
        var button=Button(label,action,style);button.FontFamily=new("Segoe UI Variable Text, Segoe UI");button.FontSize=12;button.FontWeight=FontWeights.SemiBold;
        var geometry=icon switch{
            "partyMaster"=>"M2,3 H9 V15 H2 Z M9,7 H14 V15 H9 M4,6 H6 M4,9 H6 M4,12 H6 M11,10 H12 M11,13 H12",
            "agentMaster"=>"M2,3 H14 V13 H2 Z M6,3 V1 H10 V3 M2,7 H14 M7,7 V9 H9 V7",
            "invoice"=>"M2,2 H14 V14 H2 Z M2,6 H14 M6,6 V14",
            "details"=>"M4,1.5 H10 L14,5.5 V14.5 H4 Z M10,1.5 V5.5 H14 M7,8 H11 M7,11 H11",
            "partyReceipt"=>"M3,1 H12 V15 L9,13 L7,15 L5,13 L3,15 Z M5,5 H10 M5,8 H8",
            "agentReceipt"=>"M1,6 L8,1 L15,6 Z M3,7 V12 M8,7 V12 M13,7 V12 M1,15 H15",
            "ledger"=>"M2,5 H14 M11,2 L14,5 L11,8 M14,11 H2 M5,8 L2,11 L5,14",
            "reports"=>"M2,1 H11 L14,4 V15 H2 Z M10,1 V5 H14 M5,12 V9 M8,12 V7 M11,12 V6",
            _=>"M6,7 A2.5,2.5 0 1 0 6,2 A2.5,2.5 0 1 0 6,7 M1.5,14 V12 A4.5,4.5 0 0 1 10.5,12 M13,7 V13 M10,10 H16"};
        var panel=new StackPanel{Orientation=Orientation.Horizontal};var path=new System.Windows.Shapes.Path{Data=Geometry.Parse(geometry),Width=16,Height=16,Stretch=Stretch.Uniform,StrokeThickness=1.5,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round,Margin=new(0,0,8,0),VerticalAlignment=VerticalAlignment.Center};path.SetBinding(System.Windows.Shapes.Shape.StrokeProperty,new System.Windows.Data.Binding("Foreground"){Source=button});panel.Children.Add(path);
        var text=new TextBlock{Text=label,VerticalAlignment=VerticalAlignment.Center};text.SetBinding(TextBlock.ForegroundProperty,new System.Windows.Data.Binding("Foreground"){Source=button});panel.Children.Add(text);button.Content=panel;return button;
    }
    public static Border Card(UIElement child,Thickness? padding=null)=>new(){Background=Ui.Color("#FFFFFF"),BorderBrush=Color("#E2E8F0"),BorderThickness=new(1),CornerRadius=new(8),Padding=padding??new(0),Child=child};
    public static Border Badge(string text,string background="#EDF3FF",string foreground="#2458D3")=>new(){Background=Color(background),CornerRadius=new(4),Padding=new(8,4,8,4),Child=Text(text,11,foreground,true),VerticalAlignment=VerticalAlignment.Center};
}







