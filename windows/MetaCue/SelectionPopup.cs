using System;
using System.Drawing;
using System.Windows.Forms;

namespace MetaCue;

internal sealed class SelectionPopup : Form
{
    private string heading = "", detail = "";
    internal SelectionPopup()
    {
        FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true;
        StartPosition = FormStartPosition.Manual; DoubleBuffered = true;
        BackColor = Color.FromArgb(28, 28, 28); ForeColor = Color.White;
    }
    internal void Present(Rectangle anchor, string title, string body)
    {
        heading = title; detail = body;
        var work = Screen.FromRectangle(anchor).WorkingArea;
        int width = Math.Min(520, work.Width), height = Math.Min(150, work.Height);
        Bounds = new Rectangle(Math.Clamp(anchor.Left + anchor.Width / 2 - width / 2, work.Left, work.Right - width),
            Math.Max(work.Top, anchor.Top - height - 10), width, height);
        Show(); Invalidate(); Update();
    }
    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams
    { get { var p = base.CreateParams; p.ExStyle |= 0x08000000 | 0x80; return p; } }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        TextRenderer.DrawText(e.Graphics, heading, SystemFonts.MessageBoxFont, new Rectangle(16, 12, Width - 32, 28), ForeColor,
            TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        TextRenderer.DrawText(e.Graphics, detail, SystemFonts.MessageBoxFont, new Rectangle(16, 45, Width - 32, Height - 57), ForeColor,
            TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
    }
}
