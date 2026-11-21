using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using MetaCue;

internal static class Program
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    [STAThread]
    private static void Main(string[] args)
    {
        using var log = args.Length == 1 ? new StreamWriter(args[0]) { AutoFlush = true } : null;
        if (log != null) Console.SetOut(log);
        try { Run(); }
        catch (Exception e) { Console.WriteLine(e); Environment.ExitCode = 1; }
    }
    private static void Run()
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        const string path = @"C:\Apps\example.exe";
        var one = new TaskbarItem(new Rectangle(0, 0, 40, 40), "app.one", "One", null!);
        var two = new TaskbarItem(new Rectangle(40, 0, 40, 40), "app.two", "Two", null!);
        var windows = new List<AppWindow> {
            new(new IntPtr(1), 1, "A", "app.one", path),
            new(new IntPtr(2), 1, "B", "app.two", path),
            new(new IntPtr(3), 1, "C", "", path) };
        var identities = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase) {
            ["app.one"] = new(StringComparer.OrdinalIgnoreCase) { path },
            ["app.two"] = new(StringComparer.OrdinalIgnoreCase) { path } };
        var items = new List<TaskbarItem> { one, two };
        Check(WindowCatalog.Match(one, items, windows, identities).Select(w => w.Handle).SequenceEqual(new[] { new IntPtr(1) }), "Ambiguous same-exe groups must not be guessed");
        Check(WindowCatalog.Match(two, items, windows, identities).Single().Handle == new IntPtr(2), "Explicit window identity wins over process path");
        Check(WindowCatalog.Match(one, new[] { one }, windows, identities).Count == 2, "Unique shortcut path can resolve implicit group");
        Check(!WindowCatalog.Activate(new AppWindow(IntPtr.Zero, 0, "", "", "")), "Closed window rejected");

        IntPtr original = GetForegroundWindow();
        try
        {
            using var first = new Form { Text = "Meta Cue test window A", Bounds = new Rectangle(20, 20, 250, 100) };
            using var second = new Form { Text = "Meta Cue test window B", Bounds = new Rectangle(40, 40, 250, 100) };
            first.Show(); second.Show(); Application.DoEvents();
            var actual = WindowCatalog.Read();
            var candidate = actual.Single(w => w.Handle == first.Handle);
            Check(actual.Any(w => w.Handle == second.Handle), "Both real windows enumerated");
            if (original == IntPtr.Zero)
                Console.WriteLine("SKIP activation: no foreground window is accessible in this test desktop. Real hotkey activation must be tested in the user's input desktop.");
            else
            {
                first.WindowState = FormWindowState.Minimized;
                Check(WindowCatalog.Activate(candidate), "Activate returned success");
                var timeout = Stopwatch.StartNew();
                while (timeout.ElapsedMilliseconds < 1000 && (IsIconic(first.Handle) || GetForegroundWindow() != first.Handle))
                { Application.DoEvents(); Thread.Sleep(10); }
                Check(!IsIconic(first.Handle) && GetForegroundWindow() == first.Handle, "Native foreground and restored state read back");
                Console.WriteLine("Native foreground/restore readback passed.");
            }
            var wrongPid = candidate with { ProcessId = candidate.ProcessId + 1 };
            Check(!WindowCatalog.Activate(wrongPid), "Reused handle with different PID rejected");
            first.Close();
            Check(!WindowCatalog.Activate(candidate), "Window closed during selection rejected");
        }
        finally { if (original != IntPtr.Zero) SetForegroundWindow(original); }
        Console.WriteLine("Native tests passed: identity ambiguity, real enumeration, PID and closed-window protection. See activation result above.");
    }
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
}
