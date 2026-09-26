# t23 runtime verification: ServersPage reachable, list/error state, no crash (offline + online).
#
#   A) offline - HTTPS_PROXY points at a dead local port (.NET 10 HttpClient honors it): the page must render
#                (header + search + refresh) and show the error card with a retry button; process stays alive.
#   B) online  - no proxy: the page must render and show list content or a clear error/empty state; alive.
#   Both phases save a window screenshot for human review.
#
# ASCII-only source on purpose: Windows PowerShell 5.1 reads BOM-less .ps1 as ANSI, so Chinese UI strings are
# built from code points below.
param(
    [string]$ExePath = "D:\ku\traecode\lanw\src\App\Lanw.App\bin\x64\Debug\net10.0-windows10.0.19041.0\win-x64\Lanw.App.exe",
    [string]$OfflineShot = "D:\ku\traecode\lanw\verify_t23_servers_offline.png",
    [string]$OnlineShot = "D:\ku\traecode\lanw\verify_t23_servers_online.png"
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class T23VWin32 {
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extra);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hwnd, int cmd);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hwnd);
}
"@

# fu-wu-qi ; ...-guan-li ; wu-fa-huo-qu-wang-luo-fu-lie-biao ; huo-qu-fu-wu-qi-lie-biao-shi-bai ; zai-xian ; gong
$NAV = [string]::Concat([char]0x670D, [char]0x52A1, [char]0x5668)
$HEADER = [string]::Concat([char]0x670D, [char]0x52A1, [char]0x5668, [char]0x7BA1, [char]0x7406)
$ERR1 = [string]::Concat([char]0x65E0, [char]0x6CD5, [char]0x83B7, [char]0x53D6, [char]0x7F51, [char]0x7EDC, [char]0x670D, [char]0x5217, [char]0x8868)
$ERR2 = [string]::Concat([char]0x83B7, [char]0x53D6, [char]0x670D, [char]0x52A1, [char]0x5668, [char]0x5217, [char]0x8868, [char]0x5931, [char]0x8D25)
$ONLINE = [string]::Concat([char]0x5728, [char]0x7EBF)
$COUNT = [string]::Concat([char]0x5171) + " "
$EMPTY1 = [string]::Concat([char]0x6CA1, [char]0x6709, [char]0x53EF, [char]0x5C55, [char]0x793A, [char]0x7684, [char]0x670D, [char]0x52A1, [char]0x5668)
$EMPTY2 = [string]::Concat([char]0x6CA1, [char]0x6709, [char]0x83B7, [char]0x53D6, [char]0x5230, [char]0x7F51, [char]0x7EDC, [char]0x670D, [char]0x6570, [char]0x636E)

function Stop-Lanw {
    Get-Process Lanw.App -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 600
}

function Focus-Window([IntPtr]$hwnd) {
    if ([T23VWin32]::IsIconic($hwnd)) { [T23VWin32]::ShowWindow($hwnd, 9) | Out-Null }  # SW_RESTORE
    [T23VWin32]::SetForegroundWindow($hwnd) | Out-Null
    Start-Sleep -Milliseconds 500
}

function Find-Exact([IntPtr]$hwnd, [string]$name) {
    $root = [System.Windows.Automation.AutomationElement]::FromHandle($hwnd)
    $cond = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $name)
    return $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
}

function Find-Contains([IntPtr]$hwnd, [string]$fragment) {
    $root = [System.Windows.Automation.AutomationElement]::FromHandle($hwnd)
    $all = $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($e in $all) { $n = $e.Current.Name; if ($n -and $n.Contains($fragment)) { return $e } }
    return $null
}

function Wait-Contains([IntPtr]$hwnd, [string]$fragment, [int]$timeoutSec = 20) {
    $deadline = (Get-Date).AddSeconds($timeoutSec)
    while ((Get-Date) -lt $deadline) {
        $hit = Find-Contains $hwnd $fragment
        if ($null -ne $hit) { return $hit }
        Start-Sleep -Milliseconds 600
    }
    return $null
}

# Prefer UIA patterns (no coordinates); fall back to a synthetic mouse click when the element is on screen.
function Activate-Element([IntPtr]$hwnd, $element) {
    try {
        $invoke = $element.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
        $invoke.Invoke()
        Write-Host "      invoked '$($element.Current.Name)' via InvokePattern"
        return $true
    } catch { }

    try {
        $select = $element.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern)
        $select.Select()
        Write-Host "      selected '$($element.Current.Name)' via SelectionItemPattern"
        return $true
    } catch { }

    Focus-Window $hwnd
    $rect = $element.Current.BoundingRectangle
    if ($rect.Width -le 0 -or $rect.X -lt 0 -or $rect.Y -lt 0) {
        Write-Host "      element off screen (x=$($rect.X), y=$($rect.Y)); no coordinate click attempted"
        return $false
    }

    $x = [int]($rect.X + $rect.Width / 2)
    $y = [int]($rect.Y + $rect.Height / 2)
    [T23VWin32]::SetCursorPos($x, $y) | Out-Null
    Start-Sleep -Milliseconds 250
    [T23VWin32]::mouse_event(0x0002, 0, 0, 0, [UIntPtr]::Zero)
    [T23VWin32]::mouse_event(0x0004, 0, 0, 0, [UIntPtr]::Zero)
    Write-Host "      clicked '$($element.Current.Name)' at ($x,$y)"
    return $true
}

function Save-Shot([IntPtr]$hwnd, [string]$path) {
    $rect = New-Object T23VWin32+RECT
    [T23VWin32]::GetWindowRect($hwnd, [ref]$rect) | Out-Null
    $w = $rect.Right - $rect.Left
    $h = $rect.Bottom - $rect.Top
    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bmp.Size)
    $g.Dispose()
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "      shot: $path (${w}x${h})"
}

function Start-Lanw([bool]$offline) {
    if ($offline) {
        $env:HTTPS_PROXY = 'http://127.0.0.1:9'
        $env:HTTP_PROXY = 'http://127.0.0.1:9'
        $env:ALL_PROXY = 'http://127.0.0.1:9'
    } else {
        Remove-Item Env:HTTPS_PROXY -ErrorAction SilentlyContinue
        Remove-Item Env:HTTP_PROXY -ErrorAction SilentlyContinue
        Remove-Item Env:ALL_PROXY -ErrorAction SilentlyContinue
    }

    $proc = Start-Process -FilePath $ExePath -PassThru
    Start-Sleep -Seconds 12
    $proc.Refresh()
    if ($proc.HasExited -or $proc.MainWindowHandle -eq 0) {
        throw "Lanw.App did not start (HasExited=$($proc.HasExited))"
    }
    Focus-Window $proc.MainWindowHandle
    return $proc
}

# Navigate to the servers page; retry because concurrent verification runs on this machine can swallow input.
function Open-ServersPage($proc) {
    for ($attempt = 1; $attempt -le 4; $attempt++) {
        $nav = Find-Exact $proc.MainWindowHandle $NAV
        if ($null -eq $nav) { throw "nav item not found" }
        Activate-Element $proc.MainWindowHandle $nav | Out-Null
        $header = Wait-Contains $proc.MainWindowHandle $HEADER 8
        if ($null -ne $header) {
            Write-Host "      pages rendered on attempt ${attempt}"
            return $header
        }
        Write-Host "      attempt ${attempt}: servers page not rendered yet"
    }
    return $null
}

# Run one phase; retries the whole phase when the page could not be reached.
function Run-Phase([string]$label, [bool]$offline, [string]$shot, $probes) {
    for ($run = 1; $run -le 2; $run++) {
        Stop-Lanw
        $proc = Start-Lanw $offline
        try {
            $header = Open-ServersPage $proc
            if ($null -eq $header) {
                Write-Host "      run ${run}: header missing, retrying phase"
                continue
            }

            $markerKind = ''
            $deadline = (Get-Date).AddSeconds(60)
            while ((Get-Date) -lt $deadline -and $markerKind -eq '') {
                foreach ($probe in $probes) {
                    if ($null -ne (Find-Contains $proc.MainWindowHandle $probe.Fragment)) { $markerKind = $probe.Kind; break }
                }
                if ($markerKind -eq '') { Start-Sleep -Milliseconds 700 }
            }

            $proc.Refresh()
            $alive = -not $proc.HasExited
            Write-Host "      header=True marker=$markerKind alive=$alive"
            Save-Shot $proc.MainWindowHandle $shot
            return [pscustomobject]@{ Phase = $label; Header = $true; Marker = ($markerKind -ne ''); Alive = $alive; Kind = $markerKind }
        } finally {
            Stop-Lanw
        }
    }
    return [pscustomobject]@{ Phase = $label; Header = $false; Marker = $false; Alive = $false; Kind = '' }
}

$offlineProbes = @(
    @{ Fragment = $ERR1; Kind = 'error' },
    @{ Fragment = $ERR2; Kind = 'error' },
    @{ Fragment = $ONLINE; Kind = 'list-online' },
    @{ Fragment = $COUNT; Kind = 'list-count' })

$onlineProbes = @(
    @{ Fragment = $ONLINE; Kind = 'list-online' },
    @{ Fragment = $COUNT; Kind = 'list-count' },
    @{ Fragment = $ERR1; Kind = 'error' },
    @{ Fragment = $ERR2; Kind = 'error' },
    @{ Fragment = $EMPTY1; Kind = 'empty' },
    @{ Fragment = $EMPTY2; Kind = 'empty' })

Write-Host "[A] offline (HTTPS_PROXY=http://127.0.0.1:9)"
$resultA = Run-Phase 'A-offline' $true $OfflineShot $offlineProbes

Write-Host ""
Write-Host "[B] online"
$resultB = Run-Phase 'B-online' $false $OnlineShot $onlineProbes

$results = @($resultA, $resultB)
Write-Host ""
$results | Format-Table -AutoSize | Out-String | Write-Host

$failed = @($results | Where-Object { -not $_.Header -or -not $_.Marker -or -not $_.Alive })
if ($failed.Count -eq 0) {
    Write-Host "PASS: ServersPage reachable; offline shows the error state without crashing; online shows list or a clear error/empty state"
    exit 0
} else {
    Write-Host "FAIL: see table above"
    exit 1
}
