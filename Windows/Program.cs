using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FSGolfPL;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        var smokeTest = args.Contains("--smoke-test", StringComparer.OrdinalIgnoreCase);
        var testTarget = args.Contains("--test-target", StringComparer.OrdinalIgnoreCase);
        var diagnosticMode = smokeTest || testTarget;
        Exception? unhandledException = null;

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, eventArgs) =>
        {
            unhandledException = eventArgs.Exception;
            RuntimeDiagnostics.Log(eventArgs.Exception);
            if (!diagnosticMode)
            {
                MessageBox.Show(
                    $"FS Golf PL napotkał błąd. Szczegóły zapisano w pliku:\n{RuntimeDiagnostics.LogFilePath}",
                    "FS Golf PL",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        };
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            if (eventArgs.ExceptionObject is Exception exception)
                RuntimeDiagnostics.Log(exception);
        };

        try
        {
            ApplicationConfiguration.Initialize();

            if (testTarget)
            {
                Application.Run(new TestTargetForm());
                return unhandledException is null ? 0 : 1;
            }

            var form = new OverlayForm();
            if (smokeTest)
            {
                var smokeCompleted = false;
                using var smokeTimer = new System.Windows.Forms.Timer { Interval = 8000 };
                smokeTimer.Tick += (_, _) =>
                {
                    smokeTimer.Stop();
                    smokeCompleted = true;
                    form.Close();
                };
                form.Shown += (_, _) => smokeTimer.Start();
                Application.Run(form);
                return smokeCompleted && unhandledException is null && form.LastRefreshError is null ? 0 : 1;
            }

            Application.Run(form);
            return unhandledException is null ? 0 : 1;
        }
        catch (Exception exception)
        {
            RuntimeDiagnostics.Log(exception);
            if (!diagnosticMode)
            {
                MessageBox.Show(
                    $"Nie można uruchomić FS Golf PL. Szczegóły zapisano w pliku:\n{RuntimeDiagnostics.LogFilePath}\n\n{exception.Message}",
                    "FS Golf PL",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            return 2;
        }
    }
}

internal static class RuntimeDiagnostics
{
    public static string LogFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FSGolfPL",
        "overlay.log");

    public static void Log(Exception exception)
    {
        try
        {
            var directory = Path.GetDirectoryName(LogFilePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.AppendAllText(LogFilePath,
                $"[{DateTimeOffset.Now:O}] {exception}\r\n\r\n");
        }
        catch
        {
            // Diagnostics must never prevent the application from starting.
        }
    }
}

/// <summary>
/// Visible controller window. Its caption is draggable and its standard close
/// button remains available while a separate readout window passes mouse input
/// through to FS Golf.
/// </summary>
public sealed class OverlayForm : Form
{
    private const int PollIntervalMs = 350;
    private static readonly IntPtr HwndTopMost = new(-1);
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;

    private readonly Label _status = new()
    {
        Dock = DockStyle.Fill,
        AutoSize = false,
        ForeColor = Color.White,
        BackColor = Color.FromArgb(42, 52, 65),
        Font = new Font("Segoe UI", 9, FontStyle.Regular),
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(10, 0, 8, 0)
    };
    private readonly TranslationOverlayForm _readout = new();
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = PollIntervalMs };
    private FsGolfWindow? _currentTarget;
    private Point _targetOffset = new(18, 18);
    private bool _positioning;
    private string? _lastError;
    private string? _firstRefreshError;

    public string? LastRefreshError => _firstRefreshError;

    public OverlayForm()
    {
        Text = "FS Golf PL Overlay v3";
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        ControlBox = true;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowIcon = false;
        ShowInTaskbar = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(42, 52, 65);
        ForeColor = Color.White;
        Size = new Size(560, 72);
        Location = new Point(36, 36);
        Controls.Add(_status);

        _status.Text = "FS Golf PC 2.0 nie jest uruchomiony";
        Shown += (_, _) => RefreshTarget();
        LocationChanged += (_, _) => HandleManualMove();
        _timer.Tick += (_, _) => RefreshTarget();
        FormClosed += (_, _) =>
        {
            _timer.Dispose();
            _readout.Dispose();
        };
        _timer.Start();
    }

    private void HandleManualMove()
    {
        if (_positioning || _currentTarget is null || _currentTarget.IsMinimized)
            return;

        _targetOffset = new Point(
            Left - _currentTarget.Bounds.Left,
            Top - _currentTarget.Bounds.Top);
        PositionReadout(_currentTarget);
    }

    private void RefreshTarget()
    {
        try
        {
            RefreshTargetCore();
            _lastError = null;
        }
        catch (Exception exception)
        {
            _firstRefreshError ??= exception.ToString();
            var signature = $"{exception.GetType().FullName}: {exception.Message}";
            if (_lastError != signature)
            {
                RuntimeDiagnostics.Log(exception);
                _lastError = signature;
            }

            _readout.Hide();
            TopMost = false;
            Text = "FS Golf PL Overlay v3 — błąd";
            _status.Text = $"Błąd: {exception.Message}  •  log: {RuntimeDiagnostics.LogFilePath}";
        }
    }

    private void RefreshTargetCore()
    {
        var target = FsGolfWindowLocator.FindBestCandidate();
        if (target is null)
        {
            if (_currentTarget is not null)
            {
                _currentTarget = null;
                _targetOffset = new Point(18, 18);
                _readout.Hide();
                TopMost = false;
                _positioning = true;
                Location = new Point(36, 36);
                Size = new Size(560, 72);
                _positioning = false;
            }

            Text = "FS Golf PL Overlay v3";
            _status.Text = "FS Golf PC 2.0 nie jest uruchomiony";
            return;
        }

        if (_currentTarget?.Handle != target.Handle)
            _targetOffset = new Point(18, 18);
        _currentTarget = target;

        if (target.IsMinimized)
        {
            _readout.Hide();
            TopMost = false;
            Text = "FS Golf PL Overlay v3";
            _status.Text = "FS Golf PC 2.0 jest zminimalizowany";
            return;
        }

        Text = "FS Golf PL Overlay v3 — FS Golf PC 2.0 wykryty";
        _status.Text = "FS Golf PC 2.0 wykryty  •  Przeciągnij pasek, aby przesunąć  •  × zamyka";
        PositionReadout(target);
    }

    private void PositionReadout(FsGolfWindow target)
    {
        var bounds = target.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;

        var x = bounds.Left + _targetOffset.X;
        var y = bounds.Top + _targetOffset.Y;
        var controllerWidth = Math.Clamp(bounds.Width - 24, 440, 620);
        _positioning = true;
        Size = new Size(controllerWidth, 72);
        Location = new Point(x, y);
        _positioning = false;

        _readout.PrepareForTarget(bounds.Width, bounds.Height);
        if (!_readout.Visible)
            _readout.Show();

        _readout.TopMost = true;
        var readoutY = y + Height + 3;
        if (readoutY + _readout.Height > bounds.Bottom)
            readoutY = Math.Max(bounds.Top, y - _readout.Height - 3);

        if (!SetWindowPos(_readout.Handle, HwndTopMost, x, readoutY, _readout.Width, _readout.Height,
                SwpNoActivate | SwpShowWindow))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());

        // Put the interactive control last in the topmost Z order; only its small
        // title/status bar receives clicks. The larger readout is click-through.
        TopMost = true;
        if (!SetWindowPos(Handle, HwndTopMost, x, y, Width, Height, SwpNoActivate | SwpShowWindow))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y,
        int width, int height, uint flags);
}

internal sealed class TranslationOverlayForm : Form
{
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExLayered = 0x00080000;
    private const int WsExNoActivate = 0x08000000;

    private readonly FlowLayoutPanel _flow = new()
    {
        Dock = DockStyle.Fill,
        AutoScroll = false,
        WrapContents = true,
        FlowDirection = FlowDirection.LeftToRight,
        Padding = new Padding(9),
        BackColor = Color.FromArgb(32, 43, 57)
    };
    private readonly List<Label> _items = new();

    public TranslationOverlayForm()
    {
        Text = "FS Golf PL Readouts v3";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(32, 43, 57);
        ForeColor = Color.White;
        Opacity = 0.94;
        ClientSize = new Size(720, 104);
        Controls.Add(_flow);

        foreach (var parameter in ParameterCatalog.All)
        {
            var label = new Label
            {
                AutoSize = true,
                Text = $"{parameter.EnglishLabel} → {parameter.PolishLabel}",
                ForeColor = Color.White,
                BackColor = Color.FromArgb(58, 72, 91),
                Font = new Font("Segoe UI", 9, FontStyle.Regular),
                Padding = new Padding(7, 5, 7, 5),
                Margin = new Padding(4)
            };
            _items.Add(label);
            _flow.Controls.Add(label);
        }
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= WsExToolWindow | WsExNoActivate | WsExLayered | WsExTransparent;
            return parameters;
        }
    }

    public void PrepareForTarget(int targetWidth, int targetHeight)
    {
        Width = Math.Clamp(targetWidth - 24, 460, 1200);
        PerformLayout();
        _flow.PerformLayout();

        var contentBottom = _items.Count == 0 ? 45 : _items.Max(item => item.Bottom);
        var desiredHeight = Math.Max(68, contentBottom + 10);
        Height = Math.Min(desiredHeight, Math.Max(72, targetHeight - 96));
    }
}

internal sealed class TestTargetForm : Form
{
    public TestTargetForm()
    {
        Text = "FS Golf PC 2.0 — Test Window";
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1000, 700);
        BackColor = Color.FromArgb(230, 235, 240);
    }
}
