using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MetaCue;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Length == 2 && args[0] == "--diagnose")
        {
            File.WriteAllText(args[1], Taskbar.Diagnose());
            return;
        }
        using var single = new Mutex(true, @"Local\MetaCue.Windows", out bool first);
        if (!first) return;
        try { using var app = new CueApplication(); Application.Run(app); }
        catch (Exception e) { MessageBox.Show(e.Message, "Meta Cue", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
}

internal sealed class CueApplication : ApplicationContext
{
    private readonly Gesture gesture = new();
    private readonly HintQueryGate queries = new();
    private readonly Native.HookProc callback;
    private readonly IntPtr hook;
    private readonly NotifyIcon tray;
    private readonly Icon trayIcon;
    private readonly System.Windows.Forms.Timer timer = new() { Interval = 40 };
    private readonly Control dispatcher = new();
    private readonly Badge overlay = new();
    private readonly Stopwatch elapsed = new();
    private readonly string configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MetaCue", "delay-ms.txt");
    private int delay = 300;
    private bool enabled = true, preview, shown, presentedDuringHold, disposed;
    private long lastRefresh, retryAt;
    private List<Rectangle> positions = new();

    internal CueApplication()
    {
        _ = dispatcher.Handle;
        LoadDelay();
        SeedPressedKeys();
        callback = Keyboard;
        hook = Native.SetWindowsHookEx(13, callback, Native.GetModuleHandle(null), 0);
        if (hook == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        var menu = new ContextMenuStrip();
        menu.Items.Add("预览 2 秒", null, (_, _) => StartPreview());
        var toggle = new ToolStripMenuItem("启用") { Checked = true, CheckOnClick = true };
        toggle.CheckedChanged += (_, _) => { enabled = toggle.Checked; Cancel(); };
        menu.Items.Add(toggle);
        menu.Items.Add("重新读取延迟配置", null, (_, _) => { Cancel(); LoadDelay(); });
        menu.Items.Add("退出", null, (_, _) => ExitThread());
        using (var stream = typeof(CueApplication).Assembly.GetManifestResourceStream("MetaCue.Icon")
            ?? throw new InvalidOperationException("Meta Cue icon resource is missing."))
        using (var source = new Icon(stream, SystemInformation.SmallIconSize))
            trayIcon = (Icon)source.Clone();
        tray = new NotifyIcon { Icon = trayIcon, Text = "Meta Cue — 长按 Win 显示编号", ContextMenuStrip = menu, Visible = true };
        tray.DoubleClick += (_, _) => StartPreview();
        timer.Tick += Tick;
        SystemEvents.SessionSwitch += SessionChanged;
        SystemEvents.PowerModeChanged += PowerChanged;
        SystemEvents.DisplaySettingsChanged += DisplayChanged;
    }

    private void SeedPressedKeys()
    {
        presentedDuringHold = false;
        gesture.Reset();
        foreach (int key in PressedKeys()) gesture.Update(key, true);
    }

    private static List<int> PressedKeys()
    {
        var keys = new List<int>();
        for (int key = 8; key < 256; key++)
        {
            // The hook reports sided modifiers. Do not seed their generic aliases,
            // which would otherwise remain down after a sided key-up.
            if (key is 0x10 or 0x11 or 0x12) continue;
            if ((Native.GetAsyncKeyState(key) & 0x8000) != 0) keys.Add(key);
        }
        return keys;
    }

    private IntPtr Keyboard(int code, IntPtr w, IntPtr data)
    {
        if (code >= 0 && (Marshal.ReadInt32(data, 8) & 0x10) == 0)
        {
            int message = w.ToInt32();
            bool pressed = message == 0x100 || message == 0x104;
            if (pressed || message == 0x101 || message == 0x105)
            {
                int key = Marshal.ReadInt32(data);
                bool winKey = key is 0x5B or 0x5C;
                bool cancelRelease = !pressed && winKey && presentedDuringHold && gesture.Eligible
                    && (Native.GetAsyncKeyState(key) & 0x8000) != 0;
                bool arm = pressed && winKey
                    ? gesture.WinDown(key, PressedKeys()) : gesture.Update(key, pressed);
                if (arm || !gesture.Eligible) presentedDuringHold = false;
                if (!gesture.Eligible) Cancel();
                else if (arm && enabled)
                {
                    Cancel();
                    elapsed.Restart();
                    timer.Start();
                }
                if (cancelRelease && Native.ReleaseWinWithoutStartMenu(key, Marshal.ReadInt32(data, 4)))
                    return new IntPtr(1);
            }
        }
        return Native.CallNextHookEx(hook, code, w, data);
    }

    private void Cancel()
    {
        queries.Invalidate();
        timer.Stop();
        elapsed.Reset();
        preview = false;
        retryAt = 0;
        HideHints();
    }

    private void HideHints()
    {
        shown = false;
        overlay.Hide();
        positions.Clear();
    }

    private void LoadDelay()
    {
        delay = 300;
        try { if (int.TryParse(File.ReadAllText(configPath).Trim(), out int value)) delay = Math.Clamp(value, 100, 2000); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private void StartPreview()
    {
        Cancel();
        preview = true;
        elapsed.Restart();
        timer.Start();
    }

    private async void Tick(object? sender, EventArgs e)
    {
        bool winHeld = (Native.GetAsyncKeyState(0x5B) & 0x8000) != 0 || (Native.GetAsyncKeyState(0x5C) & 0x8000) != 0;
        if (preview ? elapsed.ElapsedMilliseconds >= 2000 : !gesture.Eligible || !winHeld)
        { Cancel(); return; }
        long now = Environment.TickCount64;
        if (queries.Busy)
        {
            if (queries.Overdue(now)) HideHints();
            return;
        }
        if (now < retryAt) return;
        if (!shown && positions.Count > 0)
        {
            if (preview || elapsed.ElapsedMilliseconds >= delay) Present();
            return;
        }
        // Resolve positions during the hold delay instead of adding UIA latency after it.
        if (!preview && elapsed.ElapsedMilliseconds < Math.Max(0, delay - 250)) return;
        if (shown && elapsed.ElapsedMilliseconds - lastRefresh < 200) return;
        if (!queries.TryBegin(now, out var ticket)) return;
        try
        {
            // UI Automation can block in Explorer; never call it from the keyboard hook.
            var found = await Task.Run(Taskbar.Read);
            if (disposed || !queries.IsCurrent(ticket)) return;
            if (found.Count == 0) { HideHints(); retryAt = Environment.TickCount64 + 100; return; }
            if (shown)
            {
                bool changed = found.Count != positions.Count;
                for (int i = 0; !changed && i < found.Count; i++) changed = found[i] != positions[i];
                if (changed) Cancel();
                else overlay.RaiseWithoutActivation();
                lastRefresh = elapsed.ElapsedMilliseconds;
                return;
            }
            positions = found;
            if (preview || elapsed.ElapsedMilliseconds >= delay) Present();
        }
        catch (Exception ex) when (ex is System.Windows.Automation.ElementNotAvailableException || ex is COMException || ex is InvalidOperationException || ex is Win32Exception)
        {
            if (!disposed && queries.IsCurrent(ticket))
            { HideHints(); retryAt = Environment.TickCount64 + 100; }
        }
        finally { queries.Complete(ticket); }
    }

    private void Present()
    {
        // A query can finish between timer ticks after a release that the hook missed.
        if (!preview && (!gesture.Eligible ||
            ((Native.GetAsyncKeyState(0x5B) & 0x8000) == 0 && (Native.GetAsyncKeyState(0x5C) & 0x8000) == 0)))
        { Cancel(); return; }
        try { overlay.Present(positions); }
        catch (Win32Exception) { Cancel(); return; }
        shown = true;
        if (!preview) presentedDuringHold = true;
        lastRefresh = elapsed.ElapsedMilliseconds;
    }

    private void ResetSession()
    {
        if (disposed) return;
        try { dispatcher.BeginInvoke(new Action(() => { Cancel(); SeedPressedKeys(); })); }
        catch (InvalidOperationException) { }
    }
    private void SessionChanged(object sender, SessionSwitchEventArgs e) => ResetSession();
    private void PowerChanged(object sender, PowerModeChangedEventArgs e) => ResetSession();
    private void DisplayChanged(object? sender, EventArgs e) => ResetSession();
    protected override void Dispose(bool disposing)
    {
        if (disposing && !disposed)
        {
            disposed = true;
            SystemEvents.SessionSwitch -= SessionChanged;
            SystemEvents.PowerModeChanged -= PowerChanged;
            SystemEvents.DisplaySettingsChanged -= DisplayChanged;
            Native.UnhookWindowsHookEx(hook);
            Cancel(); timer.Dispose(); overlay.Dispose();
            tray.Visible = false; tray.ContextMenuStrip?.Dispose(); tray.Dispose(); trayIcon.Dispose(); dispatcher.Dispose();
        }
        base.Dispose(disposing);
    }
}
