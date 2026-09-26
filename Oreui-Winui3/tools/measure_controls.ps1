# Measure OreUI control geometry from a screenshot.
# Reports connected components of a fill colour (switch knobs / slider thumbs /
# disabled tracks) and the x centres of 2px tick marks inside a slider rail.
# All pixel work is done in C# (LockBits + flood fill); PowerShell alone is far
# too slow over 1.15M pixels. ASCII-only (Windows PowerShell 5.1).
param(
    [Parameter(Mandatory = $true)][string]$ExeDir,
    [Parameter(Mandatory = $true)][string]$Out,
    [int]$SettleMs = 1400
)

Add-Type -AssemblyName System.Drawing
Add-Type -ReferencedAssemblies System.Drawing @"
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

public static class OreMeasure
{
    static int W, H;
    static int[] px;

    public static string[] Run(string path)
    {
        var lines = new List<string>();
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
                {
                    int o = y * stride + x * 4;
                    px[y * W + x] = (raw[o + 2] << 16) | (raw[o + 1] << 8) | raw[o];
                }
        }
        lines.Add(string.Format("image {0}x{1}", W, H));

        lines.Add("");
        lines.Add("=== #D0D1D4 components (>=120px): switch knob / disabled track / slider thumb / neutral button ===");
        foreach (var b in Label(0xD0D1D4, 120))
            lines.Add(string.Format("  x{0}-{1} y{2}-{3}  {4}x{5}  n={6}  cx={7}",
                b[0], b[2], b[1], b[3], b[2] - b[0] + 1, b[3] - b[1] + 1, b[4],
                Math.Round((b[0] + b[2]) / 2.0, 1)));

        lines.Add("");
        lines.Add("=== #8C8D90 components (>=80px): switch off-track / slider rail ===");
        var rails = new List<int[]>();
        foreach (var b in Label(0x8C8D90, 80))
        {
            bool flat = (b[3] - b[1] + 1) <= 20 && (b[2] - b[0] + 1) >= 40;
            if (flat) rails.Add(b);
            lines.Add(string.Format("  x{0}-{1} y{2}-{3}  {4}x{5}  n={6}  cx={7}{8}",
                b[0], b[2], b[1], b[3], b[2] - b[0] + 1, b[3] - b[1] + 1, b[4],
                Math.Round((b[0] + b[2]) / 2.0, 1), flat ? "   <- rail" : ""));
        }

        lines.Add("");
        lines.Add("=== tick marks (#1E1E1F, 2px wide) inside each rail band ===");
        foreach (var r in rails)
        {
            int yc = (r[1] + r[3]) / 2;
            var xs = new List<int>();
            for (int x = Math.Max(0, r[0] - 8); x <= Math.Min(W - 1, r[2] + 8); x++)
                if (px[yc * W + x] == 0x1E1E1F) xs.Add(x);
            if (xs.Count == 0) { lines.Add(string.Format("  rail x{0}-{1} y{2}: no ticks", r[0], r[2], yc)); continue; }
            var cent = new List<double>();
            int s = xs[0], p = xs[0];
            for (int i = 1; i < xs.Count; i++)
            {
                if (xs[i] - p > 1) { cent.Add((s + p) / 2.0); s = xs[i]; }
                p = xs[i];
            }
            cent.Add((s + p) / 2.0);
            var parts = new List<string>();
            foreach (var v in cent) parts.Add(v.ToString("0.#"));
            lines.Add(string.Format("  rail x{0}-{1} y{2} (w={3}): ticks at {4}",
                r[0], r[2], yc, r[2] - r[0] + 1, string.Join(", ", parts.ToArray())));
        }

        lines.Add("");
        lines.Add("=== #1F1F1F (pop background) components >=200px ===");
        foreach (var b in Label(0x1F1F1F, 200))
            lines.Add(string.Format("  x{0}-{1} y{2}-{3}  {4}x{5}  n={6}",
                b[0], b[2], b[1], b[3], b[2] - b[0] + 1, b[3] - b[1] + 1, b[4]));

        lines.Add("");
        lines.Add("=== #48494A (modal title / button area / loading mask) coverage ===");
        int n4849 = 0;
        for (int i = 0; i < px.Length; i++) if (px[i] == 0x48494A) n4849++;
        lines.Add(string.Format("  #48494A pixels = {0} ({1:0.00}% of image)", n4849, 100.0 * n4849 / (W * H)));

        lines.Add("");
        lines.Add("=== #5A5B5C / #333334 (modal button-area borders) ===");
        int a = 0, b2 = 0;
        for (int i = 0; i < px.Length; i++) { if (px[i] == 0x5A5B5C) a++; if (px[i] == 0x333334) b2++; }
        lines.Add(string.Format("  #5A5B5C = {0}   #333334 = {1}", a, b2));

        return lines.ToArray();
    }

    static List<int[]> Label(int target, int minPixels)
    {
        var res = new List<int[]>();
        var seen = new bool[W * H];
        var stack = new Stack<int>();
        for (int i = 0; i < px.Length; i++)
        {
            if (seen[i] || px[i] != target) continue;
            stack.Clear(); stack.Push(i); seen[i] = true;
            int n = 0, x0 = W, x1 = -1, y0 = H, y1 = -1;
            while (stack.Count > 0)
            {
                int cur = stack.Pop(); n++;
                int cy = cur / W, cx = cur - cy * W;
                if (cx < x0) x0 = cx; if (cx > x1) x1 = cx;
                if (cy < y0) y0 = cy; if (cy > y1) y1 = cy;
                if (cx > 0)     { int j = cur - 1; if (!seen[j] && px[j] == target) { seen[j] = true; stack.Push(j); } }
                if (cx < W - 1) { int j = cur + 1; if (!seen[j] && px[j] == target) { seen[j] = true; stack.Push(j); } }
                if (cy > 0)     { int j = cur - W; if (!seen[j] && px[j] == target) { seen[j] = true; stack.Push(j); } }
                if (cy < H - 1) { int j = cur + W; if (!seen[j] && px[j] == target) { seen[j] = true; stack.Push(j); } }
            }
            if (n >= minPixels) res.Add(new int[] { x0, y0, x1, y1, n });
        }
        res.Sort((l, r) => l[1] != r[1] ? l[1].CompareTo(r[1]) : l[0].CompareTo(r[0]));
        return res;
    }
}
"@

$exe = Join-Path $ExeDir 'OreUI.Gallery.exe'
function Get-Win {
    $p = Get-Process -Name 'OreUI.Gallery' -ErrorAction SilentlyContinue |
         Where-Object { $_.MainWindowHandle -ne [IntPtr]::Zero } | Select-Object -First 1
    if (-not $p) { return $null }
    return $p.MainWindowHandle
}

if (-not (Get-Win)) {
    Start-Process -FilePath $exe -WorkingDirectory $ExeDir | Out-Null
    # The gallery needs ~14s before its window exists; poll rather than guess.
    for ($i = 0; $i -lt 60; $i++) {
        Start-Sleep -Seconds 1
        if (Get-Win) { break }
    }
    Start-Sleep -Seconds 3
}
if (-not (Get-Win)) { Write-Output 'WINDOW_NOT_AVAILABLE'; exit 1 }

& (Join-Path $PSScriptRoot 'capture_window.ps1') -Out $Out -WaitMs 900 | Out-Null
if (-not (Test-Path $Out)) { Write-Output 'CAPTURE_FAILED'; exit 1 }

[OreMeasure]::Run($Out) | ForEach-Object { $_ }
