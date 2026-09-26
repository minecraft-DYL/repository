# Runtime verification v2: accounts page shows no literal "{x:Bind ...}" text.
# Fixes v1's flaw: the nav click silently failed (matched a TextBlock, pattern unsupported),
# so v1 only ever scanned the startup page and its PASS was meaningless.
# This version: robust nav activation + proof that navigation actually happened.
# ASCII-only source; Chinese UI strings come from char codes via S().
param(
    [string]$ExePath = "D:\ku\traecode\lanw\src\App\Lanw.App\bin\Debug\net10.0-windows10.0.19041.0\win-x64\Lanw.App.exe",
    [string]$ShotPath = "D:\ku\traecode\lanw\verify_accounts_literal.png"
)
$ErrorActionPreference = 'Continue'

function S([int[]]$codes) { return (-join ($codes | ForEach-Object { [char]$_ })) }
$N_NAV_ACCOUNTS = S @(0x8D26, 0x53F7, 0x7BA1, 0x7406)                       # nav: account management
$K_CUR          = S @(0x5F53, 0x524D, 0x8D26, 0x53F7)                       # "current account"
$K_LOGOUT       = S @(0x6CE8, 0x9500)                                       # "log out" (accounts page only)
$K_BAD          = 'x:Bind'

if (-not (Test-Path $ExePath)) { Write-Host "MISSING EXE: $ExePath"; exit 1 }

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class W32c {
    public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
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

$AE = [System.Windows.Automation.AutomationElement]
$TS = [System.Windows.Automation.TreeScope]
$ANY = [System.Windows.Automation.Condition]::TrueCondition

Get-Process 'Lanw.App' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 800

$p = Start-Process -FilePath $ExePath -PassThru
$hwnd = [IntPtr]::Zero
for ($i = 0; $i -lt 40; $i++) {
    Start-Sleep -Milliseconds 500
    $p.Refresh()
    if ($p.HasExited) { Write-Host "PROCESS EXITED EARLY"; exit 1 }
    $hwnd = [W32c]::FindLargest([uint32]$p.Id)
    if ($hwnd -ne [IntPtr]::Zero) { break }
}
if ($hwnd -eq [IntPtr]::Zero) { Write-Host "NO WINDOW"; Stop-Process -Id $p.Id -Force -EA SilentlyContinue; exit 1 }
Write-Host "[1] pid=$($p.Id) hwnd=$hwnd"
[void][W32c]::SetForegroundWindow($hwnd)
Start-Sleep -Seconds 3
$root = $AE::FromHandle($hwnd)

function TryActivate($el) {
    # 1) SelectionItemPattern (NavigationViewItem), 2) InvokePattern, 3) LegacyIAccessible default action
    $obj = $null
    if ($el.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$obj)) {
        $obj.Select(); return "SelectionItemPattern"
    }
    if ($el.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$obj)) {
        $obj.Invoke(); return "InvokePattern"
    }
    $obj = $null
    if ($el.TryGetCurrentPattern([System.Windows.Automation.LegacyIAccessiblePattern]::Pattern, [ref]$obj)) {
        $obj.DoDefaultAction(); return "LegacyIAccessible.DoDefaultAction"
    }
    return $null
}

function ActivateNav([string]$name) {
    $cands = $root.FindAll($TS::Descendants, (New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, $name)))
    Write-Host "   candidates named '$name': $($cands.Count)"
    $walker = [System.Windows.Automation.TreeWalker]::ControlViewWalker
    foreach ($c in $cands) {
        $t = $c.Current.ControlType.ProgrammaticName
        $how = TryActivate $c
        if ($how) { Write-Host "   activated via $how on $t"; return $true }
        # walk up to the owning ListItem / NavigationViewItem and retry there
        $cur = $c
        for ($d = 0; $d -lt 4; $d++) {
            $cur = $walker.GetParent($cur)
            if ($null -eq $cur) { break }
            $how = TryActivate $cur
            if ($how) { Write-Host "   activated via $how on ancestor $($cur.Current.ControlType.ProgrammaticName)"; return $true }
        }
        Write-Host "   no pattern on $t nor its ancestors"
    }
    return $false
}

function Count-Marker([string]$m) {
    $all = $root.FindAll($TS::Descendants, $ANY)
    $n = 0
    foreach ($e in $all) { if ($e.Current.Name -and $e.Current.Name.Contains($m)) { $n++ } }
    return $n
}

Write-Host "[2] startup page: marker 'logout' count = $(Count-Marker $K_LOGOUT)"

if (-not (ActivateNav $N_NAV_ACCOUNTS)) { Write-Host "RESULT=FAIL  could not activate accounts nav"; Stop-Process -Id $p.Id -Force -EA SilentlyContinue; exit 1 }
Start-Sleep -Seconds 4

$logoutCount = Count-Marker $K_LOGOUT
Write-Host "[3] after nav: marker 'logout' count = $logoutCount"
if ($logoutCount -eq 0) {
    Write-Host "RESULT=FAIL  navigation did NOT happen (accounts page marker absent) - check would be meaningless"
    Stop-Process -Id $p.Id -Force -EA SilentlyContinue
    exit 1
}

$all = $root.FindAll($TS::Descendants, $ANY)
Write-Host "[4] accounts page: $($all.Count) elements; 'current account' lines:"
foreach ($e in $all) {
    if ($e.Current.Name -and $e.Current.Name.Contains($K_CUR)) { Write-Host "   -> " + $e.Current.Name }
}

$bad = @()
foreach ($e in $all) { if ($e.Current.Name -and $e.Current.Name.Contains($K_BAD)) { $bad += $e.Current.Name } }
if ($bad.Count -eq 0) { Write-Host "RESULT=PASS  no literal x:Bind text on the accounts page" }
else { Write-Host "RESULT=FAIL  literal binding text visible: $($bad -join ' || ')" }

try {
    $r = New-Object W32c+RECT
    [void][W32c]::GetWindowRect($hwnd, [ref]$r)
    $w = $r.Right - $r.Left; $h = $r.Bottom - $r.Top
    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($r.Left, $r.Top, 0, 0, (New-Object System.Drawing.Size($w, $h)))
    $bmp.Save($ShotPath, [System.Drawing.Imaging.ImageFormat]::Png)
    Write-Host "[5] screenshot: $ShotPath"
} catch { Write-Host "[5] screenshot failed: $_" }

Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
Write-Host "[6] done"
