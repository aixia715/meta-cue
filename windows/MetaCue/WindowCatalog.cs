using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace MetaCue;

internal sealed record AppWindow(IntPtr Handle, uint ProcessId, string Title, string AppId, string Path);

internal static class WindowCatalog
{
    internal static List<AppWindow> Read()
    {
        var result = new List<AppWindow>();
        EnumWindows((window, _) =>
        {
            if (!Native.IsWindowVisible(window) || GetWindow(window, 4) != IntPtr.Zero) return true;
            long style = GetWindowLongPtr(window, -20).ToInt64();
            if ((style & 0x80) != 0) return true;
            DwmGetWindowAttribute(window, 14, out int cloaked, sizeof(int));
            if (cloaked != 0) return true;
            var title = new StringBuilder(1024);
            GetWindowText(window, title, title.Capacity);
            if (title.Length == 0) return true;
            GetWindowThreadProcessId(window, out uint pid);
            string appId = ReadWindowAppId(window), path = "";
            IntPtr process = OpenProcess(0x1000, false, pid);
            if (process != IntPtr.Zero)
            {
                try
                {
                    var buffer = new StringBuilder(32768);
                    int length = buffer.Capacity;
                    if (QueryFullProcessImageName(process, 0, buffer, ref length)) path = buffer.ToString();
                    if (appId.Length == 0)
                    {
                        uint count = 0;
                        if (GetApplicationUserModelId(process, ref count, null) == 122 && count < 32768)
                        {
                            var id = new StringBuilder((int)count);
                            if (GetApplicationUserModelId(process, ref count, id) == 0) appId = id.ToString();
                        }
                    }
                }
                finally { CloseHandle(process); }
            }
            result.Add(new AppWindow(window, pid, title.ToString(), appId, LongPath(path)));
            return true;
        }, IntPtr.Zero);
        return result;
    }

    internal static string ExpandAppIdPath(string id)
    {
        if (id.StartsWith("{", StringComparison.Ordinal) && id.IndexOf('}') is int end && end > 0 && Guid.TryParse(id[..(end + 1)], out var folder))
        {
            if (SHGetKnownFolderPath(ref folder, 0, IntPtr.Zero, out IntPtr path) == 0)
            {
                try { return LongPath((Marshal.PtrToStringUni(path) ?? "") + id[(end + 1)..]); }
                finally { Marshal.FreeCoTaskMem(path); }
            }
        }
        return Path.IsPathRooted(id) ? LongPath(id) : id;
    }

    internal static string LongPath(string path)
    {
        var buffer = new StringBuilder(32768);
        uint count = GetLongPathName(path, buffer, (uint)buffer.Capacity);
        return count > 0 && count < buffer.Capacity ? buffer.ToString() : path;
    }

    internal static List<AppWindow> Match(string id, IReadOnlyList<AppWindow> windows)
    {
        string path = ExpandAppIdPath(id);
        bool isPath = Path.IsPathRooted(path);
        var result = new List<AppWindow>();
        foreach (var window in windows)
        {
            if (string.Equals(window.AppId, id, StringComparison.OrdinalIgnoreCase) ||
                (window.AppId.Length == 0 && isPath && string.Equals(window.Path, path, StringComparison.OrdinalIgnoreCase)))
                result.Add(window);
        }
        return result;
    }

    internal static List<AppWindow> Match(TaskbarItem item, IReadOnlyList<TaskbarItem> items,
        IReadOnlyList<AppWindow> windows, Dictionary<string, HashSet<string>> shortcuts)
    {
        var result = Match(item.AppId, windows);
        if (!shortcuts.TryGetValue(item.AppId, out var paths)) return result;
        foreach (var window in windows)
        {
            if (window.AppId.Length != 0 || result.Contains(window) || !paths.Contains(window.Path)) continue;
            bool ambiguous = false;
            foreach (var other in items)
            {
                if (other.AppId == item.AppId) continue;
                if (string.Equals(ExpandAppIdPath(other.AppId), window.Path, StringComparison.OrdinalIgnoreCase) ||
                    (shortcuts.TryGetValue(other.AppId, out var otherPaths) && otherPaths.Contains(window.Path)))
                { ambiguous = true; break; }
            }
            if (!ambiguous) result.Add(window);
        }
        return result;
    }

    internal static bool Activate(AppWindow window)
    {
        if (!IsWindow(window.Handle)) return false;
        GetWindowThreadProcessId(window.Handle, out uint pid);
        if (pid != window.ProcessId) return false;
        if (IsIconic(window.Handle)) ShowWindowAsync(window.Handle, 9);
        return SetForegroundWindow(window.Handle);
    }

    private static string ReadWindowAppId(IntPtr window)
    {
        var iid = typeof(IPropertyStore).GUID;
        if (SHGetPropertyStoreForWindow(window, ref iid, out var store) != 0) return "";
        try
        {
            var key = new PropertyKey { Format = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), Id = 5 };
            if (store.GetValue(ref key, out var value) != 0) return "";
            try { return value.Type == 31 ? Marshal.PtrToStringUni(value.Pointer) ?? "" : ""; }
            finally { PropVariantClear(ref value); }
        }
        finally { Marshal.ReleaseComObject(store); }
    }

    [StructLayout(LayoutKind.Sequential)] private struct PropertyKey { public Guid Format; public uint Id; }
    [StructLayout(LayoutKind.Explicit, Size = 24)] private struct PropVariant
    { [FieldOffset(0)] public ushort Type; [FieldOffset(8)] public IntPtr Pointer; }
    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
        [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant value);
        [PreserveSig] int Commit();
    }
    private delegate bool EnumProc(IntPtr window, IntPtr data);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc callback, IntPtr data);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr window, uint command);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, StringBuilder text, int count);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out int value, int size);
    [DllImport("kernel32.dll")] private static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern uint GetLongPathName(string path, StringBuilder buffer, uint count);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder name, ref int size);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern int GetApplicationUserModelId(IntPtr process, ref uint size, StringBuilder? id);
    [DllImport("shell32.dll")] private static extern int SHGetPropertyStoreForWindow(IntPtr window, ref Guid iid, out IPropertyStore store);
    [DllImport("shell32.dll")] private static extern int SHGetKnownFolderPath(ref Guid folder, uint flags, IntPtr token, out IntPtr path);
    [DllImport("ole32.dll")] private static extern int PropVariantClear(ref PropVariant value);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] private static extern bool ShowWindowAsync(IntPtr window, int command);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
}
