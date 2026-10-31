using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace TradeVault.Desktop;

/// <summary>Custom chrome must maximize inside the current monitor's physical work area.</summary>
internal static class WindowBounds
{
    [StructLayout(LayoutKind.Sequential)]internal struct Point{public int X,Y;}
    [StructLayout(LayoutKind.Sequential)]internal struct Rectangle{public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)]private struct MonitorInfo{public int Size;public Rectangle Monitor,Work;public uint Flags;}
    [StructLayout(LayoutKind.Sequential)]private struct MinMaxInfo{public Point Reserved,MaxSize,MaxPosition,MinTrackSize,MaxTrackSize;}
    [DllImport("user32.dll")]private static extern IntPtr MonitorFromWindow(IntPtr window,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Auto)]private static extern bool GetMonitorInfo(IntPtr monitor,ref MonitorInfo info);
    [DllImport("user32.dll")]internal static extern bool GetWindowRect(IntPtr window,out Rectangle rectangle);
    internal static Rectangle WorkArea(IntPtr window)
    {
        var info=new MonitorInfo{Size=Marshal.SizeOf<MonitorInfo>()};if(!GetMonitorInfo(MonitorFromWindow(window,2),ref info))throw new InvalidOperationException("Cannot read monitor work area.");return info.Work;
    }
    internal static void Attach(Window window)
    {
        HwndSource? source=null;
        IntPtr Hook(IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam,ref bool handled)
        {
            if(message!=0x0024)return IntPtr.Zero;
            var info=new MonitorInfo{Size=Marshal.SizeOf<MonitorInfo>()};if(!GetMonitorInfo(MonitorFromWindow(hwnd,2),ref info))return IntPtr.Zero;
            var bounds=Marshal.PtrToStructure<MinMaxInfo>(lParam);bounds.MaxPosition=new(){X=info.Work.Left-info.Monitor.Left,Y=info.Work.Top-info.Monitor.Top};bounds.MaxSize=new(){X=info.Work.Right-info.Work.Left,Y=info.Work.Bottom-info.Work.Top};
            Marshal.StructureToPtr(bounds,lParam,false);handled=true;return IntPtr.Zero;
        }
        void Connect(){if(source!=null)return;source=PresentationSource.FromVisual(window) as HwndSource;source?.AddHook(Hook);}
        window.SourceInitialized+=(_,_)=>Connect();window.Loaded+=(_,_)=>Connect();
        window.Closed+=(_,_)=>source?.RemoveHook(Hook);
    }
}
