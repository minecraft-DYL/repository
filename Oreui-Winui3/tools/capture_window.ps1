# Capture a top-level window to PNG.
#
# METHOD=printwindow (default, strongly recommended)
#   Uses PrintWindow(hwnd, dc, PW_RENDERFULLCONTENT). Renders the window's OWN
#   content into a bitmap, independent of what else is on the desktop. This is
#   the only reliable option on a busy machine: Graphics.CopyFromScreen captures
#   the *composited desktop*, so any overlapping window silently replaces your
#   screenshot with someone else's UI (observed: 40% of a capture was another app).
#
# METHOD=copyscreen
#   Raises the window (Show/Restore/HWND_TOPMOST + AttachThreadInput foreground
#   trick) then CopyFromScreen on the client area. Use only when PrintWindow
#   returns a blank image.
#
# ASCII only on purpose: Chinese comments break Windows PowerShell 5.1 parsing.
param(
    [string]$TitlePattern = 'OreUI\.WinUI',
    [string]$Out = 'D:\ku\traecode\Oreui-Winui3\artifacts\capture.png',
    [ValidateSet('printwindow', 'copyscreen')][string]$Method = 'printwindow',
    [string]$ProcessName = 'OreUI.Gallery',
    [int]$WaitMs = 1500,
    [int]$MinWidth = 200
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$src = @'
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;

public class OreCap
{
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr p);
    delegate bool EnumProc(IntPtr h, IntPtr p);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassNameW(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll", EntryPoint = "GetWindowThreadProcessId")] static extern uint GetWindowThread(IntPtr h, IntPtr p);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr h, ref POINT p);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] static extern bool BringWindowToTop(IntPtr h);
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr h, IntPtr dc, uint flags);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] static extern bool AttachThreadInput(uint a, uint b, bool attach);

    public struct RECT { public int L, T, R, B; }
    public struct POINT { public int X, Y; }
    public struct Info { public IntPtr Hwnd; public string Title; public string Class; public int X, Y, W, H; public bool Visible; }

    const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_SHOWWINDOW = 0x0040;
    static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

    static uint PidOf(IntPtr h) { uint pid; GetWindowThreadProcessId(h, out pid); return pid; }

    public static Info Find(string pattern, string procName, int minWidth)
    {
        var re = new System.Text.RegularExpressions.Regex(pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var pids = new List<int>();
        foreach (var p in System.Diagnostics.Process.GetProcessesByName(procName)) pids.Add(p.Id);

        Info best = new Info(); long bestArea = -1;
        EnumWindows(delegate(IntPtr h, IntPtr p)
        {
            var sb = new StringBuilder(1024);
            GetWindowTextW(h, sb, 1024);
            if (!re.IsMatch(sb.ToString())) return true;
            if (pids.Count > 0 && !pids.Contains((int)PidOf(h))) return true;
            RECT r; GetWindowRect(h, out r);
            int w = r.R - r.L, hh = r.B - r.T;
            if (w < minWidth) return true;
            if ((long)w * hh > bestArea)
            {
                bestArea = (long)w * hh;
                var cls = new StringBuilder(256); GetClassNameW(h, cls, 256);
                best = new Info { Hwnd = h, Title = sb.ToString(), Class = cls.ToString(), X = r.L, Y = r.T, W = w, H = hh, Visible = IsWindowVisible(h) };
            }
            return true;
        }, IntPtr.Zero);
        return best;
    }

    public static string Describe(Info w)
    {
        return string.Format("hwnd={0} visible={1} iconic={2} rect={3},{4} {5}x{6} class={7} title='{8}'",
            w.Hwnd, w.Visible, IsIconic(w.Hwnd), w.X, w.Y, w.W, w.H, w.Class, w.Title);
    }

    public static bool SavePrintWindow(Info w, string path)
    {
        using (var bmp = new Bitmap(w.W, w.H))
        using (var g = Graphics.FromImage(bmp))
        {
            IntPtr dc = g.GetHdc();
            bool ok;
            try { ok = PrintWindow(w.Hwnd, dc, 2 /* PW_RENDERFULLCONTENT */); }
            finally { g.ReleaseHdc(dc); }
            if (!ok) return false;
            bmp.Save(path, ImageFormat.Png);
        }
        return true;
    }

    public static bool SaveCopyFromScreen(Info w, string path)
    {
        RECT cr; GetClientRect(w.Hwnd, out cr);
        var po = new POINT(); ClientToScreen(w.Hwnd, ref po);
        int cw = cr.R - cr.L, ch = cr.B - cr.T;
        if (cw <= 0 || ch <= 0) return false;
        using (var bmp = new Bitmap(cw, ch))
        using (var g = Graphics.FromImage(bmp))
        {
            g.CopyFromScreen(po.X, po.Y, 0, 0, new Size(cw, ch));
            bmp.Save(path, ImageFormat.Png);
        }
        return true;
    }

    public static bool IsForeground(IntPtr h) { return GetForegroundWindow() == h; }

    public static void Focus(IntPtr h)
    {
        if (IsIconic(h)) ShowWindow(h, 9);
        ShowWindow(h, 5);
        SetWindowPos(h, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
        BringWindowToTop(h);
        IntPtr fg = GetForegroundWindow();
        uint fgThread = GetWindowThread(fg, IntPtr.Zero);
        uint myThread = GetCurrentThreadId();
        bool attached = false;
        if (fgThread != myThread) attached = AttachThreadInput(fgThread, myThread, true);
        try { SetForegroundWindow(h); BringWindowToTop(h); }
        finally { if (attached) AttachThreadInput(fgThread, myThread, false); }
    }

    public static void DropTopmost(IntPtr h)
    {
        SetWindowPos(h, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
    }
}
'@

Add-Type -TypeDefinition $src -ReferencedAssemblies System.Drawing

$w = [OreCap]::Find($TitlePattern, $ProcessName, $MinWidth)
if ($w.Hwnd -eq [IntPtr]::Zero) {
    Write-Output "WINDOW_NOT_FOUND: pattern='$TitlePattern' process='$ProcessName'"
    exit 1
}

if ($Method -eq 'printwindow') {
    $ok = [OreCap]::SavePrintWindow($w, $Out)
} else {
    [OreCap]::Focus($w.Hwnd)
    Start-Sleep -Milliseconds $WaitMs
    $fg = [OreCap]::IsForeground($w.Hwnd)
    $ok = [OreCap]::SaveCopyFromScreen($w, $Out)
    [OreCap]::DropTopmost($w.Hwnd)
    Write-Output "foreground=$fg"
    if (-not $fg) { Write-Output "WARNING: not foreground; the capture may contain an overlapping window" }
}

Write-Output ([OreCap]::Describe($w))
Write-Output "method=$Method saved=$ok"
Write-Output "SAVED: $Out"
if (-not $ok) { exit 2 }
