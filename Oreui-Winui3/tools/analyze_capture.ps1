# Analyse a captured PNG against the expected OreUI palette.
# Deterministic, no vision model involved. Windows PowerShell 5.1 compatible.
param(
    [Parameter(Mandatory = $true)][string]$Image,
    [int]$Tolerance = 6
)

Add-Type -AssemblyName System.Drawing

$bmp = New-Object System.Drawing.Bitmap($Image)
$w = $bmp.Width
$h = $bmp.Height
Write-Output "image: ${w}x${h}"

# Palette actually used by the port (OreUI tokens)
$targets = [ordered]@{
    'bg-page        #1E1E1F' = @(0x1E, 0x1E, 0x1F)
    'panel/sidebar  #313233' = @(0x31, 0x32, 0x33)
    'block-titlebar #48494A' = @(0x48, 0x49, 0x4A)
    'frame-border   #333334' = @(0x33, 0x33, 0x34)
    'button-normal  #D0D1D4' = @(0xD0, 0xD1, 0xD4)
    'button-green   #3C8527' = @(0x3C, 0x85, 0x27)
    'button-red     #CA3636' = @(0xCA, 0x36, 0x36)
    'button-edge    #1E1E1F' = @(0x1E, 0x1E, 0x1F)
    'specular       #FFFFFF99' = @(0x99, 0x99, 0x99)
    'text-white     #FFFFFF' = @(0xFF, 0xFF, 0xFF)
    'disabled-fill  #8C8D90' = @(0x8C, 0x8D, 0x90)
    'line           #58585A' = @(0x58, 0x58, 0x5A)
}

$counts = @{}
foreach ($k in $targets.Keys) { $counts[$k] = 0 }
$greenish = 0
$bluish = 0
$total = 0
$distinct = New-Object 'System.Collections.Generic.HashSet[int]'

for ($y = 0; $y -lt $h; $y += 2) {
    for ($x = 0; $x -lt $w; $x += 2) {
        $c = $bmp.GetPixel($x, $y)
        $total++
        [void]$distinct.Add($c.ToArgb())
        if ($c.G -gt $c.R + 25 -and $c.G -gt $c.B + 25) { $greenish++ }
        if ($c.B -gt $c.R + 12 -and $c.B -gt 60) { $bluish++ }
        foreach ($k in $targets.Keys) {
            $t = $targets[$k]
            if ([Math]::Abs($c.R - $t[0]) -le $Tolerance -and
                [Math]::Abs($c.G - $t[1]) -le $Tolerance -and
                [Math]::Abs($c.B - $t[2]) -le $Tolerance) {
                $counts[$k]++
            }
        }
    }
}

Write-Output "sampled pixels: $total   distinct colours: $($distinct.Count)"
Write-Output ""
Write-Output "--- palette hits (tolerance $Tolerance) ---"
foreach ($k in $targets.Keys) {
    $pct = [Math]::Round(100.0 * $counts[$k] / $total, 3)
    Write-Output ("{0}  {1,8}  {2,7}%" -f $k, $counts[$k], $pct)
}
Write-Output ""
Write-Output ("greenish pixels : {0} ({1}%)" -f $greenish, [Math]::Round(100.0 * $greenish / $total, 3))
Write-Output ("bluish  pixels  : {0} ({1}%)" -f $bluish, [Math]::Round(100.0 * $bluish / $total, 3))

# Column profile: where does the sidebar panel end and the page background begin?
Write-Output ""
Write-Output "--- row y=400 colour at x = 20..1300 step 60 ---"
$row = @()
foreach ($x in 20, 80, 140, 200, 240, 260, 320, 500, 800, 1100, 1250) {
    if ($x -lt $w) {
        $c = $bmp.GetPixel($x, [Math]::Min(400, $h - 1))
        $row += ("x=$x #" + $c.R.ToString('X2') + $c.G.ToString('X2') + $c.B.ToString('X2'))
    }
}
Write-Output ($row -join '  ')

$bmp.Dispose()
