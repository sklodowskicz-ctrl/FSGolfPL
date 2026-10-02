using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace FSGolfPL;

static class Program
{
    private const string MutexName = @"Local\FSGolfPL.WindowsOverlay.SingleInstance.v4";

    [STAThread]
    static int Main(string[] args)
    {
        var smokeTest = args.Contains("--smoke-test", StringComparer.OrdinalIgnoreCase);
        var testTarget = args.Contains("--test-target", StringComparer.OrdinalIgnoreCase);
        var legacyTestWindow = args.Contains("--legacy-test-window", StringComparer.OrdinalIgnoreCase);
        var diagnosticMode = smokeTest || testTarget || legacyTestWindow;
        Exception? unhandledException = null;

        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, eventArgs) =>
        {
            unhandledException = eventArgs.Exception;
            RuntimeDiagnostics.Log(eventArgs.Exception);
            if (!diagnosticMode)
                MessageBox.Show($"FS Golf PL napotkał błąd. Szczegóły zapisano w:\n{RuntimeDiagnostics.LogFilePath}",
                    "FS Golf PL", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            if (eventArgs.ExceptionObject is Exception exception)
                RuntimeDiagnostics.Log(exception);
        };

        try
        {
            ApplicationConfiguration.Initialize();

            // Test windows intentionally run beside the actual overlay process.
            if (testTarget)
            {
                Application.Run(new TestTargetForm());
                return unhandledException is null ? 0 : 1;
            }
            if (legacyTestWindow)
            {
                Application.Run(new LegacyTestForm());
                return unhandledException is null ? 0 : 1;
            }

            using var instanceMutex = new Mutex(initiallyOwned: true, MutexName, out var firstInstance);
            if (!firstInstance)
                return 0;

            LegacyOverlayCleanup.CloseOldOverlayProcesses();
            var form = new OverlayForm();
            var context = new ApplicationContext(form);
            form.FormClosed += (_, _) => context.ExitThread();

            if (smokeTest)
            {
                var completed = false;
                using var timer = new System.Windows.Forms.Timer { Interval = 5000 };
                timer.Tick += (_, _) => { timer.Stop(); completed = true; form.Close(); };
                timer.Start();
                Application.Run(context);
                return completed && unhandledException is null && form.LastRefreshError is null ? 0 : 1;
            }

            Application.Run(context);
            return unhandledException is null ? 0 : 1;
        }
        catch (Exception exception)
        {
            RuntimeDiagnostics.Log(exception);
            if (!diagnosticMode)
                MessageBox.Show($"Nie można uruchomić FS Golf PL. Szczegóły zapisano w:\n{RuntimeDiagnostics.LogFilePath}\n\n{exception.Message}",
                    "FS Golf PL", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 2;
        }
    }
}

internal static class RuntimeDiagnostics
{
    public static string LogFilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FSGolfPL", "overlay.log");

    public static void Log(Exception exception)
    {
        try
        {
            var directory = Path.GetDirectoryName(LogFilePath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.AppendAllText(LogFilePath, $"[{DateTimeOffset.Now:O}] {exception}\r\n\r\n");
        }
        catch { /* Logging must never prevent startup. */ }
    }
}

/// <summary>Hides and closes this application's older overlay processes before v4 starts.</summary>
internal static class LegacyOverlayCleanup
{
    private const int SwHide = 0;
    private const uint WmClose = 0x0010;

    public static void CloseOldOverlayProcesses()
    {
        var currentPid = Environment.ProcessId;
        var legacyWindows = new List<(IntPtr Handle, int ProcessId)>();
        EnumWindows((handle, _) =>
        {
            if (!IsWindowVisible(handle)) return true;
            var pid = 0u;
            GetWindowThreadProcessId(handle, out pid);
            if (pid == 0 || pid == (uint)currentPid) return true;

            var title = GetTitle(handle);
            if (!IsLegacyOverlayTitle(title)) return true;

            try
            {
                using var process = Process.GetProcessById((int)pid);
                if (!process.ProcessName.Equals("FSGolfPL", StringComparison.OrdinalIgnoreCase)) return true;
                ShowWindowAsync(handle, SwHide);
                PostMessage(handle, WmClose, IntPtr.Zero, IntPtr.Zero);
                legacyWindows.Add((handle, (int)pid));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                ShowWindowAsync(handle, SwHide);
            }
            return true;
        }, IntPtr.Zero);

        foreach (var pid in legacyWindows.Select(window => window.ProcessId).Distinct())
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                if (!process.WaitForExit(900))
                {
                    // Old releases have no single-instance guard and may be stuck
                    // in a polling loop. Their known app window has already been hidden.
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(900);
                }
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // The old process may have exited between enumeration and cleanup.
            }
        }
    }

    private static bool IsLegacyOverlayTitle(string title)
    {
        if (title.Length == 0 || title.Contains("Test Window", StringComparison.OrdinalIgnoreCase))
            return false;

        return title.Equals("FS Golf PL", StringComparison.OrdinalIgnoreCase)
            || title.StartsWith("FS Golf PL Overlay", StringComparison.OrdinalIgnoreCase)
            || title.StartsWith("FS Golf PL Readouts", StringComparison.OrdinalIgnoreCase)
            || title.StartsWith("FS Golf PL —", StringComparison.OrdinalIgnoreCase)
            || title.StartsWith("FSGolfPL", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetTitle(IntPtr handle)
    {
        var buffer = new StringBuilder(512);
        GetWindowText(handle, buffer, buffer.Capacity);
        return buffer.ToString().Trim();
    }

    private delegate bool EnumWindowsProc(IntPtr handle, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr handle);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr handle, StringBuilder text, int maxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindowAsync(IntPtr handle, int command);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(IntPtr handle, uint message, IntPtr wParam, IntPtr lParam);
}

/// <summary>
/// One native window only. It stays hidden until the real FS Golf window exists;
/// Windows keeps it owned by the game window, without placing it in the TopMost band.
/// </summary>
public sealed class OverlayForm : Form
{
    private const int PollIntervalMs = 300;
    private static readonly IntPtr HwndTop = IntPtr.Zero;
    private static readonly IntPtr GwlpHwndParent = new(-8);
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;

    private readonly FlowLayoutPanel _cards = new()
    {
        Dock = DockStyle.Fill,
        AutoScroll = false,
        WrapContents = true,
        FlowDirection = FlowDirection.LeftToRight,
        Padding = new Padding(8),
        BackColor = Color.FromArgb(25, 35, 48)
    };
    private readonly Label _status = new()
    {
        Dock = DockStyle.Bottom,
        Height = 30,
        ForeColor = Color.White,
        BackColor = Color.FromArgb(35, 48, 64),
        Font = new Font("Segoe UI", 9, FontStyle.Regular),
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(12, 0, 8, 0)
    };
    private readonly List<Panel> _tiles = new();
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = PollIntervalMs };
    private FsGolfWindow? _currentTarget;
    private IntPtr _ownerHandle;
    private Point _targetOffset = new(18, 18);
    private bool _positioning;
    private string? _lastError;
    private string? _firstRefreshError;

    public string? LastRefreshError => _firstRefreshError;

    public OverlayForm()
    {
        Text = "FS Golf PL — Overlay v4";
        FormBorderStyle = FormBorderStyle.FixedToolWindow;
        ControlBox = true;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowIcon = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.FromArgb(25, 35, 48);
        MinimumSize = new Size(500, 180);
        ClientSize = new Size(760, 300);
        Controls.Add(_cards);
        Controls.Add(_status);
        AddTiles();
        _timer.Tick += (_, _) => RefreshTarget();
        FormClosed += (_, _) => _timer.Dispose();
        _timer.Start();
        // Deliberately do not show a startup/waiting window. The timer reveals
        // this one form only after the target game window has been found.
    }

    private void AddTiles()
    {
        foreach (var parameter in ParameterCatalog.All)
        {
            var tile = new Panel
            {
                Height = 58,
                Margin = new Padding(4),
                BackColor = Color.FromArgb(49, 65, 84),
                Padding = new Padding(9, 4, 7, 3)
            };
            tile.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = parameter.PolishLabel,
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = false
            });
            tile.Controls.Add(new Label
            {
                Dock = DockStyle.Top,
                Height = 20,
                Text = parameter.EnglishLabel,
                ForeColor = Color.FromArgb(196, 210, 224),
                BackColor = Color.Transparent,
                Font = new Font("Segoe UI", 8, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = false
            });
            _tiles.Add(tile);
            _cards.Controls.Add(tile);
        }
    }

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        if (_positioning || _currentTarget is null || _currentTarget.IsMinimized) return;
        _targetOffset = new Point(Left - _currentTarget.Bounds.Left, Top - _currentTarget.Bounds.Top);
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
            if (_lastError != signature) { RuntimeDiagnostics.Log(exception); _lastError = signature; }
            HideAndDetach();
        }
    }

    private void RefreshTargetCore()
    {
        var target = FsGolfWindowLocator.FindBestCandidate();
        if (target is null || target.IsMinimized || !IsWindow(target.Handle))
        {
            HideAndDetach();
            _currentTarget = null;
            _targetOffset = new Point(18, 18);
            return;
        }

        if (_currentTarget?.Handle != target.Handle)
            _targetOffset = new Point(18, 18);
        _currentTarget = target;

        if (!IsWindowVisible(target.Handle))
        {
            HideAndDetach();
            return;
        }

        if (_ownerHandle != target.Handle)
            SetOwner(target.Handle);

        PositionForTarget(target);
    }

    private void PositionForTarget(FsGolfWindow target)
    {
        var bounds = target.Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0) { HideAndDetach(); return; }

        var width = Math.Clamp((int)Math.Round(bounds.Width * 0.58), 520, 980);
        var columns = Math.Clamp((width - 16) / 156, 2, 6);
        var tileWidth = Math.Max(120, (width - 16 - columns * 8) / columns);
        foreach (var tile in _tiles) tile.Width = tileWidth;

        var rows = (int)Math.Ceiling(_tiles.Count / (double)columns);
        var cardsHeight = 16 + rows * 66;
        ClientSize = new Size(width, cardsHeight + _status.Height);
        _cards.PerformLayout();

        if (!_tiles.All(tile => _cards.ClientRectangle.Contains(tile.Bounds)))
            throw new InvalidOperationException("Kafelki tłumaczeń nie mieszczą się w oknie.");

        var x = bounds.Left + _targetOffset.X;
        var y = bounds.Top + _targetOffset.Y;
        if (x + Width > bounds.Right) x = Math.Max(bounds.Left, bounds.Right - Width - 12);
        if (y + Height > bounds.Bottom) y = Math.Max(bounds.Top, bounds.Bottom - Height - 12);

        _positioning = true;
        Location = new Point(x, y);
        _positioning = false;
        _status.Text = $"FS Golf PC 2.0  •  {bounds.Width} × {bounds.Height}  •  X zamyka overlay";
        if (!Visible) Show();

        if (!SetWindowPos(Handle, target.Handle, x, y, Width, Height, SwpNoActivate | SwpShowWindow))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }

    private void HideAndDetach()
    {
        if (Visible) Hide();
        if (_ownerHandle != IntPtr.Zero && IsWindow(Handle))
            SetWindowLongPtr(Handle, GwlpHwndParent, IntPtr.Zero);
        _ownerHandle = IntPtr.Zero;
    }

    private void SetOwner(IntPtr targetHandle)
    {
        if (!IsWindow(Handle)) _ = Handle;
        SetWindowLongPtr(Handle, GwlpHwndParent, targetHandle);
        _ownerHandle = targetHandle;
    }

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr window, IntPtr index, IntPtr value);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr handle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr handle);
}

internal sealed class TestTargetForm : Form
{
    public TestTargetForm()
    {
        Text = "FS Golf PC 2.0 [Test Window]";
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1280, 800);
        BackColor = Color.FromArgb(230, 235, 240);
        Controls.Add(new Label
        {
            Dock = DockStyle.Top,
            Height = 58,
            Text = "FS Golf PC 2.0 — okno testowe\r\nPrzykładowy wynik: Carry 198.4 m | Ball Speed 62.1 m/s",
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        });
    }
}

internal sealed class LegacyTestForm : Form
{
    public LegacyTestForm()
    {
        Text = "FS Golf PL";
        FormBorderStyle = FormBorderStyle.None;
        BackColor = Color.Black;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(0, 0);
        Size = new Size(700, 42);
    }
}
