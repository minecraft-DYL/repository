# Click-probe: drives the running app with synthetic mouse input to reproduce
# "click the mask/overlay and it crashes", then reports whether the process
# survived and what the app logged.
#
# ASCII-only on purpose: Windows PowerShell 5.1 mis-parses non-ASCII sources.
param(
    [Parameter(Mandatory = $true)][string]$ExeDir,
    [int]$MaxClicks = 16,
    [int]$SettleMs = 450
)

Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing @"
using System;
using System.Linq;
using System.Runtime.InteropServices;
public static class OreClick {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint f);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, IntPtr e);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    public static void Click(int x, int y) {
        SetCursorPos(x, y);
        System.Threading.Thread.Sleep(70);
        mouse_event(0x2, 0, 0, 0, IntPtr.Zero);
        System.Threading.Thread.Sleep(50);
        mouse_event(0x4, 0, 0, 0, IntPtr.Zero);
    }
    public static int[] Grab(IntPtr hwnd) {
        RECT r; GetWindowRect(hwnd, out r);
        int w = r.R - r.L, ht = r.B - r.T;
        using (var bmp = new System.Drawing.Bitmap(w, ht))
        using (var g = System.Drawing.Graphics.FromImage(bmp)) {
            IntPtr dc = g.GetHdc();
            PrintWindow(hwnd, dc, 2);
            g.ReleaseHdc(dc);
            int[] px = new int[w * ht];
            for (int yy = 0; yy < ht; yy += 2)
                for (int xx = 0; xx < w; xx += 2) {
                    var c = bmp.GetPixel(xx, yy);
                    px[yy * w + xx] = (c.R << 16) | (c.G << 8) | c.B;
                }
            return new int[] { w, ht }.Concat(px).ToArray();
        }
    }
}
"@

$log = Join-Path $ExeDir 'gallery-errors.log'

function Get-Hwnd([string]$name) {
    while ($true) {
        $p = Get-Process -Name $name -ErrorAction SilentlyContinue | Select-Object -First 1
        if (-not $p) { return @{ Proc = $null; Hwnd = [IntPtr]::Zero } }
        $p.Refresh()
        if ($p.MainWindowHandle -ne [IntPtr]::Zero) { return @{ Proc = $p; Hwnd = $p.MainWindowHandle } }
        Start-Sleep -Milliseconds 300
    }
}

function Get-Rect($hwnd) {
    $r = New-Object OreClick+RECT
    [void][OreClick]::GetWindowRect($hwnd, [ref]$r)
    return $r
}

# --- button fill colours in the OreUI palette (CSS order -> single int) ---
$targets = @(0xD0D1D4, 0x3C8527, 0xCA3636)

$state = Get-Hwnd 'OreUI.Gallery'
if (-not $state.Proc) { Write-Output 'PROCESS_NOT_FOUND'; exit 1 }
$hwnd = $state.Hwnd
[void][OreClick]::SetForegroundWindow($hwnd)
Start-Sleep -Milliseconds 600
$rect = Get-Rect $hwnd
Write-Output ("window rect = {0},{1} {2}x{3}" -f $rect.L, $rect.T, ($rect.R - $rect.L), ($rect.B - $rect.T))

function Grab-Pixels {
    $raw = [OreClick]::Grab($hwnd)
    $w = $raw[0]; $h = $raw[1]
    return @{ W = $w; H = $h; Px = $raw }
}

function Mean-Luma($img) {
    $sum = 0.0; $n = 0
    for ($i = 2; $i -lt $img.Px.Length; $i++) {
        $v = $img.Px[$i]
        if ($v -eq 0) { continue }
        $sum += ((($v -shr 16) -band 255) * 0.11 + (($v -shr 8) -band 255) * 0.59 + ($v -band 255) * 0.3)
        $n++
    }
    if ($n -eq 0) { return 0 }
    return $sum / $n
}

# --- locate button clusters on a coarse 8px grid ---
function Find-Buttons($img) {
    $w = $img.W; $h = $img.H
    $bw = [Math]::Floor($w / 8); $bh = [Math]::Floor($h / 8)
    $grid = New-Object 'bool[,]' $bw, $bh
    for ($by = 0; $by -lt $bh; $by++) {
        for ($bx = 0; $bx -lt $bw; $bx++) {
            $hit = 0; $tot = 0
            for ($yy = 0; $yy -lt 8; $yy += 2) {
                for ($xx = 0; $xx -lt 8; $xx += 2) {
                    $v = $img.Px[(($by * 8 + $yy) * $w) + ($bx * 8 + $xx)]
                    $tot++
                    foreach ($t in $targets) {
                        if ([Math]::Abs((($v -shr 16) -band 255) - (($t -shr 16) -band 255)) -le 6 -and
                            [Math]::Abs((($v -shr 8) -band 255) - (($t -shr 8) -band 255)) -le 6 -and
                            [Math]::Abs(($v -band 255) - ($t -band 255)) -le 6) { $hit++; break }
                    }
                }
            }
            if ($tot -gt 0 -and ($hit / $tot) -ge 0.5) { $grid[$bx, $by] = $true }
        }
    }
    # 4-connected grouping
    $seen = New-Object 'bool[,]' $bw, $bh
    $out = New-Object System.Collections.ArrayList
    for ($by = 0; $by -lt $bh; $by++) {
        for ($bx = 0; $bx -lt $bw; $bx++) {
            if (-not $grid[$bx, $by] -or $seen[$bx, $by]) { continue }
            $stack = New-Object System.Collections.Stack
            $stack.Push(@($bx, $by)); $seen[$bx, $by] = $true
            $x0 = $bx; $x1 = $bx; $y0 = $by; $y1 = $by; $n = 0
            while ($stack.Count -gt 0) {
                $c = $stack.Pop(); $cx = $c[0]; $cy = $c[1]; $n++
                if ($cx -lt $x0) { $x0 = $cx }; if ($cx -gt $x1) { $x1 = $cx }
                if ($cy -lt $y0) { $y0 = $cy }; if ($cy -gt $y1) { $y1 = $cy }
                foreach ($d in @(@(1, 0), @(-1, 0), @(0, 1), @(0, -1))) {
                    $nx = $cx + $d[0]; $ny = $cy + $d[1]
                    if ($nx -ge 0 -and $ny -ge 0 -and $nx -lt $bw -and $ny -lt $bh -and $grid[$nx, $ny] -and -not $seen[$nx, $ny]) {
                        $seen[$nx, $ny] = $true; $stack.Push(@($nx, $ny))
                    }
                }
            }
            if ($n -ge 3) { [void]$out.Add(@(($x0 * 8), ($y0 * 8), (($x1 + 1) * 8), (($y1 + 1) * 8))) }
        }
    }
    return $out
}

$baseImg = Grab-Pixels
$base = Mean-Luma $baseImg
$clusters = Find-Buttons $baseImg
Write-Output ("baseline luma = {0:N1}; button clusters = {1}" -f $base, $clusters.Count)

$done = 0
foreach ($c in $clusters) {
    if ($done -ge $MaxClicks) { break }
    $done++
    $cx = $rect.L + [int](($c[0] + $c[2]) / 2)
    $cy = $rect.T + [int](($c[1] + $c[3]) / 2)

    $st = Get-Hwnd 'OreUI.Gallery'
    if (-not $st.Proc) { Write-Output "DIED before attempt $done"; break }

    [OreClick]::Click($cx, $cy)
    Start-Sleep -Milliseconds $SettleMs

    $st = Get-Hwnd 'OreUI.Gallery'
    if (-not $st.Proc) { Write-Output "*** PROCESS DIED after clicking cluster $done at $cx,$cy ***"; break }

    $now = Grab-Pixels
    $luma = Mean-Luma $now
    $dimmed = $luma -lt ($base * 0.75)
    Write-Output ("click {0}: ({1},{2}) luma={3:N1} {4}" -f $done, $cx, $cy, $luma, $(if ($dimmed) { '<-- overlay present' } else { '' }))

    if ($dimmed) {
        # An overlay is up -> click well off-centre, i.e. on the overlay itself.
        [OreClick]::Click(($rect.L + 40), ($rect.T + 200))
        Start-Sleep -Milliseconds 900
        $st = Get-Hwnd 'OreUI.Gallery'
        if (-not $st.Proc) {
            Write-Output "*** PROCESS DIED on overlay click (cluster $done) ***"
            break
        }
        Write-Output "    overlay dismissed, process alive"
        [OreClick]::Click($cx, $cy)
        Start-Sleep -Milliseconds 300
    }
}

Write-Output ''
Write-Output '=== gallery-errors.log ==='
if (Test-Path $log) { Get-Content $log -Raw -Encoding UTF8 } else { Write-Output '(no log file)' }
