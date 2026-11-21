using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Automation;

namespace MetaCue;

internal static class Taskbar
{
    internal static List<Rectangle> Read()
    {
        var result = new List<Rectangle>();
        var handle = Native.FindWindow("Shell_TrayWnd", null);
        if (handle == IntPtr.Zero || !Native.IsWindowVisible(handle)) return result;
        var root = AutomationElement.FromHandle(handle);
        var buttons = root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button));
        for (int i = 0; i < buttons.Count; i++)
        {
            var info = buttons[i].Current;
            // Explorer exposes app buttons separately from Start, Search, Widgets and the tray.
            if (!info.ClassName.Equals("Taskbar.TaskListButtonAutomationPeer", StringComparison.Ordinal) &&
                !info.ClassName.Equals("TaskListButton", StringComparison.Ordinal)) continue;
            // Skipping an app would shift every later Win+number label.
            if (info.IsOffscreen || info.BoundingRectangle.IsEmpty) return new List<Rectangle>();
            var r = info.BoundingRectangle;
            result.Add(Rectangle.FromLTRB((int)r.Left, (int)r.Top, (int)r.Right, (int)r.Bottom));
        }
        result.Sort((a, b) => a.Left.CompareTo(b.Left));
        // Fail closed for vertical/multirow/partially hidden layouts.
        if (result.Count > 0)
        {
            var screen = System.Windows.Forms.Screen.FromHandle(handle).Bounds;
            if (result.Exists(r => Math.Abs(r.Top - result[0].Top) > 4 || r.Bottom > screen.Bottom || r.Top < screen.Bottom - 150)) result.Clear();
        }
        return result.GetRange(0, Math.Min(10, result.Count));
    }

    internal static string Diagnose()
    {
        var handle = Native.FindWindow("Shell_TrayWnd", null);
        if (handle == IntPtr.Zero) return "Explorer taskbar not found.";
        var all = AutomationElement.FromHandle(handle).FindAll(TreeScope.Descendants, System.Windows.Automation.Condition.TrueCondition);
        var lines = new List<string>();
        for (int i = 0; i < all.Count; i++)
        {
            var c = all[i].Current;
            lines.Add($"{c.ControlType.ProgrammaticName} | {c.ClassName} | {c.AutomationId} | {c.BoundingRectangle} | offscreen={c.IsOffscreen}");
        }
        return string.Join(Environment.NewLine, lines);
    }
}
