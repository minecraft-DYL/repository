# t28 runtime verification v3 (ASCII-only source; Chinese UI names built from char codes via S()).
# Verifies: proxy page opens via nav click, config persists to resources/config.json, start path is guarded.
param(
    [string]$ExeDir = "D:\ku\traecode\lanw\src\App\Lanw.App\bin\x64\Debug\net10.0-windows10.0.19041.0\win-x64",
    [string]$ExeName = "Lanw.App.exe",
    [string]$ShotPath = "D:\ku\traecode\lanw\verify_t28_proxy_page.png"
)
$ErrorActionPreference = 'Continue'
$ExePath = Join-Path $ExeDir $ExeName
$ProcName = [System.IO.Path]::GetFileNameWithoutExtension($ExeName)

function S([int[]]$codes) { return (-join ($codes | ForEach-Object { [char]$_ })) }
$N_NAV      = S @(0x4EE3, 0x7406)                                                  #
$N_TITLE    = S @(0x4EE3, 0x7406, 0x7BA1, 0x7406)                                  #
$N_CFGCARD  = S @(0x62E6, 0x622A, 0x5668, 0x914D, 0x7F6E)                          #
$N_RUNCARD  = S @(0x8FD0, 0x884C, 0x72B6, 0x6001)                                  #
$N_SAVE     = S @(0x4FDD, 0x5B58, 0x914D, 0x7F6E)                                  #
$N_START    = S @(0x542F, 0x52A8, 0x672C, 0x673A, 0x62E6, 0x622A, 0x5668)          #
$N_TOGGLE   = S @(0x5F00, 0x542F, 0x8131, 0x76D2, 0x62E6, 0x622A)                  #
$N_CLOSEALL = S @(0x5173, 0x95ED, 0x5168, 0x90E8, 0x4EE3, 0x7406)                  #
$K_STARTED  = S @(0x5DF2, 0x542F, 0x52A8, 0x4EE3, 0x7406)                          #
$K_NEEDLOGIN = S @(0x8BF7, 0x5148)                                                 #
$K_SWITCHOFF = S @(0x5F00, 0x5173, 0x5DF2, 0x5173, 0x95ED)                         #
$K_SAVEDCFG = S @(0x5DF2, 0x4FDD, 0x5B58, 0x4EE3, 0x7406, 0x914D, 0x7F6E)          #

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class W32 {
    public delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint c, uint e);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    public const uint LEFTDOWN = 0x0002, LEFTUP = 0x0004;
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

# clean slate: no other Lanw.App instance may steal focus/die under us; remove stale config so a save is provable
Get-Process $ProcName -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
$cfgPath = Join-Path $ExeDir "resources\config.json"
if (Test-Path $cfgPath) { Remove-Item $cfgPath -Force }
Write-Host "[0] removed stale config: $cfgPath"
Start-Sleep -Milliseconds 1000

$proc = $null; $hwnd = [IntPtr]::Zero; $root = $null
for ($attempt = 1; $attempt -le 4; $attempt++) {
    $proc = Start-Process -FilePath $ExePath -PassThru
    Start-Sleep -Seconds 12
    $proc.Refresh()
    if ($proc.HasExited) { Write-Host "[1] attempt $attempt : pid $($proc.Id) exited early - retry"; continue }
    $hwnd = [W32]::FindLargest([uint32]$proc.Id)
    Write-Host "[1] attempt $attempt : pid $($proc.Id) alive, hwnd=$hwnd"
    if ($hwnd -ne [IntPtr]::Zero) { $root = $AE::FromHandle($hwnd); break }
}
if ($null -eq $root) { Write-Host "FAIL: no live app window"; exit 2 }
[W32]::SetForegroundWindow($hwnd) | Out-Null
Start-Sleep -Milliseconds 500
Write-Host "[2] window: '$($root.Current.Name)'"

function All-Elements { return $root.FindAll($TS::Descendants, [System.Windows.Automation.Condition]::TrueCondition) }
function Find-ByName([string]$name) {
    $cond = New-Object System.Windows.Automation.PropertyCondition($AE::NameProperty, $name)
    return $root.FindFirst($TS::Descendants, $cond)
}
function Find-ByContains([string]$needle) {
    foreach ($el in (All-Elements)) { $n = $el.Current.Name; if ($n -and $n.Contains($needle)) { return $el } }
    return $null
}
function Click-Element($element, [string]$label) {
    if ($null -eq $element) { Write-Host "[x] element missing: $label"; return $false }
    $x = 0; $y = 0
    try { $pt = $element.GetClickablePoint(); $x = [int]$pt.X; $y = [int]$pt.Y }
    catch {
        $r = $element.Current.BoundingRectangle
        if ($r.Width -le 0 -or $r.Height -le 0) { Write-Host "[x] no clickable point: $label"; return $false }
        $x = [int]($r.X + $r.Width / 2); $y = [int]($r.Y + $r.Height / 2)
    }
    [W32]::ShowWindow($hwnd, 3) | Out-Null
    [W32]::SetForegroundWindow($hwnd) | Out-Null
    Start-Sleep -Milliseconds 300
    [W32]::SetCursorPos($x, $y) | Out-Null
    Start-Sleep -Milliseconds 200
    [W32]::mouse_event([W32]::LEFTDOWN, 0, 0, 0, 0)
    [W32]::mouse_event([W32]::LEFTUP, 0, 0, 0, 0)
    Start-Sleep -Milliseconds 1200
    return $true
}
function Get-StatusText {
    $hits = @()
    foreach ($el in (All-Elements)) {
        $n = $el.Current.Name
        if (-not $n) { continue }
        if ($n.Contains($K_STARTED) -or $n.Contains($K_NEEDLOGIN) -or $n.Contains($K_SWITCHOFF) -or $n.Contains($K_SAVEDCFG)) { $hits += $n }
    }
    return ($hits -join ' | ')
}
function Get-Toggle([string]$needle) {
    foreach ($el in (All-Elements)) {
        $n = $el.Current.Name
        if ($n -and $n.Contains($needle)) {
            try { $el.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern) | Out-Null; return $el } catch { }
        }
    }
    return $null
}
function Set-Toggle($el, [string]$state) {
    if ($null -eq $el) { return "NOTFOUND" }
    $tp = $el.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
    if ($state -eq 'Off' -and $tp.Current.ToggleState -ne [System.Windows.Automation.ToggleState]::Off) { $tp.Toggle() }
    if ($state -eq 'On' -and $tp.Current.ToggleState -ne [System.Windows.Automation.ToggleState]::On) { $tp.Toggle() }
    Start-Sleep -Milliseconds 800
    return $tp.Current.ToggleState.ToString()
}
function Set-ToggleState([string]$state, [int]$tries = 4) {
    for ($t = 1; $t -le $tries; $t++) {
        $el = Get-Toggle $N_TOGGLE
        if ($null -eq $el) { Start-Sleep -Milliseconds 700; continue }
        try {
            $tp = $el.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
            if ($tp.Current.ToggleState.ToString() -eq $state) { return $state }
            $tp.Toggle()
            Start-Sleep -Milliseconds 900
            $el2 = Get-Toggle $N_TOGGLE
            if ($null -ne $el2) {
                $tp2 = $el2.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
                if ($tp2.Current.ToggleState.ToString() -eq $state) { return $state }
            }
        } catch { }
        Start-Sleep -Milliseconds 700
    }
    return "FAILED"
}
function Read-Config {
    $p = Join-Path $ExeDir "resources\config.json"
    if (-not (Test-Path $p)) { return "" }
    return (Get-Content $p -Raw)
}

# 1) nav -> proxy page (elements may need a moment after navigation; retry)
$navItem = Find-ByName $N_NAV
Write-Host "[3] nav item found: $($null -ne $navItem)"
Click-Element $navItem "nav" | Out-Null

$title = $null; $configCard = $null; $runCard = $null; $saveBtn = $null; $startBtn = $null; $toggle = $null
for ($r = 1; $r -le 5; $r++) {
    $title = Find-ByName $N_TITLE
    $configCard = Find-ByContains $N_CFGCARD
    $runCard = Find-ByContains $N_RUNCARD
    $saveBtn = Find-ByName $N_SAVE
    $startBtn = Find-ByName $N_START
    $toggle = Get-Toggle $N_TOGGLE
    $found = ($null -ne $title) -and ($null -ne $configCard) -and ($null -ne $runCard) -and ($null -ne $saveBtn) -and ($null -ne $startBtn) -and ($null -ne $toggle)
    Write-Host "[4] round $r : title=$($null -ne $title) cfgCard=$($null -ne $configCard) runCard=$($null -ne $runCard) save=$($null -ne $saveBtn) start=$($null -ne $startBtn) toggle=$($null -ne $toggle)"
    if ($found) { break }
    Click-Element $navItem "nav retry" | Out-Null
    Start-Sleep -Milliseconds 1000
}
$pageOk = ($null -ne $title) -and ($null -ne $configCard) -and ($null -ne $runCard) -and ($null -ne $saveBtn) -and ($null -ne $startBtn) -and ($null -ne $toggle)
Write-Host "[4] page elements complete: $pageOk"

# 2) switch OFF -> save -> start (guard) ; then ON -> save
$offState = Set-ToggleState 'Off'
Click-Element $saveBtn "save(off)" | Out-Null
$offSaved = $false
for ($i = 0; $i -lt 6; $i++) { $offSaved = (Read-Config) -match '"proxyInterceptEnabled":"False"'; if ($offSaved) { break }; Start-Sleep -Milliseconds 500 }
Write-Host "[5] toggle=$offState ; config saved False: $offSaved"
Click-Element $startBtn "start(off)" | Out-Null
Start-Sleep -Milliseconds 1500
$guardOff = Get-StatusText
$guardOffOk = $guardOff.Contains($K_SWITCHOFF)
Write-Host "[5] guard(off): $guardOff  ok=$guardOffOk"

$onState = Set-ToggleState 'On'
Click-Element $saveBtn "save(on)" | Out-Null
$onSaved = $false
for ($i = 0; $i -lt 6; $i++) { $onSaved = (Read-Config) -match '"proxyInterceptEnabled":"True"'; if ($onSaved) { break }; Start-Sleep -Milliseconds 500 }
Write-Host "[6] toggle=$onState ; config saved True: $onSaved"

Click-Element $startBtn "start(on)" | Out-Null
Start-Sleep -Seconds 3
$startStatus = Get-StatusText
$startOk = ($startStatus.Length -gt 0) -and ($startStatus.Contains($K_STARTED) -or $startStatus.Contains($K_NEEDLOGIN))
Write-Host "[6] start status: $startStatus  ok=$startOk"
if ($startStatus.Contains($K_STARTED)) {
    Click-Element (Find-ByName $N_CLOSEALL) "close-all" | Out-Null
    Write-Host "[6] close-all status: $(Get-StatusText)"
}

$configOn = Read-Config
$allKeys = ($configOn -match 'proxyLocalPort') -and ($configOn -match 'proxyForwardPort') -and
          ($configOn -match 'proxyForwardAddress') -and ($configOn -match 'proxyLocalAddress') -and
          ($configOn -match 'proxyServerName') -and ($configOn -match 'proxyNickName')
Write-Host "[7] config keys present: $allKeys"
Write-Host "[7] config: $configOn"

# 3) screenshot
[W32]::SetForegroundWindow($hwnd) | Out-Null
Start-Sleep -Milliseconds 800
$rect = New-Object W32+RECT
[W32]::GetWindowRect($hwnd, [ref]$rect) | Out-Null
$w = $rect.Right - $rect.Left; $h = $rect.Bottom - $rect.Top
if ($w -gt 0 -and $h -gt 0) {
    $bmp = New-Object System.Drawing.Bitmap($w, $h)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bmp.Size)
    $g.Dispose()
    $bmp.Save($ShotPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    Write-Host "[8] screenshot: $ShotPath (${w}x${h})"
}

$proc.Refresh()
$alive = -not $proc.HasExited
Write-Host "[9] alive at end: $alive"
Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue

$ok = $alive -and $pageOk -and $allKeys -and $guardOffOk -and $startOk -and $offSaved -and $onSaved
Write-Host ""
if ($ok) { Write-Host "PASS: proxy page opens, switch switch-off/on persists to resources/config.json, start path guarded"; exit 0 }
Write-Host ("FAIL: alive={0} pageOk={1} keys={2} guardOff={3} start={4} offSaved={5} onSaved={6}" -f $alive, $pageOk, $allKeys, $guardOffOk, $startOk, $offSaved, $onSaved)
exit 1
