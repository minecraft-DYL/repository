# Locate the OreUI button row in a live window, click buttons one at a time,
# and report what changed. ASCII-only (Windows PowerShell 5.1).
#
# Button positions are derived from the CURRENT capture every run, because the
# row's y offset shifts between builds/window positions.
param(
    [Parameter(Mandatory = $true)][string]$ExeDir,
    [int[]]$Only = @(),
    [int]$SettleMs = 800
)

Add-Type -AssemblyName System.Drawing
Add-Type @"
using System; using System.Runtime.InteropServices;
public static class OreVK {
    [StructLayout(LayoutKind.Sequential)] public struct R { public int L,T,Rr,B; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out R r);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x,int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint f,uint a,uint b,uint c,IntPtr e);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
    public static void Activate(IntPtr h){ ShowWindow(h,5); SetForegroundWindow(h); System.Threading.Thread.Sleep(250); }
    public static void Click(int x,int y){ SetCursorPos(x,y); System.Threading.Thread.Sleep(120); mouse_event(0x2,0,0,0,IntPtr.Zero); System.Threading.Thread.Sleep(70); mouse_event(0x4,0,0,0,IntPtr.Zero); }
}
"@

$exe = Join-Path $ExeDir 'OreUI.Gallery.exe'
$log = Join-Path $ExeDir 'gallery-errors.log'

function Get-Win {
    $p = Get-Process -Name 'OreUI.Gallery' -ErrorAction SilentlyContinue |
         Where-Object { $_.MainWindowHandle -ne [IntPtr]::Zero } | Select-Object -First 1
    if (-not $p) { return $null }
    return @{ Proc = $p; Hwnd = $p.MainWindowHandle }
}

function Shot([string]$out) {
    $r = New-Object OreVK+R
    $w = Get-Win
    if (-not $w) { return $null }
    [void][OreVK]::GetWindowRect($w.Hwnd, [ref]$r)
    $bmp = New-Object System.Drawing.Bitmap(($r.Rr - $r.L), ($r.B - $r.T))
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $dc = $g.GetHdc()
    # PrintWindow with PW_RENDERFULLCONTENT
    $null = [OreVK]::GetWindowRect($w.Hwnd, [ref]$r)
    $g.ReleaseHdc($dc)
    $g.Dispose(); $bmp.Dispose()
    return $null
}

# Use the repo's own capture tool (it handles EnumWindows + PrintWindow + largest window).
function Capture([string]$out) {
    & (Join-Path $PSScriptRoot 'capture_window.ps1') -Out $out -WaitMs 400 *> $null
    return (Test-Path $out)
}

function Load($path) {
    $f = New-Object System.Drawing.Bitmap($path)
    return $f
}

function IsBtn($p) {
    return ((($p.R -eq 208) -and ($p.G -eq 209) -and ($p.B -eq 212)) -or
            (($p.R -eq 60)  -and ($p.G -eq 133) -and ($p.B -eq 39))  -or
            (($p.R -eq 202) -and ($p.G -eq 54)  -and ($p.B -eq 54)))
}

# Find the button row band (y center) = the row with the most button-coloured pixels.
function Find-Band($bmp) {
    $best = -1; $bestN = 0
    for ($y = 0; $y -lt $bmp.Height; $y += 2) {
        $n = 0
        for ($x = 0; $x -lt $bmp.Width; $x += 2) { if (IsBtn $bmp.GetPixel($x, $y)) { $n++ } }
        if ($n -gt $bestN) { $bestN = $n; $best = $y }
    }
    return @{ Y = $best; N = $bestN }
}

# Split the row into same-coloured segments = individual buttons.
function Find-Buttons($bmp, $y) {
    $segs = @(); $start = -1; $prev = $null
    for ($x = 0; $x -lt $bmp.Width; $x++) {
        $p = $bmp.GetPixel($x, $y)
        $key = $null
        if (IsBtn $p) { $key = "$($p.R),$($p.G),$($p.B)" }
        if ($key -ne $prev) {
            if ($null -ne $prev -and ($x - $start) -gt 16) { $segs += @{ X0 = $start; X1 = $x - 1 } }
            $start = $x; $prev = $key
        }
    }
    if ($null -ne $prev -and ($bmp.Width - $start) -gt 16) { $segs += @{ X0 = $start; X1 = $bmp.Width - 1 } }
    return $segs
}

# ---- fresh launch ----
Get-Process OreUI.Gallery -ErrorAction SilentlyContinue | ForEach-Object { try { $_.Kill() } catch {} }
Start-Sleep -Seconds 2
Remove-Item $log -Force -ErrorAction SilentlyContinue
Start-Process -FilePath $exe -WorkingDirectory $ExeDir | Out-Null
Start-Sleep -Seconds 14

$base = Join-Path $env:TEMP 'oreui-base.png'
if (-not (Capture $base)) { Write-Output 'BASE_CAPTURE_FAILED'; exit 1 }
$bmp = Load $base
$band = Find-Band $bmp
$segs = Find-Buttons $bmp $band.Y
Write-Output ("band y={0} (px={1}); buttons={2}" -f $band.Y, $band.N, $segs.Count)
$bmp.Dispose()
if ($segs.Count -eq 0) { Write-Output 'NO_BUTTONS_FOUND'; exit 1 }

$w = Get-Win
$r = New-Object OreVK+R
[void][OreVK]::GetWindowRect($w.Hwnd, [ref]$r)
Write-Output ("window rect = {0},{1} {2}x{3}" -f $r.L, $r.T, ($r.Rr - $r.L), ($r.B - $r.T))

$idx = 0
foreach ($s in $segs) {
    $idx++
    if ($Only.Count -gt 0 -and ($Only -notcontains $idx)) { continue }
    $w = Get-Win
    if (-not $w) { Write-Output "*** NO WINDOW before button $idx ***"; break }
    [void][OreVK]::GetWindowRect($w.Hwnd, [ref]$r)

    $cx = $r.L + [int](($s.X0 + $s.X1) / 2)
    $cy = $r.T + $band.Y
    [OreVK]::Activate($w.Hwnd)
    [OreVK]::Click($cx, $cy)
    Start-Sleep -Milliseconds $SettleMs

    $w2 = Get-Win
    $alive = [bool](Get-Process -Name 'OreUI.Gallery' -ErrorAction SilentlyContinue)
    $out = Join-Path $env:TEMP "oreui-btn$idx.png"
    $ok = Capture $out
    $hash = if ($ok) { (Get-FileHash $out -Algorithm MD5).Hash.Substring(0,10) } else { 'NO-CAPTURE' }
    $baseHash = (Get-FileHash $base -Algorithm MD5).Hash.Substring(0,10)
    Write-Output ("button {0}: x={1}-{2} click=({3},{4})  window={5}  proc_alive={6}  capture={7}  changed={8}" -f `
        $idx, $s.X0, $s.X1, $cx, $cy, ([bool]$w2), $alive, $hash, ($hash -ne $baseHash))

    if (-not $w2) { Write-Output "*** window gone after button $idx ***"; break }

    # dismiss anything that opened: click near the window's top-left content area
    [OreVK]::Click(($r.L + 60), ($r.T + 150))
    Start-Sleep -Milliseconds 400
}

Write-Output ''
Write-Output '=== gallery-errors.log ==='
if (Test-Path $log) { Get-Content $log -Raw -Encoding UTF8 } else { Write-Output '(EMPTY)' }
