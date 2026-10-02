using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FSGolfPL;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new OverlayForm());
    }
}

/// <summary>
/// Desktop overlay prototype. The window locator is deliberately isolated from
/// parameter rendering so a local OCR/value provider can be connected later.
/// </summary>
public sealed class OverlayForm : Form
{
    private const int OverlayHeight = 112;
    private const int PollIntervalMs = 500;
    private static readonly IntPtr GwlpHwndParent = new(-8);
    private static readonly IntPtr HwndTop = IntPtr.Zero;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;

    private readonly FlowLayoutPanel _panel = new()
    {
        Dock = DockStyle.Fill,
        AutoScroll = false,
        WrapContents = true,
        FlowDirection = FlowDirection.LeftToRight,
        BackColor = Color.FromArgb(226, 20, 20, 20),
        Padding = new Padding(8)
    };
    private readonly Label _status = new()
    {
        AutoSize = false,
        Dock = DockStyle.Bottom,
        Height = 20,
        ForeColor = Color.Gainsboro,
        BackColor = Color.FromArgb(226, 20, 20, 20),
        Font = new Font("Segoe UI", 8, FontStyle.Regular),
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(8, 0, 0, 0)
    };
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = PollIntervalMs };
    private IntPtr _targetHandle;
    private bool _hasTarget;

    public OverlayForm()
    {
        Text = "FS Golf PL — tłumaczenia";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.Magenta;
        TransparencyKey = Color.Magenta;
        Opacity = 0.96;
        MinimumSize = new Size(360, 72);
        Size = new Size(900, OverlayHeight);
        Location = new Point(40, 40);

        foreach (var parameter in ParameterCatalog.All)
            AddParameter(parameter);

        Controls.Add(_panel);
        Controls.Add(_status);
        _timer.Tick += (_, _) => RefreshTarget();
        Shown += (_, _) => RefreshTarget();
        FormClosed += (_, _) => _timer.Dispose();
        _timer.Start();
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= WsExToolWindow | WsExNoActivate;
            return parameters;
        }
    }

    private void AddParameter(ParameterDefinition parameter)
    {
        _panel.Controls.Add(new Label
        {
            Text = $"{parameter.EnglishLabel} → {parameter.PolishLabel}",
            ForeColor = Color.White,
            BackColor = Color.FromArgb(190, 42, 42, 42),
            AutoSize = true,
            Font = new Font("Segoe UI", 9, FontStyle.Regular),
            Padding = new Padding(7, 5, 7, 5),
            Margin = new Padding(3)
        });
    }

    private void RefreshTarget()
    {
        var target = FsGolfWindowLocator.FindBestCandidate();
        if (target is null)
        {
            if (_hasTarget)
            {
                SetOwner(IntPtr.Zero);
                _targetHandle = IntPtr.Zero;
                _hasTarget = false;
                TopMost = false;
                Location = new Point(40, 40);
                Size = new Size(900, OverlayHeight);
                if (!Visible)
                    Show();
            }

            _status.Text = "Oczekiwanie na widoczne okno FS Golf / FlightScope…";
            return;
        }

        if (!_hasTarget || _targetHandle != target.Handle)
            SetOwner(target.Handle);
        _targetHandle = target.Handle;
        _hasTarget = true;

        if (target.IsMinimized)
        {
            _status.Text = $"Wykryto: {target.Title} — overlay ukryty dla zminimalizowanego okna";
            if (Visible)
                Hide();
            return;
        }

        if (!Visible)
            Show();

        // The overlay is an owned window: Windows keeps it above the game window
        // and hides it with the owner. It does not alter or interact with the game.
        TopMost = false;
        var bounds = target.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;

        var height = Math.Min(OverlayHeight, bounds.Height);
        SetWindowPos(Handle, HwndTop, bounds.Left, bounds.Top, bounds.Width, height,
            SwpNoActivate | SwpShowWindow);
        _status.Text = $"Wykryto: {target.Title} ({target.ProcessName})  •  {bounds.Width} × {bounds.Height}";
    }

    private void SetOwner(IntPtr owner)
    {
        SetWindowLongPtrW(Handle, GwlpHwndParent, owner);
    }

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtrW(IntPtr window, IntPtr index, IntPtr value);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y,
        int width, int height, uint flags);
}
