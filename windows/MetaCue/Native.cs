using System;
using System.Runtime.InteropServices;

namespace MetaCue;

internal static class Native
{
    internal delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", SetLastError = true)] internal static extern IntPtr SetWindowsHookEx(int id, HookProc callback, IntPtr module, uint thread);
    [DllImport("user32.dll")] internal static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] internal static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr GetModuleHandle(string? name);
    [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern IntPtr FindWindow(string className, string? title);
    [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr handle);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, INPUT[] inputs, int size);
    [StructLayout(LayoutKind.Sequential)] private struct INPUT { public uint type; public INPUTUNION data; }
    [StructLayout(LayoutKind.Explicit)] private struct INPUTUNION
    {
        [FieldOffset(0)] public KEYBDINPUT keyboard;
        [FieldOffset(0)] public MOUSEINPUT mouse;
    }
    [StructLayout(LayoutKind.Sequential)] private struct KEYBDINPUT { public ushort vk, scan; public uint flags, time; public UIntPtr extra; }
    [StructLayout(LayoutKind.Sequential)] private struct MOUSEINPUT { public int x, y; public uint data, flags, time; public UIntPtr extra; }
    // Keep the Ctrl tap immediately before Win-up, even if Win repeated after showing the badges.
    internal static bool ReleaseWinWithoutStartMenu(int winKey, int winScan)
    {
        const uint keyboard = 1, extended = 0x0001, keyUp = 0x0002, scanCode = 0x0008;
        var inputs = new INPUT[3];
        inputs[0].type = inputs[1].type = inputs[2].type = keyboard;
        inputs[0].data.keyboard.scan = inputs[1].data.keyboard.scan = 0x1D; // Left Ctrl
        inputs[0].data.keyboard.flags = scanCode;
        inputs[1].data.keyboard.flags = scanCode | keyUp;
        inputs[2].data.keyboard.vk = (ushort)winKey;
        inputs[2].data.keyboard.scan = (ushort)winScan;
        inputs[2].data.keyboard.flags = extended | keyUp;
        int inputSize = Marshal.SizeOf<INPUT>();
        uint sent = SendInput(3, inputs, inputSize);
        if (sent == 1) SendInput(1, new[] { inputs[1] }, inputSize);
        return sent == 3;
    }
}
