using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace FSGolfPL;

public sealed record FsGolfWindow(IntPtr Handle, string Title, string ProcessName, Rectangle Bounds, bool IsMinimized);

/// <summary>
/// Finds visible top-level FS Golf / FlightScope windows without attaching to,
/// injecting into, or changing the target application.
/// </summary>
public static class FsGolfWindowLocator
{
    private static readonly string[] MatchTerms = ["fs golf", "fsgolf", "flightscope"];

    public static FsGolfWindow? FindBestCandidate()
    {
        var candidates = new List<(FsGolfWindow Window, int Score)>();
        var ownProcessId = (uint)Environment.ProcessId;

        EnumWindows((handle, _) =>
        {
            if (!IsWindowVisible(handle))
                return true;

            var processId = 0u;
            GetWindowThreadProcessId(handle, out processId);
            if (processId == ownProcessId || processId == 0)
                return true;

            var titleBuffer = new StringBuilder(512);
            GetWindowText(handle, titleBuffer, titleBuffer.Capacity);
            var title = titleBuffer.ToString().Trim();
            if (title.Length == 0)
                return true;

            string processName;
            try
            {
                using var process = Process.GetProcessById((int)processId);
                processName = process.ProcessName;
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException
                                           or System.ComponentModel.Win32Exception)
            {
                return true;
            }

            var score = MatchScore(title, processName);
            if (score == 0)
                return true;

            if (GetWindowRect(handle, out var rect))
            {
                var bounds = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
                var minimized = IsIconic(handle);
                if (minimized || (bounds.Width > 200 && bounds.Height > 160))
                    candidates.Add((new FsGolfWindow(handle, title, processName, bounds, minimized), score));
            }

            return true;
        }, IntPtr.Zero);

        return candidates
            .OrderByDescending(candidate => candidate.Score)
            .ThenByDescending(candidate => (long)candidate.Window.Bounds.Width * candidate.Window.Bounds.Height)
            .Select(candidate => candidate.Window)
            .FirstOrDefault();
    }

    private static int MatchScore(string title, string processName)
    {
        var normalizedTitle = title.Replace(" ", "", StringComparison.Ordinal).ToLowerInvariant();
        var normalizedProcess = processName.Replace("_", "", StringComparison.Ordinal)
            .Replace("-", "", StringComparison.Ordinal).ToLowerInvariant();

        // The overlay's own caption identifies it so another instance is never selected.
        if (normalizedTitle.Contains("fsgolfpl", StringComparison.Ordinal))
            return 0;

        var titleMatch = MatchTerms.Any(term => normalizedTitle.Contains(
            term.Replace(" ", "", StringComparison.Ordinal), StringComparison.Ordinal));
        var processMatch = MatchTerms.Any(term => normalizedProcess.Contains(
            term.Replace(" ", "", StringComparison.Ordinal), StringComparison.Ordinal));
        return (titleMatch ? 100 : 0) + (processMatch ? 50 : 0);
    }

    private delegate bool EnumWindowsProc(IntPtr handle, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr handle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr handle);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetWindowText(IntPtr handle, StringBuilder text, int maxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr handle, out NativeRect rect);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
