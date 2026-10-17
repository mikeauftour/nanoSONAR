using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NanoSonarController;

// An informational, focus-free topmost window. Never activated by mouse or keyboard.
internal sealed class VolumeOsdForm : Form
{
    private readonly System.Windows.Forms.Timer dismiss = new() { Interval = 3000 };
    private string channel = "Game";
    private string mix = "Monitoring";
    private int percent;
    private bool muted;
    

    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x08000000 | 0x00000080 | 0x00000020; // NOACTIVATE, TOOLWINDOW, TRANSPARENT
            return cp;
        }
    }

    public VolumeOsdForm()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(318, 110);
        BackColor = Color.FromArgb(16, 21, 28);
        DoubleBuffered = true;
        dismiss.Tick += (_, _) => { dismiss.Stop(); Hide(); };
    }

    public void Display(string channelName, string mixName, int level, bool isMuted, string position, int seconds)
    {
        if (IsDisposed) return;
        channel = channelName;
        mix = mixName;
        percent = Math.Clamp(level, 0, 100);
        muted = isMuted;
        dismiss.Stop();
        dismiss.Interval = Math.Clamp(seconds, 1, 10) * 1000;
        var screen = Screen.FromPoint(Cursor.Position).WorkingArea;
        // Always place on the primary screen for predictable positioning.
        screen = Screen.PrimaryScreen?.WorkingArea ?? screen;
        const int margin = 18;
        int x = position switch
        {
            "top-left" or "bottom-left" => screen.Left + margin,
            "top-center" or "bottom-center" => screen.Left + (screen.Width - Width) / 2,
            _ => screen.Right - Width - margin
        };
        int y = position.StartsWith("bottom-", StringComparison.Ordinal)
            ? screen.Bottom - Height - margin
            : screen.Top + margin;
        Location = new Point(x, y);
        if (!Visible) Show();
        Invalidate();
        dismiss.Start();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.FromArgb(16, 21, 28));
        using var border = new Pen(Color.FromArgb(62, 85, 101), 1);
        g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        using var small = new Font("Segoe UI", 9, FontStyle.Regular);
        using var large = new Font("Segoe UI Semibold", 17, FontStyle.Bold);
        using var number = new Font("Segoe UI Semibold", 16, FontStyle.Bold);
        using var mutedBrush = new SolidBrush(Color.FromArgb(240, 91, 91));
        using var accent = new SolidBrush(Color.FromArgb(67, 205, 211));
        using var secondary = new SolidBrush(Color.FromArgb(161, 175, 190));
        using var white = new SolidBrush(Color.White);
        g.DrawString("nanoSONAR  ·  " + mix.ToUpperInvariant(), small, secondary, 16, 12);
        g.DrawString(channel, large, white, 14, 35);
        var value = muted ? "STUMM" : percent + "%";
        var brush = muted ? mutedBrush : accent;
        var valueSize = g.MeasureString(value, number);
        g.DrawString(value, number, brush, Width - valueSize.Width - 16, 39);
        using var track = new SolidBrush(Color.FromArgb(51, 64, 77));
        g.FillRectangle(track, 17, 89, Width - 34, 7);
        if (!muted) g.FillRectangle(accent, 17, 89, (int)((Width - 34) * percent / 100.0), 7);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) dismiss.Dispose();
        base.Dispose(disposing);
    }
}
