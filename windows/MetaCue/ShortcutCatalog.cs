using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace MetaCue;

// Read declared identities from shortcuts, never infer groups from display names.
internal static class ShortcutCatalog
{
    internal static Dictionary<string, HashSet<string>> Read(Action<string>? diagnostic = null)
    {
        var result = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var roots = new[] {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar"),
            Environment.GetFolderPath(Environment.SpecialFolder.Programs), Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms) };
        foreach (string root in roots)
        {
            diagnostic?.Invoke("Shortcut root: " + root);
            if (!Directory.Exists(root)) continue;
            try
            {
                var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
                foreach (string file in Directory.EnumerateFiles(root, "*.lnk", options))
                {
                    object? link = null;
                    try
                    {
                        link = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("00021401-0000-0000-C000-000000000046"))!);
                        ((IPersistFile)link!).Load(file, 0); // STGM_READ: common Start Menu is not writable.
                        var key = new PropertyKey { Format = new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), Id = 5 };
                        string id = "";
                        var iid = typeof(IPropertyStore).GUID;
                        if (SHGetPropertyStoreFromParsingName(file, IntPtr.Zero, 0, ref iid, out var store) == 0)
                        {
                            try
                            {
                                if (store.GetValue(ref key, out var value) == 0)
                                {
                                    try { id = value.Type == 31 ? Marshal.PtrToStringUni(value.Pointer) ?? "" : ""; }
                                    finally { PropVariantClear(ref value); }
                                }
                            }
                            finally { Marshal.ReleaseComObject(store); }
                        }
                        if (id.Length == 0) continue;
                        var buffer = new StringBuilder(32768);
                        ((IShellLink)link).GetPath(buffer, buffer.Capacity, IntPtr.Zero, 0);
                        string path = buffer.ToString();
                        var product = new StringBuilder(39); var feature = new StringBuilder(256); var component = new StringBuilder(39);
                        if (MsiGetShortcutTarget(file, product, feature, component) == 0)
                        {
                            uint size = (uint)buffer.Capacity;
                            int state = MsiGetComponentPath(product.ToString(), component.ToString(), buffer, ref size);
                            if (state is 3 or 4) path = buffer.ToString();
                        }
                        if (!Path.IsPathRooted(path) || !path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                        if (!result.TryGetValue(id, out var paths)) result[id] = paths = new(StringComparer.OrdinalIgnoreCase);
                        paths.Add(WindowCatalog.LongPath(path));
                    }
                    catch (Exception e) when (e is COMException or IOException or UnauthorizedAccessException)
                    { diagnostic?.Invoke("Shortcut read failed: " + file + " | " + e.Message); }
                    finally { if (link != null) Marshal.ReleaseComObject(link); }
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { diagnostic?.Invoke("Shortcut root error: " + e.Message); }
        }
        return result;
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
    }
    [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellLink
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int count, IntPtr findData, uint flags);
    }
    [DllImport("ole32.dll")] private static extern int PropVariantClear(ref PropVariant value);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern int SHGetPropertyStoreFromParsingName(string path, IntPtr context, uint flags, ref Guid iid, out IPropertyStore store);
    [DllImport("msi.dll", CharSet = CharSet.Unicode)] private static extern uint MsiGetShortcutTarget(string shortcut, StringBuilder product, StringBuilder feature, StringBuilder component);
    [DllImport("msi.dll", CharSet = CharSet.Unicode)] private static extern int MsiGetComponentPath(string product, string component, StringBuilder path, ref uint size);
}
