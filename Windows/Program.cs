using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace FSGolfPL;

static class Program
{
    [STAThread]
    static void Main()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, eventArgs) =>
        {
            RuntimeDiagnostics.Log(eventArgs.Exception);
            MessageBox.Show(
                $"FS Golf PL napotkał błąd. Szczegóły zapisano w pliku:\n{RuntimeDiagnostics.LogFilePath}",
                "FS Golf PL",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            if (eventArgs.ExceptionObject is Exception exception)
                RuntimeDiagnostics.Log(exception);
        };

        try
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new OverlayForm());
        }
        catch (Exception exception)
        {
            RuntimeDiagnostics.Log(exception);
            MessageBox.Show(
                $"Nie można uruchomić FS Golf PL. Szczegóły zapisano w pliku:\n{RuntimeDiagnostics.LogFilePath}\n\n{exception.Message}",
                "FS Golf PL",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
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
/// Passive desktop overlay. Window tracking is isolated from value reading so a
/// local OCR provider can be connected in a later stage.
/// </summary>
public sealed class OverlayForm : Form
{
    private const int OverlayHeight = 112;
    private const int PollIntervalMs = 500;
    private static readonly IntPtr HwndTopMost = new(-1);
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
    private string? _lastError;

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
        try
        {
            RefreshTargetCore();
            _lastError = null;
        }
        catch (Exception exception)
        {
            var signature = $"{exception.GetType().FullName}: {exception.Message}";
            if (_lastError != signature)
            {
                RuntimeDiagnostics.Log(exception);
                _lastError = signature;
            }

            TopMost = false;
            _status.Text = $"Błąd overlayu: {exception.Message}  •  log: {RuntimeDiagnostics.LogFilePath}";
            if (!Visible)
                Show();
        }
    }

    private void RefreshTargetCore()
    {
        var target = FsGolfWindowLocator.FindBestCandidate();
        if (target is null)
        {
            if (_hasTarget)
            {
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

        _targetHandle = target.Handle;
        _hasTarget = true;
        if (target.IsMinimized)
        {
            TopMost = false;
            _status.Text = $"Wykryto: {target.Title} — overlay ukryty dla zminimalizowanego okna";
            if (Visible)
                Hide();
            return;
        }

        if (!Visible)
            Show();

        var bounds = target.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return;

        // No cross-process window ownership is used. The overlay is placed in the
        // topmost band while FS Golf is visible, so it remains above the target.
        TopMost = true;
        var height = Math.Min(OverlayHeight, bounds.Height);
        if (!SetWindowPos(Handle, HwndTopMost, bounds.Left, bounds.Top, bounds.Width, height,
                SwpNoActivate | SwpShowWindow))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());

        _status.Text = $"Wykryto: {target.Title} ({target.ProcessName})  •  {bounds.Width} × {bounds.Height}";
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y,
        int width, int height, uint flags);
}
