using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Automation;

namespace MetaCue;

internal sealed record TaskbarItem(Rectangle Bounds, string AppId, string Name, AutomationElement Element);

internal static class Taskbar
{
    internal static List<Rectangle> Read()
    {
        var items = ReadItems();
        var result = new List<Rectangle>();
        for (int i = 0; i < Math.Min(10, items.Count); i++) result.Add(items[i].Bounds);
        return result;
    }

    internal static List<TaskbarItem> ReadItems()
    {
        var result = new List<TaskbarItem>();
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
            if (info.IsOffscreen || info.BoundingRectangle.IsEmpty) return new List<TaskbarItem>();
            var r = info.BoundingRectangle;
            string id = info.AutomationId;
            if (!id.StartsWith("Appid:", StringComparison.OrdinalIgnoreCase)) return new List<TaskbarItem>();
            result.Add(new TaskbarItem(Rectangle.FromLTRB((int)r.Left, (int)r.Top, (int)r.Right, (int)r.Bottom),
                id[6..].Trim(), info.Name, buttons[i]));
        }
        result.Sort((a, b) => a.Bounds.Left.CompareTo(b.Bounds.Left));
        // Fail closed for vertical/multirow/partially hidden layouts.
        if (result.Count > 0)
        {
            var screen = System.Windows.Forms.Screen.FromHandle(handle).Bounds;
            if (result.Exists(item => Math.Abs(item.Bounds.Top - result[0].Bounds.Top) > 4 || item.Bounds.Bottom > screen.Bottom || item.Bounds.Top < screen.Bottom - 150)) result.Clear();
        }
        return result;
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
