using System.IO;
using System.Windows;
using System.Windows.Media;

namespace TradeVault.Desktop;

internal static class ThemeChecks
{
    internal static void Preference()
    {
        var path=Path.Combine(AppContext.BaseDirectory,"TestData","Theme",Guid.NewGuid().ToString("N"),"ui-settings.json");
        try
        {
            AppTheme.Initialize(path);AppTheme.SetDark(true,true);AppTheme.SetDark(false);AppTheme.Initialize(path);
            if(!AppTheme.IsDark)throw new InvalidOperationException("Dark preference was not restored.");
            AppTheme.SetDark(false,true);AppTheme.SetDark(true);AppTheme.Initialize(path);
            if(AppTheme.IsDark)throw new InvalidOperationException("Light preference was not restored.");
            File.WriteAllText(path,"invalid preference");AppTheme.Initialize(path);
            if(AppTheme.IsDark)throw new InvalidOperationException("Invalid preference did not fall back to light.");
        }
        finally{AppTheme.Initialize(null);}
    }
    internal static void Contrast()
    {
        try
        {
            foreach(var dark in new[]{false,true})
            {
                AppTheme.SetDark(dark);Flush();
                foreach(var text in new[]{"#18243B","#52627A","#64748B","#2458D3"})
                    if(Ratio(((SolidColorBrush)Ui.Color(text)).Color,((SolidColorBrush)Ui.Color("#FFFFFF")).Color)<4.5)throw new InvalidOperationException("Insufficient theme text contrast: "+text);
            }
            if(Ratio(Colors.White,((SolidColorBrush)AppTheme.AccentFill).Color)<4.5)throw new InvalidOperationException("Primary button contrast failed.");
        }
        finally{AppTheme.SetDark(false);}
    }
    internal static void Flush()=>System.Windows.Application.Current.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.ContextIdle);
    private static double Ratio(Color a,Color b)
    {
        static double Linear(byte value){var s=value/255d;return s<=0.04045?s/12.92:Math.Pow((s+0.055)/1.055,2.4);}
        static double L(Color c)=>0.2126*Linear(c.R)+0.7152*Linear(c.G)+0.0722*Linear(c.B);
        var x=L(a);var y=L(b);return (Math.Max(x,y)+0.05)/(Math.Min(x,y)+0.05);
    }
}
