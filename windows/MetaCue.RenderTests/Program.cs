using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using MetaCue;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        using var overlay = new Badge();
        if (overlay.Visible) throw new Exception("Startup warm-up must remain invisible.");
        IntPtr handle = overlay.Handle;
        var icons = new List<Rectangle>();
        for (int i = 0; i < 10; i++) icons.Add(new Rectangle(100 + i * 48, 100, 48, 48));
        for (int iteration = 0; iteration < 3; iteration++)
        {
            var watch = Stopwatch.StartNew();
            overlay.Present(icons);
            watch.Stop();
            if (!overlay.Visible || overlay.Handle != handle || Application.OpenForms.Count != 1)
                throw new Exception("Overlay must reuse exactly one visible window.");
            if (overlay.Bounds != new Rectangle(100, 100, 10 * 48, 48))
                throw new Exception($"Unexpected overlay bounds: {overlay.Bounds}");
            overlay.Hide();
            if (overlay.Visible) throw new Exception("Hide failed.");
            Console.WriteLine($"Ten labels (1-9, 0), one frame: {watch.Elapsed.TotalMilliseconds:F2} ms (pass {iteration + 1}).");
        }
        overlay.Present(Array.Empty<Rectangle>());
        if (overlay.Visible) throw new Exception("Empty layout must stay hidden.");
        using (var otherTopmost = new Form { TopMost = true, ShowInTaskbar = false, Bounds = new Rectangle(100, 100, 480, 48) })
        {
            otherTopmost.Show();
            for (int i = 0; i < 3; i++)
            {
                otherTopmost.BringToFront();
                IntPtr foreground = GetForegroundWindow();
                overlay.Present(icons);
                CheckAbove(overlay.Handle, otherTopmost.Handle);
                if (GetForegroundWindow() != foreground) throw new Exception("Overlay stole activation.");
                otherTopmost.BringToFront();
                overlay.RaiseWithoutActivation();
                CheckAbove(overlay.Handle, otherTopmost.Handle);
                overlay.Hide();
            }
        }
        Console.WriteLine("Topmost re-show and refresh ordering passed without changing foreground activation.");
        Console.WriteLine("Layered overlay lifecycle tests passed. Physical display latency and mouse pass-through still require desktop verification.");
    }
    private static void CheckAbove(IntPtr overlay, IntPtr other)
    {
        for (IntPtr window = GetWindow(overlay, 3); window != IntPtr.Zero; window = GetWindow(window, 3))
            if (window == other) throw new Exception("Overlay was covered by another topmost window.");
    }
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr window, uint command);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
}
