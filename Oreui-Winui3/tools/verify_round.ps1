# One-shot verification: click each button in the OreUI button row and report
# measured colour evidence for the pop / loading mask / modal fixes.
# ASCII-only (Windows PowerShell 5.1).
param(
    [Parameter(Mandatory = $true)][string]$ExeDir,
    [int[]]$XCenters = @(593, 697, 801, 905),
    [int]$RowY = 771
)

Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing @"
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class OreCount
{
    public static string[] Run(string path, int bandTop, int[] switchTrack)
    {
        var L = new List<string>();
        int W, H; int[] px;
        using (var bmp = new Bitmap(path))
        {
            W = bmp.Width; H = bmp.Height;
            var bd = bmp.LockBits(new Rectangle(0, 0, W, H), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var raw = new byte[bd.Stride * H];
            Marshal.Copy(bd.Scan0, raw, 0, raw.Length);
            int stride = bd.Stride;
            bmp.UnlockBits(bd);
            px = new int[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                { int o = y * stride + x * 4; px[y * W + x] = (raw[o+2] << 16) | (raw[o+1] << 8) | raw[o]; }
        }

        // ---- bottom band: pop toasts live at bottom:40px, centred, max 500px ----
        var hist = new Dictionary<int,int>();
        for (int y = Math.Max(0, bandTop); y < H; y++)
            for (int x = 0; x < W; x++)
            { int v = px[y*W+x]; int c; hist.TryGetValue(v, out c); hist[v] = c + 1; }

        int pop = 0, succ = 0, err = 0, proc = 0;
        foreach (var kv in hist)
        {
            if (kv.Key == 0x1F1F1F) pop += kv.Value;
            if (kv.Key == 0x6CC349) succ += kv.Value;
            if (kv.Key == 0xF46D6D) err += kv.Value;
            if (kv.Key == 0xFFE866) proc += kv.Value;
        }
        L.Add(string.Format("  bottom band (y>={0}): pop_bg #1F1F1F={1}  success #6CC349={2}  error #F46D6D={3}  process #FFE866={4}",
            bandTop, pop, succ, err, proc));

        var top = new List<KeyValuePair<int,int>>(hist);
        top.Sort((a,b) => b.Value.CompareTo(a.Value));
        var sb = new List<string>();
        for (int i = 0; i < Math.Min(6, top.Count); i++)
            sb.Add(string.Format("#{0:X6}={1}", top[i].Key, top[i].Value));
        L.Add("  band top colours: " + string.Join("  ", sb.ToArray()));

        // ---- whole image: loading mask coverage + yellowish (focus visual) pixels ----
        int mask = 0, yellowish = 0, anyYellow = 0;
        for (int i = 0; i < px.Length; i++)
        {
            int v = px[i];
            if (v == 0x48494A) mask++;
            int r = (v >> 16) & 255, g = (v >> 8) & 255, b = v & 255;
            if (r > 200 && g > 150 && b < 130) yellowish++;
            if (r > 150 && g > 120 && b < 110 && (r - b) > 70) anyYellow++;
        }
        L.Add(string.Format("  whole image: #48494A={0} ({1:0.00}%)  yellow-ish={2}  broad-yellow={3}",
            mask, 100.0 * mask / (W * H), yellowish, anyYellow));

        // ---- switch: #8C8D90 columns inside a track bbox (knob borders) ----
        if (switchTrack != null && switchTrack.Length == 4)
        {
            int x0 = switchTrack[0], y0 = switchTrack[1], x1 = switchTrack[2], y1 = switchTrack[3];
            int yc = (y0 + y1) / 2;
            var cols = new List<int>();
            for (int x = x0; x <= x1; x++)
                if (px[yc * W + x] == 0x8C8D90) cols.Add(x);
            L.Add(string.Format("  track x{0}-{1} y{2}-{3}: #8C8D90 cols at [{4}]",
                x0, x1, y0, y1, string.Join(",", cols.ConvertAll(c => c.ToString()).ToArray())));
            var cols2 = new List<int>();
            int yc2 = yc - 6;
            for (int x = x0; x <= x1; x++)
                if (px[yc2 * W + x] == 0x8C8D90) cols2.Add(x);
            L.Add(string.Format("  track x{0}-{1} y={2}: #8C8D90 cols at [{3}]",
                x0, x1, yc2, string.Join(",", cols2.ConvertAll(c => c.ToString()).ToArray())));
        }
        return L.ToArray();
    }
}
"@

Add-Type @"
using System; using System.Runtime.InteropServices;
public static class OreIn {
    [StructLayout(LayoutKind.Sequential)] public struct R { public int L,T,Rr,B; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out R r);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x,int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint f,uint a,uint b,uint c,IntPtr e);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    public static void Click(int x,int y){ SetCursorPos(x,y); System.Threading.Thread.Sleep(120); mouse_event(0x2,0,0,0,IntPtr.Zero); System.Threading.Thread.Sleep(70); mouse_event(0x4,0,0,0,IntPtr.Zero); }
}
"@

$exe = Join-Path $ExeDir 'OreUI.Gallery.exe'
$log = Join-Path $ExeDir 'gallery-errors.log'

function Get-H {
    $p = Get-Process -Name 'OreUI.Gallery' -ErrorAction SilentlyContinue |
         Where-Object { $_.MainWindowHandle -ne [IntPtr]::Zero } | Select-Object -First 1
    if (-not $p) { return [IntPtr]::Zero }
    return $p.MainWindowHandle
}

Get-Process OreUI.Gallery -ErrorAction SilentlyContinue | ForEach-Object { try { $_.Kill() } catch {} }
Start-Sleep -Seconds 2
Remove-Item $log -Force -ErrorAction SilentlyContinue
Start-Process -FilePath $exe -WorkingDirectory $ExeDir | Out-Null
for ($i = 0; $i -lt 60; $i++) { Start-Sleep -Seconds 1; if (Get-H) { break } }
Start-Sleep -Seconds 3
$h = Get-H
if ($h -eq [IntPtr]::Zero) { Write-Output 'WINDOW_NOT_AVAILABLE'; exit 1 }

$r = New-Object OreIn+R
[void][OreIn]::GetWindowRect($h, [ref]$r)
Write-Output ("window rect = {0},{1} {2}x{3}" -f $r.L, $r.T, ($r.Rr - $r.L), ($r.B - $r.T))

$capture = Join-Path $PSScriptRoot 'capture_window.ps1'
$base = Join-Path $env:TEMP 'oreui-round-base.png'
& $capture -Out $base -WaitMs 700 | Out-Null
$bmp = New-Object System.Drawing.Bitmap($base)
$IMG_H = $bmp.Height; $bmp.Dispose()
Write-Output ("image height = {0}; bandTop = {1}" -f $IMG_H, ($IMG_H - 220))

Write-Output ''
Write-Output '=== BASELINE (no click) ==='
[OreCount]::Run($base, ($IMG_H - 220), $null) | ForEach-Object { $_ }

$i = 0
foreach ($xc in $XCenters) {
    $i++
    $h = Get-H
    if ($h -eq [IntPtr]::Zero) { Write-Output "*** window gone before button $i ***"; break }
    [void][OreIn]::GetWindowRect($h, [ref]$r)
    [void][OreIn]::SetForegroundWindow($h)
    Start-Sleep -Milliseconds 250
    [OreIn]::Click(($r.L + $xc), ($r.T + $RowY))
    Start-Sleep -Milliseconds 700

    $out = Join-Path $env:TEMP "oreui-round-btn$i.png"
    & $capture -Out $out -WaitMs 200 | Out-Null
    Write-Output ''
    Write-Output ("=== CLICK $i at ({0},{1}) ===" -f ($r.L + $xc), ($r.T + $RowY))
    if (Test-Path $out) { [OreCount]::Run($out, ($IMG_H - 220), $null) | ForEach-Object { $_ } }
    else { Write-Output '  CAPTURE_FAILED' }

    # dismiss (click the overlay / empty left area), then re-check
    [OreIn]::Click(($r.L + 60), ($r.T + 150))
    Start-Sleep -Milliseconds 400
}

Write-Output ''
Write-Output '=== gallery-errors.log ==='
if (Test-Path $log) { Get-Content $log -Raw -Encoding UTF8 } else { Write-Output '(EMPTY)' }
Write-Output ("process alive: " + [bool](Get-Process -Name 'OreUI.Gallery' -ErrorAction SilentlyContinue))
