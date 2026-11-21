using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MetaCue;

// Compose every background and digit before publishing one frame in a reusable window.
internal sealed class Badge : Form
{
    internal Badge()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        _ = Handle;
        // Initialize fonts/GDI and the layered surface while still invisible at startup.
        Present(new[] { new Rectangle(0, 0, 24, 24) }, false);
    }

    internal void Present(IReadOnlyList<Rectangle> icons, bool show = true)
    {
        if (icons.Count == 0) { Hide(); return; }
        Rectangle bounds = icons[0];
        for (int i = 1; i < icons.Count; i++) bounds = Rectangle.Union(bounds, icons[i]);
        using var bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppPArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        using (var background = new SolidBrush(Color.FromArgb(255, 20, 20, 20)))
        using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
        {
            graphics.Clear(Color.Transparent);
            graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            int size = Math.Max(20, (int)(20 * DeviceDpi / 96.0));
            using var font = new Font(FontFamily.GenericSansSerif, size * 0.65f, FontStyle.Bold, GraphicsUnit.Pixel);
            for (int i = 0; i < icons.Count; i++)
            {
                var icon = icons[i];
                var rect = new Rectangle(icon.Left - bounds.Left + (icon.Width - size) / 2, icon.Top - bounds.Top, size, size);
                graphics.FillRectangle(background, rect);
                graphics.DrawString(((i + 1) % 10).ToString(), font, Brushes.White, rect, format);
            }
        }
        IntPtr screen = GetDC(IntPtr.Zero);
        IntPtr memory = IntPtr.Zero, image = IntPtr.Zero, previous = IntPtr.Zero;
        try
        {
            if (screen == IntPtr.Zero) throw new Win32Exception();
            memory = CreateCompatibleDC(screen);
            if (memory == IntPtr.Zero) throw new Win32Exception();
            image = bitmap.GetHbitmap(Color.FromArgb(0));
            previous = SelectObject(memory, image);
            var destination = new Point(bounds.Left, bounds.Top);
            var source = Point.Empty;
            var size = bounds.Size;
            var blend = new Blend { Alpha = 255, Format = 1 };
            if (!UpdateLayeredWindow(Handle, screen, ref destination, ref size, memory, ref source, 0, ref blend, 2))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            if (show) { Show(); RaiseWithoutActivation(); }
        }
        finally
        {
            if (previous != IntPtr.Zero && previous != new IntPtr(-1)) SelectObject(memory, previous);
            if (image != IntPtr.Zero) DeleteObject(image);
            if (memory != IntPtr.Zero) DeleteDC(memory);
            if (screen != IntPtr.Zero) ReleaseDC(IntPtr.Zero, screen);
        }
    }

    internal void RaiseWithoutActivation()
    {
        // Explorer can raise its own topmost taskbar after Start/menu interactions.
        // TopMost=true alone is cached by WinForms and does not reassert Z order.
        if (!SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x01 | 0x02 | 0x10 | 0x40))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams
    {
        get { var p = base.CreateParams; p.ExStyle |= 0x08000000 | 0x00000080 | 0x00000020 | 0x00080000; return p; }
    }
    protected override void OnPaint(PaintEventArgs e) { }
    protected override void OnPaintBackground(PaintEventArgs e) { }

    [StructLayout(LayoutKind.Sequential, Pack = 1)] private struct Blend { public byte Operation, Flags, Alpha, Format; }
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr window);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr window, IntPtr dc);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool UpdateLayeredWindow(IntPtr window, IntPtr dstDc,
        ref Point destination, ref Size size, IntPtr srcDc, ref Point source, uint key, ref Blend blend, uint flags);
}
