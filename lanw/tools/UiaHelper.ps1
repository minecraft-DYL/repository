# ============================================================================
# Lanw shared UI-automation helper.
#
# WHY THIS EXISTS
#   Earlier verification scripts drove the app with SetCursorPos + mouse_event
#   and SendKeys. Those APIs hijack the HUMAN's physical mouse and keyboard:
#   the real pointer jumps across the desktop and real clicks/keystrokes land
#   in whatever window has focus. The user reported "my cursor moves by
#   itself" - it was our own test scripts.
#
#   RULE: never use SetCursorPos / mouse_event / keybd_event / SendKeys here.
#   Drive the UI through UI Automation patterns; if a control exposes no
#   pattern, post mouse messages to that window only (PostMessage) so the
#   physical pointer never moves.
#
# USAGE
#   . "D:\ku\traecode\lanw\tools\UiaHelper.ps1"
#   $hwnd = Get-LanwWindowHandle -ProcessId $p.Id
#   $root = [System.Windows.Automation.AutomationElement]::FromHandle($hwnd)
#   Invoke-LanwByName -Root $root -Name "account management"     # patterns first
#   Get-LanwTexts -Root $root                                    # dump all names
#   Test-LanwNoLiteralBinding -Root $root                        # {x:Bind check
# ============================================================================

if (-not ('LanwUiaWin32' -as [type])) {
    Add-Type -AssemblyName UIAutomationClient
    Add-Type -AssemblyName UIAutomationTypes
    Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public class LanwUiaWin32 {
    public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool ScreenToClient(IntPtr h, ref POINT p);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    public const uint WM_LBUTTONDOWN = 0x0201, WM_LBUTTONUP = 0x0202, WM_MOUSEMOVE = 0x0200;
    public const int MK_LBUTTON = 0x0001;
    public static IntPtr MAKELPARAM(int lo, int hi) { return (IntPtr)((hi << 16) | (lo & 0xFFFF)); }
    public static IntPtr FindLargest(uint pid) {
        IntPtr best = IntPtr.Zero; int bestArea = 0;
        EnumWindows(delegate(IntPtr h, IntPtr l) {
            uint wpid; GetWindowThreadProcessId(h, out wpid);
            if (wpid != pid || !IsWindowVisible(h)) return true;
            RECT r; if (!GetWindowRect(h, out r)) return true;
            int a = (r.Right - r.Left) * (r.Bottom - r.Top);
            if (a > bestArea) { bestArea = a; best = h; }
            return true;
        }, IntPtr.Zero);
        return best;
    }
}
"@
}

function Get-LanwWindowHandle {
    param([Parameter(Mandatory = $true)][int]$ProcessId, [int]$TimeoutSeconds = 20)
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $h = [LanwUiaWin32]::FindLargest([uint32]$ProcessId)
        if ($h -ne [IntPtr]::Zero) { return $h }
        Start-Sleep -Milliseconds 500
    }
    return [IntPtr]::Zero
}

# Probe the supported UIA patterns on one element; the first hit wins.
# Returns the pattern name, or $null when the element exposes none.
function Invoke-LanwElement {
    param([Parameter(Mandatory = $true)]$Element)
    $obj = $null
    if ($Element.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$obj)) { $obj.Select(); return 'SelectionItemPattern' }
    if ($Element.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$obj)) { $obj.Invoke(); return 'InvokePattern' }
    if ($Element.TryGetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern, [ref]$obj)) { $obj.Toggle(); return 'TogglePattern' }
    if ($Element.TryGetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern, [ref]$obj)) { $obj.Expand(); return 'ExpandCollapsePattern' }
    $obj = $null
    try {
        # PS 5.1's UIAutomationClient may not expose LegacyIAccessiblePattern at all.
        if ($Element.TryGetCurrentPattern([System.Windows.Automation.LegacyIAccessiblePattern]::Pattern, [ref]$obj)) { $obj.DoDefaultAction(); return 'LegacyIAccessible.DoDefaultAction' }
    } catch { }
    return $null
}

# Find by accessible name and activate it; if the named node is an inner Text
# (very common for NavigationView items) walk up to the owning clickable node.
function Invoke-LanwByName {
    param(
        [Parameter(Mandatory = $true)]$Root,
        [Parameter(Mandatory = $true)][string]$Name,
        [int]$MaxAncestorDepth = 4
    )
    $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $Name)
    $cands = $Root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)
    if ($cands.Count -eq 0) { return $null }
    $walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
    foreach ($c in $cands) {
        $how = Invoke-LanwElement $c
        if ($how) { return $how }
        $cur = $c
        for ($d = 0; $d -lt $MaxAncestorDepth; $d++) {
            $cur = $walker.GetParent($cur)
            if ($null -eq $cur) { break }
            $how = Invoke-LanwElement $cur
            if ($how) { return $how }
        }
    }
    return $null
}

# LAST RESORT for controls with no UIA pattern: send a left click to THAT window
# via PostMessage. The physical cursor never moves; the click cannot land in any
# other application. Note: WinUI 3 input routing may ignore synthesized messages,
# so treat a $true return as "delivered", not as "the control definitely reacted".
function Send-LanwClickNoCursor {
    param([Parameter(Mandatory = $true)][IntPtr]$WindowHandle, [Parameter(Mandatory = $true)]$Element)
    try {
        $pt = $Element.GetClickablePoint()
    } catch {
        return $false
    }
    $p = New-Object LanwUiaWin32+POINT
    $p.X = [int]$pt.X; $p.Y = [int]$pt.Y
    [void][LanwUiaWin32]::ScreenToClient($WindowHandle, [ref]$p)
    $lp = [LanwUiaWin32]::MAKELPARAM($p.X, $p.Y)
    [void][LanwUiaWin32]::PostMessage($WindowHandle, [LanwUiaWin32]::WM_MOUSEMOVE, [IntPtr]::Zero, $lp)
    [void][LanwUiaWin32]::PostMessage($WindowHandle, [LanwUiaWin32]::WM_LBUTTONDOWN, [IntPtr][LanwUiaWin32]::MK_LBUTTON, $lp)
    [void][LanwUiaWin32]::PostMessage($WindowHandle, [LanwUiaWin32]::WM_LBUTTONUP, [IntPtr]::Zero, $lp)
    return $true
}

function Get-LanwTexts {
    param([Parameter(Mandatory = $true)]$Root)
    $all = $Root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
    $out = @()
    foreach ($e in $all) { if ($e.Current.Name) { $out += [pscustomobject]@{ Type = $e.Current.ControlType.ProgrammaticName; Name = $e.Current.Name } } }
    return $out
}

function Test-LanwNoLiteralBinding {
    param([Parameter(Mandatory = $true)]$Root)
    $bad = @()
    foreach ($t in (Get-LanwTexts -Root $Root)) { if ($t.Name.Contains('x:Bind')) { $bad += $t.Name } }
    if ($bad.Count -eq 0) { return [pscustomobject]@{ Pass = $true; Hit = @() } }
    return [pscustomobject]@{ Pass = $false; Hit = $bad }
}

function Save-LanwScreenshot {
    param([Parameter(Mandatory = $true)][IntPtr]$WindowHandle, [Parameter(Mandatory = $true)][string]$Path)
    Add-Type -AssemblyName System.Drawing
    $r = New-Object LanwUiaWin32+RECT
    [void][LanwUiaWin32]::GetWindowRect($WindowHandle, [ref]$r)
    $w = $r.Right - $r.Left; $h = $r.Bottom - $r.Top
    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($r.Left, $r.Top, 0, 0, (New-Object System.Drawing.Size($w, $h)))
    $bmp.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $bmp.Dispose()
    return $Path
}
