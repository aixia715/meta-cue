using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Forms;

namespace MetaCue;

internal sealed class ExtendedSwitcher : IDisposable
{
    private readonly Badge hints = new();
    private readonly SelectionPopup popup = new();
    private readonly CycleSelection selection = new();
    private readonly Action<string> notify;
    private readonly Task<Dictionary<string, HashSet<string>>> shortcuts = Task.Run(() => ShortcutCatalog.Read());
    private readonly System.Windows.Forms.Timer watchdog = new() { Interval = 100 };
    private List<TaskbarItem>? items;
    private List<AppWindow> windows = new();
    private Dictionary<string, HashSet<string>> identities = new(StringComparer.OrdinalIgnoreCase);
    private int generation;
    private bool active, disposed, busy;
    private long started;

    internal ExtendedSwitcher(Action<string> notify)
    {
        this.notify = notify;
        watchdog.Tick += (_, _) =>
        {
            if (active && items == null && Environment.TickCount64 - started > 1500)
            { Cancel(); notify("任务栏识别超时，请松开按键后重试。"); }
            // Also clean up if a key-up was lost on a desktop/session transition.
            if (active && !Held(0x5B) && !Held(0x5C) && !Held(0x10)) Cancel();
        };
    }
    private static bool Held(int key) => (Native.GetAsyncKeyState(key) & 0x8000) != 0;

    internal void Handle(SwitchKey key)
    {
        if (disposed) return;
        switch (key.Action)
        {
            case SwitchAction.Begin: Begin(); break;
            case SwitchAction.Select:
                if (!active) break;
                selection.Press(key.Slot);
                if (Held(0x5B) || Held(0x5C)) Native.MaskStartMenu();
                Present(); break;
            case SwitchAction.Commit: Commit(); break;
            case SwitchAction.Cancel: Cancel(); break;
        }
    }
    private async void Begin()
    {
        Cancel();
        if (busy) { notify("上一次任务栏查询尚未结束，请稍后重试。"); return; }
        active = busy = true;
        started = Environment.TickCount64;
        int stamp = generation;
        watchdog.Start();
        try
        {
            var snapshot = await Task.Run(() => (Items: Taskbar.ReadItems(), Windows: WindowCatalog.Read()));
            var ready = await Task.WhenAny(shortcuts, Task.Delay(1000));
            if (disposed || stamp != generation) return;
            items = snapshot.Items; windows = snapshot.Windows;
            if (ready == shortcuts) identities = await shortcuts;
            if (items.Count > 10)
                hints.Present(items.Skip(10).Take(10).Select(x => x.Bounds).ToList());
            Present();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        { if (!disposed && stamp == generation) { Cancel(); notify("无法读取任务栏；本次扩展切换已取消。"); } }
        finally { busy = false; }
    }
    private void Present()
    {
        if (!active || selection.Slot < 0 || items == null) return;
        int index = selection.Slot + 10;
        if (index >= items.Count)
        {
            var anchor = items.Count > 0 ? items[^1].Bounds : Screen.PrimaryScreen!.WorkingArea;
            popup.Present(anchor, $"第 {index + 1} 个任务栏应用不存在", "Esc 取消；不会触发原有的新建窗口快捷键。");
            return;
        }
        var item = items[index];
        var matches = WindowCatalog.Match(item, items, windows, identities);
        if (matches.Count == 0)
            popup.Present(item.Bounds, $"第 {index + 1} 项 · {item.Name}", "无法可靠枚举窗口；松开 Win 和 Shift 后调用原生任务栏按钮。Esc 取消。");
        else
        {
            int chosen = selection.Index(matches.Count);
            popup.Present(item.Bounds, $"第 {index + 1} 项 · 窗口 {chosen + 1}/{matches.Count}",
                matches[chosen].Title + "\n\n再按同一数字选择下一个；松开 Win 和 Shift 确认；Esc 取消。");
        }
    }
    private async void Commit()
    {
        if (!active) return;
        int slot = selection.Slot;
        if (items == null) { Cancel(); notify("识别尚未完成，本次未切换；请稍微多按住片刻。"); return; }
        if (slot < 0 || slot + 10 >= items.Count) { Cancel(); return; }
        var item = items[slot + 10];
        var matches = WindowCatalog.Match(item, items, windows, identities);
        var chosen = matches.Count > 0 ? matches[selection.Index(matches.Count)] : null;
        Cancel();
        int stamp = generation;
        if (busy) return;
        busy = true;
        try
        {
            // Re-read order and identity before any side effect, so a reordered taskbar
            // cannot cause the stale positional shortcut to activate a different app.
            var fresh = await Task.Run(() => (Items: Taskbar.ReadItems(), Windows: WindowCatalog.Read()));
            var current = fresh.Items;
            if (disposed || stamp != generation) return;
            if (slot + 10 >= current.Count || current[slot + 10].AppId != item.AppId || current[slot + 10].Bounds != item.Bounds)
            { notify("任务栏顺序已变化，本次切换已取消。"); return; }
            if (Held(0x5B) || Held(0x5C) || Held(0x10)) return;
            if (chosen != null)
            {
                if (!WindowCatalog.Match(current[slot + 10], current, fresh.Windows, identities)
                    .Any(w => w.Handle == chosen.Handle && w.ProcessId == chosen.ProcessId))
                { notify("选中的窗口已关闭或改变分组，本次切换已取消。"); return; }
                if (!WindowCatalog.Activate(chosen)) notify("Windows 未允许激活该窗口，或窗口已经关闭。");
            }
            else
            {
                // Invoke only after modifiers have been released. Never simulate Shift+click.
                var element = current[slot + 10].Element;
                await Task.Run(() =>
                {
                    if (element.TryGetCurrentPattern(InvokePattern.Pattern, out var pattern)) ((InvokePattern)pattern).Invoke();
                    else throw new InvalidOperationException("Taskbar button does not support Invoke.");
                });
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        { if (!disposed && stamp == generation) notify("无法激活目标任务栏应用。"); }
        finally { busy = false; }
    }
    internal void Cancel()
    {
        generation++; active = false; items = null; windows.Clear(); selection.Reset();
        watchdog.Stop(); hints.Hide(); popup.Hide();
    }
    public void Dispose()
    { disposed = true; Cancel(); watchdog.Dispose(); hints.Dispose(); popup.Dispose(); }
}
