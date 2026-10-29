using System.Windows;
using System.Windows.Controls;
namespace TradeVault.Desktop;
internal sealed class RevealPassword:UserControl
{
    private readonly PasswordBox hidden=new(){Height=40,Padding=new(10,7,10,7)};
    private readonly TextBox visible=new(){Height=40,Visibility=Visibility.Collapsed};
    private readonly Button toggle;
    private bool revealed;
    public string Secret=>revealed?visible.Text:hidden.Password;
    public RevealPassword(string label="Password")
    {
        var root=new Grid();root.ColumnDefinitions.Add(new());root.ColumnDefinitions.Add(new(){Width=new GridLength(42)});
        hidden.BorderThickness=new(0);hidden.Background=System.Windows.Media.Brushes.Transparent;visible.BorderThickness=new(0);visible.Background=System.Windows.Media.Brushes.Transparent;
        root.Children.Add(hidden);root.Children.Add(visible);
        toggle=Ui.Button("",Toggle,"QuietButton");toggle.Content=Eye(false);toggle.ToolTip="Show password";toggle.Margin=new(0);toggle.Padding=new Thickness(6);System.Windows.Automation.AutomationProperties.SetName(toggle,"Show password");Grid.SetColumn(toggle,1);root.Children.Add(toggle);
        var frame=new Border{Background=System.Windows.Media.Brushes.White,BorderBrush=Ui.Color("#D5DEEA"),BorderThickness=new(1),CornerRadius=new(5)};Grid.SetColumnSpan(frame,2);Panel.SetZIndex(frame,-1);root.Children.Add(frame);root.IsKeyboardFocusWithinChanged+=(_,_)=>frame.BorderBrush=Ui.Color(root.IsKeyboardFocusWithin?"#2458D3":"#D5DEEA");Content=root;
        System.Windows.Automation.AutomationProperties.SetName(hidden,label);System.Windows.Automation.AutomationProperties.SetName(visible,label+" visible");
    }
    internal void Toggle()
    {
        if(revealed){hidden.Password=visible.Text;visible.Clear();}else visible.Text=hidden.Password;
        revealed=!revealed;hidden.Visibility=revealed?Visibility.Collapsed:Visibility.Visible;visible.Visibility=revealed?Visibility.Visible:Visibility.Collapsed;
        toggle.Content=Eye(revealed);toggle.ToolTip=revealed?"Hide password":"Show password";
        System.Windows.Automation.AutomationProperties.SetName(toggle,revealed?"Hide password":"Show password");
    }
    public void Clear(){hidden.Clear();visible.Clear();if(revealed)Toggle();}
    private static UIElement Eye(bool crossed)
    {
        var surface=new Grid{Width=20,Height=20};
        surface.Children.Add(new System.Windows.Shapes.Path{Data=System.Windows.Media.Geometry.Parse("M 1,10 C 5,3 15,3 19,10 C 15,17 5,17 1,10 Z M 13,10 A 3,3 0 1 1 7,10 A 3,3 0 1 1 13,10"),Stroke=Ui.Color("#52627A"),StrokeThickness=1.5});
        if(crossed)surface.Children.Add(new System.Windows.Shapes.Line{X1=2,Y1=2,X2=18,Y2=18,Stroke=Ui.Color("#52627A"),StrokeThickness=1.8});
        return surface;
    }
}
