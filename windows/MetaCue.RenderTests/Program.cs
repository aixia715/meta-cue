using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
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
        Console.WriteLine("Layered overlay lifecycle tests passed. Physical display latency and mouse pass-through still require desktop verification.");
    }
}
